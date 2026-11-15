using Microsoft.Win32;

namespace MultyAudioOutput.Platform;

internal static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MultyAudioOutput";

    private static string Command => $"\"{Environment.ProcessPath}\" --minimized";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue(ValueName, Command);
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            Log.Error("AutoStart", ex);
        }
    }

    /// <summary>Keeps the Run entry pointing at the current exe if the app was moved.</summary>
    public static void RefreshPathIfEnabled()
    {
        if (IsEnabled) Set(true);
    }
}
