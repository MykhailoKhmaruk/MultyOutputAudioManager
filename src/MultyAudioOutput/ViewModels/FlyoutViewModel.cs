using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultyAudioOutput.Audio;
using MultyAudioOutput.Platform;

namespace MultyAudioOutput.ViewModels;

/// <summary>Item of the device ComboBox. Id "" = follow the Windows default device.</summary>
public sealed record DeviceOption(string Id, string Name);

public interface IAppActions
{
    void Route(string key, string displayName, string deviceId);
    void SetVolume(string key, float volume);
    void SetMute(string key, bool mute);
    void OpenSettings();
}

public sealed partial class FlyoutViewModel : ObservableObject
{
    private readonly IAppActions _actions;

    public FlyoutViewModel(IAppActions actions) => _actions = actions;

    public ObservableCollection<DeviceOption> Devices { get; } = new();
    public ObservableCollection<AppRowViewModel> Apps { get; } = new();

    [ObservableProperty] private bool _hasApps;
    [ObservableProperty] private string _hotkeyHint = "";

    [RelayCommand]
    private void OpenSettings() => _actions.OpenSettings();

    public void Update(IReadOnlyList<AppAudio> apps, IReadOnlyList<AudioDevice> devices, AudioDevice? defaultDevice, ISet<string> rememberedKeys)
    {
        var options = new List<DeviceOption>
        {
            new("", defaultDevice is null ? "Системний за замовчуванням" : $"Системний ({defaultDevice.Name})"),
        };
        options.AddRange(devices.Select(d => new DeviceOption(d.Id, d.Name)));

        if (!options.SequenceEqual(Devices))
        {
            foreach (var row in Apps) row.Suppress = true;
            Devices.Clear();
            foreach (var o in options) Devices.Add(o);
            foreach (var row in Apps) row.Suppress = false;
        }

        var keys = apps.Select(a => a.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (int i = Apps.Count - 1; i >= 0; i--)
        {
            if (!keys.Contains(Apps[i].Key)) Apps.RemoveAt(i);
        }

        foreach (var app in apps)
        {
            var row = Apps.FirstOrDefault(r => string.Equals(r.Key, app.Key, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                row = new AppRowViewModel(app.Key, Devices, _actions) { Icon = IconCache.Get(app.ExePath) };
                Apps.Add(row);
            }
            row.Sync(app, rememberedKeys.Contains(app.Key));
        }

        HasApps = Apps.Count > 0;
    }
}

public sealed partial class AppRowViewModel : ObservableObject
{
    private readonly IAppActions _actions;
    private DateTime _volumeTouchedAt = DateTime.MinValue;

    public AppRowViewModel(string key, ObservableCollection<DeviceOption> devices, IAppActions actions)
    {
        Key = key;
        Devices = devices;
        _actions = actions;
    }

    public string Key { get; }
    public ObservableCollection<DeviceOption> Devices { get; }

    /// <summary>While true, property changes come from a refresh and are not sent to Windows.</summary>
    internal bool Suppress { get; set; }

    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private ImageSource? _icon;
    [ObservableProperty] private DeviceOption? _selectedDevice;
    [ObservableProperty] private double _volume = 100;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isRemembered;

    public void Sync(AppAudio app, bool remembered)
    {
        Suppress = true;
        try
        {
            DisplayName = app.DisplayName;
            IsActive = app.IsActive;
            IsRemembered = remembered;
            IsMuted = app.Muted;
            if (DateTime.UtcNow - _volumeTouchedAt > TimeSpan.FromSeconds(2))
                Volume = Math.Round(app.Volume * 100);

            var target = Devices.FirstOrDefault(d => string.Equals(d.Id, app.CurrentDeviceId, StringComparison.OrdinalIgnoreCase))
                         ?? Devices.FirstOrDefault();
            if (!Equals(SelectedDevice, target)) SelectedDevice = target;
        }
        finally
        {
            Suppress = false;
        }
    }

    partial void OnSelectedDeviceChanged(DeviceOption? value)
    {
        if (Suppress || value is null) return;
        _actions.Route(Key, DisplayName, value.Id);
    }

    partial void OnVolumeChanged(double value)
    {
        if (Suppress) return;
        _volumeTouchedAt = DateTime.UtcNow;
        _actions.SetVolume(Key, (float)(value / 100.0));
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (Suppress) return;
        _actions.SetMute(Key, value);
    }
}
