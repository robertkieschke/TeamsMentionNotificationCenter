using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Dev-Werkzeug: erzeugt app.ico aus demselben Logo wie Core/Branding.RenderBitmap –
// mehrere Auflösungen (16–256 px) als PNG-Frames in einer echten .ico-Datei.
//   dotnet run --project tools/IconGen -- src/TeamsMentionNotificationCenter/app.ico
int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
var accent = Color.FromArgb(0xE0, 0x3B, 0x2F); // Branding.Accent
string outPath = args.Length > 0 ? args[0] : "app.ico";

var frames = new List<byte[]>();
foreach (var s in sizes)
{
    using var bmp = RenderBitmap(s, accent);
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    frames.Add(ms.ToArray());
}

using (var fs = File.Create(outPath))
using (var w = new BinaryWriter(fs))
{
    w.Write((short)0);              // reserved
    w.Write((short)1);              // type = icon
    w.Write((short)sizes.Length);   // image count
    int offset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        int s = sizes[i];
        w.Write((byte)(s >= 256 ? 0 : s)); // width  (0 => 256)
        w.Write((byte)(s >= 256 ? 0 : s)); // height (0 => 256)
        w.Write((byte)0);           // palette count
        w.Write((byte)0);           // reserved
        w.Write((short)1);          // color planes
        w.Write((short)32);         // bits per pixel
        w.Write(frames[i].Length);  // size of PNG data
        w.Write(offset);            // offset of PNG data
        offset += frames[i].Length;
    }
    foreach (var f in frames) w.Write(f);
}
Console.WriteLine($"Wrote {outPath} ({sizes.Length} frames, {new FileInfo(outPath).Length} bytes)");

static Bitmap RenderBitmap(int size, Color dotColor)
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
        using var dots = new SolidBrush(Color.FromArgb(0x15, 0x55, 0xC8));
        float r = s * 0.035f, cy = s * 0.43f;
        foreach (var cx in new[] { 0.34f, 0.47f, 0.60f })
            g.FillEllipse(dots, s * cx - r, cy - r, r * 2, r * 2);
    }

    // Melde-Punkt oben rechts (Marken-Rot) mit weißem Ring.
    var dot = new RectangleF(s * 0.58f, s * 0.09f, s * 0.30f, s * 0.30f);
    using (var db = new SolidBrush(dotColor))
        g.FillEllipse(db, dot);
    using (var ring = new Pen(Color.White, Math.Max(1f, s * 0.05f)))
        g.DrawEllipse(ring, dot);

    return bmp;
}

static GraphicsPath RoundedRect(RectangleF r, float radius)
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
