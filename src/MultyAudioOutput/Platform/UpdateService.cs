using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace MultyAudioOutput.Platform;

internal sealed record UpdateInfo(Version Version, string DownloadUrl, string PageUrl);

/// <summary>
/// Updates from GitHub Releases: the latest release tagged "vX.Y.Z" with a
/// "MultyAudioOutput.exe" asset. The running exe is renamed to *.old (Windows allows
/// renaming a running exe), the new one takes its place and is started with --updated.
/// </summary>
internal static class UpdateService
{
    public const string Repository = "MykhailoKhmaruk/MultyOutputAudioManager";
    private const string AssetName = "MultyAudioOutput.exe";

    private static readonly HttpClient Http = CreateClient();

    public static Version CurrentVersion => Normalize(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0));

    private static string ExePath => Environment.ProcessPath ?? throw new InvalidOperationException("No process path");
    private static string OldExePath => ExePath + ".old";

    /// <summary>Newer release, or null if up to date. Throws on network/API errors.</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        // MULTYAUDIOOUTPUT_UPDATE_API lets you test the updater against a local server.
        var url = Environment.GetEnvironmentVariable("MULTYAUDIOOUTPUT_UPDATE_API")
                  ?? $"https://api.github.com/repos/{Repository}/releases/latest";
        using var response = await Http.GetAsync(url).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null; // no releases yet
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        var root = json.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return null;
        version = Normalize(version);
        if (version <= CurrentVersion) return null;

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (string.Equals(asset.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase))
            {
                return new UpdateInfo(version,
                    asset.GetProperty("browser_download_url").GetString()!,
                    root.GetProperty("html_url").GetString() ?? "");
            }
        }
        return null;
    }

    /// <summary>Downloads the new exe, swaps it with the running one and starts it. Caller then exits.</summary>
    public static async Task InstallAsync(UpdateInfo info)
    {
        var dir = Path.GetDirectoryName(ExePath)!;
        var newPath = Path.Combine(dir, AssetName + ".new");

        using (var response = await Http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var target = File.Create(newPath);
            await source.CopyToAsync(target).ConfigureAwait(false);
        }

        if (!LooksLikeExe(newPath))
        {
            File.Delete(newPath);
            throw new InvalidDataException("Downloaded file is not a valid exe.");
        }

        if (File.Exists(OldExePath)) File.Delete(OldExePath);
        File.Move(ExePath, OldExePath);
        try
        {
            File.Move(newPath, ExePath);
        }
        catch
        {
            File.Move(OldExePath, ExePath); // roll back
            throw;
        }

        Process.Start(new ProcessStartInfo(ExePath, "--updated") { UseShellExecute = false });
    }

    /// <summary>Removes the previous exe left over by an update.</summary>
    public static void CleanupOldVersion()
    {
        try
        {
            if (File.Exists(OldExePath)) File.Delete(OldExePath);
        }
        catch
        {
            // the old process may still be exiting; next start will retry
        }
    }

    private static bool LooksLikeExe(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 1_000_000) return false;
        using var stream = info.OpenRead();
        return stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MultyAudioOutput-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
