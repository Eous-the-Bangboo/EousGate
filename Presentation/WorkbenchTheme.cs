using System.Windows;
namespace EousGate;

internal static class WorkbenchTheme
{
    internal static void Apply(ResourceDictionary resources, string theme = "Light")
    {
        var palette = ThemePalette.Create(theme, Infrastructure.WindowsAppearance.IsSystemDark, SystemParameters.HighContrast, SkinCatalog.DefaultId);
        ThemeResources.Apply(resources, palette, SkinCatalog.DefaultId);
    }
}
