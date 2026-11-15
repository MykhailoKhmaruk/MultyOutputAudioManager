using System.IO;
using System.Text;
using MultyAudioOutput.Audio;
using MultyAudioOutput.Platform;

namespace MultyAudioOutput;

/// <summary>`MultyAudioOutput.exe --diag` writes %AppData%\MultyAudioOutput\diag.txt for troubleshooting.</summary>
internal static class Diagnostics
{
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Windows: {Environment.OSVersion}");
        sb.AppendLine($"AudioPolicyConfig supported: {AudioPolicyConfig.IsSupported}");
        try
        {
            using var audio = new AudioService();
            sb.AppendLine($"Default: {audio.GetDefaultDevice()?.Name}");
            sb.AppendLine("Devices:");
            foreach (var d in audio.GetDevices()) sb.AppendLine($"  {d.Name}  [{d.Id}]");
            sb.AppendLine("Apps:");
            foreach (var a in audio.GetApps())
                sb.AppendLine($"  {a.DisplayName} ({a.Key}) pids={string.Join(",", a.Pids)} active={a.IsActive} vol={a.Volume:0.00} mute={a.Muted} device='{a.CurrentDeviceId}'");

            // --diag-route <exe>: round-trip test (route to a non-default device, read back, reset).
            var args = Environment.GetCommandLineArgs();
            int i = Array.FindIndex(args, x => x.Equals("--diag-route", StringComparison.OrdinalIgnoreCase));
            if (i >= 0 && i + 1 < args.Length)
            {
                var key = args[i + 1];
                var def = audio.GetDefaultDevice();
                var target = audio.GetDevices().First(d => d.Id != def?.Id);
                var pids = audio.RouteApp(key, target.Id);
                sb.AppendLine($"Route {key} → {target.Name}: pids={string.Join(",", pids)}");
                foreach (var pid in pids) sb.AppendLine($"  read back {pid}: '{AudioPolicyConfig.GetDefaultEndpoint(pid)}' match={AudioPolicyConfig.GetDefaultEndpoint(pid) == target.Id}");
                audio.RouteApp(key, null);
                foreach (var pid in pids) sb.AppendLine($"  after reset {pid}: '{AudioPolicyConfig.GetDefaultEndpoint(pid)}'");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"ERROR: {ex}");
        }
        File.WriteAllText(Path.Combine(AppPaths.DataDir, "diag.txt"), sb.ToString());
    }
}
