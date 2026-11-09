using EousGate;
using EousGate.Infrastructure;
using System.Windows;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Windows.Interop;

internal static class GlassTests
{
    internal static void NativeBackdrop(AssistantWindow window, string output)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        Check(hwnd != IntPtr.Zero, "material must be verified on a real HWND");
        var supported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
        var composition = DwmIsCompositionEnabled(out var enabled) == 0 && enabled;
        var eligible = DesktopGlassHelper.CanUseBackdrop(supported, composition, SystemParameters.HighContrast, WindowsAppearance.TransparencyEnabled, window.AllowsTransparency);
        Check(!window.HasDesktopGlass || eligible, "native material must respect actual system policy");
        var result = DwmGetWindowAttribute(hwnd, 38, out var backdrop, sizeof(int));
        if (window.HasDesktopGlass)
        {
            Check(result == 0 && backdrop == 3, "DWM must retain the requested transient backdrop after showing the window");
            Check(HwndSource.FromHwnd(hwnd)?.CompositionTarget.BackgroundColor.A == 0, "WPF composition target must expose the native backdrop");
            Check(window.Background is SolidColorBrush { Color.A: 0 }, "window must not cover the native backdrop");
        }
        else Check(window.Background is SolidColorBrush { Color.A: 255 }, "native fallback must provide an opaque window base");
        if (supported)
        {
            Check(result == 0 && backdrop == (window.HasDesktopGlass ? 3 : 1), "fallback must disable the system backdrop");
            Check(DwmGetWindowAttribute(hwnd, 20, out var dark, sizeof(int)) == 0, "native theme preference must be readable");
            var expectedDark = window.FindResource("PanelBackground") is SolidColorBrush brush && brush.Color.R < 128;
            Check((dark != 0) == expectedDark, "native material must follow the applied foreground theme");
        }
        File.AppendAllText(Path.Combine(output, "native-glass.txt"), $"OS={Environment.OSVersion.Version}; theme={window.CurrentTheme}; eligible={eligible}; applied={window.HasDesktopGlass}; DWM={backdrop}; HRESULT={result}\n");
    }

    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);

    internal static void FallbackAndContrast()
    {
        Check(DesktopGlassHelper.CanUseBackdrop(true, true, false, true, false), "supported desktop windows may use glass");
        Check(!DesktopGlassHelper.CanUseBackdrop(false, true, false, true, false), "older Windows must remain opaque");
        Check(!DesktopGlassHelper.CanUseBackdrop(true, false, false, true, false), "disabled composition must remain opaque");
        Check(!DesktopGlassHelper.CanUseBackdrop(true, true, true, true, false), "high contrast must override glass");
        Check(!DesktopGlassHelper.CanUseBackdrop(true, true, false, false, false), "system transparency preference must be respected");
        Check(!DesktopGlassHelper.CanUseBackdrop(true, true, false, true, true), "layered drag/drop windows retain their input model");
        var resources = new ResourceDictionary
        {
            ["PanelElevatedSurface"] = SystemColors.WindowBrush, ["PanelBackground"] = SystemColors.WindowBrush,
            ["PanelSurfaceSoft"] = SystemColors.WindowBrush, ["PanelBorder"] = SystemColors.WindowTextBrush,
            ["PanelSelection"] = SystemColors.HighlightBrush, ["PanelSuccess"] = SystemColors.HighlightBrush
        };
        GlassResources.Apply(resources, ThemePalette.Create("Dark", true, true), SkinCatalog.DefaultId);
        foreach (var key in new[] { "GlassWindowSurface", "GlassPanelSurface", "GlassControlSurface", "GlassSidebarSurface", "GlassFooterSurface" })
            Check(resources[key] is SolidColorBrush { Color.A: 255 }, "high contrast must remove translucent material: " + key);
        Check(resources["GlassSelectedForeground"] == SystemColors.HighlightTextBrush, "selection text must use the matching system foreground");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
