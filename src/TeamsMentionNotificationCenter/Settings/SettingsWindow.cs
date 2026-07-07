using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TeamsMentionNotificationCenter.Core;
using TeamsMentionNotificationCenter.Interop;
using TeamsMentionNotificationCenter.Localization;

namespace TeamsMentionNotificationCenter.Settings;

/// <summary>
/// In-App-Einstellungsoberfläche (Tab-Ansicht, mehrsprachig über <see cref="Loc"/>).
/// Übernehmen schreibt die Werte in die laufende App (Callback), das Fenster bleibt offen.
/// Tabs mit ungespeicherten Änderungen erhalten ein "*"; "Verwerfen" stellt den letzten Stand her;
/// der Info-Tab bietet "Auf Standard zurücksetzen". Bei Sprachwechsel wird das Fenster neu aufgebaut.
/// </summary>
public sealed class SettingsWindow : Window
{
    private readonly AppSettings _current;
    private readonly Core.MentionStore _mentionStore;
    private readonly Action<AppSettings> _onApply;
    private readonly Action<AppSettings> _onTestGlow;
    private readonly Action<AppSettings> _onTestBanner;

    // --- Eingabefelder ---
    private readonly TextBox _triggerWords = Multiline(90);
    private readonly TextBox _ownSpeaker = new();
    private readonly CheckBox _ignoreOwn = new() { Content = Loc.T("Nicht auslösen, wenn ich selbst spreche") };
    private readonly CheckBox _fuzzy = new() { Content = Loc.T("Fuzzy-Match (Erkennungsfehler tolerieren)") };
    private readonly TextBox _fuzzyDist = Num();
    private readonly TextBox _cooldown = Num();

