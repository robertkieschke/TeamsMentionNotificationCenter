using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using TeamsMentionNotificationCenter.Interop;
using TeamsMentionNotificationCenter.Localization;
using TeamsMentionNotificationCenter.Settings;

namespace TeamsMentionNotificationCenter.Overlay;

/// <summary>
/// Text-Einblendung bei Erkennung: zeigt an, WER den Namen gesagt hat (z. B. „Anna hat dich gerufen").
/// Position (3×3), Schriftgröße, Farbe, Anzeigedauer, Deckkraft, Monitore und der Text selbst
/// ({Name}-Platzhalter) kommen aus den Einstellungen. Pro ausgewähltem Monitor ein transparentes,
/// immer-oben-liegendes, klick-durchlässiges Fenster, das nach der konfigurierten Zeit ausblendet.
/// Muss vom WPF-UI-Thread aus angesprochen werden.
/// </summary>
public sealed class CallerBanner : IDisposable
{
    private readonly AppSettings _settings;
    private readonly List<Window> _windows = new();

    public CallerBanner(AppSettings settings) => _settings = settings;

    /// <summary>Zeigt die Einblendung mit den gespeicherten Einstellungen.</summary>
    public void Show(string speaker) => ShowCore(_settings, speaker);

    /// <summary>Vorschau mit (noch nicht gespeicherten) Werten aus dem Einstellungsfenster.</summary>
    public void PreviewWith(AppSettings s) => ShowCore(s, "Max Mustermann");

    private void ShowCore(AppSettings s, string speaker)
    {
        Teardown(); // eine ggf. noch sichtbare Einblendung ersetzen

        if (string.IsNullOrWhiteSpace(speaker)) speaker = Loc.T("Jemand");
        string text = (string.IsNullOrWhiteSpace(s.BannerText) ? "{Name}" : s.BannerText)
            .Replace("{Name}", speaker)
            .Replace("{name}", speaker);

        double fontSize = Math.Clamp(s.BannerFontSize <= 0 ? 32 : s.BannerFontSize, 8, 200);
        double targetOpacity = Math.Clamp(s.BannerOpacityPercent, 5, 100) / 100.0;
        int holdMs = Math.Max(500, s.BannerDurationMs);
        var color = ParseColor(s.BannerColorHex);
        double scale = NativeMethods.GetSystemScale();

        var monitors = NativeMethods.GetMonitorRects();
        if (monitors.Count == 0)
            monitors.Add(new NativeMethods.RECT
            {
                Left = 0,
                Top = 0,
                Right = (int)(SystemParameters.PrimaryScreenWidth * scale),
                Bottom = (int)(SystemParameters.PrimaryScreenHeight * scale)
            });

        if (s.BannerMonitors.Count > 0)
        {
            var selected = monitors.Where((m, i) => s.BannerMonitors.Contains(i)).ToList();
            if (selected.Count > 0) monitors = selected; // sonst Fallback: alle Monitore
        }

        foreach (var rect in monitors)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                MaxWidth = Math.Max(200, rect.Width / scale - 140)
            };

            // Dunkle, abgerundete „Kapsel" hinter dem Text, damit er auf jedem Hintergrund lesbar ist.
            var pill = new Border
            {
                Child = textBlock,
                Background = new SolidColorBrush(Color.FromArgb(0xD9, 0x1E, 0x1E, 0x1E)),
                CornerRadius = new CornerRadius(fontSize * 0.55),
                Padding = new Thickness(fontSize * 0.9, fontSize * 0.45, fontSize * 0.9, fontSize * 0.45),
                Margin = new Thickness(48),
                IsHitTestVisible = false,
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Opacity = 0.55 },
                HorizontalAlignment = s.BannerHorizontal switch
                {
                    BannerHorizontal.Left => HorizontalAlignment.Left,
                    BannerHorizontal.Right => HorizontalAlignment.Right,
                    _ => HorizontalAlignment.Center
                },
                VerticalAlignment = s.BannerVertical switch
                {
                    BannerVertical.Center => VerticalAlignment.Center,
                    BannerVertical.Bottom => VerticalAlignment.Bottom,
                    _ => VerticalAlignment.Top
                }
            };

            var win = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize,
                IsHitTestVisible = false,
                Focusable = false,
                Opacity = 0,
                Title = "TeamsMentionNotificationCenter Banner",
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = rect.Left / scale,
                Top = rect.Top / scale,
                Width = Math.Max(1, rect.Width / scale),
                Height = Math.Max(1, rect.Height / scale),
                Content = pill
            };

            win.SourceInitialized += (_, _) =>
                NativeMethods.MakeClickThrough(new WindowInteropHelper(win).Handle);

            win.Show();
            win.Topmost = true;

            // Einblenden -> halten -> ausblenden -> Fenster schließen.
            var anim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(targetOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(targetOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(holdMs))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(holdMs + 450))));
            var target = win;
            anim.Completed += (_, _) =>
            {
                try { target.Close(); } catch { /* ignore */ }
                _windows.Remove(target);
            };
            win.BeginAnimation(Window.OpacityProperty, anim);
            _windows.Add(win);
        }
    }

    private void Teardown()
    {
        foreach (var win in _windows)
        {
            try { win.BeginAnimation(Window.OpacityProperty, null); win.Close(); }
            catch { /* ignore */ }
        }
        _windows.Clear();
    }

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Color.FromRgb(0xFF, 0x3B, 0x30); } // Fallback Rot
    }

    public void Dispose() => Teardown();
}
