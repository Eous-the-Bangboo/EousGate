using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EousGate.Infrastructure;

internal static class MicaWindowHelper
{
    private const int DwmAttributeSystemBackdropType = 38;
    private const int DwmSystemBackdropMica = 2;

    public static bool IsBackdropSupported
        => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && !SystemParameters.HighContrast;

    public static void Enable(Window window, string? skinId = null)
        => Apply(window, skinId);

    internal static int GetBackdropTypeForSkin(string? skinId)
        => DwmSystemBackdropMica;

    public static void Apply(Window window, string? skinId = null)
    {
        if (!IsBackdropSupported) return;

        void ApplyBackdrop(object? sender, EventArgs args)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            var backdrop = GetBackdropTypeForSkin(skinId);
            _ = DwmSetWindowAttribute(handle, DwmAttributeSystemBackdropType, ref backdrop, sizeof(int));
            var dark = window.TryFindResource("PanelBackground") is System.Windows.Media.SolidColorBrush brush && brush.Color.R < 128 ? 1 : 0;
            _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        }

        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) ApplyBackdrop(null, EventArgs.Empty);
        else window.SourceInitialized += ApplyBackdrop;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
