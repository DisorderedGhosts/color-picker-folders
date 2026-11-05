using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace ColorPicker
{
    // Shows a picture with its palette:
    //   left  - the picture; hover shows a magnifier, click picks that exact pixel
    //   right - [Main colours | Details] toggle, the colours, and "Save all to folder"
    sealed class ImageWindow : Form
    {
        public static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" };
        public const string OpenFilter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff";

        readonly Store store;
        readonly ImageView view = new ImageView();
        readonly PaletteView palette = new PaletteView();
        readonly Panel paletteScroll = new Panel();
        readonly FlatButton mainTab, detailTab, saveAll, openButton;
        readonly Label hint = new Label();
        List<PaletteEntry> mainColors = new List<PaletteEntry>(), detailColors = new List<PaletteEntry>();
        bool details;

        public ImageWindow(Store store, Action<Color> onSelect, Action<Color> onPick)
        {
            this.store = store;

            Text = "Colours from picture";
            Icon = Icons.App;
            Font = new Font("Segoe UI", 9.5f);
            StartPosition = FormStartPosition.Manual;
            ClientSize = Dpi.Px(860, 520);
            MinimumSize = SizeFromClientSize(Dpi.Px(640, 420));
            ShowInTaskbar = false;
            KeyPreview = true;
            AllowDrop = true;

            int pad = Dpi.Px(16), rightW = Dpi.Px(280), rightX = ClientSize.Width - pad - rightW;
            var right = AnchorStyles.Top | AnchorStyles.Right;

            view.Bounds = new Rectangle(pad, pad, rightX - 2 * pad, ClientSize.Height - 2 * pad - Dpi.Px(24));
            view.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            view.Picked += c => onPick(c);
            Controls.Add(view);

            hint.Text = "Click the picture to pick an exact pixel";
            hint.Font = new Font(Font.FontFamily, 8.5f);
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.Bounds = new Rectangle(pad, ClientSize.Height - pad - Dpi.Px(18), rightX - 2 * pad - Dpi.Px(130), Dpi.Px(18));
            hint.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(hint);

            openButton = new FlatButton { Text = "Open another…", Bounds = new Rectangle(rightX - pad - Dpi.Px(120), ClientSize.Height - pad - Dpi.Px(22), Dpi.Px(120), Dpi.Px(26)), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            openButton.Font = new Font(Font.FontFamily, 8.5f);
            openButton.Click += delegate { BrowseAndLoad(); };
            Controls.Add(openButton);

            int half = (rightW - Dpi.Px(6)) / 2;
            mainTab = new FlatButton { Text = "Main colours", Bounds = new Rectangle(rightX, pad, half, Dpi.Px(34)), Anchor = right };
            detailTab = new FlatButton { Text = "Details", Bounds = new Rectangle(rightX + half + Dpi.Px(6), pad, half, Dpi.Px(34)), Anchor = right };
            mainTab.Click += delegate { ShowDetails(false); };
            detailTab.Click += delegate { ShowDetails(true); };
            Controls.Add(mainTab);
            Controls.Add(detailTab);

            int listY = pad + Dpi.Px(34) + Dpi.Px(12), saveH = Dpi.Px(36);
            paletteScroll.Bounds = new Rectangle(rightX, listY, rightW, ClientSize.Height - listY - pad - saveH - Dpi.Px(12));
            paletteScroll.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            paletteScroll.AutoScroll = true;
            palette.Location = Point.Empty;
            palette.Width = rightW;
            palette.Selected += c => onSelect(c);
            paletteScroll.Controls.Add(palette);
            paletteScroll.Resize += delegate { FitPalette(); };
            Controls.Add(paletteScroll);

            saveAll = new FlatButton { Text = "Save all to folder  ▾", Bounds = new Rectangle(rightX, ClientSize.Height - pad - saveH, rightW, saveH), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            saveAll.Click += delegate { ShowSaveMenu(); };
            Controls.Add(saveAll);

            ApplyTheme();
            ShowDetails(false);
        }

        public void ApplyTheme()
        {
            var p = Theme.Current;
            BackColor = paletteScroll.BackColor = p.Back;
            ForeColor = p.Text;
            hint.ForeColor = p.Muted;
            Theme.ApplyTitleBar(this);
            foreach (Control c in Controls) c.Invalidate(true);
            palette.Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this);
        }

        // ---------- loading ----------

        public static bool IsImageFile(string path)
        {
            return Array.IndexOf(Extensions, Path.GetExtension(path).ToLowerInvariant()) >= 0;
        }

        public void BrowseAndLoad()
        {
            using (var dlg = new OpenFileDialog { Filter = OpenFilter, Title = "Choose a picture" })
                if (dlg.ShowDialog(this) == DialogResult.OK) LoadImage(dlg.FileName);
        }

        public bool LoadImage(string path)
        {
            Bitmap bmp;
            try
            {
                // Read through a copy so the file isn't kept locked while the window is open.
                using (var fs = new MemoryStream(File.ReadAllBytes(path)))
                using (var img = Image.FromStream(fs))
                    bmp = new Bitmap(img);
            }
            catch (Exception)
            {
                MessageBox.Show(Visible ? this : null, "Couldn't open that picture. PNG, JPG, BMP, GIF and TIFF work.", MainWindow.WindowTitle);
                return false;
            }

            Cursor = Cursors.WaitCursor;
            try
            {
                var pixels = ImagePalette.Sample(bmp);
                mainColors = ImagePalette.Extract(pixels, 5);
                detailColors = ImagePalette.Detailed(pixels);
            }
            finally { Cursor = Cursors.Default; }

            view.SetImage(bmp);
            Text = Path.GetFileName(path) + " – Colours from picture";
            ShowDetails(details);
            return true;
        }

        // ---------- palette ----------

        void ShowDetails(bool on)
        {
            details = on;
            mainTab.Primary = !on;
            detailTab.Primary = on;
            mainTab.Invalidate();
            detailTab.Invalidate();
            palette.SetEntries(on ? detailColors : mainColors, on);
            FitPalette();
            saveAll.Enabled = mainColors.Count > 0;
        }

        void FitPalette()
        {
            int w = paletteScroll.ClientSize.Width;
            palette.Width = w;
            palette.Height = Math.Max(paletteScroll.ClientSize.Height, palette.ContentHeight(paletteScroll.ClientSize.Height));
        }

        void ShowSaveMenu()
        {
            var colors = details ? detailColors : mainColors;
            if (colors.Count == 0) return;
            var menu = new ContextMenuStrip { Font = Font, ShowImageMargin = false, Padding = new Padding(Dpi.Px(2)) };
            Theme.RoundWindow(menu, true);
            menu.Closed += delegate { BeginInvoke(new Action(menu.Dispose)); };
            var folders = store.GetFolders();
            foreach (var name in folders)
            {
                string target = name;
                menu.Items.Add(name, null, delegate { SaveAll(target, colors); });
            }
            if (folders.Count > 0) menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("New folder…", null, delegate
            {
                string name = Prompt.Ask(this, "New folder", "", n => store.ValidateName(n, null));
                if (name == null) return;
                store.CreateFolder(name);
                SaveAll(name, colors);
            });
            Theme.FitMenuWidth(menu, saveAll.Width);
            menu.Show(saveAll, new Point(0, saveAll.Height));
        }

        void SaveAll(string folder, List<PaletteEntry> colors)
        {
            try
            {
                var list = new List<Color>();
                foreach (var e in colors) list.Add(e.Color);
                store.AddAllToFolder(folder, list);
                saveAll.Text = "Saved " + list.Count + " to " + folder + "  ✓";
                var t = new Timer { Interval = 1800 };
                t.Tick += delegate { t.Dispose(); if (!saveAll.IsDisposed) saveAll.Text = "Save all to folder  ▾"; };
                t.Start();
            }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't save: " + ex.Message, MainWindow.WindowTitle); }
        }

        // ---------- drag and drop ----------

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            e.Effect = DroppedImage(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            string path = DroppedImage(e);
            if (path != null) LoadImage(path);
        }

        public static string DroppedImage(DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return null;
            foreach (var f in files) if (IsImageFile(f)) return f;
            return null;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            view.SetImage(null);
            base.OnFormClosed(e);
        }
    }

    // The picture, scaled to fit, with a magnifier under the cursor. Click picks the exact pixel.
    sealed class ImageView : Control
    {
        public event Action<Color> Picked;

        const int Cells = 11;
        readonly int zoom = Math.Max(8, Dpi.Px(10));
        Bitmap image;
        Rectangle shown;        // where the picture is drawn, in control coordinates
        Point mouse = new Point(-1, -1);
        Rectangle lastLens;

        public ImageView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        public void SetImage(Bitmap bmp)
        {
            if (image != null) image.Dispose();
            image = bmp;
            Fit();
            Invalidate();
        }

        void Fit()
        {
            if (image == null) { shown = Rectangle.Empty; return; }
            double s = Math.Min((double)Width / image.Width, (double)Height / image.Height);
            int w = Math.Max(1, (int)(image.Width * s)), h = Math.Max(1, (int)(image.Height * s));
            shown = new Rectangle((Width - w) / 2, (Height - h) / 2, w, h);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Fit(); }

        // Control point -> pixel in the full-size picture, or (-1,-1) when outside it.
        Point ToImage(Point p)
        {
            if (image == null || !shown.Contains(p)) return new Point(-1, -1);
            int x = (int)((p.X - shown.X) * (double)image.Width / shown.Width);
            int y = (int)((p.Y - shown.Y) * (double)image.Height / shown.Height);
            return new Point(Math.Min(image.Width - 1, x), Math.Min(image.Height - 1, y));
        }

        Color PixelAt(int x, int y)
        {
            if (image == null || x < 0 || y < 0 || x >= image.Width || y >= image.Height) return Theme.Current.Back;
            var c = image.GetPixel(x, y);
            return Color.FromArgb(c.R, c.G, c.B);
        }

        Rectangle LensBounds()
        {
            int lens = Cells * zoom + 2, labelH = Dpi.Px(24), gap = Dpi.Px(18);
            int w = lens, h = lens + labelH;
            int x = mouse.X + gap, y = mouse.Y + gap;
            if (x + w > Width) x = mouse.X - gap - w;
            if (y + h > Height) y = mouse.Y - gap - h;
            return new Rectangle(x, y, w, h);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            mouse = e.Location;
            Cursor = ToImage(mouse).X >= 0 ? Cursors.Cross : Cursors.Default;
            var r = ToImage(mouse).X >= 0 ? LensBounds() : Rectangle.Empty;
            Invalidate(lastLens);
            Invalidate(r);
            lastLens = r;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            mouse = new Point(-1, -1);
            Invalidate(lastLens);
            lastLens = Rectangle.Empty;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            var p = ToImage(e.Location);
            if (p.X >= 0 && e.Button == MouseButtons.Left && Picked != null) Picked(PixelAt(p.X, p.Y));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var pal = Theme.Current;
            var g = e.Graphics;
            g.Clear(pal.Back);

            if (image == null)
            {
                var box = Theme.Inner(ClientRectangle);
                Theme.FillRound(g, pal.Surface, box, Dpi.Px(8));
                using (var pen = new Pen(pal.Line, 2) { DashStyle = DashStyle.Dash })
                using (var path = Theme.RoundRect(Rectangle.Inflate(box, -Dpi.Px(6), -Dpi.Px(6)), Dpi.Px(6)))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawPath(pen, path);
                }
                TextRenderer.DrawText(g, "Drop a picture here", Font, ClientRectangle, pal.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(image, shown);
            g.InterpolationMode = InterpolationMode.Default;
            g.PixelOffsetMode = PixelOffsetMode.Default;

            var at = ToImage(mouse);
            if (at.X < 0) return;
            var b = LensBounds();
            int half = Cells / 2;
            using (var brush = new SolidBrush(Color.Black))
            {
                g.FillRectangle(brush, b.X, b.Y, Cells * zoom + 2, Cells * zoom + 2);
                for (int dy = -half; dy <= half; dy++)
                    for (int dx = -half; dx <= half; dx++)
                    {
                        brush.Color = PixelAt(at.X + dx, at.Y + dy);
                        g.FillRectangle(brush, b.X + 1 + (dx + half) * zoom, b.Y + 1 + (dy + half) * zoom, zoom, zoom);
                    }
                var centre = new Rectangle(b.X + 1 + half * zoom, b.Y + 1 + half * zoom, zoom - 1, zoom - 1);
                g.DrawRectangle(Pens.White, centre);
                centre.Inflate(1, 1);
                g.DrawRectangle(Pens.Black, centre);

                var label = new Rectangle(b.X, b.Y + Cells * zoom + 2, b.Width, b.Height - Cells * zoom - 2);
                brush.Color = Color.FromArgb(32, 32, 32);
                g.FillRectangle(brush, label);
                var c = PixelAt(at.X, at.Y);
                int sw = label.Height - Dpi.Px(8);
                brush.Color = c;
                g.FillRectangle(brush, label.X + Dpi.Px(5), label.Y + Dpi.Px(4), sw, sw);
                TextRenderer.DrawText(g, ColorFormats.Hex(c), Font,
                    new Rectangle(label.X + sw + Dpi.Px(10), label.Y, label.Width - sw - Dpi.Px(10), label.Height),
                    Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && image != null) { image.Dispose(); image = null; }
            base.Dispose(disposing);
        }
    }

    // Main colours: five big blocks with HEX and share.
    // Details: one row per colour with a bar showing how much of the picture it covers.
    sealed class PaletteView : Control
    {
        public event Action<Color> Selected;

        List<PaletteEntry> entries = new List<PaletteEntry>();
        bool details;
        int hover = -1;

        public PaletteView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        public void SetEntries(List<PaletteEntry> list, bool detailView)
        {
            entries = list;
            details = detailView;
            hover = -1;
            Invalidate();
        }

        int RowH { get { return Dpi.Px(30); } }

        public int ContentHeight(int available)
        {
            return details ? entries.Count * RowH : available;
        }

        Rectangle ItemRect(int i)
        {
            if (details) return new Rectangle(0, i * RowH, Width, RowH);
            int gap = Dpi.Px(8), n = Math.Max(1, entries.Count);
            int h = (Height - gap * (n - 1)) / n;
            return new Rectangle(0, i * (h + gap), Width, h);
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < entries.Count; i++) if (ItemRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            if (h == hover) return;
            hover = h;
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int h = HitTest(e.Location);
            if (h >= 0 && Selected != null) Selected(entries[h].Color);
        }

        static string Percent(double share)
        {
            double v = share * 100;
            return (v < 1 ? v.ToString("0.0") : v.ToString("0")) + "%";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Theme.Current;
            var g = e.Graphics;
            g.Clear(p.Back);

            if (entries.Count == 0)
            {
                TextRenderer.DrawText(g, "Open or drop a picture to see its colours.", Font, ClientRectangle, p.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
                return;
            }

            var left = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding;
            var right = TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPadding;
            int pad = Dpi.Px(12);

            if (!details)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var r = ItemRect(i);
                    var c = entries[i].Color;
                    var fore = ColorFormats.Contrast(c);
                    Theme.FillRound(g, c, Theme.Inner(r), Dpi.Px(8));
                    if (hover == i) Theme.DrawRound(g, fore, 2, new Rectangle(r.X + 1, r.Y + 1, r.Width - 3, r.Height - 3), Dpi.Px(7));
                    var text = new Rectangle(r.X + pad, r.Y, r.Width - 2 * pad, r.Height);
                    TextRenderer.DrawText(g, ColorFormats.Hex(c), Font, text, fore, left);
                    TextRenderer.DrawText(g, Percent(entries[i].Share), Font, text, fore, right);
                }
                return;
            }

            double max = entries[0].Share;
            int sw = Dpi.Px(20), hexW = Dpi.Px(70), pctW = Dpi.Px(44);
            for (int i = 0; i < entries.Count; i++)
            {
                var r = ItemRect(i);
                var c = entries[i].Color;
                if (hover == i) Theme.FillRound(g, p.Surface, Theme.Inner(r), Dpi.Px(6));
                int cy = r.Y + (r.Height - sw) / 2;
                Theme.FillRound(g, c, new Rectangle(r.X + Dpi.Px(6), cy, sw, sw), Dpi.Px(4));
                Theme.DrawRound(g, p.Line, 1, new Rectangle(r.X + Dpi.Px(6), cy, sw, sw), Dpi.Px(4));
                int x = r.X + Dpi.Px(6) + sw + Dpi.Px(8);
                TextRenderer.DrawText(g, ColorFormats.Hex(c), Font, new Rectangle(x, r.Y, hexW, r.Height), p.Text, left);
                x += hexW;
                int barW = r.Right - Dpi.Px(8) - pctW - x;
                int barH = Dpi.Px(8), by = r.Y + (r.Height - barH) / 2;
                Theme.FillRound(g, hover == i ? p.Hover : p.Surface, new Rectangle(x, by, barW, barH), barH / 2);
                int fill = Math.Max(barH, (int)(barW * entries[i].Share / max));
                Theme.FillRound(g, c, new Rectangle(x, by, fill, barH), barH / 2);
                Theme.DrawRound(g, Color.FromArgb(70, p.Muted), 1, new Rectangle(x, by, fill, barH), barH / 2); // keeps dark colours visible on a dark track
                TextRenderer.DrawText(g, Percent(entries[i].Share), Font, new Rectangle(r.Right - Dpi.Px(8) - pctW, r.Y, pctW, r.Height), p.Muted, right);
            }
        }
    }
}
