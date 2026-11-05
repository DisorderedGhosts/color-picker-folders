using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ColorPicker
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Crash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Crash(e.ExceptionObject as Exception);

            bool first;
            using (var mutex = new Mutex(true, "DisorderedGhosts.ColorPicker", out first))
            {
                if (!first)
                {
                    // Already open: bring that window to the front instead of starting a second copy.
                    Native.PostMessage(Native.HWND_BROADCAST, Native.WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                Native.EnableDpiAwareness(); // must run before any window exists
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try { Application.Run(new MainWindow(Store.Load())); }
                catch (Exception ex) { Crash(ex); }
                GC.KeepAlive(mutex);
            }
        }

        // Show the error instead of vanishing silently, and keep a copy in crash.log next to the .exe.
        static void Crash(Exception ex)
        {
            string text = ex == null ? "Unknown error" : ex.ToString();
            string log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorPicker", "crash.log");
            try { Directory.CreateDirectory(Path.GetDirectoryName(log)); File.AppendAllText(log, DateTime.Now.ToString("s") + Environment.NewLine + text + Environment.NewLine + Environment.NewLine); }
            catch (Exception) { }
            MessageBox.Show("Color Picker hit an error:\n\n" + text + "\n\nA copy is in " + log, "Color Picker",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
