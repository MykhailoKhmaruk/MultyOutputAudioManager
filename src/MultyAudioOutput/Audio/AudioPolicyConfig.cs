using System.Runtime.InteropServices;
using MultyAudioOutput.Platform;

namespace MultyAudioOutput.Audio;

/// <summary>
/// Per-application default audio endpoint, via the undocumented WinRT factory
/// "Windows.Media.Internal.AudioPolicyConfig" (the same API that powers
/// Settings → Sound → Volume mixer → "Output device" per app, EarTrumpet, SoundSwitch).
///
/// The interface is called through its vtable directly, because .NET 5+ has no
/// built-in IInspectable/HSTRING marshalling.
/// </summary>
internal static unsafe class AudioPolicyConfig
{
    private const string ClassName = "Windows.Media.Internal.AudioPolicyConfig";

    // Windows 10 21H2+ / Windows 11, and the older (1803–21H1) variant.
    private static readonly Guid[] FactoryIids =
    {
        new("ab3d4648-e242-459f-b02f-541c70306324"),
        new("2a59116d-6c4f-45e0-a74f-707e3fef9258"),
    };

    // IUnknown(3) + IInspectable(3) + 19 other methods precede these.
    private const int SlotSetPersistedDefaultAudioEndpoint = 25;
    private const int SlotGetPersistedDefaultAudioEndpoint = 26;
    private const int SlotClearAllPersistedApplicationDefaultEndpoints = 27;

    private const string DevicePrefix = @"\\?\SWD#MMDEVAPI#";
    private const string DeviceSuffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

    private const int EDataFlow_Render = 0;
    private const int ERole_Console = 0;
    private const int ERole_Multimedia = 1;

    private static IntPtr _factory;
    private static bool _initialized;

    public static bool IsSupported
    {
        get
        {
            EnsureFactory();
            return _factory != IntPtr.Zero;
        }
    }

    /// <summary>Routes the process to <paramref name="deviceId"/> (MMDevice.ID); null/empty resets to system default.</summary>
    public static bool SetDefaultEndpoint(uint processId, string? deviceId)
    {
        EnsureFactory();
        if (_factory == IntPtr.Zero) return false;

        IntPtr hstring = IntPtr.Zero;
        try
        {
            if (!string.IsNullOrEmpty(deviceId))
            {
                var full = DevicePrefix + deviceId + DeviceSuffix;
                Marshal.ThrowExceptionForHR(WindowsCreateString(full, (uint)full.Length, out hstring));
            }

            var fn = (delegate* unmanaged[Stdcall]<IntPtr, uint, int, int, IntPtr, int>)VTable(SlotSetPersistedDefaultAudioEndpoint);
            int hr1 = fn(_factory, processId, EDataFlow_Render, ERole_Multimedia, hstring);
            int hr2 = fn(_factory, processId, EDataFlow_Render, ERole_Console, hstring);
            return hr1 >= 0 && hr2 >= 0;
        }
        catch (Exception ex)
        {
            Log.Error($"SetDefaultEndpoint pid={processId}", ex);
            return false;
        }
        finally
        {
            if (hstring != IntPtr.Zero) WindowsDeleteString(hstring);
        }
    }

    /// <summary>Returns the MMDevice.ID the process is routed to, or "" when it follows the system default.</summary>
    public static string GetDefaultEndpoint(uint processId)
    {
        EnsureFactory();
        if (_factory == IntPtr.Zero) return "";

        IntPtr hstring = IntPtr.Zero;
        try
        {
            var fn = (delegate* unmanaged[Stdcall]<IntPtr, uint, int, int, IntPtr*, int>)VTable(SlotGetPersistedDefaultAudioEndpoint);
            int hr = fn(_factory, processId, EDataFlow_Render, ERole_Multimedia, &hstring);
            if (hr < 0 || hstring == IntPtr.Zero) return "";

            char* raw = WindowsGetStringRawBuffer(hstring, out uint length);
            var full = new string(raw, 0, (int)length);
            return ParseDeviceId(full);
        }
        catch (Exception ex)
        {
            Log.Error($"GetDefaultEndpoint pid={processId}", ex);
            return "";
        }
        finally
        {
            if (hstring != IntPtr.Zero) WindowsDeleteString(hstring);
        }
    }

    /// <summary>Removes every per-app output assignment stored by Windows.</summary>
    public static void ClearAll()
    {
        EnsureFactory();
        if (_factory == IntPtr.Zero) return;
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, int>)VTable(SlotClearAllPersistedApplicationDefaultEndpoints);
        Marshal.ThrowExceptionForHR(fn(_factory));
    }

    private static string ParseDeviceId(string full)
    {
        if (full.StartsWith(DevicePrefix, StringComparison.OrdinalIgnoreCase))
            full = full[DevicePrefix.Length..];
        int suffix = full.IndexOf(DeviceSuffix, StringComparison.OrdinalIgnoreCase);
        if (suffix >= 0) full = full[..suffix];
        return full;
    }

    private static IntPtr VTable(int slot) => (*(IntPtr**)_factory)[slot];

    private static void EnsureFactory()
    {
        if (_initialized) return;
        _initialized = true;

        IntPtr className = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(WindowsCreateString(ClassName, (uint)ClassName.Length, out className));
            foreach (var iid in FactoryIids)
            {
                var id = iid;
                if (RoGetActivationFactory(className, ref id, out var factory) >= 0 && factory != IntPtr.Zero)
                {
                    _factory = factory;
                    return;
                }
            }
            Log.Info("AudioPolicyConfig factory is not available on this Windows build.");
        }
        catch (Exception ex)
        {
            Log.Error("AudioPolicyConfig init", ex);
        }
        finally
        {
            if (className != IntPtr.Zero) WindowsDeleteString(className);
        }
    }

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

    [DllImport("combase.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int WindowsCreateString(string sourceString, uint length, out IntPtr hstring);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern char* WindowsGetStringRawBuffer(IntPtr hstring, out uint length);
}