    private readonly ComboBox _quietBehavior = Combo();
    private readonly TextBox _quietLevel = Num();
    private readonly TextBox _convLevel = Num();
    private readonly TextBox _musicHint = new() { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly CheckBox _manageTeams = new() { Content = Loc.T("Teams-Ton automatisch steuern (laut/leise)") };
    private readonly CheckBox _manageMusic = new() { Content = Loc.T("Musik automatisch steuern (Pause/Fortsetzen)") };
    private readonly CheckBox _autoConv = new() { Content = Loc.T("Bei Namensnennung automatisch in den Gesprächs-Modus") };
    private readonly CheckBox _convOnCall = new() { Content = Loc.T("Bei eingehendem Anruf automatisch in den Gesprächs-Modus (Klingeln wird laut)") };
    private readonly List<CheckBox> _audioExcludeChecks = new();
    private readonly List<string> _audioExcludeIds = new();

    private readonly TextBox _glowColor = new() { Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _glowThickness = Num();
    private readonly TextBox _glowDuration = Num();
    private readonly ComboBox _persistentBorderMode = Combo();
    private readonly List<CheckBox> _monitorChecks = new();

    private readonly CheckBox _bannerEnabled = new() { Content = Loc.T("Bei Erkennung einblenden, wer dich gerufen hat") };
    private readonly TextBox _bannerText = new() { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ComboBox _bannerVert = Combo();
    private readonly ComboBox _bannerHorz = Combo();
    private readonly TextBox _bannerSize = Num();
    private readonly TextBox _bannerColor = new() { Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _bannerDuration = Num();
    private readonly TextBox _bannerOpacity = Num();
    private readonly List<CheckBox> _bannerMonitorChecks = new();

    private readonly CheckBox _soundEnabled = new() { Content = Loc.T("Bei Erkennung zusätzlich einen Ton abspielen") };
    private readonly ComboBox _soundFile = Combo();
    private readonly List<string> _soundPaths = new();
    private readonly TextBox _soundVolume = Num();
    private readonly ComboBox _soundDevice = new() { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly List<string> _soundDeviceIds = new();

    private readonly TextBox _hotkeyToggle = new() { Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _hotkeyQuiet = new() { Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _hotkeyConv = new() { Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _hotkeyDetection = new() { Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox _hotkeyMissed = new() { Width = 160, HorizontalAlignment = HorizontalAlignment.Left };

    private readonly ComboBox _language = Combo();
    private readonly ComboBox _themeMode = Combo();
    private readonly CheckBox _startInConversation = new() { Content = Loc.T("Beim Start im Gesprächs-Modus starten (erster Ruhe-Modus muss aktiv gewählt werden)") };
    private readonly CheckBox _autoReturn = new() { Content = Loc.T("Automatisch zurück in den Ruhe-Modus, wenn ich eine Zeit lang nichts sage") };
    private readonly TextBox _autoReturnSec = Num();
    private readonly CheckBox _autoReturnManual = new() { Content = Loc.T("Auto-Rückkehr auch bei manueller Aktivierung (Shortcut/Menü)") };
    private readonly CheckBox _detection = new() { Content = Loc.T("Erkennung aktiv") };
    private readonly CheckBox _autostart = new() { Content = Loc.T("Mit Windows starten") };
    private readonly CheckBox _checkUpdates = new() { Content = Loc.T("Beim Start auf Updates prüfen") };
    private readonly CheckBox _silentUpdate = new() { Content = Loc.T("Updates automatisch im Hintergrund installieren (ohne Nachfrage)") };
    private readonly CheckBox _showNotes = new() { Content = Loc.T("Nach einem Update die Versionshinweise anzeigen") };
    private readonly CheckBox _offerInstall = new() { Content = Loc.T("Installation nach %LOCALAPPDATA%\\Programs anbieten (bei portablem Start)") };
    private readonly CheckBox _debug = new() { Content = Loc.T("Debug-Log schreiben (%APPDATA%\\TeamsMentionNotificationCenter\\log.txt)") };
    private readonly TextBox _pollInterval = Num();

    // --- Rahmen ---
    private readonly Button _applyButton = new()
    {
        Content = Loc.T("Übernehmen"),
        Padding = new Thickness(14, 5, 14, 5),
        IsDefault = true,
        IsEnabled = false
    };
    private readonly Button _discardButton = new()
    {
        Content = Loc.T("Verwerfen"),
        Padding = new Thickness(14, 5, 14, 5),
        Margin = new Thickness(8, 0, 0, 0),
        IsEnabled = false
    };
    private readonly TextBlock _statusText = new()
    {
        Foreground = System.Windows.Media.Brushes.SeaGreen,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 12, 0)
    };

    private readonly List<TabItem> _tabs = new();
    private readonly Dictionary<FrameworkElement, TabItem> _tabOf = new();
    private readonly Dictionary<TabItem, TextBlock> _tabHeaderText = new(); // Text-Teil des Headers (für "*")
    private readonly TabControl _tabControl = new();
    private TabItem? _notesTab;

    // --- Verpasst-Tab ---
    private readonly StackPanel _missedListHost = new();
    private readonly CheckBox _missedEnabled = new() { Content = Loc.T("Nennungen ohne Antwort als verpasst erfassen") };
    private readonly TextBox _mentionTimeout = Num();
    private readonly TextBox _mentionRepeat = Num();
    private readonly TextBox _mentionRetention = Num();
    private readonly ComboBox _missedVert = Combo();
    private readonly ComboBox _missedHorz = Combo();
    private readonly TextBox _snoozePresets = new() { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
    private bool _suppressDirty;
    private static readonly System.Windows.Media.Brush DirtyBrush = System.Windows.Media.Brushes.RoyalBlue;

    public SettingsWindow(AppSettings current, Core.MentionStore mentions, Action<AppSettings> onApply, Action<AppSettings> onTestGlow, Action<AppSettings> onTestBanner)
    {
        _current = current;
        _mentionStore = mentions;
        _onApply = onApply;
        _onTestGlow = onTestGlow;
        _onTestBanner = onTestBanner;

        Title = AppInfo.DisplayName + Loc.T(" – Einstellungen");
        Width = 1000;
        Height = 760;
        MinWidth = 820;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = Branding.CreateImageSource(64, Branding.Accent);
        Core.Theme.Prepare(this); // Win11-Look: Theme-Farben + dunkle Titelleiste

        _quietBehavior.Items.Add(Loc.T("Leiser stellen"));
        _quietBehavior.Items.Add(Loc.T("Stumm schalten"));
        _persistentBorderMode.Items.Add(Loc.T("Nie"));
        _persistentBorderMode.Items.Add(Loc.T("Nur bei Namensnennung (Trigger)"));
        _persistentBorderMode.Items.Add(Loc.T("Immer im Gesprächs-Modus"));
        _bannerVert.Items.Add(Loc.T("Oben"));
        _bannerVert.Items.Add(Loc.T("Mitte"));
        _bannerVert.Items.Add(Loc.T("Unten"));
        _bannerHorz.Items.Add(Loc.T("Links"));
        _bannerHorz.Items.Add(Loc.T("Mitte"));
        _bannerHorz.Items.Add(Loc.T("Rechts"));
        _missedVert.Items.Add(Loc.T("Oben"));
        _missedVert.Items.Add(Loc.T("Mitte"));
        _missedVert.Items.Add(Loc.T("Unten"));
        _missedHorz.Items.Add(Loc.T("Links"));
        _missedHorz.Items.Add(Loc.T("Mitte"));
        _missedHorz.Items.Add(Loc.T("Rechts"));
        _language.Items.Add("Deutsch");
        _language.Items.Add("English");
        _language.Items.Add("Italiano");
        _themeMode.Items.Add(Loc.T("System"));
        _themeMode.Items.Add(Loc.T("Hell"));
        _themeMode.Items.Add(Loc.T("Dunkel"));

        var rects = NativeMethods.GetMonitorRects();
        for (int i = 0; i < rects.Count; i++)
        {
            var r = rects[i];
            bool primary = r.Left == 0 && r.Top == 0;
            string label = $"Monitor {i + 1} – {r.Width}×{r.Height} @ ({r.Left},{r.Top}){(primary ? "  " + Loc.T("[primär]") : "")}";
            _monitorChecks.Add(new CheckBox { Content = label, Margin = new Thickness(0, 2, 0, 2) });
            _bannerMonitorChecks.Add(new CheckBox { Content = label, Margin = new Thickness(0, 2, 0, 2) });
        }

        foreach (var snd in SoundNotifier.GetAvailableSounds())
        {
            _soundFile.Items.Add(snd.Display);
            _soundPaths.Add(snd.Path);
        }
        _soundDevice.Items.Add(Loc.T("Standard (Standardgerät)"));
        _soundDeviceIds.Add("");
        foreach (var dev in SoundNotifier.GetOutputDevices())
        {
            _soundDevice.Items.Add(dev.Name);
            _soundDeviceIds.Add(dev.Id);
            _audioExcludeChecks.Add(new CheckBox { Content = dev.Name, Margin = new Thickness(0, 2, 0, 2) });
            _audioExcludeIds.Add(dev.Id);
        }

        var glowTest = TestButton(Loc.T("Glow testen"));
        glowTest.Click += (_, _) => { try { _onTestGlow(BuildSettings()); } catch { } };
        var bannerTest = TestButton(Loc.T("Einblendung testen"));
        bannerTest.Click += (_, _) => { try { _onTestBanner(BuildSettings()); } catch { } };
        var soundTest = TestButton(Loc.T("Ton testen"));
        soundTest.Click += (_, _) => SoundNotifier.Play(SelectedSoundPath(), ParseInt(_soundVolume.Text, 100, 0, 100), SelectedDeviceId());

        var tabControl = _tabControl;

        tabControl.Items.Add(BuildMissedTab()); // erster Tab

        tabControl.Items.Add(MakeTab(Loc.T("Erkennung"), "",
            new UIElement[]
            {
                Labeled(Loc.T("Trigger-Wörter (eines pro Zeile):"), _triggerWords),
                Group(Row(Loc.T("Eigener Name (im Transkript):"), _ownSpeaker), _ignoreOwn),
                Group(_fuzzy, Row(Loc.T("Fuzzy-Toleranz (max. Abweichung):"), _fuzzyDist)),
                Row(Loc.T("Cooldown zwischen Auslösungen (ms):"), _cooldown)
            },
            new FrameworkElement[] { _triggerWords, _ownSpeaker, _ignoreOwn, _fuzzy, _fuzzyDist, _cooldown }));

        var deviceGroup = new List<UIElement>
        {
            new TextBlock { Text = Loc.T("Diese Wiedergabegeräte nie automatisch anpassen:"), FontWeight = FontWeights.SemiBold }
        };
        deviceGroup.AddRange(_audioExcludeChecks);
        deviceGroup.Add(new TextBlock
        {
            Text = Loc.T("Tipp: In Teams ein 'Zweites Rufsignal' auf ein hier ausgeschlossenes Gerät legen (z. B. Lautsprecher) – dann bleibt das Klingeln zusätzlicher Anrufe immer laut, während der Call leise wird."),
            Foreground = System.Windows.Media.Brushes.Gray,
            TextWrapping = TextWrapping.Wrap
        });
        var audioRows = new List<UIElement>
        {
            Group(Row(Loc.T("Teams im Ruhe-Modus:"), _quietBehavior),
                  Row(Loc.T("Teams Ruhe-Lautstärke (%):"), _quietLevel),
                  Row(Loc.T("Teams Gesprächs-Lautstärke (%):"), _convLevel)),
            Row(Loc.T("Musik-App (SMTC-Kennung):"), _musicHint),
            Group(_manageTeams, _manageMusic, _autoConv, _convOnCall),
            Group(deviceGroup.ToArray())
        };
        var audioInputs = new List<FrameworkElement> { _quietBehavior, _quietLevel, _convLevel, _musicHint, _manageTeams, _manageMusic, _autoConv, _convOnCall };
        audioInputs.AddRange(_audioExcludeChecks);
        tabControl.Items.Add(MakeTab(Loc.T("Ton & Musik"), "", audioRows.ToArray(), audioInputs.ToArray()));

        var glowMonitorGroup = new List<UIElement>
        {
            new TextBlock { Text = Loc.T("Zu beleuchtende Monitore (nichts markiert = alle):"), FontWeight = FontWeights.SemiBold }
        };
        glowMonitorGroup.AddRange(_monitorChecks);
        glowMonitorGroup.Add(glowTest);
        var glowRows = new List<UIElement>
        {
            Group(Row(Loc.T("Farbe (Hex, z. B. #FF3B30):"), _glowColor),
                  Row(Loc.T("Dicke:"), _glowThickness),
                  Row(Loc.T("Dauer (ms):"), _glowDuration),
                  Row(Loc.T("Dauer-Rand im Gespräch zeigen:"), _persistentBorderMode)),
            Group(glowMonitorGroup.ToArray())
        };
        var glowInputs = new List<FrameworkElement> { _glowColor, _glowThickness, _glowDuration, _persistentBorderMode };
        glowInputs.AddRange(_monitorChecks);
        tabControl.Items.Add(MakeTab(Loc.T("Glow-Rand"), "", glowRows.ToArray(), glowInputs.ToArray()));

        var bannerMonitorGroup = new List<UIElement>
        {
            new TextBlock { Text = Loc.T("Monitore für die Einblendung (nichts markiert = alle):"), FontWeight = FontWeights.SemiBold }
        };
        bannerMonitorGroup.AddRange(_bannerMonitorChecks);
        bannerMonitorGroup.Add(bannerTest);
        var bannerRows = new List<UIElement>
        {
            _bannerEnabled,
            Group(Row(Loc.T("Text ({Name} = Sprecher):"), _bannerText),
                  Row(Loc.T("Vertikale Position:"), _bannerVert),
                  Row(Loc.T("Horizontale Position:"), _bannerHorz),
                  Row(Loc.T("Schriftgröße:"), _bannerSize),
                  Row(Loc.T("Farbe (Hex, z. B. #FF3B30):"), _bannerColor),
                  Row(Loc.T("Anzeigedauer (ms):"), _bannerDuration),
                  Row(Loc.T("Deckkraft (%):"), _bannerOpacity)),
            Group(bannerMonitorGroup.ToArray())
        };
        var bannerInputs = new List<FrameworkElement> { _bannerEnabled, _bannerText, _bannerVert, _bannerHorz, _bannerSize, _bannerColor, _bannerDuration, _bannerOpacity };
        bannerInputs.AddRange(_bannerMonitorChecks);
        tabControl.Items.Add(MakeTab(Loc.T("Einblendung"), "", bannerRows.ToArray(), bannerInputs.ToArray()));

        tabControl.Items.Add(MakeTab(Loc.T("Signalton"), "",
            new UIElement[]
            {
                Group(_soundEnabled,
                      Row(Loc.T("Ton:"), _soundFile),
                      Row(Loc.T("Lautstärke (%):"), _soundVolume),
                      Row(Loc.T("Ausgabegerät:"), _soundDevice),
                      soundTest,
                      new TextBlock
                      {
                          Text = Loc.T("Auswahl aus %WINDIR%\\Media. Der Test nutzt die aktuell eingestellten Werte, ohne zu speichern."),
                          Foreground = System.Windows.Media.Brushes.Gray,
                          TextWrapping = TextWrapping.Wrap
                      })
            },
            new FrameworkElement[] { _soundEnabled, _soundFile, _soundVolume, _soundDevice }));

        tabControl.Items.Add(MakeTab(Loc.T("Tastenkürzel"), "",
            new UIElement[]
            {
                Group(new TextBlock { Text = Loc.T("Format z. B. Ctrl+Alt+G, Shift+F9 …"), Foreground = System.Windows.Media.Brushes.Gray },
                      Row(Loc.T("Umschalten Ruhe/Gespräch:"), _hotkeyToggle),
                      Row(Loc.T("In Ruhe-Modus:"), _hotkeyQuiet),
                      Row(Loc.T("In Gesprächs-Modus:"), _hotkeyConv),
                      Row(Loc.T("Erkennung an/aus:"), _hotkeyDetection),
                      Row(Loc.T("Verpasste Erwähnungen anzeigen:"), _hotkeyMissed))
            },
            new FrameworkElement[] { _hotkeyToggle, _hotkeyQuiet, _hotkeyConv, _hotkeyDetection, _hotkeyMissed }));

        tabControl.Items.Add(MakeTab(Loc.T("Sonstiges"), "",
            new UIElement[]
            {
                Group(Row(Loc.T("Sprache:"), _language),
                      Row(Loc.T("Farbmodus:"), _themeMode)),
                Group(_startInConversation, _detection, _autostart),
                Group(_autoReturn,
                      Row(Loc.T("Zeit ohne eigene Wortmeldung (Sekunden):"), _autoReturnSec),
                      _autoReturnManual),
                Group(_checkUpdates, _silentUpdate, _showNotes, _offerInstall),
                Group(_debug, Row(Loc.T("Poll-Intervall (ms):"), _pollInterval))
            },
            new FrameworkElement[] { _language, _themeMode, _startInConversation, _autoReturn, _autoReturnSec, _autoReturnManual, _detection, _autostart, _checkUpdates, _silentUpdate, _showNotes, _offerInstall, _debug, _pollInterval }));

        _notesTab = BuildReleaseNotesTab();
        tabControl.Items.Add(_notesTab);
        tabControl.Items.Add(BuildInfoTab());

        var close = new Button { Content = Loc.T("Schließen"), Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        close.Click += (_, _) => Close();
        _discardButton.Click += (_, _) => OnDiscard();
        _applyButton.Click += OnApplyClick;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        buttons.Children.Add(_statusText);
        buttons.Children.Add(_applyButton);
        buttons.Children.Add(_discardButton);
        buttons.Children.Add(close);

        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(tabControl);
        Content = root;

        LoadFrom(current);
        WireDirty();
    }

    private TabItem MakeTab(string title, string iconGlyph, UIElement[] rows, FrameworkElement[] inputs)
    {
        var panel = new StackPanel { Margin = new Thickness(2, 4, 10, 4) };
        foreach (var r in rows)
        {
            if (r is FrameworkElement fe) fe.Margin = new Thickness(0);
            panel.Children.Add(Core.Theme.Card(r)); // jede Zeile als Win11-Kachel
        }
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel };
        Core.Theme.ThinScroll(scroll);
        var tab = new TabItem
        {
            Tag = title, // Basis-Titel (bereits lokalisiert; "*" wird angehängt)
            Content = scroll
        };
        tab.Header = MakeTabHeader(tab, iconGlyph, title);
        _tabs.Add(tab);
        foreach (var input in inputs) _tabOf[input] = tab;
        return tab;
    }

    /// <summary>Header eines Seitenleisten-Eintrags: MDL2-Icon + Text (Text-Teil wird für "*" gemerkt).</summary>
    private StackPanel MakeTabHeader(TabItem tab, string iconGlyph, string title)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal };
        p.Children.Add(new TextBlock
        {
            Text = iconGlyph,
            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center
        });
        var text = new TextBlock { Text = title, Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        p.Children.Add(text);
        _tabHeaderText[tab] = text;
        return p;
    }

    /// <summary>Setzt/entfernt die „ungespeichert"-Markierung am Seitenleisten-Eintrag.</summary>
    private void SetTabStar(TabItem tab, bool dirty)
    {
        var title = (string)tab.Tag!;
        if (_tabHeaderText.TryGetValue(tab, out var text)) text.Text = dirty ? title + " *" : title;
        else tab.Header = dirty ? title + " *" : title;
    }

    /// <summary>Wechselt (z. B. vom Tray aus) direkt auf den Release-Notes-Tab.</summary>
    public void ShowReleaseNotesTab()
    {
        if (_notesTab != null) _tabControl.SelectedItem = _notesTab;
    }

    /// <summary>Erster Tab: verpasste Erwähnungen – Liste (nach Tagen gruppiert, heute offen) mit
    /// denselben Aktionen wie im Overlay plus Wieder-öffnen/Löschen, darunter die Einstellungen dazu.</summary>
    private TabItem BuildMissedTab()
    {
        var panel = new StackPanel { Margin = new Thickness(10) };

        var deleteAll = new Button { Content = Loc.T("Alle löschen"), Padding = new Thickness(10, 3, 10, 3), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 4) };
        deleteAll.Click += (_, _) => _mentionStore.DeleteAll();
        panel.Children.Add(deleteAll);

        panel.Children.Add(Core.Theme.Card(_missedListHost));

        panel.Children.Add(new TextBlock { Text = Loc.T("Einstellungen"), FontWeight = FontWeights.Bold, Margin = new Thickness(0, 14, 0, 4) });
        panel.Children.Add(Core.Theme.Card(Group(
            _missedEnabled,
            Row(Loc.T("Als unbeantwortet nach (Sekunden):"), _mentionTimeout),
            Row(Loc.T("Gleiche Person erneut frühestens nach (Minuten):"), _mentionRepeat),
            Row(Loc.T("Einträge automatisch löschen nach (Tagen):"), _mentionRetention),
            Row(Loc.T("Snooze-Auswahl (Minuten, kommagetrennt):"), _snoozePresets))));
        panel.Children.Add(Core.Theme.Card(Group(
            Row(Loc.T("Vertikale Position:"), _missedVert),
            Row(Loc.T("Horizontale Position:"), _missedHorz))));

        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel };
        Core.Theme.ThinScroll(sv);
        var tab = new TabItem { Tag = Loc.T("Verpasst"), Content = sv };
        _tabs.Add(tab); // am Stern-Reset von Übernehmen/Verwerfen teilnehmen
        tab.Header = MakeTabHeader(tab, "", Loc.T("Verpasst"));

        // Konfig-Felder in die Dirty-Markierung einhängen (Liste selbst wirkt direkt, ohne Übernehmen).
        foreach (var input in new FrameworkElement[]
                 { _missedEnabled, _mentionTimeout, _mentionRepeat, _mentionRetention, _snoozePresets, _missedVert, _missedHorz })
            _tabOf[input] = tab;

        _mentionStore.Changed += (_, _) => { if (IsVisible) RebuildMissedList(); };
        sv.IsVisibleChanged += (_, _) => { if (sv.IsVisible) RebuildMissedList(); };
        RebuildMissedList();
        return tab;
    }

    private static readonly System.Windows.Media.Brush MissedOpen = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD9, 0x77, 0x06));
    private static readonly System.Windows.Media.Brush MissedDone = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x8E, 0x4F));
    private static readonly System.Windows.Media.Brush MissedWait = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8A, 0x8A, 0x8A));

