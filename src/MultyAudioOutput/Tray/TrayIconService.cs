using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;
using MultyAudioOutput.Platform;

namespace MultyAudioOutput.Tray;

internal sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _autoStartItem;
    private Icon? _icon;

    public TrayIconService(Action openFlyout, Action openSettings, Action exit)
    {
        _autoStartItem = new ToolStripMenuItem("Запускати разом з Windows") { CheckOnClick = true };
        _autoStartItem.Click += (_, _) => AutoStart.Set(_autoStartItem.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Відкрити панель", null, (_, _) => openFlyout()) { Font = new Font(menu.Font, FontStyle.Bold) });
        menu.Items.Add(new ToolStripMenuItem("Налаштування…", null, (_, _) => openSettings()));
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Вихід", null, (_, _) => exit()));
        menu.Opening += (_, _) => _autoStartItem.Checked = AutoStart.IsEnabled;

        _notifyIcon = new NotifyIcon
        {
            Text = "MultyAudioOutput — вивід звуку для програм",
            ContextMenuStrip = menu,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) openFlyout();
        };

        UpdateTheme();
        _notifyIcon.Visible = true;
    }

    public void ShowInfo(string title, string text) =>
        _notifyIcon.ShowBalloonTip(2000, title, text, ToolTipIcon.None);

    public void ShowWarning(string title, string text) =>
        _notifyIcon.ShowBalloonTip(5000, title, text, ToolTipIcon.Warning);

    /// <summary>Redraws the headphones glyph in the taskbar's light/dark colour.</summary>
    public void UpdateTheme()
    {
        var old = _icon;
        _icon = CreateGlyphIcon(ThemeService.TaskbarUsesLightTheme ? Color.Black : Color.White);
        _notifyIcon.Icon = _icon;
        if (old != null)
        {
            Native.DestroyIcon(old.Handle);
            old.Dispose();
        }
    }

    private static Icon CreateGlyphIcon(Color color)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            using var font = CreateIconFont(26);
            using var brush = new SolidBrush(color);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            // E7F6 = Headphone
            g.DrawString("", font, brush, new RectangleF(0, 1, size, size), format);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static Font CreateIconFont(float px)
    {
        foreach (var family in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
        {
            var font = new Font(family, px, GraphicsUnit.Pixel);
            if (font.Name == family) return font;
            font.Dispose();
        }
        return new Font(FontFamily.GenericSansSerif, px, GraphicsUnit.Pixel);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        if (_icon != null)
        {
            Native.DestroyIcon(_icon.Handle);
            _icon.Dispose();
        }
    }
}
