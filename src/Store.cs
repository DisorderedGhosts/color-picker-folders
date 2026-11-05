using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorPicker
{
    // The .exe can live anywhere (often Downloads), so nothing is kept next to it:
    //   settings  -> %APPDATA%\ColorPicker\settings.ini
    //   folders   -> Documents\Color Picker\<name>.colors (changeable in settings)
    // Each folder file is plain text with one HEX value per line.
    sealed class Store
    {
        public const int HistoryMax = 20;
        const string Ext = ".colors";

        public event Action Changed;

        readonly string settingsPath;
        ColorFormat format = ColorFormat.Hex;
        bool onTop;
        bool dark = true;
        string storageDir;
        bool customStorage; // false = the default Documents\Color Picker, so it follows the user's Documents folder
        readonly List<Color> history = new List<Color>();

        Store(string home)
        {
            settingsPath = Path.Combine(home, "settings.ini");
            storageDir = DefaultStorage;
        }

        static string DefaultStorage
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Color Picker"); }
        }

        public static Store Load()
        {
            string home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorPicker");
            Directory.CreateDirectory(home);
            var s = new Store(home);
            MoveOldData(s.settingsPath);
            string[] lines;
            try { lines = File.Exists(s.settingsPath) ? File.ReadAllLines(s.settingsPath) : null; }
            catch (Exception) { lines = null; } // unreadable settings: start with the defaults
            if (lines != null)
            {
                foreach (var line in lines)
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant(), val = line.Substring(eq + 1).Trim();
                    if (key == "format") Enum.TryParse(val, true, out s.format);
                    else if (key == "ontop") s.onTop = val == "1";
                    else if (key == "theme") s.dark = val != "light";
                    else if (key == "storage" && val.Length > 0) { s.storageDir = val; s.customStorage = true; }
                    else if (key == "history")
                        foreach (var h in val.Split(','))
                        {
                            Color c;
                            if (ColorFormats.TryParseHex(h, out c) && s.history.Count < HistoryMax) s.history.Add(c);
                        }
                }
            }
            return s;
        }

        // Earlier builds kept settings.ini and a Folders dir next to the .exe. Move them once.
        static void MoveOldData(string newSettings)
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string oldSettings = Path.Combine(exeDir, "settings.ini"), oldFolders = Path.Combine(exeDir, "Folders");
                if (File.Exists(oldSettings))
                {
                    if (!File.Exists(newSettings)) File.Copy(oldSettings, newSettings);
                    File.Delete(oldSettings);
                }
                if (Directory.Exists(oldFolders))
                {
                    Directory.CreateDirectory(DefaultStorage);
                    foreach (var f in Directory.GetFiles(oldFolders, "*" + Ext))
                    {
                        string dst = Path.Combine(DefaultStorage, Path.GetFileName(f));
                        if (!File.Exists(dst)) File.Move(f, dst);
                    }
                    if (Directory.GetFileSystemEntries(oldFolders).Length == 0) Directory.Delete(oldFolders);
                }
            }
            catch (Exception) { } // best effort; never block startup
        }

        void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("format=" + format);
            sb.AppendLine("ontop=" + (onTop ? "1" : "0"));
            sb.AppendLine("theme=" + (dark ? "dark" : "light"));
            sb.AppendLine("storage=" + (customStorage ? storageDir : ""));
            sb.AppendLine("history=" + string.Join(",", history.Select(x => ColorFormats.Hex(x))));
            try { File.WriteAllText(settingsPath, sb.ToString()); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        void Notify()
        {
            var h = Changed;
            if (h != null) h();
        }

        // ---- settings ----

        public ColorFormat Format
        {
            get { return format; }
            set { format = value; Save(); }
        }

        public bool Dark
        {
            get { return dark; }
            set { dark = value; Save(); }
        }

        public bool OnTop
        {
            get { return onTop; }
            set { onTop = value; Save(); }
        }

        public string StorageDir { get { return storageDir; } }

        // Changes where folders are kept and brings the existing folders along.
        public void SetStorageDir(string dir)
        {
            if (string.Equals(Path.GetFullPath(dir).TrimEnd('\\'), Path.GetFullPath(storageDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
            Directory.CreateDirectory(dir);
            foreach (var name in GetFolders())
            {
                string dst = Path.Combine(dir, name + Ext);
                if (!File.Exists(dst)) File.Move(PathOf(name), dst);
            }
            storageDir = dir;
            customStorage = true;
            Save();
            Notify();
        }

        // ---- history (last 20 picks, newest first) ----

        public IList<Color> History { get { return history.AsReadOnly(); } }

        public void AddHistory(Color c)
        {
            history.RemoveAll(x => x.ToArgb() == c.ToArgb());
            history.Insert(0, c);
            if (history.Count > HistoryMax) history.RemoveRange(HistoryMax, history.Count - HistoryMax);
            Save();
            Notify();
        }

        public void RemoveHistory(int index)
        {
            if (index < 0 || index >= history.Count) return;
            history.RemoveAt(index);
            Save();
            Notify();
        }

        // ---- folders ----

        string PathOf(string name) { return Path.Combine(storageDir, name + Ext); }

        public List<string> GetFolders()
        {
            List<string> names;
            try
            {
                if (!Directory.Exists(storageDir)) return new List<string>();
                names = Directory.GetFiles(storageDir, "*" + Ext).Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
            }
            catch (IOException) { return new List<string>(); }               // e.g. a removed USB drive
            catch (UnauthorizedAccessException) { return new List<string>(); }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public List<Color> ReadFolder(string name)
        {
            var list = new List<Color>();
            string p = PathOf(name);
            string[] lines;
            try { lines = File.Exists(p) ? File.ReadAllLines(p) : new string[0]; }
            catch (IOException) { return list; }  // e.g. open in another app right now
            catch (UnauthorizedAccessException) { return list; }
            foreach (var line in lines)
            {
                Color c;
                if (ColorFormats.TryParseHex(line, out c)) list.Add(c);
            }
            return list;
        }

        // Returns an error message, or null when the name is usable.
        public string ValidateName(string name, string ignore)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Enter a name.";
            if (name.Length > 60) return "Keep the name under 60 characters.";
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(".")) return "Names can't contain \\ / : * ? \" < > |";
            bool taken = GetFolders().Any(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase)
                                              && !string.Equals(f, ignore, StringComparison.OrdinalIgnoreCase));
            return taken ? "A folder with that name already exists." : null;
        }

        public void CreateFolder(string name)
        {
            Directory.CreateDirectory(storageDir);
            if (!File.Exists(PathOf(name))) File.WriteAllText(PathOf(name), "");
            Notify();
        }

        public void RenameFolder(string oldName, string newName)
        {
            if (oldName == newName) return;
            string src = PathOf(oldName), dst = PathOf(newName);
            if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            {
                string tmp = src + ".tmp"; // case-only rename on a case-insensitive file system
                File.Move(src, tmp);
                File.Move(tmp, dst);
            }
            else File.Move(src, dst);
            Notify();
        }

        public void DuplicateFolder(string name, string newName)
        {
            File.Copy(PathOf(name), PathOf(newName));
            Notify();
        }

        // Sends the folder's file to the Recycle Bin, so a mistake can be undone.
        public void DeleteFolder(string name)
        {
            string p = PathOf(name);
            if (File.Exists(p))
                Native.SendToRecycleBin(p);
            Notify();
        }

        public void AddToFolder(string name, Color c)
        {
            Append(name, new[] { ColorFormats.Hex(c) });
            Notify();
        }

        // Adds lines to the end of a folder file. If someone edited the file by hand and left
        // no line break at the end, adds one first so two colours never end up on one line.
        void Append(string name, IEnumerable<string> lines)
        {
            Directory.CreateDirectory(storageDir);
            string p = PathOf(name);
            var sb = new StringBuilder();
            if (File.Exists(p))
                using (var fs = File.OpenRead(p))
                    if (fs.Length > 0) { fs.Seek(-1, SeekOrigin.End); if (fs.ReadByte() != '\n') sb.AppendLine(); }
            foreach (var line in lines) sb.AppendLine(line);
            File.AppendAllText(p, sb.ToString());
        }

        public bool FolderContains(string name, Color c)
        {
            int argb = Color.FromArgb(c.R, c.G, c.B).ToArgb();
            foreach (var x in ReadFolder(name)) if (x.ToArgb() == argb) return true;
            return false;
        }

        public void AddAllToFolder(string name, IEnumerable<Color> colors)
        {
            Append(name, colors.Select(x => ColorFormats.Hex(x)));
            Notify();
        }

        public void RemoveFromFolder(string name, int index)
        {
            var colors = ReadFolder(name);
            if (index < 0 || index >= colors.Count) return;
            colors.RemoveAt(index);
            File.WriteAllLines(PathOf(name), colors.Select(x => ColorFormats.Hex(x)));
            Notify();
        }
    }
}
