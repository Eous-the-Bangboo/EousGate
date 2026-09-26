namespace EousGate;

public enum DragSessionState { Detecting, PanelShown, Opening, Cancelled, Failed, Completed }

public sealed record AppCandidate(
    string Id,
    string DisplayName,
    string ExecutablePath,
    string? Arguments,
    bool IsAvailable,
    string? IconPath = null,
    // Packaged Windows apps do not expose a launchable .exe. Keep the Shell
    // resolved AppUserModelID so Shell invocation targets the selected app.
    string? AssociationHandlerName = null)
{
    public bool IsPackaged => !string.IsNullOrWhiteSpace(AssociationHandlerName);
}

public sealed class CustomAppDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string? Arguments { get; set; }
}

public sealed class EdgeBandLayout
{
    public double LengthPercent { get; set; } = 100;
    public double WidthPercent { get; set; } = 0.5;
    public int OffsetPixels { get; set; }

    public EdgeBandLayout Clone() => new()
    {
        LengthPercent = LengthPercent,
        WidthPercent = WidthPercent,
        OffsetPixels = OffsetPixels
    };
}

public sealed class DragSession
{
    public DragSession(IReadOnlyList<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        if (filePaths.Count == 0) throw new ArgumentException("A drag session requires at least one file.", nameof(filePaths));
        FilePaths = Array.AsReadOnly(filePaths.ToArray());
    }

    public IReadOnlyList<string> FilePaths { get; }
    public string PrimaryFilePath => FilePaths[0];
    public int FileCount => FilePaths.Count;
    public DragSessionState State { get; set; } = DragSessionState.Detecting;
    public AppCandidate? SelectedCandidate { get; set; }
    public string? FailureReason { get; set; }
}

public sealed record OpenResult(bool Success, string? Error = null);

public sealed class UserSettings
{
    public string DesignSchemeId { get; set; } = PanelDesignCatalog.DefaultId;
    public bool Paused { get; set; }
    public int CandidateCount { get; set; } = 4;
    public double EdgeBandWidth { get; set; } = 8;
    public double EdgeBandOpacity { get; set; } = 0.58;
    public string EdgeBandColor { get; set; } = "#2563EB";
    // Comma-separated, persisted for compatibility with the earlier single-position setting.
    public string EdgeBandPosition { get; set; } = "Left,Right";
    // Legacy global pixel width. New settings use the per-side layouts below.
    public EdgeBandLayout? LeftEdgeBand { get; set; } = new();
    public EdgeBandLayout? RightEdgeBand { get; set; } = new();
    public EdgeBandLayout? TopEdgeBand { get; set; } = new();
    public int TriggerDelayMs { get; set; } = 120;
    public int AutoDismissDelayMs { get; set; } = 500;
    public double PanelWidth { get; set; } = 404;
    public double PanelMaxHeight { get; set; } = 640;
    public double CandidateGap { get; set; } = 5;
    public int AnimationDurationMs { get; set; } = 320;
    public bool EdgePulseEnabled { get; set; } = true;
    public string Theme { get; set; } = "System";
    public string SkinId { get; set; } = SkinCatalog.DefaultId;
    public bool LargeText { get; set; }
    public bool DiagnosticsEnabled { get; set; }
    public Dictionary<string, FileTypePreference> FileTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public UserSettings Clone()
    {
        var clone = new UserSettings
        {
            DesignSchemeId = DesignSchemeId,
            Paused = Paused,
            CandidateCount = CandidateCount,
            EdgeBandWidth = EdgeBandWidth,
            EdgeBandOpacity = EdgeBandOpacity,
            EdgeBandColor = EdgeBandColor,
            EdgeBandPosition = EdgeBandPosition,
            LeftEdgeBand = LeftEdgeBand?.Clone(),
            RightEdgeBand = RightEdgeBand?.Clone(),
            TopEdgeBand = TopEdgeBand?.Clone(),
            TriggerDelayMs = TriggerDelayMs,
            AutoDismissDelayMs = AutoDismissDelayMs,
            PanelWidth = PanelWidth,
            PanelMaxHeight = PanelMaxHeight,
            CandidateGap = CandidateGap,
            AnimationDurationMs = AnimationDurationMs,
            EdgePulseEnabled = EdgePulseEnabled,
            Theme = Theme,
            SkinId = SkinId,
            LargeText = LargeText,
            DiagnosticsEnabled = DiagnosticsEnabled
        };
        foreach (var (extension, preference) in FileTypes)
        {
            clone.FileTypes[extension] = new FileTypePreference
            {
                CandidateOrder = [.. (preference.CandidateOrder ?? [])],
                HiddenCandidateIds = [.. (preference.HiddenCandidateIds ?? [])],
                DisplayCount = preference.DisplayCount,
                CustomApps = (preference.CustomApps ?? []).Select(app => new CustomAppDefinition
                {
                    Id = app.Id,
                    DisplayName = app.DisplayName,
                    ExecutablePath = app.ExecutablePath,
                    Arguments = app.Arguments
                }).ToList()
            };
        }
        return clone;
    }
}

public sealed class FileTypePreference
{
    public List<string> CandidateOrder { get; set; } = [];
    public List<string> HiddenCandidateIds { get; set; } = [];
    public int? DisplayCount { get; set; }
    public List<CustomAppDefinition> CustomApps { get; set; } = [];
}

public interface IAppDiscovery { IReadOnlyList<AppCandidate> GetCandidates(IReadOnlyList<string> filePaths); }
public interface IOpenFileService { OpenResult Open(IReadOnlyList<string> filePaths, AppCandidate candidate); }
public interface ISettingsStore { UserSettings Load(); void Save(UserSettings settings); }
