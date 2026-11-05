using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ColorPicker
{
    // The whole app in one small, wide window:
    //
    //   [ Pick colour        ][⚙]   | [ Recent colours ▾      ][⋯]
    //   [      big swatch       ]   | ■ ■ ■ ■ ■ ■
    //   HEX  #3498DB          Copy  | ■ ■ ■
    //   RGB / HSL / CMYK rows       |
    //   [ Save to folder ▾      ]   |                 Ctrl+Alt+C tip
    sealed class MainWindow : Form
    {
        public const string WindowTitle = "Color Picker";
        const string HotkeyText = "Ctrl+Alt+C";
        const string RecentName = "Recent colours";

        readonly Store store;
        readonly HotkeyWindow hotkey;
        readonly FlatButton pickButton, imageButton, settingsButton, saveButton, folderButton, folderMenuButton;
        ImageWindow imageWindow;
        readonly SwatchPanel swatch = new SwatchPanel();
        readonly List<ValueRow> rows = new List<ValueRow>();
        readonly Panel divider = new Panel();
        readonly FlowLayoutPanel tiles = new FlowLayoutPanel();
        readonly Label emptyLabel = new Label();
        readonly Label hint = new Label();
        readonly ToolTip tips = new ToolTip();
        string folder; // null = recent colours
        Color? current;
        bool picking;

        public MainWindow(Store store)
        {
            this.store = store;
            Theme.Current = store.Dark ? Theme.Dark : Theme.Light;
            ToolStripManager.Renderer = Theme.MenuRenderer();

            Text = WindowTitle;
            Icon = Icons.App;
            Font = new Font("Segoe UI", 9.5f);
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            ClientSize = Dpi.Px(660, 370);
            MinimumSize = SizeFromClientSize(Dpi.Px(560, 370));
            KeyPreview = true;
            AllowDrop = true;
            TopMost = store.OnTop;

            int pad = Dpi.Px(16), leftW = Dpi.Px(300), gapX = Dpi.Px(20);
            int rightX = pad + leftW + gapX, rightW = ClientSize.Width - rightX - pad;
            var fixedLeft = AnchorStyles.Top | AnchorStyles.Left;
            var stretch = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // ---- left column: the current colour ----
            int y = pad;
            pickButton = new FlatButton { Text = "Pick colour", Primary = true, Bounds = new Rectangle(pad, y, leftW - Dpi.Px(96), Dpi.Px(44)), Anchor = fixedLeft };
            pickButton.Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold);
            pickButton.Click += delegate { StartPick(); };
            tips.SetToolTip(pickButton, "Or press " + HotkeyText + " from any app");
            imageButton = new FlatButton { Text = "\uE8B9", Bounds = new Rectangle(pad + leftW - Dpi.Px(88), y, Dpi.Px(40), Dpi.Px(44)), Anchor = fixedLeft };
            imageButton.Font = new Font("Segoe MDL2 Assets", 12f);
            imageButton.Click += delegate { OpenImage(null); };
            tips.SetToolTip(imageButton, "Get colours from a picture (or drop one on this window)");
            Controls.Add(imageButton);
            settingsButton = new FlatButton { Text = "\uE713", Bounds = new Rectangle(pad + leftW - Dpi.Px(40), y, Dpi.Px(40), Dpi.Px(44)), Anchor = fixedLeft };
            settingsButton.Font = new Font("Segoe MDL2 Assets", 12f); // Windows 10/11 icon font
            settingsButton.Click += delegate { ShowSettingsMenu(); };
            tips.SetToolTip(settingsButton, "Settings");
            Controls.Add(pickButton);
            Controls.Add(settingsButton);
            y += Dpi.Px(44) + Dpi.Px(12);

            swatch.Bounds = new Rectangle(pad, y, leftW, Dpi.Px(80));
            swatch.Anchor = fixedLeft;
            swatch.Click += delegate { if (current == null) StartPick(); };
            Controls.Add(swatch);
            y += Dpi.Px(80) + Dpi.Px(8);

            foreach (var f in ColorFormats.All)
            {
                var row = new ValueRow(f) { Bounds = new Rectangle(pad, y, leftW, Dpi.Px(30)), Anchor = fixedLeft };
                rows.Add(row);
                Controls.Add(row);
                y += Dpi.Px(30);
            }
            y += Dpi.Px(8);

            saveButton = new FlatButton { Text = "Save to folder  ▾", Bounds = new Rectangle(pad, y, leftW, Dpi.Px(36)), Anchor = fixedLeft };
            saveButton.Click += delegate { ShowSaveMenu(); };
            Controls.Add(saveButton);

            // ---- divider ----
            divider.Bounds = new Rectangle(pad + leftW + gapX / 2, pad, 1, ClientSize.Height - 2 * pad);
            divider.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(divider);

            // ---- right column: recent colours and folders ----
            folderButton = new FlatButton { Bounds = new Rectangle(rightX, pad, rightW - Dpi.Px(42), Dpi.Px(34)), Anchor = stretch, AlignLeft = true };
            folderButton.Click += delegate { ShowFolderPicker(); };
            folderMenuButton = new FlatButton { Text = "⋯", Bounds = new Rectangle(rightX + rightW - Dpi.Px(36), pad, Dpi.Px(36), Dpi.Px(34)), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            folderMenuButton.Font = new Font(Font.FontFamily, 11f, FontStyle.Bold);
            folderMenuButton.Click += delegate { ShowFolderMenu(); };
            tips.SetToolTip(folderMenuButton, "New, rename or delete folders");
            Controls.Add(folderButton);
            Controls.Add(folderMenuButton);

            int tilesY = pad + Dpi.Px(34) + Dpi.Px(10), hintH = Dpi.Px(20);
            tiles.Bounds = new Rectangle(rightX - Dpi.Px(3), tilesY, rightW + Dpi.Px(6), ClientSize.Height - tilesY - pad - hintH);
            tiles.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tiles.AutoScroll = true;
            Controls.Add(tiles);
            emptyLabel.AutoSize = true;
            emptyLabel.MaximumSize = new Size(rightW, 0);
            emptyLabel.Margin = new Padding(Dpi.Px(3));

            hint.Text = "Tip: " + HotkeyText + " picks from any app";
            hint.Font = new Font(Font.FontFamily, 8.5f);
            hint.TextAlign = ContentAlignment.BottomRight;
            hint.Bounds = new Rectangle(rightX, ClientSize.Height - pad - hintH, rightW, hintH);
            hint.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(hint);

            hotkey = new HotkeyWindow(StartPick);
            if (!hotkey.Register(Native.MOD_CONTROL | Native.MOD_ALT, Keys.C))
                hint.Text = "";

            store.Changed += RefreshTiles;
            SetCurrent(store.History.Count > 0 ? (Color?)store.History[0] : null);
            ApplyTheme();
            if (imageWindow != null) imageWindow.ApplyTheme();
            RefreshTiles();
        }

        // ---------- theme ----------

        void ApplyTheme()
        {
            var p = Theme.Current;
            BackColor = p.Back;
            ForeColor = p.Text;
            divider.BackColor = p.Line;
            tiles.BackColor = p.Back;
            emptyLabel.ForeColor = p.Muted;
            hint.ForeColor = p.Muted;
            Theme.ApplyTitleBar(this);
            foreach (Control c in Controls) c.Invalidate(true);
        }

        void ToggleTheme()
        {
            store.Dark = !store.Dark;
            Theme.Current = store.Dark ? Theme.Dark : Theme.Light;
            ApplyTheme();
            if (imageWindow != null) imageWindow.ApplyTheme();
            RefreshTiles();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this);
        }

        // ---------- colours from a picture ----------

        // path == null asks for a file.
        void OpenImage(string path)
        {
            bool fresh = imageWindow == null;
            if (fresh)
            {
                imageWindow = new ImageWindow(store, c => SetCurrent(c), UsePick);
                imageWindow.TopMost = TopMost;
                imageWindow.FormClosed += delegate { imageWindow.Dispose(); imageWindow = null; };
                PlaceImageWindow();
            }
            if (path == null)
            {
                using (var dlg = new OpenFileDialog { Filter = ImageWindow.OpenFilter, Title = "Choose a picture" })
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK) path = dlg.FileName;
                }
            }
            if (path == null || !imageWindow.LoadImage(path))
            {
                if (fresh) { imageWindow.Dispose(); imageWindow = null; }
                return;
            }
            if (!imageWindow.Visible) imageWindow.Show(this);
            imageWindow.Activate();
        }

        // Beside the main window if there's room, otherwise centred on the same screen.
        void PlaceImageWindow()
        {
            var area = Screen.FromControl(this).WorkingArea;
            var size = imageWindow.Size;
            int gap = Dpi.Px(8);
            var at = new Point(Right + gap, Top);
            if (at.X + size.Width > area.Right) at.X = Left - gap - size.Width;
            if (at.X < area.Left) at = new Point(area.Left + (area.Width - size.Width) / 2, area.Top + (area.Height - size.Height) / 2);
            at.Y = Math.Max(area.Top, Math.Min(at.Y, area.Bottom - size.Height));
            imageWindow.Location = at;
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            e.Effect = ImageWindow.DroppedImage(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            string path = ImageWindow.DroppedImage(e);
            if (path != null) BeginInvoke(new Action(() => OpenImage(path))); // let Explorer finish the drop first
        }

        // ---------- picking ----------

        void StartPick()
        {
            if (picking) return;
            picking = true;
            // Get out of the way so we're not in the screenshot, then freeze the screen.
            bool wasVisible = WindowState != FormWindowState.Minimized;
            Opacity = 0;
            var t = new Timer { Interval = 120 };
            t.Tick += delegate
            {
                t.Dispose();
                var overlay = new PickerOverlay();
                overlay.Picked += OnPicked;
                overlay.FormClosed += delegate
                {
                    overlay.Dispose();
                    picking = false;
                    Opacity = 1;
                    if (wasVisible) RestoreWindow();
                };
                overlay.Show();
            };
            t.Start();
        }

        void OnPicked(Color c)
        {
            UsePick(c);
            RestoreWindow();
        }

        // A pick (from the screen or a picture): copy it, remember it, show it.
        void UsePick(Color c)
        {
            Clip.Copy(ColorFormats.Format(c, store.Format));
            store.AddHistory(c);
            SetCurrent(c);
            rows[(int)store.Format].FlashCopied();
        }

        public void RestoreWindow()
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Show();
            Activate();
            Native.SetForegroundWindow(Handle);
        }

        void SetCurrent(Color? c)
        {
            current = c;
            swatch.Color = c;
            foreach (var row in rows) row.SetColor(c);
            saveButton.Enabled = c != null;
        }

        // ---------- menus ----------

        ContextMenuStrip NewMenu()
        {
            var m = new ContextMenuStrip { Font = Font, ShowCheckMargin = true, ShowImageMargin = false, Padding = new Padding(Dpi.Px(2)) };
            Theme.RoundWindow(m, true);
            m.Closed += delegate { BeginInvoke(new Action(m.Dispose)); };
            return m;
        }

        void ShowSaveMenu()
        {
            if (current == null) return;
            var menu = NewMenu();
            menu.ShowCheckMargin = false;
            var folders = store.GetFolders();
            foreach (var name in folders)
            {
                string target = name;
                menu.Items.Add(name, null, delegate { SaveTo(target); });
            }
            if (folders.Count > 0) menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("New folder…", null, delegate
            {
                string name = AskName("New folder", "");
                if (name == null) return;
                store.CreateFolder(name);
                SaveTo(name);
            });
            Theme.FitMenuWidth(menu, saveButton.Width);
            menu.Show(saveButton, new Point(0, saveButton.Height));
        }

        void SaveTo(string name)
        {
            try
            {
                folder = name;
                store.AddToFolder(name, current.Value);
                saveButton.Text = "Saved to " + name + "  ✓";
                var t = new Timer { Interval = 1500 };
                t.Tick += delegate { t.Dispose(); saveButton.Text = "Save to folder  ▾"; };
                t.Start();
            }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't save: " + ex.Message, WindowTitle); }
        }

        void ShowFolderPicker()
        {
            var menu = NewMenu();
            var recent = new ToolStripMenuItem(RecentName, null, delegate { folder = null; RefreshTiles(); }) { Checked = folder == null };
            menu.Items.Add(recent);
            var folders = store.GetFolders();
            if (folders.Count > 0) menu.Items.Add(new ToolStripSeparator());
            foreach (var name in folders)
            {
                string target = name;
                menu.Items.Add(new ToolStripMenuItem(name, null, delegate { folder = target; RefreshTiles(); }) { Checked = name == folder });
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("New folder…", null, delegate { NewFolder(); });
            Theme.FitMenuWidth(menu, folderButton.Width);
            menu.Show(folderButton, new Point(0, folderButton.Height));
        }

        void ShowFolderMenu()
        {
            var menu = NewMenu();
            menu.ShowCheckMargin = false;
            string sel = folder;
            menu.Items.Add("New folder…", null, delegate { NewFolder(); });
            var rename = menu.Items.Add("Rename folder…", null, delegate
            {
                string name = AskName("Rename folder", sel);
                if (name == null || name == sel) return;
                try { folder = name; store.RenameFolder(sel, name); }
                catch (Exception ex) { folder = sel; MessageBox.Show(this, "Couldn't rename: " + ex.Message, WindowTitle); }
            });
            var delete = menu.Items.Add("Delete folder…", null, delegate { DeleteFolder(sel); });
            rename.Enabled = delete.Enabled = sel != null;
            menu.Show(folderMenuButton, new Point(folderMenuButton.Width, folderMenuButton.Height), ToolStripDropDownDirection.BelowLeft);
        }

        void ShowSettingsMenu()
        {
            var menu = NewMenu();
            var copyAs = new ToolStripMenuItem("Copy on pick");
            foreach (var f in ColorFormats.All)
            {
                var fmt = f;
                copyAs.DropDownItems.Add(new ToolStripMenuItem(ColorFormats.Name(f), null, delegate { store.Format = fmt; }) { Checked = f == store.Format });
            }
            Theme.RoundWindow(copyAs.DropDown, true);
            menu.Items.Add(copyAs);
            menu.Items.Add(new ToolStripMenuItem("Dark theme", null, delegate { ToggleTheme(); }) { Checked = store.Dark });
            menu.Items.Add(new ToolStripMenuItem("Keep on top", null, delegate { store.OnTop = !store.OnTop; TopMost = store.OnTop; if (imageWindow != null) imageWindow.TopMost = TopMost; }) { Checked = store.OnTop });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Where colours are saved…", null, delegate { ChooseStorage(); });
            menu.Items.Add("Open saved colours folder", null, delegate
            {
                System.IO.Directory.CreateDirectory(store.StorageDir);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + store.StorageDir + "\"");
            });
            menu.Show(settingsButton, new Point(0, settingsButton.Height));
        }

        string AskName(string title, string initial)
        {
            string ignore = initial == "" ? null : initial;
            return Prompt.Ask(this, title, initial, n => store.ValidateName(n, ignore));
        }

        void NewFolder()
        {
            string name = AskName("New folder", "");
            if (name == null) return;
            folder = name;
            store.CreateFolder(name);
        }

        void DeleteFolder(string name)
        {
            if (name == null) return;
            int n = store.ReadFolder(name).Count;
            var answer = MessageBox.Show(this, "Delete \"" + name + "\" and its " + n + (n == 1 ? " colour" : " colours") + "?\n\nYou can get it back from the Recycle Bin.",
                "Delete folder", MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.OK) return;
            try { folder = null; store.DeleteFolder(name); }
            catch (Exception ex) { folder = name; MessageBox.Show(this, "Couldn't delete: " + ex.Message, WindowTitle); }
        }

        void ChooseStorage()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Choose where your colour folders are saved. Your existing folders move there too.";
                dlg.SelectedPath = store.StorageDir;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { store.SetStorageDir(dlg.SelectedPath); }
                catch (Exception ex) { MessageBox.Show(this, "Couldn't move your folders there: " + ex.Message, WindowTitle); }
            }
        }

        // ---------- tiles ----------

        void RefreshTiles()
        {
            if (IsDisposed) return;
            if (folder != null && !store.GetFolders().Contains(folder)) folder = null;
            folderButton.Text = (folder ?? RecentName) + "  ▾";

            IList<Color> colors = folder != null ? (IList<Color>)store.ReadFolder(folder) : store.History;
            string shownFolder = folder;

            tiles.SuspendLayout();
            foreach (Control c in tiles.Controls) if (c != emptyLabel) c.Dispose();
            tiles.Controls.Clear();
            if (colors.Count == 0)
            {
                emptyLabel.Text = shownFolder != null
                    ? "Nothing saved here yet. Pick a colour, then use Save to folder."
                    : "Colours you pick show up here.";
                tiles.Controls.Add(emptyLabel);
            }
            int size = Dpi.Px(40);
            for (int i = 0; i < colors.Count; i++)
            {
                var c = colors[i];
                int index = i;
                var tile = new SwatchTile(c) { Size = new Size(size, size), Margin = new Padding(Dpi.Px(3)) };
                tips.SetToolTip(tile, ColorFormats.Hex(c) + "  ·  click to select, right-click for more");
                tile.MouseUp += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left) SetCurrent(c);
                    else if (e.Button == MouseButtons.Right)
                    {
                        var menu = NewMenu();
                        menu.ShowCheckMargin = false;
                        menu.Items.Add("Select", null, delegate { SetCurrent(c); });
                        menu.Items.Add("Copy " + ColorFormats.Hex(c), null, delegate { Clip.Copy(ColorFormats.Hex(c)); });
                        menu.Items.Add(new ToolStripSeparator());
                        menu.Items.Add(shownFolder != null ? "Remove from folder" : "Remove from recent", null, delegate
                        {
                            BeginInvoke(new Action(() =>
                            {
                                if (shownFolder != null) store.RemoveFromFolder(shownFolder, index);
                                else store.RemoveHistory(index);
                            }));
                        });
                        menu.Show(tile, e.Location);
                    }
                };
                tiles.Controls.Add(tile);
            }
            tiles.ResumeLayout();
        }

        // ---------- misc ----------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.C) && current != null)
            {
                Clip.Copy(ColorFormats.Format(current.Value, store.Format));
                rows[(int)store.Format].FlashCopied();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_SHOWME) RestoreWindow(); // a second copy was started
            base.WndProc(ref m);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            store.Changed -= RefreshTiles;
            hotkey.Dispose();
            base.OnFormClosed(e);
        }
    }

    // Flat button painted with the current theme.
    sealed class FlatButton : Control
    {
        bool hover, down;
        public bool Primary, AlignLeft;

        public FlatButton()
        {
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Theme.Current;
            Color back, fore;
            if (Primary)
            {
                back = !Enabled ? p.Surface : (hover || down) ? p.AccentHover : p.Accent;
                fore = Enabled ? p.AccentText : p.Muted;
            }
            else
            {
                back = !Enabled ? p.Surface : down ? p.Pressed : hover ? p.Hover : p.Surface;
                fore = Enabled ? p.Text : p.Muted;
            }
            e.Graphics.Clear(Parent != null ? Parent.BackColor : p.Back);
            Theme.FillRound(e.Graphics, back, Theme.Inner(ClientRectangle), Dpi.Px(6));
            var r = AlignLeft ? new Rectangle(Dpi.Px(12), 0, Width - Dpi.Px(20), Height) : ClientRectangle;
            TextRenderer.DrawText(e.Graphics, Text, Font, r, fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | (AlignLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter));
        }
    }

    // Big preview of the current colour.
    sealed class SwatchPanel : Control
    {
        Color? color;
        public SwatchPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        public Color? Color
        {
            get { return color; }
            set { color = value; Cursor = value == null ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Theme.Current;
            var r = ClientRectangle;
            if (color == null)
            {
                e.Graphics.Clear(p.Back);
                Theme.FillRound(e.Graphics, p.Surface, Theme.Inner(r), Dpi.Px(8));
                TextRenderer.DrawText(e.Graphics, "No colour yet", Font, r, p.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            e.Graphics.Clear(p.Back);
            Theme.FillRound(e.Graphics, color.Value, Theme.Inner(r), Dpi.Px(8));
            Theme.DrawRound(e.Graphics, p.Line, 1, Theme.Inner(r), Dpi.Px(8));
        }
    }

    // One small saved colour in the grid.
    sealed class SwatchTile : Control
    {
        readonly Color color;
        bool hover;
        public SwatchTile(Color c)
        {
            color = c;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var r = ClientRectangle;
            int radius = Dpi.Px(6);
            e.Graphics.Clear(Theme.Current.Back);
            Theme.FillRound(e.Graphics, color, Theme.Inner(r), radius);
            if (hover)
                Theme.DrawRound(e.Graphics, ColorFormats.Contrast(color), 2, new Rectangle(1, 1, r.Width - 3, r.Height - 3), radius - 1);
            else
                Theme.DrawRound(e.Graphics, Theme.Current.Line, 1, Theme.Inner(r), radius);
        }
    }

    // One format per row: "RGB  [R 255] [G 214] [B 100]".
    // Click a box to copy just that number (for the matching field in Figma, Photoshop, Canva...).
    // Click the row name to copy the whole value, e.g. rgb(255, 214, 100), for code.
    sealed class ValueRow : Control
    {
        readonly ColorFormat format;
        readonly string[] labels;
        readonly Timer reset = new Timer { Interval = 1200 };
        readonly ToolTip tip = new ToolTip();
        string full;
        string[] values = new string[0];
        int hover = -2;   // -2 none, -1 row name, 0.. box index
        int copied = -2;  // same, for the "Copied" flash

        public ValueRow(ColorFormat format)
        {
            this.format = format;
            labels = ColorFormats.PartLabels(format);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            reset.Tick += delegate { reset.Stop(); copied = -2; Invalidate(); };
        }

        public void SetColor(Color? c)
        {
            if (c == null) { full = null; values = new string[0]; }
            else
            {
                full = ColorFormats.Format(c.Value, format);
                if (format == ColorFormat.Hex) values = new[] { full };
                else values = Array.ConvertAll(ColorFormats.Parts(c.Value, format), v => v.ToString());
            }
            Invalidate();
        }

        // Flash after the whole value was copied (on pick or Ctrl+C).
        public void FlashCopied() { Flash(format == ColorFormat.Hex ? 0 : -1); }

        void Flash(int what)
        {
            copied = what;
            reset.Stop();
            reset.Start();
            Invalidate();
        }

        int NameWidth { get { return Dpi.Px(52); } }

        Rectangle BoxRect(int i)
        {
            int gap = Dpi.Px(4), x0 = NameWidth, n = values.Length;
            int w = (Width - x0 - gap * (n - 1)) / Math.Max(1, n);
            return new Rectangle(x0 + i * (w + gap), Dpi.Px(3), w, Height - Dpi.Px(6));
        }

        int HitTest(Point p)
        {
            if (full == null) return -2;
            if (p.X < NameWidth) return -1;
            for (int i = 0; i < values.Length; i++) if (BoxRect(i).Contains(p)) return i;
            return -2;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            if (h == hover) return;
            hover = h;
            Cursor = h == -2 ? Cursors.Default : Cursors.Hand;
            if (h == -1) tip.SetToolTip(this, "Copy " + full);
            else if (h >= 0 && format != ColorFormat.Hex) tip.SetToolTip(this, "Copy " + labels[h] + " only");
            else tip.SetToolTip(this, null);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -2; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int h = HitTest(e.Location);
            if (h == -2) return;
            string text = h == -1 ? full : values[h];
            if (Clip.Copy(text)) Flash(h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Theme.Current;
            var g = e.Graphics;
            g.Clear(p.Back);
            var left = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding;
            var centre = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding;

            Color nameColor = copied == -1 ? p.Success : hover == -1 ? p.Accent : p.Muted;
            TextRenderer.DrawText(g, ColorFormats.Name(format), Font, new Rectangle(Dpi.Px(2), 0, NameWidth, Height), nameColor, left);

            if (full == null)
            {
                TextRenderer.DrawText(g, "—", Font, new Rectangle(NameWidth, 0, Width - NameWidth, Height), p.Muted, left);
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                var r = BoxRect(i);
                bool isCopied = copied == i || copied == -1;
                Theme.FillRound(g, hover == i ? p.Hover : p.Surface, Theme.Inner(r), Dpi.Px(5));
                if (isCopied) Theme.DrawRound(g, p.Success, 1, Theme.Inner(r), Dpi.Px(5));

                if (format == ColorFormat.Hex)
                {
                    TextRenderer.DrawText(g, values[i], Font, r, p.Text, centre);
                    continue;
                }
                // "R 255": small field letter, then the number
                var inner = new Rectangle(r.X + Dpi.Px(7), r.Y, r.Width - Dpi.Px(12), r.Height);
                TextRenderer.DrawText(g, labels[i], Font, inner, p.Muted, left);
                TextRenderer.DrawText(g, values[i], Font, inner, p.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPadding);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { reset.Dispose(); tip.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
