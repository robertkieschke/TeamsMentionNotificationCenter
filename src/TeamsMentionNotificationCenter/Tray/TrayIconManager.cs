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
    private readonly MenuItem _missedItem;
    private readonly MenuItem _exitItem;
    private int _missedCount;
    private readonly ContextMenu _menu;
    private System.Windows.Window? _fgHelper;
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
        Action onShowMissedMentions,
        Action onExit)
    {
        _detectionEnabled = settings.DetectionEnabled;

        _statusItem = new MenuItem { Header = AppInfo.DisplayName, IsEnabled = false };
        _modeItem = new MenuItem { IsEnabled = false };
        // Menü-Aktionen ENTKOPPELT ausführen: Erst muss sich das Popup schließen und wegzeichnen,
        // dann startet die Aktion. Läuft sie sofort (z. B. der ~300-ms-Aufbau des Einstellungs-
        // fensters), friert das halb geschlossene Popup als weißes Rechteck ein („helles Aufblitzen").
        void Defer(Action action) => System.Windows.Application.Current.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background, action);

        _detectionItem = new MenuItem { IsCheckable = true, IsChecked = _detectionEnabled };
        _detectionItem.Click += (_, _) => Defer(onToggleDetection);
        _conversationItem = new MenuItem();
        _conversationItem.Click += (_, _) => Defer(onEnterConversation);
        _quietItem = new MenuItem();
        _quietItem.Click += (_, _) => Defer(onEnterQuiet);
        _testItem = new MenuItem();
        _testItem.Click += (_, _) => Defer(onTestGlow);
        _settingsItem = new MenuItem();
        _settingsItem.Click += (_, _) => Defer(onOpenSettings);
        _reloadItem = new MenuItem();
        _reloadItem.Click += (_, _) => Defer(onReloadSettings);
        _updateItem = new MenuItem();
        _updateItem.Click += (_, _) => Defer(onCheckUpdates);
        _notesItem = new MenuItem();
        _notesItem.Click += (_, _) => Defer(onShowReleaseNotes);
        _missedItem = new MenuItem { IsEnabled = false };
        _missedItem.Click += (_, _) => Defer(onShowMissedMentions);
        _exitItem = new MenuItem();
        _exitItem.Click += (_, _) => Defer(onExit);

        var menu = new ContextMenu();
        menu.Items.Add(_statusItem);
        menu.Items.Add(_modeItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_detectionItem);
        menu.Items.Add(_conversationItem);
        menu.Items.Add(_quietItem);
        menu.Items.Add(_missedItem);
        menu.Items.Add(_testItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_settingsItem);
        menu.Items.Add(_reloadItem);
        menu.Items.Add(_updateItem);
        menu.Items.Add(_notesItem);
        menu.Items.Add(_exitItem);

        _menu = menu;

        // Kanonische Lösung fürs „Aufblitzen" beim ersten Rechtsklick: Das Menü wird NICHT von der
        // Bibliothek geöffnet (deren internes SetForegroundWindow scheitert an der Windows-
        // Vordergrundsperre), sondern von uns – und zwar erst, NACHDEM ein verstecktes eigenes
        // Fenster per ForceForeground (mit Sperren-Umgehung) den Vordergrund übernommen hat.
        // Dann lässt Windows das Popup zuverlässig offen (TrackPopupMenu-Muster).
        _fgHelper = new System.Windows.Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = System.Windows.WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Opacity = 0,
            Left = -32000,
            Top = -32000
        };
        _fgHelper.SourceInitialized += (_, _) =>
        {
            var h = new System.Windows.Interop.WindowInteropHelper(_fgHelper).Handle;
            int ex = Interop.NativeMethods.GetWindowLong(h, Interop.NativeMethods.GWL_EXSTYLE);
            Interop.NativeMethods.SetWindowLong(h, Interop.NativeMethods.GWL_EXSTYLE,
                ex | Interop.NativeMethods.WS_EX_TOOLWINDOW); // nicht in Alt+Tab
        };
        _fgHelper.Show();

        _icon = new TaskbarIcon
        {
            ToolTipText = AppInfo.DisplayName,
            Icon = BuildIcon()
            // BEWUSST kein ContextMenu zugewiesen – wir öffnen selbst (siehe ShowMenu).
        };
        _icon.TrayRightMouseUp += (_, _) => ShowMenu();
        _icon.TrayMouseDoubleClick += (_, _) => onOpenSettings(); // Doppelklick = Einstellungen (Tray-Konvention)
        _icon.ForceCreate();
        Relocalize();
    }

    private void ShowMenu()
    {
        if (_fgHelper != null)
        {
            var h = new System.Windows.Interop.WindowInteropHelper(_fgHelper).Handle;
            Interop.NativeMethods.ForceForeground(h); // VOR dem Öffnen – entscheidend
        }
        _menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        _menu.IsOpen = true;
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
        SetMissedCount(_missedCount); // lokalisierten Text inkl. Zähler setzen
        _exitItem.Header = Loc.T("Beenden");
        _modeItem.Header = Loc.T(_mode == AppMode.Conversation ? "Modus: Gespräch" : "Modus: Ruhe");
    }

    /// <summary>Zähler der nicht erledigten verpassten Erwähnungen (0 = Menüpunkt deaktiviert).</summary>
    public void SetMissedCount(int count)
    {
        _missedCount = count;
        _missedItem.IsEnabled = count > 0;
        _missedItem.Header = count > 0
            ? Loc.Tf("Verpasste Erwähnungen ({0}) …", count)
            : Loc.T("Verpasste Erwähnungen …");
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

    public void Dispose()
    {
        _icon.Dispose();
        try { _fgHelper?.Close(); } catch { /* App fährt herunter */ }
        if (_hIcon != IntPtr.Zero) DestroyIcon(_hIcon);
    }
}
