using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using TeamsMentionNotificationCenter.Interop;
using TeamsMentionNotificationCenter.Settings;

namespace TeamsMentionNotificationCenter.Overlay;

/// <summary>
/// Roter (konfigurierbarer) Rand-Glow rund um den Bildschirm.
/// Pro ausgewähltem Monitor ein transparentes, immer-oben-liegendes, klick-durchlässiges Fenster.
/// Muss vom WPF-UI-Thread aus erzeugt und angesprochen werden.
/// </summary>
public sealed class GlowOverlay : IDisposable
{
    private readonly AppSettings _settings;
    private readonly List<Window> _windows = new();
    private bool _persistent;

    public GlowOverlay(AppSettings settings) => _settings = settings;

    /// <summary>Erstellt die Overlay-Fenster aus den aktuellen (gespeicherten) Einstellungen.</summary>
    public void Build() => Build(_settings.GlowColorHex, _settings.GlowThickness, _settings.GlowMonitors);

    /// <summary>
    /// Vorschau: baut das Overlay mit den übergebenen (noch nicht gespeicherten) Werten auf – inkl.
    /// Monitor-Auswahl – und leuchtet einmal auf. Mit <see cref="Build()"/> wird danach der echte Stand
    /// wiederhergestellt.
    /// </summary>
    public void PreviewWith(AppSettings s)
    {
        Build(s.GlowColorHex, s.GlowThickness, s.GlowMonitors);
        FlashCore(Math.Max(400, s.GlowDurationMs));
    }

    private void Build(string colorHex, double thicknessValue, List<int> monitorIndices)
    {
        Teardown();

        var color = ParseColor(colorHex);
        double thickness = Math.Max(4, thicknessValue);
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

        if (monitorIndices.Count > 0)
        {
            var selected = monitors.Where((m, i) => monitorIndices.Contains(i)).ToList();
            if (selected.Count > 0) monitors = selected; // sonst Fallback: alle Monitore
        }

        foreach (var rect in monitors)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(thickness),
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
                Effect = new BlurEffect { Radius = Math.Max(4, thickness * 0.85), KernelType = KernelType.Gaussian }
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
                Title = "TeamsMentionNotificationCenter Glow",
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = rect.Left / scale,
                Top = rect.Top / scale,
                Width = Math.Max(1, rect.Width / scale),
                Height = Math.Max(1, rect.Height / scale),
                Content = border
            };

            win.SourceInitialized += (_, _) =>
                NativeMethods.MakeClickThrough(new WindowInteropHelper(win).Handle);

            win.Show();
            win.Topmost = true;
            _windows.Add(win);
        }

        if (_persistent) ApplyPersistent(true);
    }

    /// <summary>Kurzes Aufleuchten (Trigger: Name erkannt).</summary>
    public void Flash() => FlashCore(Math.Max(400, _settings.GlowDurationMs));

    private void FlashCore(double dur)
    {
        const double peak = 1.0;
        foreach (var win in _windows)
        {
            var anim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(peak, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(peak, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(dur * 0.65))));
            double end = _persistent ? 0.28 : 0.0; // zurück auf Persistenz-Niveau (0 oder dezenter Dauer-Rand)
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(end, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(dur))));
            win.BeginAnimation(Window.OpacityProperty, anim);
        }
    }

    /// <summary>Dezenter Dauer-Rand, solange der Gesprächs-Modus aktiv ist.</summary>
    public void SetPersistentBorder(bool on)
    {
        _persistent = on;
        ApplyPersistent(on);
    }

    private void ApplyPersistent(bool on)
    {
        foreach (var win in _windows)
        {
            var anim = new DoubleAnimation(on ? 0.28 : 0.0, TimeSpan.FromMilliseconds(300)) { FillBehavior = FillBehavior.HoldEnd };
            win.BeginAnimation(Window.OpacityProperty, anim);
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
