using System.Windows;
namespace EousGate;

internal static class WorkbenchTheme
{
    internal static void Apply(ResourceDictionary resources, string theme = "Light")
    {
        var palette = ThemePalette.Create(theme, System.Windows.SystemColors.WindowColor.R < 128, SystemParameters.HighContrast, SkinCatalog.DefaultId);
        ThemeResources.Apply(resources, palette, SkinCatalog.DefaultId);
    }
}
