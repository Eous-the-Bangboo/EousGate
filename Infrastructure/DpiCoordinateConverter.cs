using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace EousGate.Infrastructure;

/// <summary>Converts WinForms screen pixels to WPF device-independent units.</summary>
public static class DpiCoordinateConverter
{
    private const uint MonitorDefaultToNearest = 2;
    private const int MonitorDpiTypeEffective = 0;

    public static double GetScaleForPoint(System.Drawing.Point point)
    {
        var monitor = MonitorFromPoint(new NativePoint(point.X, point.Y), MonitorDefaultToNearest);
        if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, MonitorDpiTypeEffective, out var dpiX, out _) == 0 && dpiX > 0)
            return dpiX / 96d;
        return 1d;
    }

    public static double PixelsToDip(double pixels, double scale) => pixels / Math.Max(scale, 0.01d);

    public static int DipToPixels(double dip, double scale) => (int)Math.Round(dip * Math.Max(scale, 0.01d));

    public static WpfPoint ToDip(System.Drawing.Point point, double scale)
        => new(PixelsToDip(point.X, scale), PixelsToDip(point.Y, scale));

    public static WpfRect ToDip(System.Drawing.Rectangle rectangle, double scale)
        => new(PixelsToDip(rectangle.Left, scale), PixelsToDip(rectangle.Top, scale),
            PixelsToDip(rectangle.Width, scale), PixelsToDip(rectangle.Height, scale));

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint(int x, int y)
    {
        public readonly int X = x;
        public readonly int Y = y;
    }
}

public enum OverlayPlacementSide
{
    Left,
    Right,
    Top
}

public static class OverlayPlacementMath
{
    public static System.Drawing.Rectangle CalculatePixels(System.Drawing.Rectangle workingArea,
        System.Drawing.Point cursor, int panelWidth, int panelHeight, OverlayPlacementSide side, int gap = 18)
    {
        var desiredLeft = side == OverlayPlacementSide.Top
            ? cursor.X - panelWidth / 2
            : side == OverlayPlacementSide.Right
                ? cursor.X - panelWidth - gap
                : cursor.X + gap;
        var desiredTop = side == OverlayPlacementSide.Top ? cursor.Y + gap : cursor.Y - panelHeight / 2;
        var maxLeft = Math.Max(workingArea.Left, workingArea.Right - panelWidth);
        var maxTop = Math.Max(workingArea.Top, workingArea.Bottom - panelHeight);
        return new System.Drawing.Rectangle(
            Math.Clamp(desiredLeft, workingArea.Left, maxLeft),
            Math.Clamp(desiredTop, workingArea.Top, maxTop),
            panelWidth,
            panelHeight);
    }

    public static WpfRect Calculate(WpfRect workingArea, WpfPoint cursor, double panelWidth, double panelHeight,
        OverlayPlacementSide side, double gap = 18)
    {
        var desiredLeft = side == OverlayPlacementSide.Top
            ? cursor.X - panelWidth / 2
            : side == OverlayPlacementSide.Right
                ? cursor.X - panelWidth - gap
                : cursor.X + gap;
        var desiredTop = side == OverlayPlacementSide.Top ? cursor.Y + gap : cursor.Y - panelHeight / 2;
        var maxLeft = Math.Max(workingArea.Left, workingArea.Right - panelWidth);
        var maxTop = Math.Max(workingArea.Top, workingArea.Bottom - panelHeight);
        return new WpfRect(
            Math.Clamp(desiredLeft, workingArea.Left, maxLeft),
            Math.Clamp(desiredTop, workingArea.Top, maxTop),
            panelWidth,
            panelHeight);
    }
}

public static class NativeWindowPlacement
{
    public static bool TryGetBounds(Window window, out System.Drawing.Rectangle bounds)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero && GetWindowRect(handle, out var rect))
        {
            bounds = System.Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            return true;
        }
        bounds = default;
        return false;
    }

    public static bool TrySetPosition(Window window, int left, int top)
    {
        var handle = new WindowInteropHelper(window).Handle;
        return handle != IntPtr.Zero && SetWindowPos(handle, IntPtr.Zero, left, top, 0, 0,
            SetWindowPosFlags.NoSize | SetWindowPosFlags.NoZOrder | SetWindowPosFlags.NoActivate);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int cx, int cy, SetWindowPosFlags flags);

    [Flags]
    private enum SetWindowPosFlags : uint
    {
        NoSize = 0x0001,
        NoZOrder = 0x0004,
        NoActivate = 0x0010
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }
}
