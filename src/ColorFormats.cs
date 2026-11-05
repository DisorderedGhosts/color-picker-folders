using System;
using System.Drawing;
using System.Globalization;

namespace ColorPicker
{
    enum ColorFormat { Hex, Rgb, Hsb, Hsl, Cmyk } // order = row order in the window

    static class ColorFormats
    {
        public static readonly ColorFormat[] All = { ColorFormat.Hex, ColorFormat.Rgb, ColorFormat.Hsb, ColorFormat.Hsl, ColorFormat.Cmyk };

        public static string Format(Color c, ColorFormat f)
        {
            switch (f)
            {
                case ColorFormat.Rgb: return Rgb(c);
                case ColorFormat.Hsb: return Hsb(c);
                case ColorFormat.Hsl: return Hsl(c);
                case ColorFormat.Cmyk: return Cmyk(c);
                default: return Hex(c);
            }
        }

        public static string Name(ColorFormat f) { return f.ToString().ToUpperInvariant(); }

        public static string Hex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        public static string Rgb(Color c)
        {
            return string.Format("rgb({0}, {1}, {2})", c.R, c.G, c.B);
        }

        // Each part on its own, the way design apps (Figma, Photoshop, Canva) take them:
        // one field per number. Labels are the short field names.
        public static string[] PartLabels(ColorFormat f)
        {
            switch (f)
            {
                case ColorFormat.Rgb: return new[] { "R", "G", "B" };
                case ColorFormat.Hsb: return new[] { "H", "S", "B" };
                case ColorFormat.Hsl: return new[] { "H", "S", "L" };
                case ColorFormat.Cmyk: return new[] { "C", "M", "Y", "K" };
                default: return new[] { "" };
            }
        }

        public static int[] Parts(Color c, ColorFormat f)
        {
            switch (f)
            {
                case ColorFormat.Rgb: return new[] { (int)c.R, c.G, c.B };
                case ColorFormat.Hsb: return HsbParts(c);
                case ColorFormat.Hsl: return HslParts(c);
                case ColorFormat.Cmyk: return CmykParts(c);
                default: return new int[0];
            }
        }

        static double Hue(double r, double g, double b, double max, double d)
        {
            if (d <= 0) return 0;
            double h;
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
            return h < 0 ? h + 360 : h;
        }

        static int[] HsbParts(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            return new[] { R(Hue(r, g, b, max, d)) % 360, R(max > 0 ? d / max * 100 : 0), R(max * 100) };
        }

        static int[] HslParts(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            double l = (max + min) / 2, s = d > 0 ? d / (1 - Math.Abs(2 * l - 1)) : 0;
            return new[] { R(Hue(r, g, b, max, d)) % 360, R(s * 100), R(l * 100) };
        }

        static int[] CmykParts(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double k = 1 - Math.Max(r, Math.Max(g, b));
            if (k >= 1) return new[] { 0, 0, 0, 100 };
            return new[] { R((1 - r - k) / (1 - k) * 100), R((1 - g - k) / (1 - k) * 100), R((1 - b - k) / (1 - k) * 100), R(k * 100) };
        }

        public static string Hsb(Color c)
        {
            var p = HsbParts(c);
            return string.Format("hsb({0}, {1}%, {2}%)", p[0], p[1], p[2]);
        }

        public static string Hsl(Color c)
        {
            var p = HslParts(c);
            return string.Format("hsl({0}, {1}%, {2}%)", p[0], p[1], p[2]);
        }

        public static string Cmyk(Color c)
        {
            var p = CmykParts(c);
            return string.Format("cmyk({0}%, {1}%, {2}%, {3}%)", p[0], p[1], p[2], p[3]);
        }

        public static bool TryParseHex(string s, out Color c)
        {
            c = Color.Empty;
            if (s == null) return false;
            s = s.Trim().TrimStart('#');
            int v;
            if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) return false;
            c = Color.FromArgb((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
            return true;
        }

        // Black or white, whichever reads better on top of c.
        public static Color Contrast(Color c)
        {
            return (c.R * 299 + c.G * 587 + c.B * 114) / 1000 > 140 ? Color.Black : Color.White;
        }

        static int R(double v) { return (int)Math.Round(v, MidpointRounding.AwayFromZero); }
    }
}