    private void RebuildMissedList()
    {
        _missedListHost.Children.Clear();
        var items = _mentionStore.Items;
        if (items.Count == 0)
        {
            _missedListHost.Children.Add(new TextBlock
            {
                Text = Loc.T("Keine Einträge vorhanden."),
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 8, 0, 4)
            });
            return;
        }

        foreach (var dayGroup in items.GroupBy(m => m.MentionedAt.Date).OrderByDescending(g => g.Key))
        {
            var day = dayGroup.Key;
            string label = day == DateTime.Today ? Loc.T("Heute")
                : day == DateTime.Today.AddDays(-1) ? Loc.T("Gestern")
                : day.ToString("d");

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var headerText = new TextBlock
            {
                Text = $"{label} ({dayGroup.Count()})",
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            var deleteDay = new Button
            {
                Content = Loc.T("Tag löschen"),
                Padding = new Thickness(8, 1, 8, 1),
                Margin = new Thickness(12, 0, 4, 0),
                FontSize = 11
            };
            var capturedDay = day;
            deleteDay.Click += (_, e) => { e.Handled = true; _mentionStore.DeleteDay(capturedDay); };
            Grid.SetColumn(headerText, 0);
            Grid.SetColumn(deleteDay, 1);
            headerGrid.Children.Add(headerText);
            headerGrid.Children.Add(deleteDay);

            var entriesPanel = new StackPanel { Margin = new Thickness(6, 2, 0, 2) };
            foreach (var m in dayGroup.OrderByDescending(x => x.MentionedAt))
                entriesPanel.Children.Add(BuildMissedRow(m));

            _missedListHost.Children.Add(new Expander
            {
                Header = headerGrid,
                Content = entriesPanel,
                IsExpanded = day == DateTime.Today,
                Margin = new Thickness(0, 6, 0, 0)
            });
        }
    }

