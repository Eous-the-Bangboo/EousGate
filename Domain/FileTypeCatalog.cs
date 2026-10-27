namespace EousGate;

public sealed record FileTypeCategory(string Id, string Name, IReadOnlyList<string> Extensions);

/// <summary>Local browsing shortcuts; discovery still checks the actual Windows associations.</summary>
public static class FileTypeCatalog
{
    public static IReadOnlyList<FileTypeCategory> All { get; } =
    [
        new("video", "视频", [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".flv", ".m4v", ".ts", ".mpeg"]),
        new("audio", "音频", [".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".wma", ".aiff", ".opus", ".ape"]),
        new("image", "图片", [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg", ".ico", ".tif", ".tiff", ".heic", ".avif", ".psd", ".raw"]),
        new("document", "文档", [".pdf", ".docx", ".doc", ".txt", ".md", ".rtf", ".odt", ".epub"]),
        new("spreadsheet", "表格", [".xlsx", ".xls", ".csv", ".tsv", ".ods"]),
        new("presentation", "演示", [".pptx", ".ppt", ".odp"]),
        new("archive", "压缩包", [".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".iso"]),
        new("code", "代码与配置", [".json", ".xml", ".yaml", ".yml", ".toml", ".ini", ".log", ".cs", ".js", ".ts", ".tsx", ".py", ".html", ".css", ".sql", ".cpp", ".h", ".sh", ".ps1"])
    ];

    public static string NormalizeExtension(string? extension)
    {
        var value = extension?.Trim().ToLowerInvariant() ?? "";
        if (value.StartsWith("*.")) value = value[1..];
        if (value.Length == 0) return "";
        if (!value.StartsWith('.')) value = "." + value;
        return value.Length > 1 && !value[1..].Contains('.')
            && value[1..].All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '+') ? value : "";
    }

    // .ts is also a code suffix. For recommendation, prefer transport-stream video;
    // explicitly registered/custom editors remain available for user selection.
    public static FileTypeCategory? Find(string? extension)
        => All.FirstOrDefault(category => category.Extensions.Contains(NormalizeExtension(extension), StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<string> Browse(string categoryId, IEnumerable<string> configured, string? query = null)
    {
        var extensions = categoryId == "configured" ? configured
            : categoryId == "all" ? All.SelectMany(c => c.Extensions).Concat(configured)
            : All.FirstOrDefault(c => c.Id == categoryId)?.Extensions ?? [];
        var search = query?.Trim().TrimStart('*', '.');
        return extensions.Select(NormalizeExtension).Where(e => e.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(e => string.IsNullOrWhiteSpace(search) || e.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
