using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ColorPicker
{
    struct PaletteEntry
    {
        public Color Color;
        public double Share; // 0..1 of the picture's pixels
    }

    // Finds the main colours of a picture with k-means clustering.
    // The picture is shrunk to about 200px first, so even big photos take a fraction of a second.
    static class ImagePalette
    {
        const int SampleSize = 200;

        public static int[] Sample(Bitmap source)
        {
            double scale = Math.Min(1.0, (double)SampleSize / Math.Max(source.Width, source.Height));
            int w = Math.Max(1, (int)(source.Width * scale)), h = Math.Max(1, (int)(source.Height * scale));
            using (var small = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(source, 0, 0, w, h);
                }
                var data = small.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var raw = new int[w * h];
                for (int y = 0; y < h; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), raw, y * w, w);
                small.UnlockBits(data);

                // Skip see-through pixels (e.g. the empty background of a logo).
                var opaque = new List<int>(raw.Length);
                foreach (int p in raw) if (((p >> 24) & 0xFF) >= 128) opaque.Add(p);
                return opaque.ToArray();
            }
        }

        // The k most common colour groups, biggest first.
        public static List<PaletteEntry> Extract(int[] pixels, int k)
        {
            var result = new List<PaletteEntry>();
            int n = pixels.Length;
            if (n == 0) return result;

            var r = new double[n]; var g = new double[n]; var b = new double[n];
            for (int i = 0; i < n; i++) { r[i] = (pixels[i] >> 16) & 0xFF; g[i] = (pixels[i] >> 8) & 0xFF; b[i] = pixels[i] & 0xFF; }

            // k-means++ seeding with a fixed seed, so the same picture always gives the same palette.
            var rnd = new Random(7);
            var cr = new List<double>(); var cg = new List<double>(); var cb = new List<double>();
            int first = rnd.Next(n);
            cr.Add(r[first]); cg.Add(g[first]); cb.Add(b[first]);
            var dist = new double[n];
            while (cr.Count < k)
            {
                double total = 0;
                for (int i = 0; i < n; i++)
                {
                    double best = double.MaxValue;
                    for (int c = 0; c < cr.Count; c++) best = Math.Min(best, Dist(r[i], g[i], b[i], cr[c], cg[c], cb[c]));
                    dist[i] = best;
                    total += best;
                }
                if (total <= 0) break; // fewer distinct colours than k
                double pick = rnd.NextDouble() * total;
                int idx = 0;
                for (; idx < n - 1; idx++) { pick -= dist[idx]; if (pick <= 0) break; }
                cr.Add(r[idx]); cg.Add(g[idx]); cb.Add(b[idx]);
            }

            int kk = cr.Count;
            var assign = new int[n];
            var count = new int[kk];
            for (int iter = 0; iter < 25; iter++)
            {
                bool changed = false;
                for (int i = 0; i < n; i++)
                {
                    int best = 0; double bestD = double.MaxValue;
                    for (int c = 0; c < kk; c++)
                    {
                        double d = Dist(r[i], g[i], b[i], cr[c], cg[c], cb[c]);
                        if (d < bestD) { bestD = d; best = c; }
                    }
                    if (assign[i] != best || iter == 0) { assign[i] = best; changed = true; }
                }
                var sr = new double[kk]; var sg = new double[kk]; var sb = new double[kk];
                Array.Clear(count, 0, kk);
                for (int i = 0; i < n; i++) { int c = assign[i]; sr[c] += r[i]; sg[c] += g[i]; sb[c] += b[i]; count[c]++; }
                for (int c = 0; c < kk; c++)
                    if (count[c] > 0) { cr[c] = sr[c] / count[c]; cg[c] = sg[c] / count[c]; cb[c] = sb[c] / count[c]; }
                if (!changed) break;
            }

            for (int c = 0; c < kk; c++)
            {
                if (count[c] == 0) continue;
                result.Add(new PaletteEntry
                {
                    Color = Color.FromArgb(Clamp(cr[c]), Clamp(cg[c]), Clamp(cb[c])),
                    Share = (double)count[c] / n
                });
            }
            result.Sort((x, y) => y.Share.CompareTo(x.Share));
            return result;
        }

        // A fuller list for the details view: up to 16 groups, near-identical shades merged
        // (edges between two areas create in-between shades), tiny slivers dropped.
        public static List<PaletteEntry> Detailed(int[] pixels)
        {
            var list = Extract(pixels, 16);
            var merged = new List<PaletteEntry>();
            foreach (var e in list) // biggest first, so small shades fold into the big ones
            {
                int into = -1;
                for (int i = 0; i < merged.Count && into < 0; i++)
                {
                    var m = merged[i].Color;
                    if (Dist(e.Color.R, e.Color.G, e.Color.B, m.R, m.G, m.B) < 2500) into = i;
                }
                if (into < 0) merged.Add(e);
                else merged[into] = new PaletteEntry { Color = merged[into].Color, Share = merged[into].Share + e.Share };
            }
            merged.RemoveAll(e => e.Share < 0.002);
            merged.Sort((x, y) => y.Share.CompareTo(x.Share));
            return merged;
        }

        // Weighted RGB distance: closer to how different two colours look than plain RGB.
        static double Dist(double r1, double g1, double b1, double r2, double g2, double b2)
        {
            double dr = r1 - r2, dg = g1 - g2, db = b1 - b2;
            return 2 * dr * dr + 4 * dg * dg + 3 * db * db;
        }

        static int Clamp(double v) { return (int)Math.Max(0, Math.Min(255, Math.Round(v))); }
    }
}
