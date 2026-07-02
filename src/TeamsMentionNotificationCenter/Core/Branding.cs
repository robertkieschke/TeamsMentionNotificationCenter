using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace TeamsMentionNotificationCenter.Core;

/// <summary>
/// Zeichnet Logo/Icon programmatisch (keine externen Bilddateien nötig): ein modernes, blaues
/// Badge mit weißer Sprechblase und farbigem Melde-Punkt. Wird für Tray-Icon, Fenster-Icon und
/// das Logo im Info-Tab genutzt. Die Punktfarbe kann den Modus anzeigen (grün/rot/grau).
/// </summary>
public static class Branding
{
    public static readonly Color Accent = Color.FromArgb(0xE0, 0x3B, 0x2F); // Marken-Rot (Melde-Punkt)

    /// <summary>Rendert das Icon in gegebener Größe; <paramref name="dotColor"/> färbt den Melde-Punkt.</summary>
    public static Bitmap RenderBitmap(int size, Color dotColor)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        float s = size;

        // Hintergrund: abgerundetes Rechteck mit blauem Verlauf.
        var bg = new RectangleF(s * 0.06f, s * 0.06f, s * 0.88f, s * 0.88f);
        using (var path = RoundedRect(bg, s * 0.22f))
        using (var brush = new LinearGradientBrush(bg,
                   Color.FromArgb(0x33, 0x9B, 0xF5), Color.FromArgb(0x15, 0x55, 0xC8),
                   LinearGradientMode.ForwardDiagonal))
            g.FillPath(brush, path);

        // Weiße Sprechblase mit kleinem "Schwanz".
        var bubble = new RectangleF(s * 0.22f, s * 0.26f, s * 0.50f, s * 0.34f);
        using (var bpath = RoundedRect(bubble, s * 0.12f))
        using (var white = new SolidBrush(Color.White))
        {
            g.FillPath(white, bpath);
            g.FillPolygon(white, new[]
            {
                new PointF(s * 0.34f, s * 0.56f),
                new PointF(s * 0.34f, s * 0.72f),
                new PointF(s * 0.48f, s * 0.58f)
            });
            // Drei Punkte in der Blase (Gespräch/„…").
            using var dots = new SolidBrush(Color.FromArgb(0x15, 0x55, 0xC8));
            float r = s * 0.035f, cy = s * 0.43f;
            foreach (var cx in new[] { 0.34f, 0.47f, 0.60f })
                g.FillEllipse(dots, s * cx - r, cy - r, r * 2, r * 2);
        }

        // Melde-Punkt oben rechts (Farbe = Status/Marke), mit weißem Ring als Kontrast.
        var dot = new RectangleF(s * 0.58f, s * 0.09f, s * 0.30f, s * 0.30f);
        using (var db = new SolidBrush(dotColor))
            g.FillEllipse(db, dot);
        using (var ring = new Pen(Color.White, Math.Max(1f, s * 0.05f)))
            g.DrawEllipse(ring, dot);

        return bmp;
    }

    /// <summary>Vereinfachtes Tray-Icon: nur eine weiße Sprechblase plus gut sichtbarer Status-Punkt.</summary>
    public static Bitmap RenderTrayBitmap(int size, Color dotColor)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        float s = size;

        var bubble = new RectangleF(s * 0.06f, s * 0.08f, s * 0.60f, s * 0.42f);
        using (var bpath = RoundedRect(bubble, s * 0.14f))
        using (var white = new SolidBrush(Color.White))
        {
            g.FillPath(white, bpath);
            g.FillPolygon(white, new[]
            {
                new PointF(s * 0.20f, s * 0.44f),
                new PointF(s * 0.20f, s * 0.60f),
                new PointF(s * 0.36f, s * 0.48f)
            });
        }

        var dot = new RectangleF(s * 0.52f, s * 0.52f, s * 0.40f, s * 0.40f);
        using (var db = new SolidBrush(dotColor))
            g.FillEllipse(db, dot);
        using (var ring = new Pen(Color.White, Math.Max(1.5f, s * 0.06f)))
            g.DrawEllipse(ring, dot);

        return bmp;
    }

    /// <summary>WPF-Bildquelle (PNG, mit Transparenz) für Fenster-Icon und Info-Logo.</summary>
    public static BitmapImage CreateImageSource(int size, Color dotColor)
    {
        using var bmp = RenderBitmap(size, dotColor);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze();
        return img;
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
