using System.Windows;
using System.Windows.Input;
using MultyAudioOutput.ViewModels;

namespace MultyAudioOutput.Views;

public partial class FlyoutWindow : Window
{
    public FlyoutWindow(FlyoutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Deactivated += (_, _) => HideFlyout();
        SizeChanged += (_, _) => Reposition();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) HideFlyout();
        };
    }

    /// <summary>When the flyout was last hidden (used to ignore the tray click that caused it).</summary>
    public DateTime LastHiddenAt { get; private set; } = DateTime.MinValue;

    public void ShowFlyout()
    {
        Show();
        Reposition();
        Activate();
        Focus();
    }

    public void HideFlyout()
    {
        if (!IsVisible) return;
        Hide();
        LastHiddenAt = DateTime.UtcNow;
    }

    private void Reposition()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth;
        Top = area.Bottom - ActualHeight;
    }
}
