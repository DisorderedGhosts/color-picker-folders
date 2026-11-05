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
    }

    static class Theme
    {
        public static readonly Palette Dark = new Palette
        {
            Back = Color.FromArgb(24, 30, 42),
            Surface = Color.FromArgb(35, 44, 60),
            Hover = Color.FromArgb(46, 58, 78),
            Pressed = Color.FromArgb(56, 70, 94),
            Line = Color.FromArgb(48, 59, 79),
            Text = Color.FromArgb(226, 232, 240),
            Muted = Color.FromArgb(140, 154, 178),
            Accent = Color.FromArgb(59, 130, 246),
            AccentHover = Color.FromArgb(37, 99, 235),
            AccentText = Color.White,
            Success = Color.FromArgb(74, 222, 128)
        };

        public static readonly Palette Light = new Palette
        {
            Back = Color.White,
            Surface = Color.FromArgb(243, 244, 246),
            Hover = Color.FromArgb(229, 231, 235),
            Pressed = Color.FromArgb(209, 213, 219),
            Line = Color.FromArgb(229, 231, 235),
            Text = Color.FromArgb(17, 24, 39),
            Muted = Color.FromArgb(107, 114, 128),
            Accent = Color.FromArgb(37, 99, 235),
            AccentHover = Color.FromArgb(29, 78, 216),
            AccentText = Color.White,
            Success = Color.FromArgb(22, 163, 74)
        };

        public static Palette Current = Dark;
        public static bool IsDark { get { return Current == Dark; } }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

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
