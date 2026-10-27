using EousGate;

internal static class FileHopTests
{
    private static AppCandidate App(string name, bool available = true)
        => new(name, name, $@"C:\Apps\{name}.exe", null, available);

    internal static void Categories()
    {
        Check(FileTypeCatalog.All.Count == 8, "eight common categories");
        Check(FileTypeCatalog.Find(" MP4 ")?.Id == "video" && FileTypeCatalog.Find("*.FLAC")?.Id == "audio", "normalized category lookup");
        Check(FileTypeCatalog.All.All(c => c.Extensions.Count > 0 && c.Extensions.All(e => FileTypeCatalog.NormalizeExtension(e) == e)), "valid common suffixes");
        Check(FileTypeCatalog.Browse("video", [], "MK").SequenceEqual([".mkv"]), "search in category");
        Check(FileTypeCatalog.Browse("all", [".special", ".MP4"]).Count(e => e == ".mp4") == 1, "deduplicate merged suffixes");
        Check(FileTypeCatalog.Browse("configured", [".special"]).SequenceEqual([".special"]), "custom suffix browsing");
        Check(new[] { "", ".", "a.b", "C:\\a.mp4", "*.exe;*.txt", "file name" }.All(e => FileTypeCatalog.NormalizeExtension(e) == ""), "reject invalid or multiple suffixes");
    }

    internal static void Ranking()
    {
        var vlc = App("vlc"); var notepad = App("notepad"); var unknown = App("unrecognized"); var browser = App("msedge");
        var ranked = AppRecommendationPolicy.Order(".mp4", [notepad, unknown, vlc, browser]);
        Check(ranked[0] == vlc && ranked[1] == unknown, "matching player before unknown and inappropriate handlers");
        Check(!AppRecommendationPolicy.IsVisible(".mp4", notepad) && !AppRecommendationPolicy.IsVisible(".mp3", browser), "known incompatible handlers omitted by default");
        Check(AppRecommendationPolicy.IsVisible(".mp4", unknown), "unknown registered apps remain discoverable");
        Check(AppRecommendationPolicy.Order(".mp4", [vlc, unknown], defaultId: unknown.Id)[0] == unknown, "system default leads automatically");
        Check(AppRecommendationPolicy.IsVisible(".mp4", notepad, defaultId: notepad.Id), "explicit Windows default is respected even for unusual choices");
        Check(AppRecommendationPolicy.IsVisible(".pdf", browser) && AppRecommendationPolicy.IsVisible(".csv", notepad), "useful cross-category handlers retained");
        Check(AppRecommendationPolicy.IsVisible(".ts", notepad) && AppRecommendationPolicy.IsVisible(".ts", vlc), "ambiguous TS supports code and video");
        Check(AppRecommendationPolicy.IsVisible(".novel", notepad), "unknown suffixes never classified by guessing");
        Check(!AppRecommendationPolicy.IsVisible(".txt", App("notepad", false)), "unavailable app stays out of runtime");
        var packaged = new AppCandidate("photos", "照片", "shell:AppsFolder\\Microsoft.Windows.Photos_8!App", null, true, null, "Microsoft.Windows.Photos_8!App");
        Check(AppRecommendationPolicy.Score(".png", packaged) == 80 && !AppRecommendationPolicy.IsVisible(".mp4", packaged), "packaged handlers use identity rather than translated label");
    }

    internal static void Overrides()
    {
        var vlc = App("vlc"); var notepad = App("notepad"); var unknown = App("unrecognized");
        var preference = new FileTypePreference { CandidateOrder = [notepad.Id], HiddenCandidateIds = [vlc.Id] };
        Check(AppRecommendationPolicy.Order(".mp4", [vlc, notepad, unknown], preference)[0] == notepad, "manual order takes priority");
        Check(AppRecommendationPolicy.IsVisible(".mp4", notepad, preference) && !AppRecommendationPolicy.IsVisible(".mp4", vlc, preference), "manual show and hide win");
        preference.CandidateOrder.Clear();
        preference.CustomApps.Add(new() { Id = notepad.Id, ExecutablePath = notepad.ExecutablePath });
        Check(AppRecommendationPolicy.IsVisible(".mp4", notepad, preference), "custom app never suppressed by heuristic");
        preference.HiddenCandidateIds.Add(notepad.Id);
        Check(!AppRecommendationPolicy.IsVisible(".mp4", notepad, preference), "custom app can still be hidden");
        preference.CustomApps.Clear(); preference.HiddenCandidateIds.Clear();
        Check(!AppRecommendationPolicy.IsVisible(".mp4", notepad, preference), "cleared recommendation overrides resume automatic selection");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
