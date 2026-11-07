using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Audora.IconGen;

internal static partial class Program
{
    /// <summary>Draws assets/brand/social-preview.png (1280x640) for the repository and website.</summary>
    private static void WriteSocialPreview(string repo, List<(Geometry Shape, Brush Fill)> markShapes)
    {
        var wordmarkFile = Path.Combine(repo, "assets", "brand", "wordmark-dark.svg");
        if (!File.Exists(wordmarkFile))
        {
            return;
        }

        var match = Regex.Match(File.ReadAllText(wordmarkFile), "d=\"([^\"]+)\"");
        if (!match.Success)
        {
            return;
        }

        var wordmark = Geometry.Parse(match.Groups[1].Value);
        wordmark.Freeze();

        const int width = 1280;
        const int height = 640;
        const double wordmarkWidth = 1243.75;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x0B, 0x15, 0x28)), null, new Rect(0, 0, width, height));

            var markScale = 240 / MarkHeight;
            dc.PushTransform(new MatrixTransform(markScale, 0, 0, markScale, (width - MarkWidth * markScale) / 2, 80));
            foreach (var (shape, fill) in markShapes)
            {
                dc.DrawGeometry(fill, null, shape);
            }

            dc.Pop();

            var wordmarkScale = 620 / wordmarkWidth;
            dc.PushTransform(new MatrixTransform(wordmarkScale, 0, 0, wordmarkScale, (width - 620) / 2.0, 360));
            dc.DrawGeometry(Brushes.White, null, wordmark);
            dc.Pop();

            var tagline = new FormattedText(
                "Play your Windows audio on many devices at once",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                30,
                new SolidColorBrush(Color.FromRgb(0x98, 0xA6, 0xC4)),
                1.0);
            dc.DrawText(tagline, new Point((width - tagline.Width) / 2, 470));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(repo, "assets", "brand", "social-preview.png"));
        encoder.Save(stream);
    }
}
