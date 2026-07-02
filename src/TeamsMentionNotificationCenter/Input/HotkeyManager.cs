using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace TeamsMentionNotificationCenter.Input;

/// <summary>
/// Registriert globale (systemweite) Tastenkürzel über RegisterHotKey und ein
/// Message-Only-Fenster. Muss auf dem WPF-UI-Thread initialisiert werden; die
/// Callbacks laufen ebenfalls dort.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008, MOD_NOREPEAT = 0x4000;

    private HwndSource? _source;
    private IntPtr _hwnd;
    private int _nextId = 1;
    private readonly Dictionary<int, Action> _actions = new();

    public void Initialize()
    {
        var p = new HwndSourceParameters("TeamsMentionNotificationCenterHotkeys")
        {
            Width = 0,
            Height = 0,
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE: unsichtbares Message-Only-Fenster
            WindowStyle = 0
        };
        _source = new HwndSource(p);
        _hwnd = _source.Handle;
        _source.AddHook(WndProc);
    }

    /// <summary>Alle registrierten Hotkeys entfernen (z. B. vor dem Neuladen der Einstellungen).</summary>
    public void Clear()
    {
        if (_hwnd == IntPtr.Zero) return;
        foreach (var id in _actions.Keys) UnregisterHotKey(_hwnd, id);
        _actions.Clear();
    }

    /// <summary>Registriert eine Kombination wie "Ctrl+Alt+Q". Gibt false zurück bei Fehler/Konflikt.</summary>
    public bool Register(string combo, Action action)
    {
        if (_hwnd == IntPtr.Zero) return false;
        if (!TryParse(combo, out uint mods, out uint vk)) return false;
        int id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, mods | MOD_NOREPEAT, vk)) return false;
        _actions[id] = action;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            try { action(); } catch { }
        }
        return IntPtr.Zero;
    }

    private static bool TryParse(string combo, out uint mods, out uint vk)
    {
        mods = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(combo)) return false;

        foreach (var raw in combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= MOD_CONTROL; break;
                case "alt": mods |= MOD_ALT; break;
                case "shift": mods |= MOD_SHIFT; break;
                case "win": case "windows": case "meta": mods |= MOD_WIN; break;
                default:
                    var keyName = raw.Length == 1 && char.IsDigit(raw[0]) ? "D" + raw : raw;
                    if (Enum.TryParse<Key>(keyName, ignoreCase: true, out var key) && key != Key.None)
                        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    else
                        return false;
                    break;
            }
        }
        return vk != 0;
    }

    public void Dispose()
    {
        Clear();
        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
