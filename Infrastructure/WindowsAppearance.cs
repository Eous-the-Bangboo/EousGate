using Microsoft.Win32;
using System.Security;

namespace EousGate.Infrastructure;

internal static class WindowsAppearance
{
    internal static bool IsSystemDark => ReadPreference("AppsUseLightTheme", 1) == 0;
    internal static bool TransparencyEnabled => ReadPreference("EnableTransparency", 1) != 0;

    private static int ReadPreference(string name, int fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue(name) is int value ? value : fallback;
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or System.IO.IOException) { return fallback; }
    }
}
