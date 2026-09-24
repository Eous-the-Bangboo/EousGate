namespace EousGate;

public sealed record SkinDefinition(
    string Id,
    string Name,
    string Summary,
    bool IsDefault);

public static class SkinCatalog
{
    public const string DefaultId = "win11-mica";

    public static IReadOnlyList<SkinDefinition> All { get; } =
    [
        new(DefaultId, "Win11 Mica", "系统材质、清晰表面与稳定对比度", true),
        new("acrylic-glass", "玻璃质感", "柔和半透明表面与 Acrylic 背景", false),
        new("clear-surface", "透明质感", "更轻的表面与更少的边框装饰", false)
    ];

    public static string Normalize(string? id)
        => All.Any(skin => string.Equals(skin.Id, id, StringComparison.OrdinalIgnoreCase))
            ? All.First(skin => string.Equals(skin.Id, id, StringComparison.OrdinalIgnoreCase)).Id
            : DefaultId;
}
