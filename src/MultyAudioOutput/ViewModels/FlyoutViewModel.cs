using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultyAudioOutput.Audio;
using MultyAudioOutput.Localization;
using MultyAudioOutput.Platform;

namespace MultyAudioOutput.ViewModels;

public interface IAppActions
{
    /// <summary>deviceId "" = back to the Windows default device.</summary>
    void Route(string key, string displayName, string deviceId);
    void SetVolume(string key, float volume);
    void SetMute(string key, bool mute);
    void OpenSettings();
}

/// <summary>Flyout: one card per output device, apps are dragged between cards.</summary>
public sealed partial class FlyoutViewModel : ObservableObject
{
    private readonly IAppActions _actions;
    private readonly Dictionary<string, AppRowViewModel> _rows = new(StringComparer.OrdinalIgnoreCase);

    public FlyoutViewModel(IAppActions actions) => _actions = actions;

    public ObservableCollection<DeviceGroupViewModel> Groups { get; } = new();

    [ObservableProperty] private bool _hasApps;
    [ObservableProperty] private string _hotkeyHint = "";

    [RelayCommand]
    private void OpenSettings() => _actions.OpenSettings();

    /// <summary>Called on drop: routes the app and moves its row right away (the next refresh confirms it).</summary>
    public void MoveApp(AppRowViewModel row, DeviceGroupViewModel target)
    {
        var source = Groups.FirstOrDefault(g => g.Apps.Contains(row));
        if (source == target) return;
        _actions.Route(row.Key, row.RealName, target.DeviceId);
        source?.Apps.Remove(row);
        target.Apps.Add(row);
        row.IsFollowingDefault = false;
    }

    public void ResetToDefault(AppRowViewModel row) => _actions.Route(row.Key, row.RealName, "");

    public void Update(IReadOnlyList<AppAudio> apps, IReadOnlyList<AudioDevice> devices, AudioDevice? defaultDevice, ISet<string> rememberedKeys)
    {
        // ----- device cards: default device first -----
        var ordered = devices
            .OrderByDescending(d => defaultDevice != null && d.Id == defaultDevice.Id)
            .ToList();

        for (int i = Groups.Count - 1; i >= 0; i--)
        {
            if (!ordered.Any(d => d.Id == Groups[i].DeviceId)) Groups.RemoveAt(i);
        }
        for (int i = 0; i < ordered.Count; i++)
        {
            var device = ordered[i];
            var group = Groups.FirstOrDefault(g => g.DeviceId == device.Id);
            if (group is null)
            {
                group = new DeviceGroupViewModel(device.Id);
                Groups.Insert(Math.Min(i, Groups.Count), group);
            }
            else if (Groups.IndexOf(group) != i)
            {
                Groups.Move(Groups.IndexOf(group), i);
            }
            group.Name = Loc.Name(device.Name);
            group.IsDefault = defaultDevice != null && device.Id == defaultDevice.Id;
        }

        // ----- app rows -----
        var keys = apps.Select(a => a.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var gone in _rows.Keys.Where(k => !keys.Contains(k)).ToList())
            _rows.Remove(gone);

        var wanted = Groups.ToDictionary(g => g, _ => new List<AppRowViewModel>());
        var fallback = Groups.FirstOrDefault(g => g.IsDefault) ?? Groups.FirstOrDefault();

        foreach (var app in apps)
        {
            if (!_rows.TryGetValue(app.Key, out var row))
            {
                row = new AppRowViewModel(app.Key, _actions) { Icon = IconCache.Get(app.ExePath) };
                _rows[app.Key] = row;
            }
            row.Sync(app, rememberedKeys.Contains(app.Key));

            var target = Groups.FirstOrDefault(g => string.Equals(g.DeviceId, app.CurrentDeviceId, StringComparison.OrdinalIgnoreCase));
            row.IsFollowingDefault = target is null;
            target ??= fallback;
            if (target != null) wanted[target].Add(row);
        }

        foreach (var (group, rows) in wanted)
        {
            for (int i = group.Apps.Count - 1; i >= 0; i--)
            {
                if (!rows.Contains(group.Apps[i])) group.Apps.RemoveAt(i);
            }
            foreach (var row in rows)
            {
                if (!group.Apps.Contains(row)) group.Apps.Add(row);
            }
        }

        HasApps = _rows.Count > 0;
    }
}

public sealed partial class DeviceGroupViewModel : ObservableObject
{
    public DeviceGroupViewModel(string deviceId) => DeviceId = deviceId;

    public string DeviceId { get; }
    public ObservableCollection<AppRowViewModel> Apps { get; } = new();

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isDefault;
    [ObservableProperty] private bool _isDropTarget;
}

public sealed partial class AppRowViewModel : ObservableObject
{
    private readonly IAppActions _actions;
    private DateTime _volumeTouchedAt = DateTime.MinValue;
    private bool _suppress;

    public AppRowViewModel(string key, IAppActions actions)
    {
        Key = key;
        _actions = actions;
    }

    public string Key { get; }

    /// <summary>Real app name (DisplayName may be replaced by the selected language).</summary>
    public string RealName { get; private set; } = "";

    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private ImageSource? _icon;
    [ObservableProperty] private double _volume = 100;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isRemembered;
    [ObservableProperty] private bool _isFollowingDefault;
    [ObservableProperty] private bool _isDragging;

    public void Sync(AppAudio app, bool remembered)
    {
        _suppress = true;
        try
        {
            DisplayName = Loc.Name(app.DisplayName);
            RealName = app.DisplayName;
            IsActive = app.IsActive;
            IsRemembered = remembered;
            IsMuted = app.Muted;
            if (DateTime.UtcNow - _volumeTouchedAt > TimeSpan.FromSeconds(2))
                Volume = Math.Round(app.Volume * 100);
        }
        finally
        {
            _suppress = false;
        }
    }

    partial void OnVolumeChanged(double value)
    {
        if (_suppress) return;
        _volumeTouchedAt = DateTime.UtcNow;
        _actions.SetVolume(Key, (float)(value / 100.0));
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (_suppress) return;
        _actions.SetMute(Key, value);
    }
}
