using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MultyAudioOutput.ViewModels;

namespace MultyAudioOutput.Views;

public partial class FlyoutWindow : Window
{
    private const string DragFormat = "MultyAudioOutput.AppRow";

    private readonly FlyoutViewModel _viewModel;
    private Point _dragStart;
    private AppRowViewModel? _pendingDrag;
    private bool _isDragging;

    public FlyoutWindow(FlyoutViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Deactivated += (_, _) =>
        {
            // A context menu or a drag in progress must not close the flyout.
            if (!_isDragging && !IsContextMenuOpen()) HideFlyout();
        };
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

    // ---------- drag source: app chips ----------

    private void Row_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Slider and mute button keep their normal behaviour.
        if (IsInside<Slider>(e.OriginalSource) || IsInside<ButtonBase>(e.OriginalSource))
        {
            _pendingDrag = null;
            return;
        }
        _dragStart = e.GetPosition(this);
        _pendingDrag = (sender as FrameworkElement)?.DataContext as AppRowViewModel;
    }

    private void Row_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pendingDrag is null || e.LeftButton != MouseButtonState.Pressed) return;

        var delta = e.GetPosition(this) - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var row = _pendingDrag;
        _pendingDrag = null;
        _isDragging = true;
        row.IsDragging = true;
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(DragFormat, row), DragDropEffects.Move);
        }
        finally
        {
            row.IsDragging = false;
            _isDragging = false;
            foreach (var group in _viewModel.Groups) group.IsDropTarget = false;
        }
    }

    // ---------- drop target: device cards ----------

    private void Group_DragOver(object sender, DragEventArgs e)
    {
        var group = (sender as FrameworkElement)?.DataContext as DeviceGroupViewModel;
        var row = e.Data.GetData(DragFormat) as AppRowViewModel;
        bool canDrop = group != null && row != null && !group.Apps.Contains(row);

        e.Effects = canDrop ? DragDropEffects.Move : DragDropEffects.None;
        foreach (var g in _viewModel.Groups) g.IsDropTarget = canDrop && g == group;
        e.Handled = true;
    }

    private void Group_DragLeave(object sender, DragEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DeviceGroupViewModel group)
            group.IsDropTarget = false;
    }

    private void Group_Drop(object sender, DragEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DeviceGroupViewModel group &&
            e.Data.GetData(DragFormat) is AppRowViewModel row)
        {
            group.IsDropTarget = false;
            _viewModel.MoveApp(row, group);
        }
        e.Handled = true;
    }

    private void ResetToDefault_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is AppRowViewModel row)
            _viewModel.ResetToDefault(row);
    }

    // ---------- helpers ----------

    private static bool IsInside<T>(object source) where T : DependencyObject
    {
        var current = source as DependencyObject;
        while (current != null)
        {
            if (current is T) return true;
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return false;
    }

    private bool IsContextMenuOpen() =>
        ContextMenuService.GetContextMenu(this)?.IsOpen == true
        || Mouse.Captured is ContextMenu || Mouse.Captured is MenuItem;
}
