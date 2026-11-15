using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using MultyAudioOutput.Audio;
using MultyAudioOutput.Localization;
using MultyAudioOutput.Platform;
using MultyAudioOutput.Rules;
using MultyAudioOutput.Tray;
using MultyAudioOutput.ViewModels;
using MultyAudioOutput.Views;

namespace MultyAudioOutput;

/// <summary>Wires the tray icon, flyout, rules, hotkeys and the polling timer together.</summary>
internal sealed class AppController : IAppActions, IDisposable
{
    private const int HotkeyCycleId = 1;
    private const int HotkeyFlyoutId = 2;

    private readonly Application _app;
    private readonly AudioService _audio = new();
    private readonly SettingsStore _store = new();
    private readonly RuleEngine _rules;
    private readonly FlyoutViewModel _flyoutVm;
    private readonly DispatcherTimer _timer;
    private TrayIconService? _tray;
    private HotkeyService? _hotkeys;
    private FlyoutWindow? _flyout;
    private SettingsWindow? _settingsWindow;

    public AppController(Application app)
    {
        _app = app;
        _rules = new RuleEngine(_audio, () => Settings);
        _flyoutVm = new FlyoutViewModel(this);
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Tick();
    }

    public AppSettings Settings => _store.Settings;

    public void Start(bool minimized)
    {
        _store.Load();
        if (string.IsNullOrEmpty(Settings.Language)) Settings.Language = Loc.DefaultLanguage;
        Loc.I.SetLanguage(Settings.Language, userChoice: false);
        Loc.I.LanguageChanged += OnLanguageChanged;
        ThemeService.Apply(_app.Resources);
        AutoStart.RefreshPathIfEnabled();

        _tray = new TrayIconService(ToggleFlyout, OpenSettings, SetLanguage, () => _app.Shutdown());
        _hotkeys = new HotkeyService();
        RegisterHotkeys(showErrors: true);

        _flyout = new FlyoutWindow(_flyoutVm);
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        if (!AudioPolicyConfig.IsSupported)
        {
            _tray.ShowWarning("MultyAudioOutput", Loc.T("tray.unsupported"));
        }
        else if (!minimized)
        {
            _tray.ShowInfo(Loc.T("tray.runningTitle"), Loc.T("tray.runningText"));
        }

        Tick();
        _timer.Start();
    }

    // ---------- polling ----------

    private void Tick()
    {
        try
        {
            var devices = _audio.GetDevices();
            _rules.Tick(devices);
            if (_flyout?.IsVisible == true) RefreshFlyout(devices);
        }
        catch (Exception ex)
        {
            Log.Error("Tick", ex);
        }
    }

    private void RefreshFlyout(IReadOnlyList<AudioDevice>? devices = null)
    {
        devices ??= _audio.GetDevices();
        var apps = _audio.GetApps();
        var remembered = Settings.Rules.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _flyoutVm.Update(apps, devices, _audio.GetDefaultDevice(), remembered);
    }

    // ---------- flyout / windows ----------

    public void ToggleFlyout()
    {
        if (_flyout is null) return;
        if (_flyout.IsVisible)
        {
            _flyout.HideFlyout();
            return;
        }
        // The tray click itself deactivates (and hides) the flyout first; don't reopen it.
        if (DateTime.UtcNow - _flyout.LastHiddenAt < TimeSpan.FromMilliseconds(400)) return;
        ShowFlyout();
    }

    public void ShowFlyout()
    {
        if (_flyout is null) return;
        UpdateHotkeyHint();
        try { RefreshFlyout(); }
        catch (Exception ex) { Log.Error("RefreshFlyout", ex); }
        _flyout.ShowFlyout();
    }

