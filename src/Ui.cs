using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ColorPicker
{
    static class Icons
    {
        static Icon app;
        public static Icon App { get { return app ?? (app = Load() ?? Make(Dpi.Px(32))); } }

        // The real icon (assets\app.ico) is embedded by build.bat; it holds every size Windows needs.
        static Icon Load()
        {
            var stream = typeof(Icons).Assembly.GetManifestResourceStream("ColorPicker.app.ico");
            if (stream == null) return null;
            using (stream) return new Icon(stream);
        }

        // Fallback only (if the embedded icon is missing): a four-colour ring drawn in code.
        static Icon Make(int size)
        {
            using (var bmp = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Color[] cols = { Color.FromArgb(255, 90, 95), Color.FromArgb(255, 193, 69), Color.FromArgb(61, 220, 151), Color.FromArgb(74, 144, 226) };
                    float pad = Math.Max(1f, size / 16f), d = size - 2 * pad;
                    for (int i = 0; i < 4; i++)
                        using (var b = new SolidBrush(cols[i])) g.FillPie(b, pad, pad, d, d, -90 + i * 90, 90);
                    g.CompositingMode = CompositingMode.SourceCopy;
                    float hole = d * 0.38f;
                    using (var b = new SolidBrush(Color.Transparent)) g.FillEllipse(b, (size - hole) / 2, (size - hole) / 2, hole, hole);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }

    static class Prompt
    {
        // Asks for a folder name. validate returns an error message or null.
        public static string Ask(IWin32Window owner, string title, string initial, Func<string, string> validate)
        {
            var pal = Theme.Current;
            using (var f = new Form())
            {
                f.Text = title;
                f.Font = new Font("Segoe UI", 9.5f);
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MinimizeBox = f.MaximizeBox = false;
                f.ShowInTaskbar = false;
                f.KeyPreview = true;
                f.BackColor = pal.Back;
                f.ForeColor = pal.Text;
                f.ClientSize = Dpi.Px(320, 128);
                f.HandleCreated += delegate { Theme.ApplyTitleBar(f); };

                // Rounded field: a painted panel with a borderless text box inside.
                var field = new RoundPanel { Bounds = new Rectangle(Dpi.Px(16), Dpi.Px(16), Dpi.Px(288), Dpi.Px(34)) };
                var box = new TextBox
                {
                    Text = initial ?? "",
                    BorderStyle = BorderStyle.None,
                    BackColor = pal.Surface,
                    ForeColor = pal.Text,
                    Bounds = new Rectangle(Dpi.Px(10), Dpi.Px(8), Dpi.Px(268), Dpi.Px(20))
                };
                field.Controls.Add(box);
                var err = new Label { ForeColor = Color.FromArgb(239, 68, 68), Bounds = new Rectangle(Dpi.Px(16), Dpi.Px(54), Dpi.Px(288), Dpi.Px(20)) };
                var ok = new FlatButton { Text = "OK", Primary = true, Bounds = new Rectangle(Dpi.Px(136), Dpi.Px(82), Dpi.Px(80), Dpi.Px(32)) };
                var cancel = new FlatButton { Text = "Cancel", Bounds = new Rectangle(Dpi.Px(224), Dpi.Px(82), Dpi.Px(80), Dpi.Px(32)) };

                Action submit = () =>
                {
                    string problem = validate(box.Text.Trim());
                    if (problem == null) { f.DialogResult = DialogResult.OK; return; }
                    err.Text = problem;
                    box.Focus();
                };
                ok.Click += delegate { submit(); };
                cancel.Click += delegate { f.DialogResult = DialogResult.Cancel; };
                f.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; submit(); }
                    else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; f.DialogResult = DialogResult.Cancel; }
                };
                f.Controls.AddRange(new Control[] { field, err, ok, cancel });
                f.Shown += delegate { box.SelectAll(); box.Focus(); };
                return f.ShowDialog(owner) == DialogResult.OK ? box.Text.Trim() : null;
            }
        }
    }

    // Panel with rounded Surface background, used behind text boxes.
    sealed class RoundPanel : Panel
    {
        public RoundPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Theme.Current;
            e.Graphics.Clear(p.Back);
            Theme.FillRound(e.Graphics, p.Surface, Theme.Inner(ClientRectangle), Dpi.Px(6));
            Theme.DrawRound(e.Graphics, ContainsFocus ? p.Accent : p.Line, 1, Theme.Inner(ClientRectangle), Dpi.Px(6));
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            e.Control.GotFocus += delegate { Invalidate(); };
            e.Control.LostFocus += delegate { Invalidate(); };
        }
    }
}
