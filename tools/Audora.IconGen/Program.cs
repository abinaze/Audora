using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Audora.IconGen;

/// <summary>Builds assets/icons/audora.ico and PNGs from assets/brand/mark.svg.</summary>
internal static class Program
{
    private static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };

    // Size of the mark in mark.svg's own coordinates.
    private const double MarkWidth = 746;
    private const double MarkHeight = 642;

    [STAThread]
    private static int Main(string[] args)
    {
        var repo = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        var markFile = Path.Combine(repo, "assets", "brand", "mark.svg");
        if (!File.Exists(markFile))
        {
            Console.WriteLine($"Not found: {markFile}. Run this from the repository root.");
            return 1;
        }

        var shapes = ReadShapes(File.ReadAllText(markFile));
        if (shapes.Count == 0)
        {
            Console.WriteLine("No shapes found in mark.svg.");
            return 1;
        }

        var outDir = Path.Combine(repo, "assets", "icons");
        Directory.CreateDirectory(outDir);

        var images = new List<(int Size, byte[] Data)>();
        foreach (var size in Sizes)
        {
            var data = Render(shapes, size);
            images.Add((size, data));
            File.WriteAllBytes(Path.Combine(outDir, $"audora-{size}.png"), data);
        }

        WriteIco(Path.Combine(outDir, "audora.ico"), images);
        Console.WriteLine($"Wrote audora.ico and {images.Count} PNGs to {outDir}");
        return 0;
    }

    private static List<(Geometry Shape, Brush Fill)> ReadShapes(string svg)
    {
        var shapes = new List<(Geometry, Brush)>();
        var pattern = new Regex("<path\\s+fill=\"(#[0-9A-Fa-f]{6})\"\\s+d=\"([^\"]+)\"");
        foreach (Match match in pattern.Matches(svg))
        {
            var geometry = Geometry.Parse(match.Groups[2].Value);
            geometry.Freeze();
            var brush = (Brush)new BrushConverter().ConvertFromString(match.Groups[1].Value)!;
            brush.Freeze();
            shapes.Add((geometry, brush));
        }

        return shapes;
    }

    private static byte[] Render(List<(Geometry Shape, Brush Fill)> shapes, int size)
    {
        var padding = size <= 32 ? 0.04 : 0.10;
        var box = size * (1 - 2 * padding);
        var scale = box / MarkWidth;
        var offsetX = (size - MarkWidth * scale) / 2;
        var offsetY = (size - MarkHeight * scale) / 2;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, offsetX, offsetY));
            foreach (var (shape, fill) in shapes)
            {
                dc.DrawGeometry(fill, null, shape);
            }
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static void WriteIco(string path, List<(int Size, byte[] Data)> images)
    {
        using var file = File.Create(path);
        using var writer = new BinaryWriter(file);

        writer.Write((ushort)0);              // reserved
        writer.Write((ushort)1);              // type: icon
        writer.Write((ushort)images.Count);

        var offset = 6 + 16 * images.Count;
        foreach (var (size, data) in images)
        {
            var dimension = (byte)(size >= 256 ? 0 : size);
            writer.Write(dimension);          // width
            writer.Write(dimension);          // height
            writer.Write((byte)0);            // palette colours
            writer.Write((byte)0);            // reserved
            writer.Write((ushort)1);          // colour planes
            writer.Write((ushort)32);         // bits per pixel
            writer.Write(data.Length);        // image size
            writer.Write(offset);             // image offset
            offset += data.Length;
        }

        foreach (var (_, data) in images)
        {
            writer.Write(data);
        }
    }
}
