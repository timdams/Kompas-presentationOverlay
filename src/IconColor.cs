using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PresentationOverlay;

/// <summary>Bepaalt een accentkleur die bij een icoon past: de meest voorkomende levendige tint.</summary>
public static class IconColor
{
    const int Buckets = 24;

    /// <summary>Null als het icoon geen duidelijke kleur heeft (grijs, zwart-wit).</summary>
    public static Color? Dominant(ImageSource source)
    {
        if (source is not BitmapSource bitmap) return null;
        try
        {
            var bgra = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var width = bgra.PixelWidth;
            var height = bgra.PixelHeight;
            var pixels = new byte[width * height * 4];
            bgra.CopyPixels(pixels, width * 4, 0);

            var weight = new double[Buckets];
            var sum = new double[Buckets, 3];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] < 128) continue;
                double b = pixels[i] / 255.0, g = pixels[i + 1] / 255.0, r = pixels[i + 2] / 255.0;
                ToHsv(r, g, b, out var h, out var s, out var v);
                if (s < 0.3 || v < 0.3) continue; // grijs en bijna zwart zegt niets over de kleur

                var bucket = Math.Min((int)(h / 360 * Buckets), Buckets - 1);
                var w = s * v;
                weight[bucket] += w;
                sum[bucket, 0] += r * w;
                sum[bucket, 1] += g * w;
                sum[bucket, 2] += b * w;
            }

            var best = Array.IndexOf(weight, weight.Max());
            if (weight[best] < pixels.Length / 4 * 0.02) return null; // minder dan ~2% kleurig

            var color = Color.FromRgb(
                (byte)(sum[best, 0] / weight[best] * 255),
                (byte)(sum[best, 1] / weight[best] * 255),
                (byte)(sum[best, 2] / weight[best] * 255));
            return ForWhiteText(color);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Wit label op een felgele of bijna zwarte achtergrond leest slecht: terug naar een middentoon.</summary>
    static Color ForWhiteText(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        ToHsv(r, g, b, out var h, out var s, out var v);
        s = Math.Max(s, 0.55);
        v = Math.Clamp(v, 0.6, 0.9);
        var color = FromHsv(h, s, v);
        // Relatieve helderheid begrenzen zodat witte tekst leesbaar blijft.
        while (Luminance(color) > 0.28 && v > 0.3)
        {
            v *= 0.93;
            color = FromHsv(h, s, v);
        }
        return color;
    }

    static double Luminance(Color c)
    {
        static double Lin(byte x) { var v = x / 255.0; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
        return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
    }

    static void ToHsv(double r, double g, double b, out double h, out double s, out double v)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var d = max - min;
        v = max;
        s = max == 0 ? 0 : d / max;
        if (d == 0) h = 0;
        else if (max == r) h = 60 * (((g - b) / d % 6 + 6) % 6);
        else if (max == g) h = 60 * ((b - r) / d + 2);
        else h = 60 * ((r - g) / d + 4);
    }

    static Color FromHsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}
