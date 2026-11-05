using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ColorPicker
{
    static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int RegisterWindowMessage(string name);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
        // Sent by a second copy of the app so the running one comes to the front.
        public static readonly int WM_SHOWME = RegisterWindowMessage("DisorderedGhosts.ColorPicker.ShowMe");

        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

        public static void SendToRecycleBin(string path)
        {
            const uint FO_DELETE = 3;
            const ushort FOF_SILENT = 0x4, FOF_NOCONFIRMATION = 0x10, FOF_ALLOWUNDO = 0x40;
            var op = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = path + "\0\0", // double-null-terminated list
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
            };
            int result = SHFileOperation(ref op);
            if (result != 0) throw new System.IO.IOException("Windows refused to delete the file (code " + result + ").");
        }

        // Per-monitor DPI awareness so screen pixels map 1:1 to what we capture and pick.
        public static void EnableDpiAwareness()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } // PER_MONITOR_AWARE_V2 (Win10 1703+)
            catch (EntryPointNotFoundException) { }
            try { SetProcessDPIAware(); } catch (EntryPointNotFoundException) { }
        }
    }

    // Pixel scaling for hand-laid-out UI.
    static class Dpi
    {
        public static readonly float Scale = GetScale();

        static float GetScale()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f;
        }

        public static int Px(int v) { return (int)Math.Round(v * Scale); }
        public static Size Px(int w, int h) { return new Size(Px(w), Px(h)); }
        public static Padding Pad(int v) { return new Padding(Px(v)); }
    }

    // Hidden message-only window that receives the global hotkey.
    sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        const int Id = 1;
        readonly Action onHotkey;
        bool registered;

        public HotkeyWindow(Action onHotkey)
        {
            this.onHotkey = onHotkey;
            var cp = new CreateParams();
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE
            CreateHandle(cp);
        }

        public bool Register(uint mods, Keys key)
        {
            if (registered) Native.UnregisterHotKey(Handle, Id);
            registered = Native.RegisterHotKey(Handle, Id, mods | Native.MOD_NOREPEAT, (uint)key);
            return registered;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY) onHotkey();
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (registered) Native.UnregisterHotKey(Handle, Id);
            registered = false;
            DestroyHandle();
        }
    }

    static class Clip
    {
        public static bool Copy(string text)
        {
            try { Clipboard.SetDataObject(text, true, 10, 25); return true; } // retries if another app holds the clipboard
            catch (ExternalException) { return false; }
        }
    }
}
