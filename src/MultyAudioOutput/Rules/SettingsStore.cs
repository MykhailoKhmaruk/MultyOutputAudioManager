using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using MultyAudioOutput.Platform;

namespace MultyAudioOutput.Rules;

public sealed class RouteRule
{
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public sealed class AppSettings
{
    /// <summary>Key = process name in lower case (e.g. "chrome").</summary>
    public Dictionary<string, RouteRule> Rules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool RememberChoices { get; set; } = true;
    /// <summary>UI language code (pt/en/zh/uk/es/ru); empty = pick from the system on first start.</summary>
    public string Language { get; set; } = "";
    public string CycleHotkey { get; set; } = "Ctrl+Alt+F10";
    public string FlyoutHotkey { get; set; } = "Ctrl+Alt+F11";
}

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _path = Path.Combine(AppPaths.DataDir, "settings.json");

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions);
                if (loaded != null)
                {
                    loaded.Rules = new Dictionary<string, RouteRule>(loaded.Rules ?? new(), StringComparer.OrdinalIgnoreCase);
                    Settings = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Load settings", ex);
        }
    }

    public void Save()
    {
        try
        {
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Settings, JsonOptions));
            File.Move(tmp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Error("Save settings", ex);
        }
    }
}
