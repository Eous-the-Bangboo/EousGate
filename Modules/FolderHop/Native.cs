#nullable disable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EousGate.FolderHop
{
    internal static class Native
    {
        static readonly HashSet<System.Windows.Forms.Timer> navigationTimers = new();
        static System.Windows.Forms.Timer MakeTimer(int interval)
        {
            var t = new System.Windows.Forms.Timer { Interval = interval };
            navigationTimers.Add(t); t.Disposed += (_, _) => navigationTimers.Remove(t); return t;
        }
        internal static void CancelNavigation()
        {
            foreach (var t in new List<System.Windows.Forms.Timer>(navigationTimers)) { t.Stop(); t.Dispose(); }
        }
        internal static Action<string> Trace;
        static void TraceStep(string text) { if(Trace!=null)Trace(text); }
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct GuiInfo { public int cbSize; public uint flags; public IntPtr active, focus, capture, menuOwner, moveSize, caret; public Rect caretRect; }
        [StructLayout(LayoutKind.Sequential)] struct Input { public uint type; public InputUnion data; }
        [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public KeyboardInput keyboard; [FieldOffset(0)] public MouseInput mouse; }
        [StructLayout(LayoutKind.Sequential)] struct KeyboardInput { public ushort key, scan; public uint flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] struct MouseInput { public int x, y; public uint mouseData, flags, time; public UIntPtr extra; }
        internal delegate bool EnumProc(IntPtr hwnd, IntPtr param);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr hwnd, uint flag);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] internal static extern bool IsChild(IntPtr parent, IntPtr child);
        [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr hwnd);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll")] internal static extern bool EnumChildWindows(IntPtr hwnd, EnumProc callback, IntPtr param);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, IntPtr param);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);
        [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
        [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint count, Input[] inputs, int size);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wp, string text, uint flags, uint timeout, out UIntPtr result);
        [DllImport("user32.dll", CharSet=CharSet.Unicode, EntryPoint="SendMessageTimeoutW")] static extern IntPtr SendMessageTimeoutBuffer(IntPtr hwnd, uint msg, IntPtr wp, StringBuilder text, uint flags, uint timeout, out UIntPtr result);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wp, string value);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")] internal static extern int GetDlgCtrlID(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);

        internal static string Class(IntPtr hwnd) { var b = new StringBuilder(256); GetClassName(hwnd, b, b.Capacity); return b.ToString(); }
        internal static string Title(IntPtr hwnd) { var b = new StringBuilder(2048); GetWindowText(hwnd, b, b.Capacity); return b.ToString(); }
        internal static string ControlText(IntPtr hwnd)
        {
            var b = new StringBuilder(32768); UIntPtr result;
            return SendMessageTimeoutBuffer(hwnd, 0x000D, new IntPtr(b.Capacity), b, 0x22, 300, out result) == IntPtr.Zero ? "" : b.ToString();
        }
        internal static List<IntPtr> Children(IntPtr hwnd)
        {
            var rows = new List<IntPtr>();
            EnumChildWindows(hwnd, delegate(IntPtr child, IntPtr unused) { rows.Add(child); return true; }, IntPtr.Zero); return rows;
        }
        internal static bool IsExplorer(IntPtr hwnd) { string c = Class(hwnd); return c == "CabinetWClass" || c == "ExploreWClass"; }
        internal static bool IsFileDialog(IntPtr hwnd)
        {
            if (!IsWindow(hwnd) || !IsWindowVisible(hwnd) || Class(hwnd) != "#32770") return false;
            bool breadcrumb = false, shell = false;
            foreach (IntPtr child in Children(hwnd))
            {
                string c = Class(child);
                if (c == "Breadcrumb Parent" || c == "Address Band Root") breadcrumb = true;
                if (c == "SHELLDLL_DefView" || c == "DUIViewWndClassName") shell = true;
            }
            // Do not act on generic preferences, password, confirmation or message dialogs.
            return breadcrumb && shell;
        }
        internal static IntPtr Focus()
        {
            var info = new GuiInfo(); info.cbSize = Marshal.SizeOf(info);
            return GetGUIThreadInfo(0, ref info) ? info.focus : IntPtr.Zero;
        }
        internal static bool IsAddressEdit(IntPtr dialog, IntPtr edit)
        {
            if (Class(edit) != "Edit" || !IsChild(dialog, edit)) return false;
            for (IntPtr p = GetParent(edit); p != IntPtr.Zero && p != dialog; p = GetParent(p))
                if (Class(p) == "Breadcrumb Parent" || (Class(p) == "Address Band Root" && GetDlgCtrlID(edit) == 41477)) return true;
            return false;
        }
        static Input Key(ushort code, bool up)
        {
            var i = new Input(); i.type = 1; i.data.keyboard.key = code; i.data.keyboard.flags = up ? 2U : 0U; return i;
        }
        internal static bool Keys(params ushort[] keys)
        {
            var list = new List<Input>();
            foreach (ushort k in keys) list.Add(Key(k, false));
            for (int i = keys.Length - 1; i >= 0; i--) list.Add(Key(keys[i], true));
            return SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf(typeof(Input))) == list.Count;
        }
        internal static bool SetText(IntPtr hwnd, string text)
        {
            UIntPtr result;
            return SendMessageTimeout(hwnd, 0x000C, IntPtr.Zero, text, 0x22, 400, out result) != IntPtr.Zero && result != UIntPtr.Zero;
        }
        internal static void Navigate(IntPtr target, string path, Action<bool,string> complete)
        {
            path = HistoryStore.Normalize(path);
            if (path == null || !IsFileDialog(target)) { complete(false, "目标窗口已关闭，或不是支持的 Windows 文件对话框。"); return; }
            SetForegroundWindow(target);
            int tries = 0;
            var timer = MakeTimer(60);
            timer.Tick += delegate
            {
                if (++tries > 20) { timer.Stop(); timer.Dispose(); complete(false, "未能激活目标窗口。请回到打开／保存窗口后重试。"); return; }
                if (GetForegroundWindow() != target || (GetAsyncKeyState(0x11) & 0x8000) != 0 || (GetAsyncKeyState(0x12) & 0x8000) != 0 || (GetAsyncKeyState(0x0D) & 0x8000) != 0) return;
                timer.Stop(); timer.Dispose();
                if (!IsFileDialog(target) || !Keys(0x11, 0x4C)) { complete(false, "无法访问地址栏。目标程序可能以管理员身份运行。"); return; }
                int waits = 0;
                var focusTimer = MakeTimer(70);
                focusTimer.Tick += delegate
                {
                    IntPtr focus = Focus();
                    if (GetForegroundWindow() != target || !IsWindow(target)) { focusTimer.Stop(); focusTimer.Dispose(); complete(false, "焦点已切换，跳转已取消。"); return; }
                    if (IsAddressEdit(target, focus))
                    {
                        focusTimer.Stop(); focusTimer.Dispose();
                        TraceStep("Address control verified; setting text.");
                        if (!SetText(focus, path) || !String.Equals(ControlText(focus), path, StringComparison.OrdinalIgnoreCase)) { complete(false, "地址栏未接受路径，跳转已取消。"); return; }
                        TraceStep("Address text verified; committing navigation.");
                        // The Enter goes to the verified ADDRESS edit, never the filename field or dialog OK button.
                        if (GetForegroundWindow() != target || Focus() != focus || !IsAddressEdit(target, focus)) { complete(false, "地址栏焦点已改变，跳转已取消。"); return; }
                        PostMessage(focus, 0x0100, new IntPtr(0x0D), IntPtr.Zero);
                        PostMessage(focus, 0x0101, new IntPtr(0x0D), IntPtr.Zero);
                        TraceStep("Navigation key messages queued.");
                        complete(true, "已发送目录跳转。"); return;
                    }
                    if (++waits >= 12) { focusTimer.Stop(); focusTimer.Dispose(); complete(false, "未识别到地址栏，已停止操作。可手动复制路径。"); }
                };
                focusTimer.Start();
            };
            timer.Start();
        }
    }
}
