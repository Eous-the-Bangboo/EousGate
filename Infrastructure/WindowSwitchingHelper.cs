using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EousGate.Infrastructure;

internal static class WindowSwitchingHelper
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExAppWindow = 0x00040000;

    // Register before the HWND exists so Shell never sees an ordinary app window.
    internal static void ExcludeFromWindowSwitching(Window window)
    {
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
            throw new InvalidOperationException("Configure window switching before creating the window handle.");

        window.ShowInTaskbar = false;
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            var style = GetWindowLong(handle, GwlExStyle);
            var error = Marshal.GetLastPInvokeError();
            if (style == 0 && error != 0) throw new Win32Exception(error);

            var updated = (style | WsExToolWindow) & ~WsExAppWindow;
            if (updated == style) return;
            var previous = SetWindowLong(handle, GwlExStyle, updated);
            error = Marshal.GetLastPInvokeError();
            if (previous == 0 && error != 0) throw new Win32Exception(error);
        };
    }

    // Extended styles are 32-bit values on both x86 and x64.
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr handle, int index, int value);
}
