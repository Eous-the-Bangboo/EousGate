namespace EousGate;

/// <summary>Conservative, offline ordering of handlers already discovered for this extension.</summary>
public static class AppRecommendationPolicy
{
    public static IReadOnlyList<AppCandidate> Order(string extension, IEnumerable<AppCandidate> candidates,
        FileTypePreference? preference = null, string? defaultId = null, IReadOnlySet<string>? shellRecommended = null)
        => candidates.OrderByDescending(c => c.IsAvailable)
            .ThenBy(c => OrderIndex(preference, c.Id))
            .ThenByDescending(c => Score(extension, c, preference, defaultId, shellRecommended))
            .ThenBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();

    public static bool IsSuggested(string extension, AppCandidate candidate, FileTypePreference? preference = null,
        string? defaultId = null, IReadOnlySet<string>? shellRecommended = null)
        => Score(extension, candidate, preference, defaultId, shellRecommended) >= 0;

    public static bool IsVisible(string extension, AppCandidate candidate, FileTypePreference? preference = null,
        string? defaultId = null, IReadOnlySet<string>? shellRecommended = null)
        => candidate.IsAvailable
            && preference?.HiddenCandidateIds.Any(id => Same(id, candidate.Id)) != true
            && (preference?.CandidateOrder.Any(id => Same(id, candidate.Id)) == true
                || IsSuggested(extension, candidate, preference, defaultId, shellRecommended));

    public static int Score(string extension, AppCandidate candidate, FileTypePreference? preference = null,
        string? defaultId = null, IReadOnlySet<string>? shellRecommended = null)
    {
        if (Same(candidate.Id, defaultId)) return 1000;
        if (preference?.CustomApps.Any(app => Same(app.Id, candidate.Id) || Same(app.ExecutablePath, candidate.ExecutablePath)) == true) return 900;
        var ext = FileTypeCatalog.NormalizeExtension(extension);
        var category = FileTypeCatalog.Find(ext)?.Id;
        if (category is null) return shellRecommended?.Contains(candidate.Id) == true ? 50 : 20;

        // Match executable basenames / package identities, never directory names.
        var executable = candidate.ExecutablePath.Replace('\\', '/').Split('/').Last().ToLowerInvariant();
        var identity = (candidate.AssociationHandlerName ?? "").ToLowerInvariant();
        bool Is(params string[] names) => names.Any(name => executable == name + ".exe" || identity.Contains(name, StringComparison.Ordinal));
        bool? fits = null;
        if (Is("vlc", "mpv", "mpvnet", "potplayer", "potplayermini", "potplayermini64", "mpc-hc", "mpc-hc64", "mpc-be", "mpc-be64", "wmplayer", "microsoft.zunemusic", "microsoft.zunevideo", "videolan.vlc"))
            fits = category is "video" or "audio";
        else if (Is("foobar2000", "musicbee", "aimp", "cloudmusic", "qqmusic", "groove")) fits = category == "audio";
        else if (Is("photoshop", "gimp", "gimp-2.10", "paintdotnet", "mspaint", "i_view64", "i_view32", "xnviewmp", "microsoft.windows.photos", "microsoft.paint")) fits = category == "image";
        else if (Is("7zfm", "winrar", "bandizip", "peazip", "360zip", "haozip")) fits = category == "archive";
        else if (Is("excel", "et")) fits = category == "spreadsheet";
        else if (Is("powerpnt", "wpp")) fits = category == "presentation";
        else if (Is("winword", "wps", "swriter")) fits = category == "document";
        else if (Is("acrord32", "acrobat", "sumatrapdf", "foxitpdfreader", "pdfxedit")) fits = ext is ".pdf" or ".epub";
        else if (Is("notepad", "notepad++", "code", "codium", "sublime_text", "cursor", "microsoft.windowsnotepad"))
            fits = category == "code" || ext is ".txt" or ".md" or ".rtf" or ".csv" or ".tsv" or ".svg" or ".ts";
        else if (Is("msedge", "chrome", "firefox", "brave", "opera", "vivaldi"))
            fits = ext is ".pdf" or ".html" or ".svg" || category == "image";

        if (fits == false) return -100;
        if (fits == true)
        {
            if (category is "video" or "audio"
                && Is("vlc", "mpv", "mpvnet", "potplayer", "potplayermini", "potplayermini64", "mpc-hc", "mpc-hc64", "mpc-be", "mpc-be64", "foobar2000", "musicbee", "aimp")) return 100;
            return 80;
        }
        return shellRecommended?.Contains(candidate.Id) == true ? 50 : 20;
    }

    private static int OrderIndex(FileTypePreference? preference, string id)
    {
        var index = preference?.CandidateOrder.FindIndex(value => Same(value, id)) ?? -1;
        return index < 0 ? int.MaxValue : index;
    }

    private static bool Same(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
