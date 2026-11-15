using System.Diagnostics;
using MultyAudioOutput.Platform;
using NAudio.CoreAudioApi;

namespace MultyAudioOutput.Audio;

public sealed record AudioDevice(string Id, string Name);

public sealed record ProcessInfo(string Key, string DisplayName, string? Path);

/// <summary>An application (grouped by exe name) that has audio sessions.</summary>
public sealed class AppAudio
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public string? ExePath { get; init; }
    public List<uint> Pids { get; } = new();
    public float Volume { get; set; } = 1f;
    public bool Muted { get; set; }
    public bool IsActive { get; set; }
    /// <summary>MMDevice.ID the app is routed to, "" = system default.</summary>
    public string CurrentDeviceId { get; set; } = "";
}

/// <summary>
/// Thin, poll-based wrapper over Core Audio. Every call enumerates fresh COM objects
/// on the calling (UI) thread and releases them, so there are no cross-thread callbacks.
/// </summary>
internal sealed class AudioService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly Dictionary<uint, (ProcessInfo? Info, DateTime At)> _processCache = new();
    private readonly string _ownKey = Process.GetCurrentProcess().ProcessName.ToLowerInvariant();

    public List<AudioDevice> GetDevices()
    {
        var list = new List<AudioDevice>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            try
            {
                list.Add(new AudioDevice(device.ID, device.FriendlyName));
            }
            catch (Exception ex)
            {
                Log.Error("Read device", ex);
            }
            finally
            {
                device.Dispose();
            }
        }
        return list.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public AudioDevice? GetDefaultDevice()
    {
        try
        {
            if (!_enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)) return null;
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return new AudioDevice(device.ID, device.FriendlyName);
        }
        catch
        {
            return null;
        }
    }

    public List<AppAudio> GetApps()
    {
        var map = new Dictionary<string, AppAudio>(StringComparer.OrdinalIgnoreCase);

        ForEachSession((session, pid, info) =>
        {
            if (info.Key == _ownKey) return;

            bool active = session.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateActive;
            if (!map.TryGetValue(info.Key, out var app))
            {
                app = new AppAudio { Key = info.Key, DisplayName = info.DisplayName, ExePath = info.Path };
                map[info.Key] = app;
                ReadVolume(session, app);
            }
            else if (active && !app.IsActive)
            {
                ReadVolume(session, app);
            }

            app.IsActive |= active;
            if (!app.Pids.Contains(pid)) app.Pids.Add(pid);
        });

        foreach (var app in map.Values)
        {
            app.CurrentDeviceId = app.Pids
                .Select(AudioPolicyConfig.GetDefaultEndpoint)
                .FirstOrDefault(id => !string.IsNullOrEmpty(id)) ?? "";
        }

        return map.Values.OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public void SetVolume(string key, float volume) =>
        ForEachSession((s, _, info) =>
        {
            if (string.Equals(info.Key, key, StringComparison.OrdinalIgnoreCase))
                s.SimpleAudioVolume.Volume = Math.Clamp(volume, 0f, 1f);
        });

    public void SetMute(string key, bool mute) =>
        ForEachSession((s, _, info) =>
        {
            if (string.Equals(info.Key, key, StringComparison.OrdinalIgnoreCase))
                s.SimpleAudioVolume.Mute = mute;
        });

    /// <summary>Process ids of every running process with this exe name.</summary>
    public HashSet<uint> GetPids(string key)
    {
        var pids = new HashSet<uint>();
        foreach (var p in Process.GetProcessesByName(key))
        {
            using (p) pids.Add((uint)p.Id);
        }
        return pids;
    }

    /// <summary>Routes every process of the app; null deviceId = back to system default.</summary>
    public HashSet<uint> RouteApp(string key, string? deviceId)
    {
        var pids = GetPids(key);
        foreach (var pid in pids)
            AudioPolicyConfig.SetDefaultEndpoint(pid, deviceId);
        return pids;
    }

    public ProcessInfo? GetProcessInfo(uint pid)
    {
        if (_processCache.TryGetValue(pid, out var cached) && DateTime.UtcNow - cached.At < TimeSpan.FromSeconds(30))
            return cached.Info;

        ProcessInfo? info = null;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            var name = process.ProcessName;
            var path = Native.GetProcessPath(pid);
            string? description = null;
            if (path != null)
            {
                try { description = FileVersionInfo.GetVersionInfo(path).FileDescription; }
                catch { /* ignore */ }
            }
            var display = string.IsNullOrWhiteSpace(description) ? name : description.Trim();
            info = new ProcessInfo(name.ToLowerInvariant(), display, path);
        }
        catch
        {
            // process exited or is inaccessible
        }

        if (_processCache.Count > 500) _processCache.Clear();
        _processCache[pid] = (info, DateTime.UtcNow);
        return info;
    }

    private static void ReadVolume(AudioSessionControl session, AppAudio app)
    {
        try
        {
            app.Volume = session.SimpleAudioVolume.Volume;
            app.Muted = session.SimpleAudioVolume.Mute;
        }
        catch { /* ignore */ }
    }

    private void ForEachSession(Action<AudioSessionControl, uint, ProcessInfo> action)
    {
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            try
            {
                var manager = device.AudioSessionManager;
                manager.RefreshSessions();
                var sessions = manager.Sessions;
                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    try
                    {
                        if (session.IsSystemSoundsSession) continue;
                        if (session.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateExpired) continue;
                        var pid = session.GetProcessID;
                        if (pid == 0) continue;
                        var info = GetProcessInfo(pid);
                        if (info is null) continue;
                        action(session, pid, info);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Session", ex);
                    }
                    finally
                    {
                        session.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Enumerate sessions", ex);
            }
            finally
            {
                device.Dispose();
            }
        }
    }

    public void Dispose() => _enumerator.Dispose();
}
