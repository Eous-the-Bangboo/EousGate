using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SystemColors = System.Windows.SystemColors;

namespace EousGate;

internal static class ThemeResources
{
    internal static void Apply(ResourceDictionary resources, ThemePalette palette, string? skinId)
    {
        var values = new Dictionary<string, string>
        {
            ["PanelBackground"] = palette.Background, ["PanelForeground"] = palette.Foreground,
            ["PanelMutedForeground"] = palette.MutedForeground, ["PanelBorder"] = palette.Border,
            ["PanelAccent"] = palette.Accent, ["PanelAccentHover"] = palette.AccentHover,
            ["PanelAccentSoft"] = palette.AccentSoft, ["PanelSurfaceSoft"] = palette.SurfaceSoft,
            ["PanelDanger"] = palette.Danger, ["PanelDangerSoft"] = palette.DangerSoft,
            ["PanelFocusRing"] = palette.FocusRing, ["PanelSelection"] = palette.Selection,
            ["PanelElevatedSurface"] = palette.ElevatedSurface, ["PanelSuccess"] = palette.Success,
            ["PanelSuccessSoft"] = palette.SuccessSoft, ["PanelOnAccent"] = palette.OnAccent
        };
        foreach (var (key, value) in values) resources[key] = Brush(value);
        var highContrast = palette.Background == "SystemWindow";
        var background = highContrast ? SystemColors.WindowColor : (Color)ColorConverter.ConvertFromString(palette.Background);
        var portrait = background.R * 0.299 + background.G * 0.587 + background.B * 0.114 < 128 ? "charcoal" : "sage";
        resources["AssistantPortrait"] = new BitmapImage(new Uri($"pack://application:,,,/EousAssistant;component/Assets/EousPortrait-{portrait}.png"));
        resources["ToolSwitchOn"] = Brush(highContrast ? "SystemHighlight" : palette.Success);
        resources["ToolSwitchOff"] = Brush(highContrast ? "SystemWindow" : palette.Border);
        resources["ToolSwitchKnob"] = Brush(highContrast ? "SystemWindowText" : "#FFFFFF");
        resources["ToolSwitchOnKnob"] = Brush(highContrast ? "SystemHighlightText" : "#FFFFFF");
        resources["PanelDivider"] = Brush(highContrast ? "SystemWindowText" : background.R < 128 ? "#39463C" : "#EBEAE2");
        var compact = SkinCatalog.Normalize(skinId) == SkinCatalog.FocusDarkId;
        resources["ToolCardPadding"] = new Thickness(compact ? 12 : 16);
        resources["ToolCardMargin"] = new Thickness(0, 0, 0, compact ? 8 : 12);
        resources["ToolDescriptionMargin"] = new Thickness(0, compact ? 4 : 8, 0, 0);
        resources["CandidateRowHeight"] = compact ? 52d : 60d;
    }

    private static SolidColorBrush Brush(string value) => new(value switch
    {
        "SystemWindow" => SystemColors.WindowColor,
        "SystemWindowText" => SystemColors.WindowTextColor,
        "SystemGrayText" => SystemColors.GrayTextColor,
        "SystemHighlight" => SystemColors.HighlightColor,
        "SystemHighlightText" => SystemColors.HighlightTextColor,
        _ => (Color)ColorConverter.ConvertFromString(value)
    });
}