    private UIElement BuildMissedRow(Core.MissedMention m)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var statusBrush = m.Status switch
        {
            Core.MentionStatus.Done => MissedDone,
            Core.MentionStatus.Open => MissedOpen,
            _ => MissedWait
        };
        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = statusBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        string suffix = m.Status switch
        {
            Core.MentionStatus.Done when m.DoneAt != null => " · " + Loc.Tf("erledigt {0}", m.DoneAt.Value.ToString("HH:mm")),
            Core.MentionStatus.Snoozed when m.SnoozeUntil != null => " · " + Loc.Tf("zurückgestellt bis {0}", m.SnoozeUntil.Value.ToString("HH:mm")),
            Core.MentionStatus.WaitingForPerson => " · " + Loc.T("wartet auf Rückkehr"),
            _ => ""
        };
        var text = new TextBlock
        {
            Text = $"{m.MentionedAt:HH:mm} – {m.Speaker}{suffix}",
            Foreground = statusBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (m.Status == Core.MentionStatus.Done)
        {
            buttons.Children.Add(SmallButton("↩", Loc.T("Wieder öffnen"), () => _mentionStore.Reopen(m.Id)));
        }
        else
        {
            buttons.Children.Add(SmallButton("✓", Loc.T("Erledigt"), () => _mentionStore.MarkDone(m.Id)));
            var snoozeBtn = SmallButton("⏱", Loc.T("Erinnere mich in …"), null);
            snoozeBtn.Click += (_, _) => Overlay.MentionOverlay.ShowSnoozeMenu(snoozeBtn, _current, _mentionStore, m.Id);
            buttons.Children.Add(snoozeBtn);
            buttons.Children.Add(SmallButton("👤", Loc.T("Erinnern, wenn die Person wieder im Call ist"), () => _mentionStore.WaitForPerson(m.Id)));
            if (m.Status is Core.MentionStatus.Snoozed or Core.MentionStatus.WaitingForPerson)
                buttons.Children.Add(SmallButton("↺", Loc.T("Erinnerung entfernen (wieder offen)"), () => _mentionStore.Reopen(m.Id)));
        }
        buttons.Children.Add(SmallButton("🗑", Loc.T("Löschen"), () => _mentionStore.Delete(m.Id)));

        Grid.SetColumn(dot, 0);
        Grid.SetColumn(text, 1);
        Grid.SetColumn(buttons, 2);
        grid.Children.Add(dot);
        grid.Children.Add(text);
        grid.Children.Add(buttons);
        if (m.Status is Core.MentionStatus.Snoozed or Core.MentionStatus.WaitingForPerson) grid.Opacity = 0.6;
        return grid;
    }

