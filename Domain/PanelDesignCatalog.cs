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
            true),
        new(
            "compact-list",
            "方案二",
            "紧凑列表",
            "更低行高与轻量分隔线，适合快速扫描更多候选",
            false),
        new(
            "icon-grid",
            "方案三",
            "图标网格",
            "双列图标 tile，减少面板高度并缩短拖拽路径",
            false)
    ];

    public static string Normalize(string? id)
        => All.Any(scheme => string.Equals(scheme.Id, id, StringComparison.OrdinalIgnoreCase))
            ? All.First(scheme => string.Equals(scheme.Id, id, StringComparison.OrdinalIgnoreCase)).Id
            : DefaultId;
}
