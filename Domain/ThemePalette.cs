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
    public string OnAccent { get; init; } = "#FFFFFFFF";

    public static ThemePalette Create(string theme, bool systemIsDark, bool highContrast, string? skinId = null)
    {
        if (highContrast)
            return new("SystemWindow", "SystemWindowText", "SystemGrayText", "SystemWindowText", "SystemHighlight", "SystemHighlight", "SystemWindow", "SystemWindow", "SystemWindowText", "SystemWindow", "SystemHighlight", "SystemHighlight", "SystemWindow", "SystemHighlight", "SystemWindow") { OnAccent = "SystemHighlightText" };

        var dark = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(theme, "System", StringComparison.OrdinalIgnoreCase) && systemIsDark);
        var skin = SkinCatalog.Normalize(skinId);
        if (skin == SkinCatalog.FocusDarkId)
            return new("#FF17191D", "#FFF0F2F4", "#FFB3BDC8", "#FF48515C", "#FF80D8C4", "#FFA1E8D8", "#FF29423E", "#FF202429", "#FFFFA6A0", "#FF482B2B", "#FF80D8C4", "#FF29423E", "#FF22262C", "#FF80D8C4", "#FF29423E") { OnAccent = "#FF132B25" };
        return dark
            ? new("#FF29342E", "#FFF2F0E7", "#FFBAC4B7", "#FF4E5B4E", "#FFEBAD86", "#FFF2B995", "#FF3D503C", "#FF29342E", "#FFF0A093", "#FF492F2A", "#FFF2B995", "#FF3D503C", "#FF222B26", "#FFA4C198", "#FF3D503C") { OnAccent = "#FF222B26" }
            : new("#FFF6F2EA", "#FF344C43", "#FF647166", "#FFD7DCD0", "#FFAE4C25", "#FF913C1D", "#FFF4E8DD", "#FFF6F2EA", "#FFA8392F", "#FFF9E9E5", "#FFAE4C25", "#FFE7ECDD", "#FFFFFDF8", "#FF55775B", "#FFE7ECDD");
    }
}
