using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ColorPicker
{
    // Freezes the screen into one bitmap, covers every monitor with it and draws a
    // magnifier next to the cursor. Click or Enter picks, Esc or right-click cancels,
    // arrow keys nudge one pixel.
    sealed class PickerOverlay : Form
    {
        public event Action<Color> Picked;

        const int Cells = 11; // odd, so there is a centre pixel
        readonly int zoom = Math.Max(8, Dpi.Px(11));
        readonly int lens, labelH, gap;
        readonly Rectangle virt;
        readonly Bitmap shot;
        readonly SolidBrush cellBrush = new SolidBrush(Color.Black);
        readonly SolidBrush labelBrush = new SolidBrush(Color.FromArgb(32, 32, 32));
        readonly Font font = new Font("Consolas", 10f);
        Point cur;          // cursor in client (= bitmap) coordinates
        Rectangle lastLens; // area drawn last time, to repaint only what changed
        bool done;

        public PickerOverlay()
        {
            lens = Cells * zoom;
            labelH = Dpi.Px(24);
            gap = Dpi.Px(20);

            virt = SystemInformation.VirtualScreen;
            shot = new Bitmap(virt.Width, virt.Height, PixelFormat.Format32bppRgb);
            using (var g = Graphics.FromImage(shot)) g.CopyFromScreen(virt.Location, Point.Empty, virt.Size);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Cursor = Cursors.Cross;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            Bounds = virt;
            cur = ToClient(Cursor.Position);
        }

        Point ToClient(Point screen) { return new Point(screen.X - virt.X, screen.Y - virt.Y); }

        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            // Stop WinForms from shrinking the form to one monitor's size.
            base.SetBoundsCore(virt.X, virt.Y, virt.Width, virt.Height, BoundsSpecified.All);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            Native.SetForegroundWindow(Handle);
            UpdateLens();
        }

        Color PixelAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= shot.Width || y >= shot.Height) return Color.Black;
            Color c = shot.GetPixel(x, y);
            return Color.FromArgb(c.R, c.G, c.B);
        }

        Rectangle LensBounds()
        {
            int w = lens + 2, h = lens + 2 + labelH;
            var scr = Screen.FromPoint(new Point(cur.X + virt.X, cur.Y + virt.Y)).Bounds;
            scr.Offset(-virt.X, -virt.Y);
            int x = cur.X + gap, y = cur.Y + gap;
            if (x + w > scr.Right) x = cur.X - gap - w;
            if (y + h > scr.Bottom) y = cur.Y - gap - h;
            return new Rectangle(x, y, w, h);
        }

        void UpdateLens()
        {
            var r = LensBounds();
            Invalidate(lastLens);
            Invalidate(r);
            lastLens = r;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImage(shot, e.ClipRectangle, e.ClipRectangle, GraphicsUnit.Pixel);
            var b = lastLens.IsEmpty ? LensBounds() : lastLens;
            if (!b.IntersectsWith(e.ClipRectangle)) return;

            int half = Cells / 2;
            for (int dy = -half; dy <= half; dy++)
                for (int dx = -half; dx <= half; dx++)
                {
                    cellBrush.Color = PixelAt(cur.X + dx, cur.Y + dy);
                    g.FillRectangle(cellBrush, b.X + 1 + (dx + half) * zoom, b.Y + 1 + (dy + half) * zoom, zoom, zoom);
                }

            var centre = new Rectangle(b.X + 1 + half * zoom, b.Y + 1 + half * zoom, zoom - 1, zoom - 1);
            g.DrawRectangle(Pens.White, centre);
            centre.Inflate(1, 1);
            g.DrawRectangle(Pens.Black, centre);
            g.DrawRectangle(Pens.Gray, b.X, b.Y, lens + 1, lens + 1);

            var label = new Rectangle(b.X, b.Y + lens + 2, lens + 2, labelH);
            g.FillRectangle(labelBrush, label);
            Color picked = PixelAt(cur.X, cur.Y);
            int sw = labelH - Dpi.Px(8);
            cellBrush.Color = picked;
            g.FillRectangle(cellBrush, label.X + Dpi.Px(5), label.Y + Dpi.Px(4), sw, sw);
            g.DrawRectangle(Pens.Gray, label.X + Dpi.Px(5), label.Y + Dpi.Px(4), sw, sw);
            TextRenderer.DrawText(g, ColorFormats.Hex(picked), font,
                new Rectangle(label.X + sw + Dpi.Px(10), label.Y, label.Width - sw - Dpi.Px(10), label.Height),
                Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.Location == cur) return;
            cur = e.Location;
            UpdateLens();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) Finish(true);
            else Finish(false);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            int dx = 0, dy = 0;
            switch (keyData)
            {
                case Keys.Escape: Finish(false); return true;
                case Keys.Enter: case Keys.Space: Finish(true); return true;
                case Keys.Left: dx = -1; break;
                case Keys.Right: dx = 1; break;
                case Keys.Up: dy = -1; break;
                case Keys.Down: dy = 1; break;
                default: return base.ProcessCmdKey(ref msg, keyData);
            }
            var p = Cursor.Position;
            Cursor.Position = new Point(p.X + dx, p.Y + dy);
            cur = ToClient(Cursor.Position);
            UpdateLens();
            return true;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Finish(false); // another window took focus: cancel
        }

        void Finish(bool pick)
        {
            if (done) return;
            done = true;
            // Raise Picked before closing, so the save popup can take focus from us.
            if (pick && Picked != null) Picked(PixelAt(cur.X, cur.Y));
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                shot.Dispose();
                cellBrush.Dispose();
                labelBrush.Dispose();
                font.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