    private static Button SmallButton(string glyph, string toolTip, Action? onClick)
    {
        var b = new Button
        {
            Content = glyph,
            ToolTip = toolTip,
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(4, 0, 0, 0),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        if (onClick != null) b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary>Tab mit allen Release Notes (neueste zuerst, in der UI-Sprache). Die Daten werden
    /// erst beim ersten Öffnen des Tabs von GitHub geladen.</summary>
    private TabItem BuildReleaseNotesTab()
    {
        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock
        {
            Text = Loc.T("Release Notes werden geladen …"),
            Foreground = System.Windows.Media.Brushes.Gray
        });
        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel };
        Core.Theme.ThinScroll(sv);
        var tab = new TabItem { Tag = Loc.T("Release Notes"), Content = sv };
        tab.Header = MakeTabHeader(tab, "", Loc.T("Release Notes"));

        bool loaded = false;
        sv.IsVisibleChanged += async (_, _) =>
        {
            if (!sv.IsVisible || loaded) return;
            loaded = true;
            try
            {
                var releases = await UpdateManager.GetAllReleasesAsync();
                panel.Children.Clear();
                if (releases.Count == 0)
                {
                    panel.Children.Add(new TextBlock { Text = Loc.T("Keine Releases gefunden.") });
                    return;
                }
                foreach (var r in releases)
                {
                    string date = r.PublishedAt is { } dt ? " – " + dt.ToString("d") : "";
                    var block = new StackPanel();
                    block.Children.Add(new TextBlock
                    {
                        Text = Loc.Tf("Version {0}", r.Tag.TrimStart('v', 'V')) + date,
                        FontSize = 15,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 6)
                    });
                    block.Children.Add(new TextBlock
                    {
                        Text = UpdateManager.FormatNotesForDisplay(r.Body, Loc.Language),
                        TextWrapping = TextWrapping.Wrap
                    });
                    panel.Children.Add(Core.Theme.Card(block)); // eine Kachel pro Release
                }
            }
            catch
            {
                panel.Children.Clear();
                panel.Children.Add(new TextBlock
                {
                    Text = Loc.T("Release Notes konnten nicht geladen werden (offline?)."),
                    Foreground = System.Windows.Media.Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap
                });
                loaded = false; // beim nächsten Öffnen erneut versuchen
            }
        };
        return tab;
    }

    private TabItem BuildInfoTab()
    {
        var p = new StackPanel { Margin = new Thickness(14) };

        void Heading(string t) => p.Children.Add(new TextBlock { Text = t, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 12, 0, 4) });
        void Body(string t, System.Windows.Media.Brush? fg = null)
        {
            var tb = new TextBlock { Text = t, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 2) };
            if (fg != null) tb.Foreground = fg; else tb.SetResourceReference(TextBlock.ForegroundProperty, "ThText");
            p.Children.Add(tb);
        }

        p.Children.Add(new Image
        {
            Source = Branding.CreateImageSource(128, Branding.Accent),
            Width = 96,
            Height = 96,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 10)
        });
        p.Children.Add(new TextBlock { Text = AppInfo.DisplayName, FontSize = 18, FontWeight = FontWeights.Bold });
        p.Children.Add(new TextBlock
        {
            Text = Loc.Tf("Version {0}", AppInfo.Version),
            Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(0, 2, 0, 8)
        });
        Body(Loc.T(AppInfo.Description));

        Heading(Loc.T("Funktionen"));
        Body(Loc.T("• Erkennt deine Namen/Begriffe im Teams-Live-Transkript (per UI Automation) – auch über mehrere parallele Calls hinweg."));
        Body(Loc.T("• Roter, konfigurierbarer Bildschirm-Glow bei Nennung; optional zusätzlich ein Signalton (Lautstärke und Ausgabegerät wählbar)."));
        Body(Loc.T("• Einblendung, wer dich gerufen hat – Text, Position, Größe, Farbe, Dauer, Deckkraft und Monitore frei einstellbar."));
        Body(Loc.T("• Auf Wunsch automatisch Teams lauter stellen und Musik (z. B. Spotify) pausieren; per Shortcut oder Tray zurück in den Ruhe-Modus."));
        Body(Loc.T("• Ruhe-/Gesprächs-Modus mit globalen Tastenkürzeln, wählbarem Start-Modus und automatischer Rückkehr nach eigener Redepause."));
        Body(Loc.T("• Glow je Monitor wählbar; Erkennung sprachunabhängig; alles über diese Oberfläche einstellbar (kein fest hinterlegter Name)."));

        Heading(Loc.T("Voraussetzungen"));
        Body(Loc.T("Windows 10/11, neues Microsoft Teams, im Call aktivierte Live-Untertitel (idealerweise als eigenes Fenster ausgekoppelt)."));

        Heading(Loc.T("Datenschutz"));
        Body(Loc.T("Das Transkript wird ausschließlich lokal und nur im Arbeitsspeicher verarbeitet – es wird nichts gespeichert, geloggt (außer optional das Debug-Log) oder übertragen. Es wird ausschließlich nach den konfigurierten Begriffen gesucht."),
             System.Windows.Media.Brushes.Gray);

        Heading(Loc.T("Entwickler"));
        Body($"{AppInfo.Developer} · {AppInfo.Company} · © {AppInfo.Year}");
        Body(Loc.T("Technik: ") + AppInfo.TechStack, System.Windows.Media.Brushes.Gray);

        var reset = new Button
        {
            Content = Loc.T("Alle Einstellungen auf Standard zurücksetzen"),
            Padding = new Thickness(12, 5, 12, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 16, 0, 0)
        };
        reset.Click += (_, _) => OnResetDefaults();
        p.Children.Add(reset);

        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = p };
        Core.Theme.ThinScroll(sv);
        // Beim Wechsel auf den Info-Tab immer oben starten (sonst springt der Fokus ans Ende).
        sv.IsVisibleChanged += (_, _) =>
        {
            if (sv.IsVisible)
                sv.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(sv.ScrollToTop));
        };
        var tab = new TabItem { Tag = Loc.T("Info"), Content = sv };
        tab.Header = MakeTabHeader(tab, "", Loc.T("Info"));
        return tab;
    }

    private void LoadFrom(AppSettings s)
    {
        _triggerWords.Text = string.Join(Environment.NewLine, s.TriggerWords);
        _ownSpeaker.Text = s.OwnSpeakerName;
        _ignoreOwn.IsChecked = s.IgnoreOwnSpeaker;
        _fuzzy.IsChecked = s.FuzzyEnabled;
        _fuzzyDist.Text = s.FuzzyMaxDistance.ToString();
        _cooldown.Text = s.TriggerCooldownMs.ToString();

        _quietBehavior.SelectedIndex = s.QuietBehavior == QuietBehavior.Mute ? 1 : 0;
        _quietLevel.Text = s.QuietLevelPercent.ToString();
        _convLevel.Text = s.ConversationLevelPercent.ToString();
        _musicHint.Text = s.MusicAppHint;
        _manageTeams.IsChecked = s.RaiseTeamsOnTrigger;
        _manageMusic.IsChecked = s.PauseMusicOnTrigger;
        _autoConv.IsChecked = s.AutoEnterConversationOnTrigger;
        _convOnCall.IsChecked = s.EnterConversationOnIncomingCall;
        for (int i = 0; i < _audioExcludeChecks.Count; i++)
            _audioExcludeChecks[i].IsChecked = s.AudioExcludedDeviceIds.Contains(_audioExcludeIds[i], StringComparer.OrdinalIgnoreCase);

        _glowColor.Text = s.GlowColorHex;
        _glowThickness.Text = s.GlowThickness.ToString(CultureInfo.InvariantCulture);
        _glowDuration.Text = s.GlowDurationMs.ToString();
        _persistentBorderMode.SelectedIndex = s.PersistentBorder switch
        {
            PersistentBorderMode.Never => 0,
            PersistentBorderMode.TriggerOnly => 1,
            _ => 2
        };
        for (int i = 0; i < _monitorChecks.Count; i++)
            _monitorChecks[i].IsChecked = s.GlowMonitors.Count == 0 || s.GlowMonitors.Contains(i);

        _bannerEnabled.IsChecked = s.BannerEnabled;
        _bannerText.Text = s.BannerText;
        _bannerVert.SelectedIndex = s.BannerVertical switch { BannerVertical.Center => 1, BannerVertical.Bottom => 2, _ => 0 };
        _bannerHorz.SelectedIndex = s.BannerHorizontal switch { BannerHorizontal.Left => 0, BannerHorizontal.Right => 2, _ => 1 };
        _bannerSize.Text = s.BannerFontSize.ToString(CultureInfo.InvariantCulture);
        _bannerColor.Text = s.BannerColorHex;
        _bannerDuration.Text = s.BannerDurationMs.ToString();
        _bannerOpacity.Text = s.BannerOpacityPercent.ToString();
        for (int i = 0; i < _bannerMonitorChecks.Count; i++)
            _bannerMonitorChecks[i].IsChecked = s.BannerMonitors.Count == 0 || s.BannerMonitors.Contains(i);

        _missedEnabled.IsChecked = s.MissedMentionsEnabled;
        _mentionTimeout.Text = s.MentionAnswerTimeoutSeconds.ToString();
        _mentionRepeat.Text = s.MentionRepeatMinutes.ToString();
        _mentionRetention.Text = s.MentionRetentionDays.ToString();
        _snoozePresets.Text = string.Join(", ", s.SnoozePresetsMinutes);
        _missedVert.SelectedIndex = s.MentionOverlayVertical switch { BannerVertical.Top => 0, BannerVertical.Center => 1, _ => 2 };
        _missedHorz.SelectedIndex = s.MentionOverlayHorizontal switch { BannerHorizontal.Left => 0, BannerHorizontal.Center => 1, _ => 2 };

        _soundEnabled.IsChecked = s.TriggerSoundEnabled;
        int soundIdx = _soundPaths.FindIndex(p => string.Equals(p, s.TriggerSoundFile, StringComparison.OrdinalIgnoreCase));
        if (soundIdx < 0) soundIdx = _soundPaths.FindIndex(p => p.EndsWith("Windows Notify.wav", StringComparison.OrdinalIgnoreCase));
        if (soundIdx < 0 && _soundFile.Items.Count > 0) soundIdx = 0;
        _soundFile.SelectedIndex = soundIdx;
        _soundVolume.Text = s.TriggerSoundVolume.ToString();
        int devIdx = _soundDeviceIds.FindIndex(d => string.Equals(d, s.TriggerSoundDeviceId, StringComparison.OrdinalIgnoreCase));
        _soundDevice.SelectedIndex = devIdx < 0 ? 0 : devIdx;

        _hotkeyToggle.Text = s.HotkeyToggle;
        _hotkeyQuiet.Text = s.HotkeyQuiet;
        _hotkeyConv.Text = s.HotkeyConversation;
        _hotkeyDetection.Text = s.HotkeyToggleDetection;
        _hotkeyMissed.Text = s.HotkeyShowMissed;

        _language.SelectedIndex = s.Language switch { AppLanguage.En => 1, AppLanguage.It => 2, _ => 0 };
        _themeMode.SelectedIndex = s.Theme switch { AppThemeMode.Light => 1, AppThemeMode.Dark => 2, _ => 0 };
        _startInConversation.IsChecked = s.StartInConversationMode;
        _autoReturn.IsChecked = s.AutoReturnToQuietEnabled;
        _autoReturnSec.Text = s.AutoReturnAfterSeconds.ToString();
        _autoReturnManual.IsChecked = s.AutoReturnAlsoWhenManual;
        _detection.IsChecked = s.DetectionEnabled;
        _autostart.IsChecked = s.StartWithWindows;
        _checkUpdates.IsChecked = s.CheckUpdatesOnStartup;
        _silentUpdate.IsChecked = s.SilentAutoUpdate;
        _showNotes.IsChecked = s.ShowNotesAfterUpdate;
        _offerInstall.IsChecked = s.OfferInstallOnStartup;
        _debug.IsChecked = s.DebugLogging;
        _pollInterval.Text = s.PollIntervalMs.ToString();
    }

    private AppSettings BuildSettings()
    {
        var s = new AppSettings();
        s.CopyFrom(_current); // Felder ohne UI-Steuerung erhalten

        s.TriggerWords = _triggerWords.Text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();
        s.OwnSpeakerName = _ownSpeaker.Text.Trim();
        s.IgnoreOwnSpeaker = _ignoreOwn.IsChecked == true;
        s.FuzzyEnabled = _fuzzy.IsChecked == true;
        s.FuzzyMaxDistance = ParseInt(_fuzzyDist.Text, s.FuzzyMaxDistance, 0, 5);
        s.TriggerCooldownMs = ParseInt(_cooldown.Text, s.TriggerCooldownMs, 0, 600000);

        s.QuietBehavior = _quietBehavior.SelectedIndex == 1 ? QuietBehavior.Mute : QuietBehavior.Lower;
        s.QuietLevelPercent = ParseInt(_quietLevel.Text, s.QuietLevelPercent, 0, 100);
        s.ConversationLevelPercent = ParseInt(_convLevel.Text, s.ConversationLevelPercent, 0, 100);
        s.MusicAppHint = _musicHint.Text.Trim();
        s.RaiseTeamsOnTrigger = _manageTeams.IsChecked == true;
        s.PauseMusicOnTrigger = _manageMusic.IsChecked == true;
        s.AutoEnterConversationOnTrigger = _autoConv.IsChecked == true;
        s.EnterConversationOnIncomingCall = _convOnCall.IsChecked == true;
        var excludedDevices = new List<string>();
        for (int i = 0; i < _audioExcludeChecks.Count; i++)
            if (_audioExcludeChecks[i].IsChecked == true) excludedDevices.Add(_audioExcludeIds[i]);
        s.AudioExcludedDeviceIds = excludedDevices;

        s.GlowColorHex = string.IsNullOrWhiteSpace(_glowColor.Text) ? s.GlowColorHex : _glowColor.Text.Trim();
        s.GlowThickness = ParseDouble(_glowThickness.Text, s.GlowThickness, 1, 200);
        s.GlowDurationMs = ParseInt(_glowDuration.Text, s.GlowDurationMs, 200, 10000);
        s.PersistentBorder = _persistentBorderMode.SelectedIndex switch
        {
            0 => PersistentBorderMode.Never,
            1 => PersistentBorderMode.TriggerOnly,
            _ => PersistentBorderMode.Always
        };
        var chosen = new List<int>();
        for (int i = 0; i < _monitorChecks.Count; i++)
            if (_monitorChecks[i].IsChecked == true) chosen.Add(i);
        s.GlowMonitors = chosen.Count == _monitorChecks.Count ? new List<int>() : chosen;

        s.BannerEnabled = _bannerEnabled.IsChecked == true;
        s.BannerText = _bannerText.Text.Trim();
        s.BannerVertical = _bannerVert.SelectedIndex switch { 1 => BannerVertical.Center, 2 => BannerVertical.Bottom, _ => BannerVertical.Top };
        s.BannerHorizontal = _bannerHorz.SelectedIndex switch { 0 => BannerHorizontal.Left, 2 => BannerHorizontal.Right, _ => BannerHorizontal.Center };
        s.BannerFontSize = ParseDouble(_bannerSize.Text, s.BannerFontSize, 8, 200);
        s.BannerColorHex = string.IsNullOrWhiteSpace(_bannerColor.Text) ? s.BannerColorHex : _bannerColor.Text.Trim();
        s.BannerDurationMs = ParseInt(_bannerDuration.Text, s.BannerDurationMs, 500, 60000);
        s.BannerOpacityPercent = ParseInt(_bannerOpacity.Text, s.BannerOpacityPercent, 5, 100);
        var bannerChosen = new List<int>();
        for (int i = 0; i < _bannerMonitorChecks.Count; i++)
            if (_bannerMonitorChecks[i].IsChecked == true) bannerChosen.Add(i);
        s.BannerMonitors = bannerChosen.Count == _bannerMonitorChecks.Count ? new List<int>() : bannerChosen;

        s.MissedMentionsEnabled = _missedEnabled.IsChecked == true;
        s.MentionAnswerTimeoutSeconds = ParseInt(_mentionTimeout.Text, s.MentionAnswerTimeoutSeconds, 5, 3600);
        s.MentionRepeatMinutes = ParseInt(_mentionRepeat.Text, s.MentionRepeatMinutes, 0, 720);
        s.MentionRetentionDays = ParseInt(_mentionRetention.Text, s.MentionRetentionDays, 1, 365);
        var presets = _snoozePresets.Text
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => int.TryParse(t, out var v) ? v : 0)
            .Where(v => v > 0).Distinct().OrderBy(v => v).ToList();
        if (presets.Count > 0) s.SnoozePresetsMinutes = presets;
        s.MentionOverlayVertical = _missedVert.SelectedIndex switch { 0 => BannerVertical.Top, 1 => BannerVertical.Center, _ => BannerVertical.Bottom };
        s.MentionOverlayHorizontal = _missedHorz.SelectedIndex switch { 0 => BannerHorizontal.Left, 1 => BannerHorizontal.Center, _ => BannerHorizontal.Right };

        s.TriggerSoundEnabled = _soundEnabled.IsChecked == true;
        s.TriggerSoundFile = SelectedSoundPath();
        s.TriggerSoundVolume = ParseInt(_soundVolume.Text, s.TriggerSoundVolume, 0, 100);
        s.TriggerSoundDeviceId = SelectedDeviceId();

        s.HotkeyToggle = _hotkeyToggle.Text.Trim();
        s.HotkeyQuiet = _hotkeyQuiet.Text.Trim();
        s.HotkeyConversation = _hotkeyConv.Text.Trim();
        s.HotkeyToggleDetection = _hotkeyDetection.Text.Trim();
        s.HotkeyShowMissed = _hotkeyMissed.Text.Trim();

        s.Language = _language.SelectedIndex switch { 1 => AppLanguage.En, 2 => AppLanguage.It, _ => AppLanguage.De };
        s.Theme = _themeMode.SelectedIndex switch { 1 => AppThemeMode.Light, 2 => AppThemeMode.Dark, _ => AppThemeMode.System };
        s.StartInConversationMode = _startInConversation.IsChecked == true;
        s.AutoReturnToQuietEnabled = _autoReturn.IsChecked == true;
        s.AutoReturnAfterSeconds = ParseInt(_autoReturnSec.Text, s.AutoReturnAfterSeconds, 1, 3600);
        s.AutoReturnAlsoWhenManual = _autoReturnManual.IsChecked == true;
        s.DetectionEnabled = _detection.IsChecked == true;
        s.StartWithWindows = _autostart.IsChecked == true;
        s.CheckUpdatesOnStartup = _checkUpdates.IsChecked == true;
        s.SilentAutoUpdate = _silentUpdate.IsChecked == true;
        s.ShowNotesAfterUpdate = _showNotes.IsChecked == true;
        s.OfferInstallOnStartup = _offerInstall.IsChecked == true;
        s.DebugLogging = _debug.IsChecked == true;
        s.PollIntervalMs = ParseInt(_pollInterval.Text, s.PollIntervalMs, 150, 5000);

        return s;
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _onApply(BuildSettings());
            foreach (var tab in _tabs) SetTabStar(tab, false);
            SetDirtyButtons(false);
            ResetFieldHighlights();
            _statusText.Text = Loc.T("Übernommen ✓");
        }
        catch
        {
            _statusText.Text = Loc.T("Fehler beim Übernehmen");
        }
    }

    private void OnDiscard()
    {
        ReloadForm(_current, markDirty: false);
        _statusText.Text = Loc.T("Verworfen");
    }

    private void OnResetDefaults()
    {
        ReloadForm(new AppSettings(), markDirty: true);
        _statusText.Text = Loc.T("Standardwerte geladen – bitte übernehmen");
    }

    private void ReloadForm(AppSettings s, bool markDirty)
    {
        _suppressDirty = true;
        LoadFrom(s);
        _suppressDirty = false;
        ResetFieldHighlights(); // blaue Markierungen zurücksetzen

        if (markDirty)
        {
            foreach (var tab in _tabs) SetTabStar(tab, true);
            SetDirtyButtons(true);
        }
        else
        {
            foreach (var tab in _tabs) SetTabStar(tab, false);
            SetDirtyButtons(false);
        }
    }

    private void WireDirty()
    {
        foreach (var kv in _tabOf)
        {
            var tab = kv.Value;
            var ctrl = kv.Key as Control;
            void Dirty()
            {
                if (_suppressDirty) return;
                if (ctrl != null) ctrl.Foreground = DirtyBrush; // geändertes Feld blau markieren
                MarkDirty(tab);
            }
            switch (kv.Key)
            {
                case TextBox tb:
                    tb.TextChanged += (_, _) => Dirty();
                    break;
                case CheckBox cb:
                    cb.Checked += (_, _) => Dirty();
                    cb.Unchecked += (_, _) => Dirty();
                    break;
                case ComboBox combo:
                    combo.SelectionChanged += (_, _) => Dirty();
                    break;
            }
        }
    }

    /// <summary>Setzt alle blauen „geändert"-Markierungen zurück.</summary>
    private void ResetFieldHighlights()
    {
        foreach (var kv in _tabOf)
            if (kv.Key is Control c) c.ClearValue(Control.ForegroundProperty);
    }

    private void MarkDirty(TabItem tab)
    {
        if (_suppressDirty) return;
        SetTabStar(tab, true);
        SetDirtyButtons(true);
        _statusText.Text = "";
    }

    private void SetDirtyButtons(bool enabled)
    {
        _applyButton.IsEnabled = enabled;
        _discardButton.IsEnabled = enabled;
    }

    private string SelectedSoundPath()
    {
        int i = _soundFile.SelectedIndex;
        return i >= 0 && i < _soundPaths.Count ? _soundPaths[i] : "";
    }

    private string SelectedDeviceId()
    {
        int i = _soundDevice.SelectedIndex;
        return i >= 0 && i < _soundDeviceIds.Count ? _soundDeviceIds[i] : "";
    }

    // --- UI-Helfer -------------------------------------------------------
    private static Button TestButton(string text) => new()
    {
        Content = text,
        Padding = new Thickness(12, 3, 12, 3),
        HorizontalAlignment = HorizontalAlignment.Left,
        Margin = new Thickness(0, 8, 0, 0)
    };

    private static TextBox Num() => new() { Width = 90, HorizontalAlignment = HorizontalAlignment.Left };
    private static ComboBox Combo() => new() { Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
    private static TextBox Multiline(double height) => new()
    {
        AcceptsReturn = true,
        Height = height,
        TextWrapping = TextWrapping.Wrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    /// <summary>Fasst zusammengehörige Einstellungszeilen (z. B. Überschrift + Auswahlfelder)
    /// zu EINER Kachel zusammen.</summary>
    private static StackPanel Group(params UIElement[] children)
    {
        var p = new StackPanel();
        foreach (var c in children)
        {
            if (c is FrameworkElement fe) fe.Margin = new Thickness(0, 4, 0, 4);
            p.Children.Add(c);
        }
        return p;
    }

    private static Grid Row(string label, UIElement control)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(control, 1);
        grid.Children.Add(lbl);
        grid.Children.Add(control);
        return grid;
    }

    private static StackPanel Labeled(string label, UIElement control)
    {
        var p = new StackPanel();
        p.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 3) });
        p.Children.Add(control);
        return p;
    }

    private static int ParseInt(string text, int fallback, int min, int max) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? Math.Clamp(v, min, max) : fallback;

    private static double ParseDouble(string text, double fallback, double min, double max) =>
        double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? Math.Clamp(v, min, max) : fallback;
}
