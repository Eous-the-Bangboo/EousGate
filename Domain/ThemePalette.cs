namespace EousGate;

public sealed record ThemePalette(
    string Background,
    string Foreground,
    string MutedForeground,
    string Border,
    string Accent,
    string AccentHover,
    string AccentSoft,
    string SurfaceSoft,
    string Danger,
    string DangerSoft,
    string FocusRing,
    string Selection,
    string ElevatedSurface,
    string Success,
    string SuccessSoft)
{
    public static ThemePalette Create(string theme, bool systemIsDark, bool highContrast, string? skinId = null)
    {
        if (highContrast)
            return new("SystemWindow", "SystemWindowText", "SystemGrayText", "SystemWindowText", "SystemHighlight", "SystemHighlight", "SystemWindow", "SystemWindow", "SystemWindowText", "SystemWindow", "SystemHighlight", "SystemHighlight", "SystemWindow", "SystemHighlightText", "SystemHighlight");

        var dark = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(theme, "System", StringComparison.OrdinalIgnoreCase) && systemIsDark);
        var skin = SkinCatalog.Normalize(skinId);
        if (skin == SkinCatalog.DefaultId)
        {
            return dark
                ? new("#F2202020", "#FFF5F5F5", "#FFB8B8B8", "#B34A4A4A", "#FF60A5FA", "#FF93C5FD", "#803C5A7D", "#A02A2A2A", "#FFFF8A80", "#80482B2B", "#FF93C5FD", "#664F82B7", "#F02A2A2A", "#FF5EEAD4", "#335EEAD4")
                : new("#F2F5F5F5", "#FF1F1F1F", "#FF555B63", "#B3C7CDD4", "#FF2563EB", "#FF1D4ED8", "#80EAF2FF", "#D9F6F8FB", "#FFC2413D", "#80FFF1F0", "#FF1D4ED8", "#332563EB", "#FFFFFFFF", "#FF0F766E", "#1A0F766E");
        }

        if (skin == "acrylic-glass")
        {
            return dark
                ? new("#E6202020", "#FFF5F5F5", "#FFD0D0D0", "#996A7682", "#FF75B8FF", "#FFA7D3FF", "#66344F70", "#A02A2A2A", "#FFFFA6A0", "#66482B2B", "#FFA7D3FF", "#665B8FC7", "#F02A2A2A", "#FF5EEAD4", "#335EEAD4")
                : new("#E6F8FAFC", "#FF1F1F1F", "#FF4D5660", "#997C8D99", "#FF2563EB", "#FF1D4ED8", "#66EAF2FF", "#D9FFFFFF", "#FFC2413D", "#66FFF1F0", "#FF1D4ED8", "#332563EB", "#F5FFFFFF", "#FF0F766E", "#1A0F766E");
        }

        return dark
            ? new("#CC202020", "#FFF5F5F5", "#FFC7C7C7", "#805C6672", "#FF75B8FF", "#FFA7D3FF", "#44344F70", "#902A2A2A", "#FFFFA6A0", "#44482B2B", "#FFA7D3FF", "#445B8FC7", "#F02A2A2A", "#FF5EEAD4", "#225EEAD4")
            : new("#CCF8FAFC", "#FF1F1F1F", "#FF4D5660", "#806F7F8B", "#FF2563EB", "#FF1D4ED8", "#44EAF2FF", "#CCFFFFFF", "#FFC2413D", "#44FFF1F0", "#FF1D4ED8", "#222563EB", "#F5FFFFFF", "#FF0F766E", "#140F766E");
    }
}