    public void OpenSettings()
    {
        _flyout?.HideFlyout();
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Reload();
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(this);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    // ---------- IAppActions ----------

    public void Route(string key, string displayName, string deviceId)
    {
        var id = string.IsNullOrEmpty(deviceId) ? null : deviceId;
        var pids = _audio.RouteApp(key, id);

        if (id is null || !Settings.RememberChoices)
        {
            Settings.Rules.Remove(key);
            _rules.Forget(key);
        }
        else
        {
            var deviceName = _audio.GetDevices().FirstOrDefault(d => d.Id == id)?.Name ?? "";
            Settings.Rules[key] = new RouteRule { DeviceId = id, DeviceName = deviceName, DisplayName = displayName };
            _rules.MarkApplied(key, pids, id);
        }
        _store.Save();

        if (_flyout?.IsVisible == true)
        {
            // Let the ComboBox finish its selection before refreshing the rows.
            _app.Dispatcher.BeginInvoke(() => RefreshFlyout(), DispatcherPriority.Background);
        }
    }

    public void SetVolume(string key, float volume)
    {
        try { _audio.SetVolume(key, volume); }
        catch (Exception ex) { Log.Error("SetVolume", ex); }
    }

    public void SetMute(string key, bool mute)
    {
        try { _audio.SetMute(key, mute); }
        catch (Exception ex) { Log.Error("SetMute", ex); }
    }

    // ---------- hotkeys ----------

    private void RegisterHotkeys(bool showErrors)
    {
        if (_hotkeys is null) return;
        var failed = new List<string>();
        if (!_hotkeys.Register(HotkeyCycleId, Settings.CycleHotkey, CycleForegroundApp)) failed.Add(Settings.CycleHotkey);
        if (!_hotkeys.Register(HotkeyFlyoutId, Settings.FlyoutHotkey, ToggleFlyout)) failed.Add(Settings.FlyoutHotkey);
        if (showErrors && failed.Count > 0)
            _tray?.ShowWarning(Loc.T("tray.hotkeysTitle"), Loc.F("tray.hotkeysFailed", string.Join(", ", failed)));
    }

    /// <summary>Moves the app of the foreground window to the next output device.</summary>
    private void CycleForegroundApp()
    {
        var pid = Native.GetForegroundProcessId();
        if (pid == 0 || pid == Environment.ProcessId) return;
        var info = _audio.GetProcessInfo(pid);
        if (info is null) return;

        var devices = _audio.GetDevices();
        if (devices.Count == 0) return;

        string current = Settings.Rules.TryGetValue(info.Key, out var rule)
            ? rule.DeviceId
            : AudioPolicyConfig.GetDefaultEndpoint(pid);
        if (string.IsNullOrEmpty(current)) current = _audio.GetDefaultDevice()?.Id ?? "";

        int index = devices.FindIndex(d => string.Equals(d.Id, current, StringComparison.OrdinalIgnoreCase));
        var next = devices[(index + 1) % devices.Count];

        Route(info.Key, info.DisplayName, next.Id);
        _tray?.ShowInfo(Loc.Name(info.DisplayName), Loc.F("tray.soundTo", Loc.Name(next.Name)));
    }

    // ---------- settings window API ----------

    public bool SetHotkey(bool cycle, string gesture)
    {
        if (cycle) Settings.CycleHotkey = gesture;
        else Settings.FlyoutHotkey = gesture;
        _store.Save();

        if (_hotkeys is null) return false;
        return cycle
            ? _hotkeys.Register(HotkeyCycleId, gesture, CycleForegroundApp)
            : _hotkeys.Register(HotkeyFlyoutId, gesture, ToggleFlyout);
    }

    public void SetLanguage(string code)
    {
        Settings.Language = code;
        _store.Save();
        Loc.I.SetLanguage(code, userChoice: true);
    }

    private void OnLanguageChanged()
    {
        _tray?.UpdateTexts();
        UpdateHotkeyHint();
        if (_flyout?.IsVisible == true) RefreshFlyout();
    }

    private void UpdateHotkeyHint() =>
        _flyoutVm.HotkeyHint = string.IsNullOrWhiteSpace(Settings.CycleHotkey)
            ? ""
            : Loc.F("flyout.hotkeyHint", Settings.CycleHotkey);

    public void SetRemember(bool remember)
    {
        Settings.RememberChoices = remember;
        _store.Save();
    }

    public List<RuleRow> GetRuleRows()
    {
        var present = _audio.GetDevices().Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Settings.Rules
            .OrderBy(r => r.Value.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(r => new RuleRow(
                r.Key,
                Loc.Name(string.IsNullOrWhiteSpace(r.Value.DisplayName) ? r.Key : r.Value.DisplayName),
                Loc.I.IsRussian ? "" : $"({r.Key}.exe)",
                Loc.Name(string.IsNullOrWhiteSpace(r.Value.DeviceName) ? r.Value.DeviceId : r.Value.DeviceName),
                present.Contains(r.Value.DeviceId) ? "" : Loc.T("settings.notConnected")))
            .ToList();
    }

    public void RemoveRule(string key)
    {
        Settings.Rules.Remove(key);
        _rules.Forget(key);
        _store.Save();
        _audio.RouteApp(key, null);
    }

    public void ResetAll()
    {
        foreach (var key in Settings.Rules.Keys.ToList())
        {
            _audio.RouteApp(key, null);
            _rules.Forget(key);
        }
        Settings.Rules.Clear();
        _store.Save();
        try { AudioPolicyConfig.ClearAll(); }
        catch (Exception ex) { Log.Error("ClearAll", ex); }
    }

    // ---------- misc ----------

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        _app.Dispatcher.BeginInvoke(() =>
        {
            ThemeService.Apply(_app.Resources);
            _tray?.UpdateTheme();
        });
    }

    public void Dispose()
    {
        _timer.Stop();
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        Loc.I.LanguageChanged -= OnLanguageChanged;
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _audio.Dispose();
    }
}
