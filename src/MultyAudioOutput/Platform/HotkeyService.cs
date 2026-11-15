using System.Windows.Input;
using System.Windows.Interop;

namespace MultyAudioOutput.Platform;

internal sealed class HotkeyService : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("MultyAudioOutput.Hotkeys")
        {
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE: message-only window
            Width = 0,
            Height = 0,
            WindowStyle = 0,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>Registers (or clears when empty) a global hotkey. Returns false if the combo is invalid or taken.</summary>
    public bool Register(int id, string? gesture, Action action)
    {
        Unregister(id);
        if (string.IsNullOrWhiteSpace(gesture)) return true;
        if (!HotkeyGesture.TryParse(gesture, out var modifiers, out var vk)) return false;
        if (!Native.RegisterHotKey(_source.Handle, id, modifiers | Native.MOD_NOREPEAT, vk)) return false;
        _actions[id] = action;
        return true;
    }

    public void Unregister(int id)
    {
        if (_actions.Remove(id))
            Native.UnregisterHotKey(_source.Handle, id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            try { action(); }
            catch (Exception ex) { Log.Error("Hotkey", ex); }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys.ToList()) Unregister(id);
        _source.Dispose();
    }
}

internal static class HotkeyGesture
{
    public static bool TryParse(string text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": modifiers |= Native.MOD_CONTROL; break;
                case "alt": modifiers |= Native.MOD_ALT; break;
                case "shift": modifiers |= Native.MOD_SHIFT; break;
                case "win": modifiers |= Native.MOD_WIN; break;
                default:
                    if (!Enum.TryParse<Key>(raw, true, out var key)) return false;
                    virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
                    break;
            }
        }
        return modifiers != 0 && virtualKey != 0;
    }

    /// <summary>Builds "Ctrl+Alt+F10" from a key event; null when only modifiers are pressed.</summary>
    public static string? Format(ModifierKeys modifiers, Key key)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift
            or Key.RightShift or Key.LWin or Key.RWin or Key.None)
            return null;
        if (modifiers == ModifierKeys.None) return null;

        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
