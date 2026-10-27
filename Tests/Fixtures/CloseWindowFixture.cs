using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

// A single hidden native window: no framework helper windows, desktop activation or user input.
internal static class CloseWindowFixture
{
    internal static void Run(EventWaitHandle stop)
    {
        var elapsed = Stopwatch.StartNew();
        var windowClass = new WindowClass { Name = "EousAssistant.CloseFixture." + Guid.NewGuid().ToString("N") };
        windowClass.Procedure = (window, message, wParam, lParam) =>
        {
            if (message == 0x10 || (message == 0x113 && (stop.WaitOne(0) || elapsed.Elapsed > TimeSpan.FromSeconds(90))))
            {
                stop.Set(); PostQuitMessage(0); return IntPtr.Zero;
            }
            return DefWindowProc(window, message, wParam, lParam);
        };
        if (RegisterClass(windowClass) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        var window = IntPtr.Zero;
        try
        {
            window = CreateWindowEx(0, windowClass.Name, "Isolated close fixture", 0x80000000, 0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (window == IntPtr.Zero || SetTimer(window, 1, 50, IntPtr.Zero) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            int result;
            while ((result = GetMessage(out var message, IntPtr.Zero, 0, 0)) > 0) DispatchMessage(ref message);
            if (result < 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            if (window != IntPtr.Zero) DestroyWindow(window);
            UnregisterClass(windowClass.Name, IntPtr.Zero);
            GC.KeepAlive(windowClass.Procedure);
        }
    }

    private delegate IntPtr WindowProcedure(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class WindowClass
    {
        public uint Style;
        public WindowProcedure Procedure = null!;
        public int ClassExtra, WindowExtra;
        public IntPtr Instance, Icon, Cursor, Background;
        public string? Menu;
        public string Name = "";
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowMessage
    {
        public IntPtr Window;
        public uint Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X, Y;
        public uint Private;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClass(WindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string name, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern UIntPtr SetTimer(IntPtr window, UIntPtr id, uint milliseconds, IntPtr procedure);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern int GetMessage(out WindowMessage message, IntPtr window, uint min, uint max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DispatchMessage(ref WindowMessage message);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int exitCode);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClass(string name, IntPtr instance);
}
