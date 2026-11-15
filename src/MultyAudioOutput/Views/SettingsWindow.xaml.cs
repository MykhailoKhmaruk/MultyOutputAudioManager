using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        Reload();
    }

    public void Reload()
    {
        _loading = true;
        var settings = _controller.Settings;
        AutoStartCheck.IsChecked = AutoStart.IsEnabled;
        RememberCheck.IsChecked = settings.RememberChoices;
        CycleHotkeyBox.Text = Display(settings.CycleHotkey);
        FlyoutHotkeyBox.Text = Display(settings.FlyoutHotkey);

        var rows = _controller.GetRuleRows();
        RulesList.ItemsSource = rows;
        NoRulesText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _loading = false;
    }

    private static string Display(string hotkey) => string.IsNullOrWhiteSpace(hotkey) ? "вимкнено" : hotkey;

    private void AutoStart_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AutoStart.Set(AutoStartCheck.IsChecked == true);
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
        HotkeyStatus.Text = ok
            ? "Збережено."
            : $"Не вдалося зареєструвати {gesture} — комбінація зайнята іншою програмою. Спробуйте іншу.";
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
        var answer = MessageBox.Show(this,
            "Видалити всі збережені правила й повернути всі програми на системний пристрій виводу?",
            "MultyAudioOutput", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        _controller.ResetAll();
        Reload();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
