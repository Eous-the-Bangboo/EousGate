using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EousGate.Infrastructure;

internal static class MicaWindowHelper
{
    private const int DwmAttributeSystemBackdropType = 38;
    private const int DwmSystemBackdropNone = 1;
    private const int DwmSystemBackdropMica = 2;
    private const int DwmSystemBackdropAcrylic = 4;

    public static bool IsBackdropSupported
        => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && !SystemParameters.HighContrast;

    public static void Enable(Window window, string? skinId = null)
        => Apply(window, skinId);

    internal static int GetBackdropTypeForSkin(string? skinId)
        => SkinCatalog.Normalize(skinId) switch
        {
            "acrylic-glass" => DwmSystemBackdropAcrylic,
            "clear-surface" => DwmSystemBackdropNone,
            _ => DwmSystemBackdropMica
        };

    public static void Apply(Window window, string? skinId = null)
    {
        if (!IsBackdropSupported) return;

        void ApplyBackdrop(object? sender, EventArgs args)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            var backdrop = GetBackdropTypeForSkin(skinId);
            _ = DwmSetWindowAttribute(handle, DwmAttributeSystemBackdropType, ref backdrop, sizeof(int));
        }

        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) ApplyBackdrop(null, EventArgs.Empty);
        else window.SourceInitialized += ApplyBackdrop;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
