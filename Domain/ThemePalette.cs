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
            ? new("#FF18251E", "#FFECF1EB", "#FFAFBEB4", "#FF405449", "#FFF0B18B", "#FFF5BE9E", "#FF3D3228", "#FF24332B", "#FFF0A093", "#FF492F2A", "#FFF2B995", "#FF314A3C", "#FF202B26", "#FFB3D4AE", "#FF314A3C") { OnAccent = "#FF202B26" }
            : new("#FFF3F5EE", "#FF26332D", "#FF59675E", "#FFD3DCD0", "#FFA94C28", "#FF944021", "#FFF7E8DD", "#FFE9EFE5", "#FFA8392F", "#FFF9E9E5", "#FFA94C28", "#FFE4EDE0", "#FFFFFEF9", "#FF416452", "#FFE4EDE0");
    }
}
