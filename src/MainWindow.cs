using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ColorPicker
{
    // The whole app in one small, wide window:
    //
    //   [ Pick colour   ][+pic][⚙]  | Folders                 [+folder]
    //   [      big swatch       ]   | [ Recent colours card          ]
    //   HEX / RGB / HSB / HSL /     | [ folder card                  ]
    //   CMYK rows (click to copy)   |   (or, inside a folder: [←] name [⋯]
    //   [ Save to folder ▾      ]   |    and a grid of its colours)
    sealed class MainWindow : Form
    {
        public const string WindowTitle = "Color Picker";
        const string HotkeyText = "Ctrl+Alt+C";
        const string RecentName = "Recent colours";

        readonly Store store;
        readonly HotkeyWindow hotkey;
        readonly FlatButton pickButton, imageButton, settingsButton, saveButton, backButton, newFolderButton, folderMenuButton;
        readonly Label header = new Label();
        ImageWindow imageWindow;
        readonly SwatchPanel swatch = new SwatchPanel();
        readonly List<ValueRow> rows = new List<ValueRow>();
        readonly Panel divider = new Panel();
        readonly FlowLayoutPanel tiles = new FlowLayoutPanel();    // colours of the open folder (wraps)
        readonly FlowLayoutPanel cardList = new FlowLayoutPanel(); // folder cards (one column)
        readonly Label emptyLabel = new Label();
        readonly Label hint = new Label();
        readonly ToolTip tips = new ToolTip();
        string folder;   // open folder; null = recent colours
        bool inFolder;   // false = the list of folder cards
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
            pickButton = new FlatButton { Text = "Pick colour", Primary = true, Spectrum = true, Bounds = new Rectangle(pad, y, leftW - Dpi.Px(112), Dpi.Px(44)), Anchor = fixedLeft };
            pickButton.Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold);
            pickButton.Click += delegate { StartPick(); };
            tips.SetToolTip(pickButton, "Or press " + HotkeyText + " from any app");
            imageButton = new FlatButton { Icon = ButtonIcon.AddPicture, Bounds = new Rectangle(pad + leftW - Dpi.Px(104), y, Dpi.Px(56), Dpi.Px(44)), Anchor = fixedLeft };
            imageButton.Click += delegate { OpenImage(null); };
            tips.SetToolTip(imageButton, "Open a picture to get its colours. You can also drop one on this window.");
            Controls.Add(imageButton);
            settingsButton = new FlatButton { Icon = ButtonIcon.Settings, Bounds = new Rectangle(pad + leftW - Dpi.Px(40), y, Dpi.Px(40), Dpi.Px(44)), Anchor = fixedLeft };
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

            saveButton = new FlatButton { Icon = ButtonIcon.SaveFolder, Text = "Save to folder  ▾", Bounds = new Rectangle(pad, y, leftW, Dpi.Px(36)), Anchor = fixedLeft };
            saveButton.Click += delegate
            {
                if (OpenFolder != null) SaveTo(OpenFolder); // inside a folder: one click adds to it
                else ShowSaveMenu();
            };
            Controls.Add(saveButton);

            // ---- divider ----
            divider.Bounds = new Rectangle(pad + leftW + gapX / 2, pad, 1, ClientSize.Height - 2 * pad);
            divider.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(divider);

            // ---- right column: folder cards, or the colours of one folder ----
            int headH = Dpi.Px(44); // same height as the Pick row, so both columns line up
            backButton = new FlatButton { Icon = ButtonIcon.Back, Bounds = new Rectangle(rightX, pad, Dpi.Px(44), headH), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            backButton.Click += delegate { inFolder = false; RefreshTiles(); };
            tips.SetToolTip(backButton, "Back to all folders");
            header.Font = new Font(Font.FontFamily, 11f, FontStyle.Bold);
            header.TextAlign = ContentAlignment.MiddleLeft;
            header.AutoEllipsis = true;
            header.Bounds = new Rectangle(rightX, pad, rightW - Dpi.Px(130), headH);
            header.Anchor = stretch;
            newFolderButton = new FlatButton { Icon = ButtonIcon.AddFolder, Bounds = new Rectangle(rightX + rightW - Dpi.Px(56), pad, Dpi.Px(56), headH), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            tips.SetToolTip(newFolderButton, "New folder");
            newFolderButton.Click += delegate { NewFolder(); };
            folderMenuButton = new FlatButton { Text = "⋯", Bounds = new Rectangle(rightX + rightW - Dpi.Px(44), pad, Dpi.Px(44), headH), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            folderMenuButton.Font = new Font(Font.FontFamily, 11f, FontStyle.Bold);
            folderMenuButton.Click += delegate { ShowFolderMenu(); };
            tips.SetToolTip(folderMenuButton, "Rename, duplicate or delete this folder");
            Controls.Add(backButton);
            Controls.Add(header);
            Controls.Add(newFolderButton);
            Controls.Add(folderMenuButton);

            int tilesY = pad + headH + Dpi.Px(12), hintH = Dpi.Px(20);
            tiles.Bounds = new Rectangle(rightX - Dpi.Px(3), tilesY, rightW + Dpi.Px(6), ClientSize.Height - tilesY - pad - hintH);
            tiles.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tiles.AutoScroll = true;
            tiles.HandleCreated += delegate { Theme.ApplyScrollbars(tiles); };
            Controls.Add(tiles);

            cardList.Bounds = tiles.Bounds;
            cardList.Anchor = tiles.Anchor;
            cardList.FlowDirection = FlowDirection.TopDown;
            cardList.WrapContents = false;
            // Vertical scrolling only.
            cardList.AutoScroll = false;
            cardList.HorizontalScroll.Maximum = 0;
            cardList.HorizontalScroll.Visible = false;
            cardList.AutoScroll = true;
            cardList.Resize += delegate { FitCards(); };
            cardList.HandleCreated += delegate { Theme.ApplyScrollbars(cardList); };
            Controls.Add(cardList);
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
            RefreshTiles();
        }

        // ---------- theme ----------

        void ApplyTheme()
        {
            var p = Theme.Current;
            BackColor = p.Back;
            ForeColor = p.Text;
            divider.BackColor = p.Line;
            tiles.BackColor = cardList.BackColor = p.Back;
            emptyLabel.ForeColor = p.Muted;
            hint.ForeColor = p.Muted;
            header.ForeColor = p.Text;
            Theme.ApplyScrollbars(tiles);
            Theme.ApplyScrollbars(cardList);
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
            // Inside a folder, picks go straight into it (once) as well as into Recent.
            if (OpenFolder != null && !store.FolderContains(OpenFolder, c))
            {
                try { store.AddToFolder(OpenFolder, c); }
                catch (Exception ex) { MessageBox.Show(this, "Couldn't save to " + OpenFolder + ": " + ex.Message, WindowTitle); }
            }
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
            UpdateSaveButton();
        }

        // The folder that's open, or null on the folder list and in Recent colours.
        string OpenFolder { get { return inFolder ? folder : null; } }

        // Folder list: "Save to folder ▾" (choose one). Inside a folder: "Add to <name>",
        // or "In <name> ✓" when the colour is already there.
        void UpdateSaveButton()
        {
            string open = OpenFolder;
            if (open == null)
            {
                saveButton.Text = "Save to folder  ▾";
                saveButton.Enabled = current != null;
            }
            else if (current != null && store.FolderContains(open, current.Value))
            {
                saveButton.Text = "In " + open + "  ✓";
                saveButton.Enabled = false;
            }
            else
            {
                saveButton.Text = "Add to " + open;
                saveButton.Enabled = current != null;
            }
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
                if (name != null && CreateFolder(name)) SaveTo(name);
            });
            Theme.FitMenuWidth(menu, saveButton.Width);
            menu.Show(saveButton, new Point(0, saveButton.Height));
        }

        void SaveTo(string name)
        {
            try
            {
                store.AddToFolder(name, current.Value);
                if (OpenFolder != null) { UpdateSaveButton(); return; }
                saveButton.Text = "Saved to " + name + "  ✓";
                var t = new Timer { Interval = 1500 };
                t.Tick += delegate { t.Dispose(); UpdateSaveButton(); };
                t.Start();
            }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't save: " + ex.Message, WindowTitle); }
        }

        void ShowFolderMenu() { ShowFolderMenu(folder, folderMenuButton, new Point(folderMenuButton.Width, folderMenuButton.Height), true); }

        void ShowFolderMenu(string name, Control anchor, Point at, bool alignRight)
        {
            if (name == null) return;
            var menu = NewMenu();
            menu.ShowCheckMargin = false;
            menu.Items.Add("Rename folder…", null, delegate
            {
                string newName = AskName("Rename folder", name);
                if (newName == null || newName == name) return;
                string was = folder;
                try { if (folder == name) folder = newName; store.RenameFolder(name, newName); }
                catch (Exception ex) { folder = was; MessageBox.Show(this, "Couldn't rename: " + ex.Message, WindowTitle); }
            });
            menu.Items.Add("Duplicate folder…", null, delegate { DuplicateFolder(name); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Delete folder…", null, delegate { DeleteFolder(name); });
            if (alignRight) menu.Show(anchor, at, ToolStripDropDownDirection.BelowLeft);
            else menu.Show(anchor, at);
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
            menu.Items.Add("Change where colours are saved…", null, delegate { ChooseStorage(); });
            menu.Items.Add("Show saved colours in File Explorer", null, delegate
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
            if (name != null) CreateFolder(name);
        }

        bool CreateFolder(string name)
        {
            try { store.CreateFolder(name); return true; }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't make the folder: " + ex.Message, WindowTitle); return false; }
        }

        // Copies a folder and its colours under a new name the person chooses.
        void DuplicateFolder(string name)
        {
            string suggestion = name + " copy";
            for (int i = 2; store.ValidateName(suggestion, null) != null && i < 100; i++) suggestion = name + " copy " + i;
            string newName = Prompt.Ask(this, "Duplicate folder", suggestion, n => store.ValidateName(n, null));
            if (newName == null) return;
            try { store.DuplicateFolder(name, newName); }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't duplicate: " + ex.Message, WindowTitle); }
        }

        void DeleteFolder(string name)
        {
            if (name == null) return;
            int n = store.ReadFolder(name).Count;
            var answer = MessageBox.Show(this, "Delete \"" + name + "\" and its " + n + (n == 1 ? " colour" : " colours") + "?\n\nYou can get it back from the Recycle Bin.",
                "Delete folder", MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.OK) return;
            try { store.DeleteFolder(name); }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't delete: " + ex.Message, WindowTitle); }
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
            var folders = store.GetFolders();
            if (folder != null && !folders.Contains(folder)) { folder = null; inFolder = false; }

            UpdateSaveButton();
            backButton.Visible = inFolder;
            folderMenuButton.Visible = inFolder && folder != null;
            newFolderButton.Visible = !inFolder;
            int left = tiles.Left + Dpi.Px(3);
            header.Left = inFolder ? backButton.Right + Dpi.Px(10) : left;
            header.Width = (inFolder ? folderMenuButton.Left : newFolderButton.Left) - Dpi.Px(8) - header.Left;
            header.Text = inFolder ? (folder ?? RecentName) : "Folders";

            tiles.Visible = inFolder;
            cardList.Visible = !inFolder;
            if (!inFolder) { ShowCards(folders); return; }

            IList<Color> colors = folder != null ? (IList<Color>)store.ReadFolder(folder) : store.History;
            string shownFolder = folder;

            tiles.SuspendLayout();
            foreach (Control c in tiles.Controls) if (c != emptyLabel) { tips.SetToolTip(c, null); c.Dispose(); }
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
                        menu.Items.Add(shownFolder != null ? "Remove from " + shownFolder : "Remove from Recent colours", null, delegate
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

        // One card per folder (Recent colours first): a strip of its 5 newest colours and its name.
        void ShowCards(List<string> folders)
        {
            cardList.SuspendLayout();
            foreach (Control c in cardList.Controls) if (c != emptyLabel) { tips.SetToolTip(c, null); c.Dispose(); }
            cardList.Controls.Clear();

            var recent = new List<Color>(store.History);
            AddCard(RecentName, null, recent.GetRange(0, Math.Min(5, recent.Count)), recent.Count);
            foreach (var name in folders)
            {
                var colors = store.ReadFolder(name);
                colors.Reverse(); // newest last in the file
                AddCard(name, name, colors.GetRange(0, Math.Min(5, colors.Count)), colors.Count);
            }
            if (folders.Count == 0)
            {
                emptyLabel.Text = "Make a folder to keep colours together. Pick a colour, then use Save to folder.";
                cardList.Controls.Add(emptyLabel);
            }
            cardList.ResumeLayout();
            FitCards();
        }

        void AddCard(string title, string name, List<Color> newest, int count)
        {
            var card = new FolderCard(title, newest, count) { Height = Dpi.Px(72), Margin = new Padding(Dpi.Px(3), Dpi.Px(3), Dpi.Px(3), Dpi.Px(5)) };
            card.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) { folder = name; inFolder = true; RefreshTiles(); }
                else if (e.Button == MouseButtons.Right && name != null) ShowFolderMenu(name, card, e.Location, false);
            };
            tips.SetToolTip(card, name == null ? "Your last " + Store.HistoryMax + " picks" : "Open " + name + "  ·  right-click to rename, duplicate or delete");
            cardList.Controls.Add(card);
        }

        // Cards fill the width of the list (minus the scrollbar when there is one).
        void FitCards()
        {
            int content = 0;
            foreach (Control c in cardList.Controls) content += c.Height + c.Margin.Vertical;
            int w = cardList.Width - Dpi.Px(8) - (content > cardList.Height ? SystemInformation.VerticalScrollBarWidth : 0);
            foreach (Control c in cardList.Controls) if (c is FolderCard && c.Width != w) c.Width = w;
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

    // A folder in the list: strip of its newest colours (empty slots stay blank), name and count.
    sealed class FolderCard : Control
    {
        readonly string title;
        readonly List<Color> colors;
        readonly int count;
        bool hover;

        public FolderCard(string title, List<Color> newest, int count)
        {
            this.title = title;
            colors = newest;
            this.count = count;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Theme.Current;
            var g = e.Graphics;
            g.Clear(p.Back);
            Theme.FillRound(g, hover ? p.Hover : p.Surface, Theme.Inner(ClientRectangle), Dpi.Px(8));

            int pad = Dpi.Px(10);
            var strip = new Rectangle(pad, pad, Width - 2 * pad, Dpi.Px(28));
            Theme.FillRound(g, p.Back, strip, Dpi.Px(6));
            using (var path = Theme.RoundRect(strip, Dpi.Px(6)))
            {
                var oldClip = g.Clip;
                g.SetClip(path);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                for (int i = 0; i < colors.Count && i < 5; i++)
                {
                    int x0 = strip.X + strip.Width * i / 5, x1 = strip.X + strip.Width * (i + 1) / 5;
                    using (var b = new SolidBrush(colors[i])) g.FillRectangle(b, x0, strip.Y, x1 - x0, strip.Height + 1);
                }
                g.Clip = oldClip;
            }
            Theme.DrawRound(g, p.Line, 1, strip, Dpi.Px(6));

            var text = new Rectangle(pad + Dpi.Px(2), strip.Bottom, Width - 2 * pad - Dpi.Px(4), Height - strip.Bottom - Dpi.Px(2));
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(g, title, Font, new Rectangle(text.X, text.Y, text.Width - Dpi.Px(80), text.Height), p.Text, flags | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, count == 1 ? "1 colour" : count + " colours", Font, text, p.Muted, flags | TextFormatFlags.Right);
        }
    }

    // Flat button painted with the current theme.
    enum ButtonIcon { None, AddPicture, AddFolder, Settings, SaveFolder, OpenPicture, Back }

    sealed class FlatButton : Control
    {
        bool hover, down;
        public bool Primary, Spectrum;
        public ButtonIcon Icon;

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
            if (Spectrum && Enabled && p.Spectrum != null && p.Spectrum.Length > 1)
                using (var path = Theme.RoundRect(Theme.Inner(ClientRectangle), Dpi.Px(6)))
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle, p.Spectrum[0], p.Spectrum[p.Spectrum.Length - 1], 0f))
                {
                    var blend = new System.Drawing.Drawing2D.ColorBlend(p.Spectrum.Length);
                    blend.Colors = p.Spectrum;
                    var pos = new float[p.Spectrum.Length];
                    for (int i = 0; i < pos.Length; i++) pos[i] = (float)i / (pos.Length - 1);
                    blend.Positions = pos;
                    brush.InterpolationColors = blend;
                    var oldClip = e.Graphics.Clip;
                    e.Graphics.SetClip(path);
                    e.Graphics.FillRectangle(brush, 0, Height - Dpi.Px(4), Width, Dpi.Px(4));
                    e.Graphics.Clip = oldClip;
                }
            if (Icon != ButtonIcon.None) { DrawIcon(e.Graphics, fore); return; }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter);
        }

        // Line icons stroked with the brand rainbow (or the text colour when there is none):
        // "+ picture", "+ folder", the settings gear, and a "save into folder" icon shown before the text.
        void DrawIcon(Graphics g, Color fore)
        {
            var p = Theme.Current;
            float stroke = Math.Max(1.5f, Dpi.Scale * 1.8f);
            float plus = Dpi.Px(9), gap = Dpi.Px(5), iw = Dpi.Px(20), ih = Dpi.Px(16);
            float cy = Height / 2f;

            // Work out the whole group's width so it can be centred.
            bool hasPlus = Icon == ButtonIcon.AddPicture || Icon == ButtonIcon.AddFolder;
            bool hasText = (Icon == ButtonIcon.SaveFolder || Icon == ButtonIcon.OpenPicture) && !string.IsNullOrEmpty(Text);
            if (Icon == ButtonIcon.Settings) { iw = ih = Dpi.Px(20); }
            if (Icon == ButtonIcon.SaveFolder || Icon == ButtonIcon.OpenPicture) { iw = Dpi.Px(18); ih = Dpi.Px(15); }
            if (Icon == ButtonIcon.Back) { iw = Dpi.Px(16); ih = Dpi.Px(14); }
            int textW = hasText ? TextRenderer.MeasureText(Text, Font).Width : 0;
            float groupW = (hasPlus ? plus + gap : 0) + iw + (hasText ? Dpi.Px(8) + textW : 0);
            float x = Math.Max(Dpi.Px(6), (Width - groupW) / 2f);
            var bounds = new RectangleF(x, cy - ih / 2f, (hasPlus ? plus + gap : 0) + iw, ih);

            Brush brush;
            if (Enabled && p.Spectrum != null && p.Spectrum.Length > 1)
            {
                var lg = new LinearGradientBrush(new RectangleF(bounds.X - 1, bounds.Y - 1, bounds.Width + 2, bounds.Height + 2), p.Spectrum[0], p.Spectrum[p.Spectrum.Length - 1], 20f);
                var blend = new ColorBlend(p.Spectrum.Length) { Colors = p.Spectrum };
                var pos = new float[p.Spectrum.Length];
                for (int i = 0; i < pos.Length; i++) pos[i] = (float)i / (pos.Length - 1);
                blend.Positions = pos;
                lg.InterpolationColors = blend;
                brush = lg;
            }
            else brush = new SolidBrush(fore);

            var oldMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (brush)
            using (var pen = new Pen(brush, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (hasPlus)
                {
                    g.DrawLine(pen, x, cy, x + plus, cy);
                    g.DrawLine(pen, x + plus / 2f, cy - plus / 2f, x + plus / 2f, cy + plus / 2f);
                    x += plus + gap;
                }
                var r = new RectangleF(x, cy - ih / 2f, iw, ih);

                if (Icon == ButtonIcon.AddPicture || Icon == ButtonIcon.OpenPicture)
                {
                    using (var frame = RoundedRectF(r, Dpi.Px(3))) g.DrawPath(pen, frame);
                    g.DrawLines(pen, new[]
                    {
                        new PointF(r.X + r.Width * 0.12f, r.Bottom - r.Height * 0.18f),
                        new PointF(r.X + r.Width * 0.40f, r.Y + r.Height * 0.45f),
                        new PointF(r.X + r.Width * 0.60f, r.Y + r.Height * 0.68f),
                        new PointF(r.X + r.Width * 0.72f, r.Y + r.Height * 0.55f),
                        new PointF(r.X + r.Width * 0.88f, r.Bottom - r.Height * 0.18f)
                    });
                    float sun = r.Height * 0.22f;
                    g.DrawEllipse(pen, r.X + r.Width * 0.66f, r.Y + r.Height * 0.18f, sun, sun);
                }
                else if (Icon == ButtonIcon.AddFolder || Icon == ButtonIcon.SaveFolder)
                {
                    float tabW = r.Width * 0.42f, tabH = r.Height * 0.2f;
                    using (var path = new GraphicsPath())
                    {
                        path.AddLines(new[]
                        {
                            new PointF(r.X, r.Bottom), new PointF(r.X, r.Y), new PointF(r.X + tabW, r.Y),
                            new PointF(r.X + tabW + tabH, r.Y + tabH), new PointF(r.Right, r.Y + tabH), new PointF(r.Right, r.Bottom)
                        });
                        path.CloseFigure();
                        g.DrawPath(pen, path);
                    }
                    if (Icon == ButtonIcon.AddFolder)
                        g.DrawLine(pen, r.X, r.Y + tabH * 2f, r.Right, r.Y + tabH * 2f);
                    else
                    {
                        // arrow going into the folder
                        float ax = r.X + r.Width / 2f, top = r.Y + tabH * 1.9f, bottom = r.Bottom - r.Height * 0.2f, head = r.Height * 0.22f;
                        g.DrawLine(pen, ax, top, ax, bottom);
                        g.DrawLines(pen, new[] { new PointF(ax - head, bottom - head), new PointF(ax, bottom), new PointF(ax + head, bottom - head) });
                    }
                }
                else if (Icon == ButtonIcon.Back)
                {
                    float head = r.Height / 2f;
                    g.DrawLine(pen, r.X, cy, r.Right, cy);
                    g.DrawLines(pen, new[] { new PointF(r.X + head, cy - head), new PointF(r.X, cy), new PointF(r.X + head, cy + head) });
                }
                else if (Icon == ButtonIcon.Settings)
                {
                    // gear: 8 teeth around a ring, with a hole in the middle
                    float c0x = r.X + r.Width / 2f, c0y = r.Y + r.Height / 2f;
                    float outer = r.Width / 2f, inner = outer * 0.74f;
                    var pts = new List<PointF>();
                    for (int t = 0; t < 8; t++)
                    {
                        double a = t * Math.PI / 4;
                        double w = Math.PI / 4 * 0.28; // half width of a tooth
                        foreach (var step in new[] { -1.6 * w, -w, w, 1.6 * w })
                        {
                            float rad = Math.Abs(step) > w * 1.1 ? inner : outer;
                            pts.Add(new PointF(c0x + rad * (float)Math.Cos(a + step), c0y + rad * (float)Math.Sin(a + step)));
                        }
                    }
                    using (var path = new GraphicsPath())
                    {
                        path.AddPolygon(pts.ToArray());
                        g.DrawPath(pen, path);
                    }
                    float hole = outer * 0.36f;
                    g.DrawEllipse(pen, c0x - hole, c0y - hole, hole * 2, hole * 2);
                }

                if (hasText)
                    TextRenderer.DrawText(g, Text, Font, new Rectangle((int)(r.Right + Dpi.Px(8)), 0, Width - (int)(r.Right + Dpi.Px(8)) - Dpi.Px(4), Height), fore,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
            g.SmoothingMode = oldMode;
        }

        static GraphicsPath RoundedRectF(RectangleF r, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
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
