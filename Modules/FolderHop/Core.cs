#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace EousGate.FolderHop
{
    [DataContract]
    public sealed class FolderEntry
    {
        [DataMember] public string Path;
        [DataMember] public long VisitedUtc;
        [DataMember] public bool Favorite;
        public string Name { get { string s = Path.TrimEnd('\\'); string n = System.IO.Path.GetFileName(s); return String.IsNullOrEmpty(n) ? Path : n; } }
        public override string ToString() { return Name + " — " + Path; }
    }

    [DataContract]
    public sealed class AppData
    {
        [DataMember] public int Version = 1;
        [DataMember] public bool Paused;
        [DataMember] public bool ShowDialogButton = true;
        [DataMember] public List<FolderEntry> Folders = new List<FolderEntry>();
    }

    public sealed class HistoryStore
    {
        public readonly string DirectoryPath;
        public AppData Data = new AppData();
        public string Warning = "";
        public bool Dirty;
        bool discardBackup;
        public HistoryStore(string directory)
        {
            DirectoryPath = System.IO.Path.GetFullPath(directory);
            string file = System.IO.Path.Combine(DirectoryPath, "history.json");
            if (File.Exists(file))
            {
                try { Data = Read(file); }
                catch
                {
                    try { Data = Read(file + ".bak"); Warning = "历史文件损坏，已从备份恢复。"; }
                    catch { Warning = "历史文件无法读取。原文件保留，新的记录暂存于内存；请先备份 Data 文件夹。"; WriteBlocked = true; }
                }
            }
            if (Data.Folders == null) Data.Folders = new List<FolderEntry>();
            Data.Folders = Data.Folders.Where(x => x != null && Normalize(x.Path) != null)
                .GroupBy(x => Normalize(x.Path), StringComparer.OrdinalIgnoreCase)
                .Select(g => new FolderEntry { Path = Normalize(g.First().Path), VisitedUtc = g.Max(x => x.VisitedUtc), Favorite = g.Any(x => x.Favorite) }).ToList();
        }
        public bool WriteBlocked;
        internal static AppData Read(string file)
        {
            using (var stream = File.OpenRead(file))
            {
                var data = (AppData)new DataContractJsonSerializer(typeof(AppData)).ReadObject(stream);
                if (data == null || data.Version != 1) throw new InvalidDataException("Unsupported or empty history format.");
                return data;
            }
        }
        public static string Normalize(string path)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(path) || path.IndexOfAny(new char[] { '\r', '\n', '\0' }) >= 0) return null;
                // Only actual filesystem paths; never shell namespace commands or relative paths.
                if (!(path.StartsWith(@"\\", StringComparison.Ordinal) || (path.Length >= 3 && Char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/')))) return null;
                string full = System.IO.Path.GetFullPath(path);
                string root = System.IO.Path.GetPathRoot(full);
                if (String.Equals(full.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    return root.EndsWith("\\", StringComparison.Ordinal) ? root : root + "\\";
                return full.Length > root.Length ? full.TrimEnd('\\') : full;
            }
            catch { return null; }
        }
        public FolderEntry Touch(string path)
        {
            path = Normalize(path);
            if (path == null) return null;
            FolderEntry e = Data.Folders.FirstOrDefault(x => String.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
            if (e == null) { e = new FolderEntry { Path = path }; Data.Folders.Add(e); }
            e.VisitedUtc = DateTime.UtcNow.Ticks;
            Trim(); Dirty = true; return e;
        }
        public void Trim()
        {
            var remove = Data.Folders.Where(x => !x.Favorite).OrderByDescending(x => x.VisitedUtc).Skip(200).ToList();
            foreach (var e in remove) Data.Folders.Remove(e);
        }
        public List<FolderEntry> Query(int tab, string query, IEnumerable<string> current)
        {
            IEnumerable<FolderEntry> rows = Data.Folders;
            if (tab == 1) rows = rows.Where(x => x.Favorite);
            if (tab == 2) rows = current.Distinct(StringComparer.OrdinalIgnoreCase).Select(p =>
                Data.Folders.FirstOrDefault(x => String.Equals(x.Path, p, StringComparison.OrdinalIgnoreCase)) ?? new FolderEntry { Path = p });
            string[] terms = (query ?? "").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return rows.Where(x => terms.All(t => x.Path.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderByDescending(x => x.VisitedUtc).ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToList();
        }
        public void ToggleFavorite(FolderEntry row)
        {
            var e = Data.Folders.FirstOrDefault(x => String.Equals(x.Path, row.Path, StringComparison.OrdinalIgnoreCase));
            if (e == null) { e = Touch(row.Path); }
            e.Favorite = !e.Favorite; Dirty = true;
        }
        public void ClearRecent() { Data.Folders.RemoveAll(x => !x.Favorite); Dirty = true; discardBackup = true; }
        public int Import(string file)
        {
            if (new[] {"history.json", "history.json.bak", "history.json.tmp"}.Any(name => String.Equals(Path.GetFullPath(file), Path.Combine(DirectoryPath,name), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("请选择旧版历史文件；当前历史无需导入。");
            if (WriteBlocked) throw new InvalidOperationException("当前历史文件损坏，请先备份和修复后再导入。");
            var incoming = Read(file); // Validate the complete source before modifying our in-memory data.
            var merged = Data.Folders.Select(x => new FolderEntry { Path=x.Path, VisitedUtc=x.VisitedUtc, Favorite=x.Favorite })
                .Concat(incoming.Folders ?? new List<FolderEntry>()).Where(x => x != null && Normalize(x.Path) != null)
                .GroupBy(x => Normalize(x.Path), StringComparer.OrdinalIgnoreCase)
                .Select(g => new FolderEntry { Path=g.Key, VisitedUtc=g.Max(x => x.VisitedUtc), Favorite=g.Any(x => x.Favorite) }).ToList();
            var previous=Data.Folders; Data.Folders=merged; Trim(); Dirty=true;
            if (!Save()) { Data.Folders=previous; Dirty=false; throw new IOException(Warning); }
            return (incoming.Folders ?? new List<FolderEntry>()).Count(x => x != null && Normalize(x.Path) != null);
        }
        public void Remove(FolderEntry row) { Data.Folders.RemoveAll(x => String.Equals(x.Path, row.Path, StringComparison.OrdinalIgnoreCase)); Dirty = true; discardBackup = true; }
        public bool Save()
        {
            if (!Dirty) return true;
            if (WriteBlocked) return false;
            string temp = null;
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                string file = System.IO.Path.Combine(DirectoryPath, "history.json");
                temp = file + ".tmp";
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                { new DataContractJsonSerializer(typeof(AppData)).WriteObject(stream, Data); stream.Flush(true); }
                if (File.Exists(file)) File.Replace(temp, file, file + ".bak", true);
                else File.Move(temp, file);
                if (discardBackup && File.Exists(file + ".bak")) File.Delete(file + ".bak");
                discardBackup = false;
                Dirty = false; Warning = ""; return true;
            }
            catch (Exception ex) { Warning = "无法保存历史：" + ex.Message; return false; }
        }
    }

    public static class ExplorerReader
    {
        static readonly Guid ShellWindows = new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
        public static void Release(object obj) { if (obj != null && Marshal.IsComObject(obj)) Marshal.ReleaseComObject(obj); }
        public static List<string> Paths(IntPtr onlyWindow)
        {
            var result = new List<string>();
            object windows = null;
            try
            {
                windows = Activator.CreateInstance(Type.GetTypeFromCLSID(ShellWindows));
                dynamic list = windows;
                int count = list.Count;
                for (int i = 0; i < count; i++)
                {
                    object browser = null, document = null, folder = null, self = null;
                    try
                    {
                        browser = list.Item(i);
                        if (browser == null) continue;
                        dynamic b = browser;
                        IntPtr hwnd = new IntPtr((long)b.HWND);
                        if (onlyWindow != IntPtr.Zero && Native.GetAncestor(hwnd, 2) != Native.GetAncestor(onlyWindow, 2)) continue;
                        if (!String.Equals(System.IO.Path.GetFileName((string)b.FullName), "explorer.exe", StringComparison.OrdinalIgnoreCase)) continue;
                        // On tabbed Explorer, ignore hidden tab browser windows when the shell exposes them.
                        if (onlyWindow != IntPtr.Zero && !VisibleBrowser(browser)) continue;
                        document = b.Document;
                        dynamic d = document;
                        folder = d.Folder;
                        dynamic f = folder;
                        self = f.Self;
                        dynamic item = self;
                        string path = HistoryStore.Normalize((string)item.Path);
                        if (path != null && !result.Contains(path, StringComparer.OrdinalIgnoreCase)) result.Add(path);
                    }
                    catch { /* Explorer can close or navigate while enumerating. */ }
                    finally { Release(self); Release(folder); Release(document); Release(browser); }
                }
            }
            finally { Release(windows); }
            return result;
        }
        private static bool VisibleBrowser(object browser)
        {
            object shellBrowser = null;
            try
            {
                Guid service = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837"), iid = new Guid("000214E2-0000-0000-C000-000000000046");
                var provider = browser as IServiceProviderNative;
                if (provider == null || provider.QueryService(ref service, ref iid, out shellBrowser) != 0) return true;
                IntPtr view;
                return ((IShellBrowserWindow)shellBrowser).GetWindow(out view) != 0 || view == IntPtr.Zero || Native.IsWindowVisible(view);
            }
            catch { return true; }
            finally { Release(shellBrowser); }
        }
        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IServiceProviderNative { [PreserveSig] int QueryService(ref Guid service, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object value); }
        [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellBrowserWindow { [PreserveSig] int GetWindow(out IntPtr hwnd); }
    }
}
