using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;
using MultyAudioOutput.Localization;
using MultyAudioOutput.Platform;

namespace MultyAudioOutput.Tray;

internal sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _openItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly ToolStripMenuItem _languageItem;
    private readonly ToolStripMenuItem _exitItem;
    private Icon? _icon;

    public TrayIconService(Action openFlyout, Action openSettings, Action<string> setLanguage, Action exit)
    {
        var menu = new ContextMenuStrip();
        _openItem = new ToolStripMenuItem("", null, (_, _) => openFlyout()) { Font = new Font(menu.Font, FontStyle.Bold) };
        _settingsItem = new ToolStripMenuItem("", null, (_, _) => openSettings());
        _autoStartItem = new ToolStripMenuItem("") { CheckOnClick = true };
        _autoStartItem.Click += (_, _) => AutoStart.Set(_autoStartItem.Checked);
        _languageItem = new ToolStripMenuItem("");
        foreach (var language in Loc.Languages)
        {
            var code = language.Code;
            _languageItem.DropDownItems.Add(new ToolStripMenuItem(language.NativeName, null, (_, _) => setLanguage(code)) { Tag = code });
        }
        _exitItem = new ToolStripMenuItem("", null, (_, _) => exit());

        menu.Items.Add(_openItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(_languageItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);
        menu.Opening += (_, _) =>
        {
            _autoStartItem.Checked = AutoStart.IsEnabled;
            foreach (ToolStripMenuItem item in _languageItem.DropDownItems)
                item.Checked = (string?)item.Tag == Loc.I.Language;
        };

        _notifyIcon = new NotifyIcon { ContextMenuStrip = menu };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) openFlyout();
        };

        UpdateTexts();
        UpdateTheme();
        _notifyIcon.Visible = true;
    }

    public void UpdateTexts()
    {
        _openItem.Text = Loc.T("tray.open");
        _settingsItem.Text = Loc.T("tray.settings");
        _autoStartItem.Text = Loc.T("tray.autostart");
        _languageItem.Text = Loc.T("tray.language");
        _exitItem.Text = Loc.T("tray.exit");
        var tooltip = Loc.T("tray.tooltip");
        _notifyIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip; // NotifyIcon limit
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
