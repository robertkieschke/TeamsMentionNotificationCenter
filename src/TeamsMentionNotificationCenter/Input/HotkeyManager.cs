using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace TeamsMentionNotificationCenter.Input;

/// <summary>
/// Globale (systemweite) Tastenkürzel über einen Low-Level-Keyboard-Hook (WH_KEYBOARD_LL).
///
/// Bewusst KEIN RegisterHotKey: Windows behandelt AltGr als Strg+Alt, und RegisterHotKey kann
/// links-Alt nicht von AltGr (rechts-Alt) unterscheiden – ein Hotkey wie Ctrl+Alt+Q würde damit
/// AltGr+Q (= @) systemweit schlucken. Dieser Hook wertet nur LINKS-Alt als „Alt" und lässt
/// Tastendrücke mit gehaltenem AltGr grundsätzlich unverändert durch.
///
/// Muss auf dem WPF-UI-Thread initialisiert werden (der Thread pumpt Nachrichten, was der
/// Low-Level-Hook benötigt); Aktionen werden asynchron auf diesen Thread dispatcht, damit der
/// Hook-Callback sofort zurückkehrt (sonst entfernt Windows den Hook nach einem Timeout).
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const int VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5,
                      VK_SHIFT = 0x10, VK_LWIN = 0x5B, VK_RWIN = 0x5C;

    private IntPtr _hook;
    private LowLevelKeyboardProc? _proc; // Referenz halten, sonst räumt der GC den Delegate weg
    private Dispatcher? _dispatcher;
    private readonly Dictionary<int, List<(uint Mods, Action Action)>> _byVk = new();
    private readonly HashSet<int> _swallowedDown = new(); // gedrückte Hotkey-Tasten (Repeats/KeyUp mitschlucken)

    public void Initialize()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _proc = HookCallback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    /// <summary>Alle registrierten Hotkeys entfernen (z. B. vor dem Neuladen der Einstellungen).</summary>
    public void Clear()
    {
        _byVk.Clear();
        _swallowedDown.Clear();
    }

    /// <summary>Registriert eine Kombination wie "Ctrl+Alt+Q". Gibt false zurück, wenn sie nicht parsbar ist.</summary>
    public bool Register(string combo, Action action)
    {
        if (_hook == IntPtr.Zero) return false;
        if (!TryParse(combo, out uint mods, out uint vk)) return false;
        if (!_byVk.TryGetValue((int)vk, out var list))
            _byVk[(int)vk] = list = new List<(uint, Action)>();
        list.Add((mods, action));
        return true;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || _byVk.Count == 0)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        int msg = wParam.ToInt32();
        int vk = Marshal.ReadInt32(lParam); // KBDLLHOOKSTRUCT.vkCode ist das erste Feld

        if (msg is WM_KEYUP or WM_SYSKEYUP)
        {
            // Zu einem geschluckten KeyDown gehört auch ein geschlucktes KeyUp.
            if (_swallowedDown.Remove(vk)) return (IntPtr)1;
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        if (msg is WM_KEYDOWN or WM_SYSKEYDOWN)
        {
            if (_swallowedDown.Contains(vk)) return (IntPtr)1; // Auto-Repeat: schlucken, nicht erneut auslösen

            if (_byVk.TryGetValue(vk, out var candidates))
            {
                // AltGr (rechts-Alt) gedrückt? Dann wird gerade ein Zeichen getippt (@, €, {, … ) –
                // niemals als Hotkey werten und die Taste unverändert durchreichen.
                if (IsDown(VK_RMENU))
                    return CallNextHookEx(_hook, nCode, wParam, lParam);

                uint mods = 0;
                if (IsDown(VK_LCONTROL) || IsDown(VK_RCONTROL)) mods |= MOD_CONTROL;
                if (IsDown(VK_LMENU)) mods |= MOD_ALT; // nur LINKS-Alt zählt als Alt
                if (IsDown(VK_SHIFT)) mods |= MOD_SHIFT;
                if (IsDown(VK_LWIN) || IsDown(VK_RWIN)) mods |= MOD_WIN;

                foreach (var (wanted, action) in candidates)
                {
                    if (wanted != mods) continue; // exakte Modifier-Übereinstimmung wie RegisterHotKey
                    _swallowedDown.Add(vk);
                    _dispatcher?.BeginInvoke(() => { try { action(); } catch { } });
                    return (IntPtr)1; // Tastendruck schlucken, damit er nicht zusätzlich in der App landet
                }
            }
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

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
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _proc = null;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
