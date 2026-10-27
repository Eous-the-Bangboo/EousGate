namespace EousGate;

public sealed record SkinDefinition(
    string Id,
    string Name,
    string Summary,
    bool IsDefault)
{
    // Actual colors make the chooser useful even when another skin is active.
    private ThemePalette Preview => ThemePalette.Create("Light", false, false, Id);
    public string PreviewBackground => Preview.Background;
    public string PreviewSurface => Preview.ElevatedSurface;
    public string PreviewAccent => Preview.Accent;
    public string PreviewText => Preview.Foreground;
}

public static class SkinCatalog
{
    public const string DefaultId = "win11-mica";
    public const string FocusDarkId = "focus-dark";

    public static IReadOnlyList<SkinDefinition> All { get; } =
    [
        new(DefaultId, "A · 暖白工具", "暖白与鼠尾草绿 · 跟随系统", true),
        new(FocusDarkId, "B · 深色效率", "炭黑与薄荷色 · 固定深色", false)
    ];

    public static string Normalize(string? id)
        => All.Any(skin => string.Equals(skin.Id, id, StringComparison.OrdinalIgnoreCase))
            ? All.First(skin => string.Equals(skin.Id, id, StringComparison.OrdinalIgnoreCase)).Id
            : DefaultId;
}
