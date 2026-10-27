namespace EousGate;

public sealed record PanelDesignScheme(
    string Id,
    string Sequence,
    string Name,
    string Summary,
    bool IsDefault);

public static class PanelDesignCatalog
{
    public const string DefaultId = "floating-command";

    public static IReadOnlyList<PanelDesignScheme> All { get; } =
    [
        new(
            DefaultId,
            "方案一",
            "浮动命令面板",
            "纵向应用列表、三色强调轨道与紧凑操作区",
            true)
    ];

    public static string Normalize(string? id)
        => All.Any(scheme => string.Equals(scheme.Id, id, StringComparison.OrdinalIgnoreCase))
            ? All.First(scheme => string.Equals(scheme.Id, id, StringComparison.OrdinalIgnoreCase)).Id
            : DefaultId;
}
