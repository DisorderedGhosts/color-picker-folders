using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ColorPicker
{
    sealed class Palette
    {
        public Color Back, Surface, Hover, Pressed, Line, Text, Muted, Accent, AccentHover, AccentText, Success;
        public Color[] Spectrum; // optional brand gradient, drawn as a thin strip on the Pick button
    }

    static class Theme
    {
        // Brand theme: warm charcoal from the mascot's background, magenta from its polka dots,
        // and the hexagon's rainbow as a thin strip on the Pick button.
        public static readonly Palette Dark = new Palette
        {
            Back = Color.FromArgb(42, 41, 38),
            Surface = Color.FromArgb(55, 54, 49),
            Hover = Color.FromArgb(67, 66, 61),
            Pressed = Color.FromArgb(80, 78, 72),
            Line = Color.FromArgb(69, 67, 62),
            Text = Color.FromArgb(238, 236, 231),
            Muted = Color.FromArgb(162, 159, 151),
            Accent = Color.FromArgb(194, 48, 159),
            AccentHover = Color.FromArgb(163, 38, 138),
            AccentText = Color.White,
            Success = Color.FromArgb(74, 222, 128),
            Spectrum = new[] { Color.FromArgb(255, 62, 245), Color.FromArgb(255, 138, 92), Color.FromArgb(245, 225, 90), Color.FromArgb(127, 216, 255) }
        };

        public static readonly Palette Light = new Palette
        {
            Back = Color.FromArgb(247, 246, 243),
            Surface = Color.FromArgb(236, 234, 229),
            Hover = Color.FromArgb(226, 223, 217),
            Pressed = Color.FromArgb(214, 211, 204),
            Line = Color.FromArgb(226, 223, 217),
            Text = Color.FromArgb(34, 33, 31),
            Muted = Color.FromArgb(110, 107, 100),
            Accent = Color.FromArgb(194, 48, 159),
            AccentHover = Color.FromArgb(163, 38, 138),
            AccentText = Color.White,
            Success = Color.FromArgb(22, 163, 74),
            Spectrum = new[] { Color.FromArgb(224, 32, 216), Color.FromArgb(240, 106, 48), Color.FromArgb(217, 168, 0), Color.FromArgb(31, 168, 224) } // deeper so it reads on a light background
        };

        public static Palette Current = Dark;
        public static bool IsDark { get { return Current == Dark; } }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        // Dark scrollbars in dark mode (Windows 10 1809+). Older versions keep the normal ones.
        public static void ApplyScrollbars(Control c)
        {
            if (!c.IsHandleCreated) return;
            try { SetWindowTheme(c.Handle, IsDark ? "DarkMode_Explorer" : "Explorer", null); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            c.Invalidate();
        }

        // Dark title bar on Windows 10 (2004+) and 11. Older versions just keep the normal one.
        public static void ApplyTitleBar(Form f)
        {
            if (!f.IsHandleCreated) return;
            int on = IsDark ? 1 : 0;
            try
            {
                if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0)
                    DwmSetWindowAttribute(f.Handle, 19, ref on, 4);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        // ---- rounded shapes ----

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            if (d <= 0) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Color c, Rectangle r, int radius)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundRect(r, radius))
            using (var b = new SolidBrush(c)) g.FillPath(b, path);
            g.SmoothingMode = old;
        }

        public static void DrawRound(Graphics g, Color c, float width, Rectangle r, int radius)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundRect(r, radius))
            using (var pen = new Pen(c, width)) g.DrawPath(pen, path);
            g.SmoothingMode = old;
        }

        // Inner rectangle for drawing a rounded shape that fills a control (paths draw one pixel past width/height).
        public static Rectangle Inner(Rectangle r) { return new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1); }

        // Rounded window corners for popups and dialogs on Windows 11 (ignored elsewhere).
        public static void RoundWindow(Control c, bool small)
        {
            Action apply = () =>
            {
                int pref = small ? 3 : 2; // DWMWCP_ROUNDSMALL / DWMWCP_ROUND
                try { DwmSetWindowAttribute(c.Handle, 33, ref pref, 4); }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
            };
            if (c.IsHandleCreated) apply();
            c.HandleCreated += delegate { apply(); };
        }

        // Menus size their items to the widest one. To make a menu as wide as the button it
        // drops from, widen the first item; every item then stretches to match.
        public static void FitMenuWidth(ToolStripDropDown menu, int width)
        {
            if (menu.Items.Count == 0) return;
            int extra = width - menu.GetPreferredSize(Size.Empty).Width;
            if (extra <= 0) return;
            var first = menu.Items[0];
            var pad = first.Padding;
            first.Padding = new Padding(pad.Left, pad.Top, pad.Right + extra, pad.Bottom);
        }

        public static ToolStripRenderer MenuRenderer() { return new ThemedRenderer(); }

        sealed class ThemedRenderer : ToolStripProfessionalRenderer
        {
            public ThemedRenderer() : base(new MenuColors()) { RoundedEdges = false; }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? Current.Text : Current.Muted;
                base.OnRenderItemText(e);
            }
            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Current.Muted;
                base.OnRenderArrow(e);
            }
            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
            {
                TextRenderer.DrawText(e.Graphics, "✓", e.Item.Font, e.ImageRectangle, Current.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!(e.Item.Selected || e.Item.Pressed) || !e.Item.Enabled) return;
                // Full-width rounded highlight, inset a little from the menu's edges.
                var r = new Rectangle(Dpi.Px(2), 1, e.Item.Width - Dpi.Px(4), e.Item.Height - 2);
                FillRound(e.Graphics, e.Item.Pressed ? Current.Pressed : Current.Hover, r, Dpi.Px(4));
            }
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using (var b = new SolidBrush(Current.Surface)) e.Graphics.FillRectangle(b, e.AffectedBounds);
            }
            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using (var pen = new Pen(Current.Line))
                    e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                int y = e.Item.Height / 2;
                using (var pen = new Pen(Current.Line)) e.Graphics.DrawLine(pen, Dpi.Px(8), y, e.Item.Width - Dpi.Px(8), y);
            }
        }

        sealed class MenuColors : ProfessionalColorTable
        {
            Palette P { get { return Current; } }
            public override Color ToolStripDropDownBackground { get { return P.Surface; } }
            public override Color ImageMarginGradientBegin { get { return P.Surface; } }
            public override Color ImageMarginGradientMiddle { get { return P.Surface; } }
            public override Color ImageMarginGradientEnd { get { return P.Surface; } }
            public override Color MenuBorder { get { return P.Line; } }
            public override Color MenuItemBorder { get { return P.Hover; } }
            public override Color MenuItemSelected { get { return P.Hover; } }
            public override Color MenuItemSelectedGradientBegin { get { return P.Hover; } }
            public override Color MenuItemSelectedGradientEnd { get { return P.Hover; } }
            public override Color MenuItemPressedGradientBegin { get { return P.Pressed; } }
            public override Color MenuItemPressedGradientEnd { get { return P.Pressed; } }
            public override Color SeparatorDark { get { return P.Line; } }
            public override Color SeparatorLight { get { return P.Line; } }
            public override Color CheckBackground { get { return P.Hover; } }
            public override Color CheckSelectedBackground { get { return P.Pressed; } }
            public override Color CheckPressedBackground { get { return P.Pressed; } }
            public override Color ButtonSelectedBorder { get { return P.Hover; } }
        }
    }
}
