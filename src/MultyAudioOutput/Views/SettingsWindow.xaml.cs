using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MultyAudioOutput.Localization;
using MultyAudioOutput.Platform;

namespace MultyAudioOutput.Views;

public sealed record RuleRow(string Key, string DisplayName, string ExeLabel, string DeviceName, string Status);

public partial class SettingsWindow : Window
{
    private readonly AppController _controller;
    private bool _loading;

    internal SettingsWindow(AppController controller)
    {
        _controller = controller;
        InitializeComponent();
        LanguageCombo.ItemsSource = Loc.Languages;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
        Reload();
    }

    public void Reload()
    {
        _loading = true;
        var settings = _controller.Settings;
        LanguageCombo.SelectedItem = Loc.Languages.FirstOrDefault(l => l.Code == Loc.I.Language);
        AutoStartCheck.IsChecked = AutoStart.IsEnabled;
        RememberCheck.IsChecked = settings.RememberChoices;
        CycleHotkeyBox.Text = Display(settings.CycleHotkey);
        FlyoutHotkeyBox.Text = Display(settings.FlyoutHotkey);
        HotkeyStatus.Text = Loc.T("settings.hotkeyHint");
        VersionText.Text = Loc.F("settings.version", UpdateService.CurrentVersion.ToString(3));
        AutoUpdateCheck.IsChecked = settings.CheckForUpdates;

        var rows = _controller.GetRuleRows();
        RulesList.ItemsSource = rows;
        NoRulesText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _loading = false;
    }

    private void OnLanguageChanged() => Reload();

    private static string Display(string hotkey) =>
        string.IsNullOrWhiteSpace(hotkey) ? Loc.T("settings.off") : hotkey;

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageCombo.SelectedItem is not LanguageOption option) return;
        _controller.SetLanguage(option.Code);
    }

    private void AutoStart_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AutoStart.Set(AutoStartCheck.IsChecked == true);
    }

    private void AutoUpdate_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _controller.SetCheckForUpdates(AutoUpdateCheck.IsChecked == true);
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateStatus.Text = "…";
        UpdateStatus.Text = await _controller.CheckForUpdatesAsync(manual: true);
        CheckUpdatesButton.IsEnabled = true;
    }

    private void Remember_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _controller.SetRemember(RememberCheck.IsChecked == true);
    }

    private void Hotkey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var box = (TextBox)sender;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        string? gesture;
        if (key is Key.Back or Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
            gesture = "";
        else
            gesture = HotkeyGesture.Format(Keyboard.Modifiers, key);

        if (gesture is null) return; // only modifiers pressed so far

        bool ok = _controller.SetHotkey((string)box.Tag == "cycle", gesture);
        box.Text = Display(gesture);
        HotkeyStatus.Text = ok ? Loc.T("settings.saved") : Loc.F("settings.hotkeyTaken", gesture);
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key })
        {
            _controller.RemoveRule(key);
            Reload();
        }
    }

    private void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this, Loc.T("settings.resetConfirm"), "MultyAudioOutput",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        _controller.ResetAll();
        Reload();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
