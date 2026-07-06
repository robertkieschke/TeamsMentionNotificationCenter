using System.Runtime.InteropServices;
using System.Windows.Controls;
using H.NotifyIcon;
using TeamsMentionNotificationCenter.Core;
using TeamsMentionNotificationCenter.Localization;
using TeamsMentionNotificationCenter.Settings;
using Drawing = System.Drawing;

namespace TeamsMentionNotificationCenter.Tray;

/// <summary>
/// System-Tray-Icon mit Kontextmenü (ohne XAML aufgebaut). Das Icon zeigt einen Status-Punkt:
/// grün = Gesprächs-Modus, rot = Ruhe-Modus, grau = Erkennung aus.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly MenuItem _statusItem;
    private readonly MenuItem _modeItem;
    private readonly MenuItem _detectionItem;
    private readonly MenuItem _conversationItem;
    private readonly MenuItem _quietItem;
    private readonly MenuItem _testItem;
    private readonly MenuItem _settingsItem;
    private readonly MenuItem _reloadItem;
    private readonly MenuItem _updateItem;
    private readonly MenuItem _notesItem;
    private readonly MenuItem _exitItem;
    private IntPtr _hIcon;

    private AppMode _mode = AppMode.Quiet;
    private bool _detectionEnabled;

    public TrayIconManager(
        AppSettings settings,
        Action onTestGlow,
        Action onToggleDetection,
        Action onEnterConversation,
        Action onEnterQuiet,
        Action onOpenSettings,
        Action onReloadSettings,
        Action onCheckUpdates,
        Action onShowReleaseNotes,
        Action onExit)
    {
        _detectionEnabled = settings.DetectionEnabled;

        _statusItem = new MenuItem { Header = AppInfo.DisplayName, IsEnabled = false };
        _modeItem = new MenuItem { IsEnabled = false };
        _detectionItem = new MenuItem { IsCheckable = true, IsChecked = _detectionEnabled };
        _detectionItem.Click += (_, _) => onToggleDetection();
        _conversationItem = new MenuItem();
        _conversationItem.Click += (_, _) => onEnterConversation();
        _quietItem = new MenuItem();
        _quietItem.Click += (_, _) => onEnterQuiet();
        _testItem = new MenuItem();
        _testItem.Click += (_, _) => onTestGlow();
        _settingsItem = new MenuItem();
        _settingsItem.Click += (_, _) => onOpenSettings();
        _reloadItem = new MenuItem();
        _reloadItem.Click += (_, _) => onReloadSettings();
        _updateItem = new MenuItem();
        _updateItem.Click += (_, _) => onCheckUpdates();
        _notesItem = new MenuItem();
        _notesItem.Click += (_, _) => onShowReleaseNotes();
        _exitItem = new MenuItem();
        _exitItem.Click += (_, _) => onExit();

        var menu = new ContextMenu();
        menu.Items.Add(_statusItem);
        menu.Items.Add(_modeItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_detectionItem);
        menu.Items.Add(_conversationItem);
        menu.Items.Add(_quietItem);
        menu.Items.Add(_testItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_settingsItem);
        menu.Items.Add(_reloadItem);
        menu.Items.Add(_updateItem);
        menu.Items.Add(_notesItem);
        menu.Items.Add(_exitItem);

        // Fix für das „Aufblitzen" beim Rechtsklick: Eine App ohne sichtbares Fenster ist beim Öffnen des
        // Menüs nicht der Vordergrundprozess, daher schließt Windows das Popup sofort wieder. Sobald das
        // Menü offen ist, wird sein eigenes Popup-Fenster gezielt in den Vordergrund geholt – inklusive
        // kurzzeitiger Umgehung der Windows-Vordergrundsperre (SPI_SETFOREGROUNDLOCKTIMEOUT), die dieses
        // SetForegroundWindow aus einer fensterlosen App sonst sporadisch scheitern lässt (= das Aufblitzen).
        // WICHTIG: Es wird NUR das Menü-Popup selbst nach vorn geholt, kein Hilfsfenster – sonst verliert
        // das Menü den Fokus und schließt sich sofort.
        menu.Opened += (_, _) =>
        {
            if (System.Windows.PresentationSource.FromVisual(menu) is System.Windows.Interop.HwndSource src)
                ForceForeground(src.Handle);
        };

        _icon = new TaskbarIcon
        {
            ToolTipText = AppInfo.DisplayName,
            ContextMenu = menu,
            Icon = BuildIcon()
        };
        _icon.ForceCreate();
        Relocalize();
    }

    /// <summary>Alle Menütexte gemäß aktueller Sprache setzen.</summary>
    public void Relocalize()
    {
        _detectionItem.Header = Loc.T("Erkennung aktiv");
        _conversationItem.Header = Loc.T("In Gesprächs-Modus wechseln");
        _quietItem.Header = Loc.T("In Ruhe-Modus wechseln");
        _testItem.Header = Loc.T("Test: Glow auslösen");
        _settingsItem.Header = Loc.T("Einstellungen…");
        _reloadItem.Header = Loc.T("Einstellungen neu laden (aus Datei)");
        _updateItem.Header = Loc.T("Auf Updates prüfen …");
        _notesItem.Header = Loc.T("Release Notes anzeigen");
        _exitItem.Header = Loc.T("Beenden");
        _modeItem.Header = Loc.T(_mode == AppMode.Conversation ? "Modus: Gespräch" : "Modus: Ruhe");
    }

    public void UpdateStatus(string text, bool available)
    {
        _statusItem.Header = (available ? "● " : "○ ") + text;
        _icon.ToolTipText = $"{AppInfo.DisplayName} – {text}";
    }

    public void SetDetection(bool enabled)
    {
        _detectionEnabled = enabled;
        _detectionItem.IsChecked = enabled;
        _icon.Icon = BuildIcon();
    }

    public void SetMode(AppMode mode)
    {
        _mode = mode;
        _modeItem.Header = Loc.T(mode == AppMode.Conversation ? "Modus: Gespräch" : "Modus: Ruhe");
        _icon.Icon = BuildIcon();
    }

    /// <summary>Marken-Icon (Sprechblasen-Badge) mit Status-Punkt: grün = Gespräch, rot = Ruhe, grau = aus.</summary>
    private Drawing.Icon BuildIcon()
    {
        var prev = _hIcon;
        var dot = !_detectionEnabled
            ? Drawing.Color.FromArgb(0x88, 0x88, 0x88)
            : _mode == AppMode.Conversation
                ? Drawing.Color.FromArgb(0x2E, 0xB8, 0x72)  // grün = Gespräch
                : Drawing.Color.FromArgb(0xE0, 0x3B, 0x2F); // rot = Ruhe
        using var bmp = Branding.RenderTrayBitmap(32, dot);
        _hIcon = bmp.GetHicon();
        var icon = (Drawing.Icon)Drawing.Icon.FromHandle(_hIcon).Clone();
        if (prev != IntPtr.Zero) DestroyIcon(prev);
        return icon;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const uint SPI_GETFOREGROUNDLOCKTIMEOUT = 0x2000;
    private const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out uint pvParam, uint fWinIni);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    /// <summary>Holt <paramref name="hWnd"/> zuverlässig nach vorn. Windows blockiert SetForegroundWindow
    /// normalerweise, wenn der aufrufende Prozess nicht im Vordergrund ist (Vordergrundsperre) – deshalb wird
    /// die Sperre kurz auf 0 gesetzt und danach wieder auf den alten Wert zurückgestellt (nur im Speicher,
    /// nicht persistent).</summary>
    private static void ForceForeground(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;
        uint timeout = 0;
        bool got = false;
        try
        {
            got = SystemParametersInfo(SPI_GETFOREGROUNDLOCKTIMEOUT, 0, out timeout, 0);
            SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, IntPtr.Zero, 0);
            SetForegroundWindow(hWnd);
        }
        finally
        {
            if (got) SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, new IntPtr(timeout), 0);
        }
    }

    public void Dispose()
    {
        _icon.Dispose();
        if (_hIcon != IntPtr.Zero) DestroyIcon(_hIcon);
    }
}
