using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Brush = System.Windows.Media.Brush;
using SystemColors = System.Windows.SystemColors;

namespace EousGate.Infrastructure;

// Documented DWM system backdrops require Windows 11 22H2. Layered tool windows
// keep their existing input/composition model and use the thick material fallback.
internal static class DesktopGlassHelper
{
    internal static bool CanUseBackdrop(bool supported, bool composition, bool highContrast, bool transparency, bool layered)
        => supported && composition && !highContrast && transparency && !layered;

    internal static bool Apply(Window window)
    {
        var fallback = window.TryFindResource("PanelElevatedSurface") as Brush ?? SystemColors.WindowBrush;
        window.Background = fallback;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return false;
        var supported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
        var composition = DwmIsCompositionEnabled(out var enabled) == 0 && enabled;
        var useGlass = CanUseBackdrop(supported, composition, SystemParameters.HighContrast, WindowsAppearance.TransparencyEnabled, window.AllowsTransparency);
        var backdrop = useGlass ? 3 : 1; // DWMSBT_TRANSIENTWINDOW / DWMSBT_NONE
        var dark = window.TryFindResource("PanelBackground") is SolidColorBrush brush && brush.Color.R < 128 ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        var corner = 2; // DWMWCP_ROUND
        _ = DwmSetWindowAttribute(hwnd, 33, ref corner, sizeof(int));
        var applied = supported && DwmSetWindowAttribute(hwnd, 38, ref backdrop, sizeof(int)) == 0;
        useGlass &= applied;
        if (WindowChrome.GetWindowChrome(window) is { } chrome) chrome.GlassFrameThickness = useGlass ? new Thickness(-1) : new Thickness(0);
        var margin = useGlass ? -1 : 0;
        var margins = new Margins { Left = margin, Right = margin, Top = margin, Bottom = margin };
        if (DwmExtendFrameIntoClientArea(hwnd, ref margins) != 0) useGlass = false;
        if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target)
            target.BackgroundColor = useGlass ? Colors.Transparent : (fallback as SolidColorBrush)?.Color ?? SystemColors.WindowColor;
        if (useGlass) window.Background = System.Windows.Media.Brushes.Transparent;
        else
        {
            backdrop = 1; _ = DwmSetWindowAttribute(hwnd, 38, ref backdrop, sizeof(int));
            if (WindowChrome.GetWindowChrome(window) is { } fallbackChrome) fallbackChrome.GlassFrameThickness = new Thickness(0);
        }
        return useGlass;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
    [DllImport("dwmapi.dll")] private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);
}
