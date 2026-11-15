using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace MultyAudioOutput.Platform;

internal static class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool AppsUseLightTheme => ReadFlag("AppsUseLightTheme", false);
    public static bool TaskbarUsesLightTheme => ReadFlag("SystemUsesLightTheme", false);

    public static void Apply(ResourceDictionary resources)
    {
        bool light = AppsUseLightTheme;
        Set(resources, "BgBrush", light ? "#F3F3F3" : "#202020");
        Set(resources, "RowBrush", light ? "#FFFFFF" : "#2B2B2B");
        Set(resources, "FgBrush", light ? "#1A1A1A" : "#FFFFFF");
        Set(resources, "SubtleFgBrush", light ? "#5F5F5F" : "#A8A8A8");
        Set(resources, "ControlBrush", light ? "#FBFBFB" : "#373737");
        Set(resources, "ControlHoverBrush", light ? "#EDEDED" : "#454545");
        Set(resources, "BorderBrush", light ? "#D9D9D9" : "#3D3D3D");
        Set(resources, "PopupBrush", light ? "#FFFFFF" : "#2C2C2C");
        Set(resources, "AccentBrush", light ? "#005FB8" : "#4CC2FF");
        Set(resources, "AccentFgBrush", light ? "#FFFFFF" : "#000000");
    }

    private static void Set(ResourceDictionary resources, string key, string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        resources[key] = brush;
    }

    private static bool ReadFlag(string name, bool fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(name) is int value ? value != 0 : fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
