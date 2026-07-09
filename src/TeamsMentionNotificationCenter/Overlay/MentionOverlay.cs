using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using TeamsMentionNotificationCenter.Core;
using TeamsMentionNotificationCenter.Localization;
using TeamsMentionNotificationCenter.Settings;

namespace TeamsMentionNotificationCenter.Overlay;

/// <summary>
/// Interaktives Overlay im Stil der Windows-11-Benachrichtigungen: listet verpasste Erwähnungen
/// (wer hat wann gerufen) mit Aktionen pro Eintrag (Erledigt, Snooze, Bei-Rückkehr-erinnern) und
/// global (Alle erledigen, Schließen). Schließen versteckt nur das Fenster – die Daten bleiben im
/// <see cref="MentionStore"/>. Position, Farbe und Deckkraft kommen aus den Einstellungen.
/// Muss vom WPF-UI-Thread aus angesprochen werden.
/// </summary>
public sealed class MentionOverlay : IDisposable
{
    private readonly AppSettings _settings;
    private readonly MentionStore _store;
    private Window? _win;
    private StackPanel? _list;
    private Border? _card;
    private bool _reallyClose;

    private static readonly Brush OpenBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0x8A, 0x1A));   // orange
    private static readonly Brush WaitBrush = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));   // grau

    public MentionOverlay(AppSettings settings, MentionStore store)
    {
        _settings = settings;
        _store = store;
        _store.Changed += (_, _) =>
        {
            if (_win is { IsVisible: true })
            {
                if (_store.UnfinishedCount == 0) HideOverlay();
                else Rebuild();
            }
        };
    }

    /// <summary>True, wenn das Overlay gerade sichtbar ist.</summary>
    public bool IsOverlayVisible => _win is { IsVisible: true };

    /// <summary>Overlay anzeigen und Inhalt aktualisieren. Das Fenster wird dabei bewusst FRISCH
    /// erzeugt: Nach Standby/Monitorwechseln verschiebt Windows bestehende (auch versteckte)
    /// Fenster und ändert ihre DPI-Zuordnung – ein neues Fenster landet garantiert korrekt.</summary>
    public void ShowOverlay()
    {
        if (_store.UnfinishedCount == 0) return;
        DestroyWindow();
        EnsureWindow();
        Rebuild();
        _win!.Show();
        Reposition();
    }

    public void HideOverlay() => _win?.Hide();

    private void DestroyWindow()
    {
        if (_win == null) return;
        _reallyClose = true;
        try { _win.Close(); } catch { /* ignore */ }
        _reallyClose = false;
        _win = null;
        _card = null;
        _list = null;
    }

    private void EnsureWindow()
    {
        if (_win != null) return;

        _list = new StackPanel();
        _card = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 4, Opacity = 0.45 }
        };
        _card.SetResourceReference(Border.BackgroundProperty, "ThCard");     // Farben kommen aus dem Theme
        _card.SetResourceReference(Border.BorderBrushProperty, "ThCardBorder");

        _win = new Window
        {
            Title = Loc.T("Verpasste Erwähnungen"),
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.Height,
            Width = 400,
            Content = _card
        };
        _win.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "ThText");
        _win.SizeChanged += (_, _) => Reposition();
        var closingWin = _win;
        _win.Closing += (_, e) =>
        {
            if (_reallyClose) return;      // programmatischer Neuaufbau
            e.Cancel = true;               // Alt+F4 etc.: nur verstecken, Daten bleiben
            closingWin.Hide();
        };
    }

    /// <summary>Baut den kompletten Karteninhalt aus dem Store neu auf.</summary>
    private void Rebuild()
    {
        if (_win == null || _card == null || _list == null) return;

        var root = new StackPanel();

        // Kopfzeile: Titel + „Alle erledigen" + Schließen.
        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock
        {
            Text = Loc.T("Verpasste Erwähnungen"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        var allDone = FlatButton(Loc.T("Alle erledigen"), bold: false);
        allDone.Click += (_, _) => _store.MarkAllDone();
        var close = FlatButton("✕", bold: true);
        close.ToolTip = Loc.T("Overlay schließen (Einträge bleiben erhalten)");
        close.Click += (_, _) => HideOverlay();
        Grid.SetColumn(title, 0);
        Grid.SetColumn(allDone, 1);
        Grid.SetColumn(close, 2);
        header.Children.Add(title);
        header.Children.Add(allDone);
        header.Children.Add(close);
        root.Children.Add(header);

        // Einträge (nicht erledigte), neueste zuerst.
        _list = new StackPanel();
        foreach (var m in _store.Items
                     .Where(m => m.Status != MentionStatus.Done)
                     .OrderByDescending(m => m.MentionedAt))
            _list.Children.Add(BuildEntryRow(m));
        var listScroll = new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 480
        };
        Core.Theme.ThinScroll(listScroll);
        root.Children.Add(listScroll);

        _card.Child = root;
    }

    private UIElement BuildEntryRow(MissedMention m)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = m.Status == MentionStatus.Open ? OpenBrush : WaitBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };

        string hint = m.Status switch
        {
            MentionStatus.Snoozed when m.SnoozeUntil != null => " · " + Loc.Tf("zurückgestellt bis {0}", m.SnoozeUntil.Value.ToString("HH:mm")),
            MentionStatus.WaitingForPerson => " · " + Loc.T("wartet auf Rückkehr"),
            _ => ""
        };
        var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textPanel.Children.Add(new TextBlock
        {
            Text = m.Speaker,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        var sub = new TextBlock
        {
            Text = Loc.Tf("hat dich um {0} gerufen", m.MentionedAt.ToString("HH:mm")) + hint,
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        sub.SetResourceReference(TextBlock.ForegroundProperty, "ThTextDim");
        textPanel.Children.Add(sub);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var done = FlatButton("✓", bold: true);
        done.ToolTip = Loc.T("Erledigt");
        done.Click += (_, _) => _store.MarkDone(m.Id);
        var snooze = FlatButton("⏱", bold: false);
        snooze.ToolTip = Loc.T("Erinnere mich in …");
        snooze.Click += (_, _) => OpenSnoozeMenu(snooze, m.Id);
        var wait = FlatButton("👤", bold: false);
        wait.ToolTip = Loc.T("Erinnern, wenn die Person wieder im Call ist");
        wait.Click += (_, _) => _store.WaitForPerson(m.Id);
        buttons.Children.Add(done);
        buttons.Children.Add(snooze);
        buttons.Children.Add(wait);
        if (m.Status is MentionStatus.Snoozed or MentionStatus.WaitingForPerson)
        {
            var clear = FlatButton("↺", bold: false);
            clear.ToolTip = Loc.T("Erinnerung entfernen (wieder offen)");
            clear.Click += (_, _) => _store.Reopen(m.Id);
            buttons.Children.Add(clear);
        }

        Grid.SetColumn(dot, 0);
        Grid.SetColumn(textPanel, 1);
        Grid.SetColumn(buttons, 2);
        grid.Children.Add(dot);
        grid.Children.Add(textPanel);
        grid.Children.Add(buttons);

        if (m.Status != MentionStatus.Open) grid.Opacity = 0.55; // zurückgestellt/wartend: ausgegraut
        return grid;
    }

    private void OpenSnoozeMenu(FrameworkElement anchor, Guid id) => ShowSnoozeMenu(anchor, _settings, _store, id);

    /// <summary>Snooze-Auswahl als Button-Popup (konfigurierbare Presets + „Eigener Wert …") –
    /// auch vom Verpasst-Tab im Einstellungsfenster genutzt.</summary>
    public static void ShowSnoozeMenu(FrameworkElement anchor, AppSettings settings, MentionStore store, Guid id)
    {
        var stack = new StackPanel();
        var popup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = anchor,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade
        };

        void AddButton(string text, Action action)
        {
            var b = new Button
            {
                Content = text,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(12, 6, 12, 6),
                MinWidth = 200
            };
            b.Click += (_, _) => { popup.IsOpen = false; action(); };
            stack.Children.Add(b);
        }

        var presets = (settings.SnoozePresetsMinutes is { Count: > 0 } p ? p : new List<int> { 5, 15, 30, 60 })
            .Where(x => x > 0).Distinct().OrderBy(x => x);
        foreach (var minutes in presets)
        {
            int captured = minutes;
            AddButton(Loc.Tf("Erinnere mich in {0} min", captured), () => store.Snooze(id, captured));
        }
        AddButton(Loc.T("Eigener Wert …"), () =>
        {
            var val = PromptMinutes();
            if (val is > 0) store.Snooze(id, val.Value);
        });

        var card = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 2, 12, 12),
            Child = stack,
            Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.35 }
        };
        card.SetResourceReference(Border.BackgroundProperty, "ThCard");
        card.SetResourceReference(Border.BorderBrushProperty, "ThCardBorder");
        card.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "ThText");

        popup.Child = card;
        popup.IsOpen = true;
    }

    /// <summary>Kleiner modaler Dialog für den eigenen Minutenwert.</summary>
    internal static int? PromptMinutes()
    {
        int? result = null;
        var box = new TextBox { Width = 90, HorizontalAlignment = HorizontalAlignment.Left };
        var ok = new Button { Content = "OK", Padding = new Thickness(16, 4, 16, 4), IsDefault = true };
        var cancel = new Button { Content = Loc.T("Abbrechen"), Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = Loc.T("Minuten:"), Margin = new Thickness(0, 0, 0, 4) });
        panel.Children.Add(box);
        panel.Children.Add(buttons);
        var win = new Window
        {
            Title = AppInfo.DisplayName,
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            Icon = Branding.CreateImageSource(64, Branding.Accent)
        };
        Core.Theme.Prepare(win);
        ok.Click += (_, _) =>
        {
            if (int.TryParse(box.Text.Trim(), out var v) && v > 0) result = v;
            win.Close();
        };
        cancel.Click += (_, _) => win.Close();
        win.Loaded += (_, _) => box.Focus();
        win.ShowDialog();
        return result;
    }

    /// <summary>Positioniert das Overlay gemäß Einstellungen im Arbeitsbereich des Primärmonitors
    /// (16 px Rand). Der Arbeitsbereich kommt frisch vom System (physische Pixel -> DIP), nicht aus
    /// dem WPF-Cache – der kann nach Standby/Monitorwechseln veraltet sein.</summary>
    private void Reposition()
    {
        if (_win == null || !_win.IsVisible) return;
        var work = Interop.NativeMethods.GetPrimaryWorkArea();
        double scale = Interop.NativeMethods.GetSystemScale();
        double left = work.Left / scale, top = work.Top / scale;
        double right = work.Right / scale, bottom = work.Bottom / scale;
        const double margin = 16;
        _win.Left = _settings.MentionOverlayHorizontal switch
        {
            BannerHorizontal.Left => left + margin,
            BannerHorizontal.Center => left + ((right - left) - _win.ActualWidth) / 2,
            _ => right - _win.ActualWidth - margin
        };
        _win.Top = _settings.MentionOverlayVertical switch
        {
            BannerVertical.Top => top + margin,
            BannerVertical.Center => top + ((bottom - top) - _win.ActualHeight) / 2,
            _ => bottom - _win.ActualHeight - margin
        };
    }

    // Standard-Button – Optik (inkl. Hover) kommt aus dem zentralen Theme.
    private static Button FlatButton(string text, bool bold) => new()
    {
        Content = text,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        Padding = new Thickness(9, 3, 9, 3),
        Margin = new Thickness(4, 0, 0, 0)
    };

    public void Dispose()
    {
        try { DestroyWindow(); } catch { /* App fährt herunter */ }
    }
}
