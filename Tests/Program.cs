using EousGate;
using EousGate.Infrastructure;
using System.Diagnostics;

var tests = new (string Name, Action Run)[]
{
    ("DragSession stores an immutable ordered file snapshot", DragSessionStoresImmutableFiles),
    ("FileDropPolicy accepts one or more files", FileDropAcceptsFiles),
    ("FileDropPolicy rejects an empty input", FileDropRejectsEmptyInput),
    ("FileDropPolicy rejects a batch containing a folder", FileDropRejectsFolderBatch),
    ("Multi-file candidates keep only compatible common apps", MultiFileCandidatesIntersectCompatibleApps),
    ("AppDiscovery respects per-type visibility in a mixed batch", MultiFileDiscoveryRespectsVisibility),
    ("OpenPanelPlan limits candidates and exposes expansion", PanelPlanLimitsCandidates),
    ("OpenPanelPlan handles no candidates", PanelPlanHandlesNoCandidates),
    ("AppCandidateFilter removes unavailable and duplicate candidates", CandidateFilterRemovesUnavailableAndDuplicates),
    ("AppCandidateFilter keeps packaged Shell handlers", CandidateFilterKeepsPackagedHandlers),
    ("Packaged open failures stay inside the host", PackagedOpenFailureDoesNotCrashHost),
    ("AppDiscovery rejects blank extensions", DiscoveryRejectsBlankExtension),
    ("OpenFileService rejects an unavailable candidate", OpenUnavailableCandidateReturnsFailure),
    ("OpenFileService converts process errors into a failure", OpenProcessFailureReturnsFailure),
    ("JsonSettingsStore round-trips settings", SettingsRoundTrip),
    ("JsonSettingsStore falls back on malformed JSON", SettingsMalformedFallsBack)
    ,("UserSettings clone is independent", SettingsCloneIsIndependent)
    ,("ThemePalette creates distinct light and dark palettes", ThemePalettesAreDistinct)
    ,("ThemePalette preserves system colors in high contrast", HighContrastUsesSystemColors)
    ,("UserSettings appearance defaults are bounded", SettingsAppearanceDefaultsAreBounded)
    ,("Panel design catalog keeps scheme one as the default", PanelDesignSchemeOneIsDefault)
    ,("JsonSettingsStore normalizes unknown design schemes", SettingsNormalizeUnknownDesignScheme)
    ,("Skin catalog keeps three unique materials and normalizes ids", SkinCatalogNormalizesIds)
    ,("ThemePalette creates distinct skin surfaces", SkinPalettesAreDistinct)
    ,("DWM backdrop types match the three skin materials", SkinBackdropTypesAreDistinct)
    ,("JsonSettingsStore normalizes unknown skins", SettingsNormalizeUnknownSkin)
    ,("UserSettings clone deep-copies custom apps", SettingsCloneCopiesCustomApps)
    ,("Json settings round-trip custom apps", SettingsRoundTripCustomApps)
    ,("Legacy settings default custom apps to empty", LegacySettingsHaveEmptyCustomApps)
    ,("Custom discovery normalizes and de-duplicates paths", CustomDiscoveryDeDuplicatesPaths)
    ,("Unavailable custom apps remain only in settings view", UnavailableCustomAppsAreViewOnly)
     ,("OpenFileService appends custom arguments", OpenCustomArgumentsAreAppended)
    ,("Edge band position round-trips", EdgeBandPositionRoundTrips)
    ,("DPI pixel conversion preserves scaled coordinates", DpiPixelConversionPreservesCoordinates)
    ,("Overlay placement clamps all edges", OverlayPlacementClampsAllEdges)
    ,("Auto-dismiss delay defaults and clones", AutoDismissDelayDefaultsAndClones)
    ,("Auto-dismiss delay round-trips and keeps legacy defaults", AutoDismissDelayRoundTripsAndLegacyDefaults)
    ,("Overlay dismissal safe area covers panel and source edge", OverlayDismissalSafeAreaCoversPanelAndEdge)
    ,("Overlay dismissal timing honors zero and configured delays", OverlayDismissalTimingHonorsDelay)
    ,("Edge band layouts default and clone independently", EdgeBandLayoutsDefaultAndClone)
    ,("Edge band layouts round-trip and preserve legacy fallback", EdgeBandLayoutsRoundTripAndLegacyFallback)
    ,("Edge band layout bounds honor percentages, offsets, and clamping", EdgeBandLayoutBoundsHonorConfiguration)
    ,("Diagnostics stay disabled by default", DiagnosticsDisabledByDefault)
    ,("Diagnostics write only fixed event names when enabled", DiagnosticsWriteFixedEvents)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL {test.Name}: {ex.Message}");
        Console.WriteLine(failures[^1]);
    }
}

Console.WriteLine($"{tests.Length - failures.Count}/{tests.Length} tests passed");
return failures.Count == 0 ? 0 : 1;

static void DragSessionStoresImmutableFiles()
{
    var source = new[] { "C:\\one.txt", "C:\\two.txt" };
    var session = new DragSession(source);
    source[0] = "C:\\changed.txt";
    Assert(session.State == DragSessionState.Detecting, "new sessions must detect first");
    Assert(session.FileCount == 2 && session.PrimaryFilePath == "C:\\one.txt", "the ordered file snapshot must be preserved");
    session.State = DragSessionState.Cancelled;
    Assert(session.State == DragSessionState.Cancelled, "sessions must support cancellation");
}

static void FileDropAcceptsFiles()
{
    var accepted = FileDropPolicy.TryGetFiles(["C:\\one.txt", "C:\\two.jpg"], _ => false, out var filePaths);
    Assert(accepted && filePaths is { Count: 2 } && filePaths[0] == "C:\\one.txt" && filePaths[1] == "C:\\two.jpg", "one or more non-directory paths must be accepted in order");
}

static void FileDropRejectsEmptyInput()
{
    var accepted = FileDropPolicy.TryGetFiles([], _ => false, out var filePaths);
    Assert(!accepted && filePaths is null, "an empty drag payload must be rejected");
}

static void FileDropRejectsFolderBatch()
{
    var accepted = FileDropPolicy.TryGetFiles(["C:\\one.txt", "C:\\sample-folder"], path => path.EndsWith("folder"), out var filePaths);
    Assert(!accepted && filePaths is null, "a batch containing a directory must be rejected as a whole");
}

static void MultiFileCandidatesIntersectCompatibleApps()
{
    var first = new AppCandidate[]
    {
        new("common", "Common", "C:\\common.exe", null, true),
        new("conflict", "Conflict", "C:\\conflict.exe", "--text", true),
        new("first-only", "First", "C:\\first.exe", null, true)
    };
    var second = new AppCandidate[]
    {
        new("COMMON", "Common other label", "C:\\common.exe", null, true),
        new("conflict", "Conflict", "C:\\conflict.exe", "--image", true)
    };
    var result = MultiFileCandidateFilter.Intersect([first, second]);
    Assert(result.Count == 1 && result[0].Id == "common" && result[0].DisplayName == "Common", "intersection must preserve first-type order and exclude incompatible custom arguments");
    Assert(MultiFileCandidateFilter.Intersect([first, Array.Empty<AppCandidate>()]).Count == 0, "one empty type must make the common candidate list empty");
}

static void MultiFileDiscoveryRespectsVisibility()
{
    var executable = AppDiscovery.NormalizeExecutablePath(Environment.ProcessPath!);
    var settings = new UserSettings();
    settings.FileTypes[".egone"] = new FileTypePreference
    {
        CustomApps = [new CustomAppDefinition { DisplayName = "Common", ExecutablePath = executable }]
    };
    settings.FileTypes[".egtwo"] = new FileTypePreference
    {
        CustomApps = [new CustomAppDefinition { DisplayName = "Common", ExecutablePath = executable }]
    };
    var discovery = new AppDiscovery(() => settings);
    Assert(discovery.GetCandidates(["C:\\one.egone", "C:\\two.egtwo"]).Any(candidate => string.Equals(candidate.Id, executable, StringComparison.OrdinalIgnoreCase)), "a custom app visible for every type must remain available");
    settings.FileTypes[".egtwo"].HiddenCandidateIds.Add(executable);
    Assert(discovery.GetCandidates(["C:\\one.egone", "C:\\two.egtwo"]).All(candidate => !string.Equals(candidate.Id, executable, StringComparison.OrdinalIgnoreCase)), "an app hidden for one type must not remain in the mixed batch");
}

static void PanelPlanLimitsCandidates()
{
    var candidates = Enumerable.Range(1, 5).Select(i => new AppCandidate($"id{i}", $"App {i}", $"C:\\app{i}.exe", null, true)).ToArray();
    var plan = OpenPanelPlan.Create(candidates, 4);
    Assert(plan.VisibleCandidates.Count == 4 && plan.CanExpand, "four candidates show first four and allow expansion");
}

static void PanelPlanHandlesNoCandidates()
{
    var plan = OpenPanelPlan.Create([], 4);
    Assert(plan.VisibleCandidates.Count == 0 && !plan.CanExpand, "empty candidate lists stay empty without expansion");
}

static void CandidateFilterRemovesUnavailableAndDuplicates()
{
    var candidates = new[]
    {
        new AppCandidate("one", "One", "C:\\Apps\\One.exe", null, true),
        new AppCandidate("duplicate", "Duplicate", "c:\\apps\\one.exe", null, true),
        new AppCandidate("missing", "Missing", "C:\\Apps\\Missing.exe", null, false)
    };
    var filtered = AppCandidateFilter.Filter(candidates, path => path.EndsWith("One.exe", StringComparison.OrdinalIgnoreCase), path => path.Trim());
    Assert(filtered.Count == 1 && filtered[0].Id == "C:\\Apps\\One.exe", "only one available normalized candidate remains");
}

static void CandidateFilterKeepsPackagedHandlers()
{
    var packaged = new AppCandidate(
        "packaged:Microsoft.Windows.Photos_8wekyb3d8bbwe!App",
        "照片",
        "shell:AppsFolder\\Microsoft.Windows.Photos_8wekyb3d8bbwe!App",
        null,
        true,
        null,
        "Microsoft.Windows.Photos_8wekyb3d8bbwe!App");
    var filtered = AppCandidateFilter.Filter([packaged], _ => false, path => throw new InvalidOperationException("packaged candidates must not normalize paths"));
    Assert(filtered.Count == 1 && filtered[0].DisplayName == "照片" && filtered[0].IsPackaged, "packaged Shell handlers must remain available without an executable path");
}

static void PackagedOpenFailureDoesNotCrashHost()
{
    ProcessStartInfo? captured = null;
    using var process = new Process();
    var service = new OpenFileService(info => { captured = info; return process; });
    var candidate = new AppCandidate(
        "packaged:Contoso.Package_123!App",
        "测试系统应用",
        "shell:AppsFolder\\Contoso.Package_123!App",
        null,
        true,
        null,
        "Contoso.Package_123!App");
    var result = service.Open(["C:\\sample.jpg", "C:\\sample-2.jpg"], candidate);
    Assert(result.Success && captured?.FileName.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) == true, "packaged activation failure must fall back to an isolated Explorer process");
    Assert(captured!.Arguments.Contains("\"C:\\sample.jpg\"") && captured.Arguments.Contains("\"C:\\sample-2.jpg\""), "packaged fallback must receive every file path");
}

static void DiscoveryRejectsBlankExtension()
{
    var discovery = new AppDiscovery();
    Assert(discovery.GetCandidatesForExtension("   ", false).Count == 0, "blank extensions have no candidates");
}

static void OpenUnavailableCandidateReturnsFailure()
{
    var service = new OpenFileService();
    var candidate = new AppCandidate("missing", "Missing", "Z:\\does-not-exist\\missing.exe", null, false);
    var result = service.Open(["C:\\sample.txt"], candidate);
    Assert(!result.Success, "missing executable must not report success");
    Assert(!string.IsNullOrWhiteSpace(result.Error), "failure should carry an error");
}

static void OpenProcessFailureReturnsFailure()
{
    var executable = Environment.ProcessPath ?? throw new InvalidOperationException("test process path is unavailable");
    var service = new OpenFileService(_ => throw new System.ComponentModel.Win32Exception("simulated Windows failure"));
    var candidate = new AppCandidate("test", "Test", executable, null, true);
    var result = service.Open(["C:\\sample.txt"], candidate);
    Assert(!result.Success && !string.IsNullOrWhiteSpace(result.Error), "process exceptions must become an open failure");
}

static void DiagnosticsDisabledByDefault()
{
    var path = Path.Combine(Path.GetTempPath(), "EousGate-tests", Guid.NewGuid().ToString("N"), "diagnostics.log");
    try
    {
        new DiagnosticsLogger(path: path).Log("processstartfailed");
        Assert(!File.Exists(path), "diagnostics must not create a file while disabled");
    }
    finally { Delete(path); }
}

static void DiagnosticsWriteFixedEvents()
{
    var path = Path.Combine(Path.GetTempPath(), "EousGate-tests", Guid.NewGuid().ToString("N"), "diagnostics.log");
    try
    {
        var logger = new DiagnosticsLogger(() => true, path);
        logger.Log("processstartfailed");
        logger.Log("packagedfileactivationfailed");
        logger.Log("packagedprocessstartfailed");
        logger.Log("process-start.failed C:\\secret\\sample.txt");
        var content = File.ReadAllText(path);
        Assert(content.Contains("processstartfailed") && content.Contains("packagedfileactivationfailed") && content.Contains("packagedprocessstartfailed"), "enabled diagnostics must record fixed event names");
        Assert(!content.Contains("secret", StringComparison.OrdinalIgnoreCase) && !content.Contains("sample.txt", StringComparison.OrdinalIgnoreCase), "diagnostics must not contain file paths or names");
    }
    finally { Delete(path); }
}

static void SettingsRoundTrip()
{
    var path = TestPath();
    try
    {
        var store = new JsonSettingsStore(path);
        var settings = new UserSettings { CandidateCount = 7, Theme = "Dark", Paused = true, DesignSchemeId = "icon-grid", SkinId = "acrylic-glass" };
        store.Save(settings);
        var loaded = store.Load();
        Assert(loaded.CandidateCount == 7 && loaded.Theme == "Dark" && loaded.Paused && loaded.DesignSchemeId == "icon-grid" && loaded.SkinId == "acrylic-glass", "saved values, design scheme, and skin must be restored");
    }
    finally { Delete(path); }
}

static void SettingsMalformedFallsBack()
{
    var path = TestPath();
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ malformed");
        var loaded = new JsonSettingsStore(path).Load();
        Assert(loaded is not null && loaded.CandidateCount == 4 && loaded.Theme == "System", "malformed settings must use defaults");
    }
    finally { Delete(path); }
}

static void SettingsCloneIsIndependent()
{
    var source = new UserSettings { Theme = "Dark", SkinId = "clear-surface" };
    source.FileTypes[".txt"] = new FileTypePreference { CandidateOrder = ["one"] };
    var clone = source.Clone();
    clone.Theme = "Light";
    clone.SkinId = "acrylic-glass";
    clone.FileTypes[".txt"].CandidateOrder.Add("two");
    Assert(source.Theme == "Dark" && source.SkinId == "clear-surface" && source.FileTypes[".txt"].CandidateOrder.Count == 1, "editing a settings draft must not modify the active settings");
}

static void SettingsCloneCopiesCustomApps()
{
    var source = new UserSettings();
    source.FileTypes[".txt"] = new FileTypePreference
    {
        CustomApps = [new CustomAppDefinition { Id = "one", DisplayName = "One", ExecutablePath = "C:\\one.exe", Arguments = "--safe" }]
    };
    var clone = source.Clone();
    clone.FileTypes[".txt"].CustomApps[0].DisplayName = "Changed";
    clone.FileTypes[".txt"].CustomApps.Add(new CustomAppDefinition { DisplayName = "Two", ExecutablePath = "C:\\two.exe" });
    Assert(source.FileTypes[".txt"].CustomApps.Count == 1 && source.FileTypes[".txt"].CustomApps[0].DisplayName == "One", "custom app objects and lists must be independent");
}

static void SettingsRoundTripCustomApps()
{
    var path = TestPath();
    try
    {
        var store = new JsonSettingsStore(path);
        var executable = AppDiscovery.NormalizeExecutablePath(Environment.ProcessPath!);
        store.Save(new UserSettings
        {
            FileTypes = new Dictionary<string, FileTypePreference>(StringComparer.OrdinalIgnoreCase)
            {
                [".txt"] = new FileTypePreference
                {
                    CandidateOrder = [executable], HiddenCandidateIds = ["hidden"], DisplayCount = 2,
                    CustomApps = [new CustomAppDefinition { DisplayName = "My App", ExecutablePath = executable, Arguments = "--flag" }]
                }
            }
        });
        var loaded = store.Load().FileTypes[".txt"];
        Assert(loaded.DisplayCount == 2 && loaded.HiddenCandidateIds.Contains("hidden") && loaded.CustomApps.Count == 1 && loaded.CustomApps[0].DisplayName == "My App" && loaded.CustomApps[0].Arguments == "--flag", "custom app settings must survive JSON round-trip");
        Assert(loaded.CustomApps[0].Id == loaded.CustomApps[0].ExecutablePath, "custom app id must be normalized to its executable path");
    }
    finally { Delete(path); }
}

static void LegacySettingsHaveEmptyCustomApps()
{
    var path = TestPath();
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"FileTypes\":{\".txt\":{\"CandidateOrder\":[],\"HiddenCandidateIds\":[]}}}");
        var loaded = new JsonSettingsStore(path).Load();
        Assert(loaded.FileTypes[".txt"].CustomApps is not null && loaded.FileTypes[".txt"].CustomApps.Count == 0, "legacy settings without CustomApps must load with an empty list");
    }
    finally { Delete(path); }
}

static void CustomDiscoveryDeDuplicatesPaths()
{
    var executable = AppDiscovery.NormalizeExecutablePath(Environment.ProcessPath!);
    var settings = new UserSettings();
    settings.FileTypes[".egtest"] = new FileTypePreference
    {
        CustomApps =
        [
            new CustomAppDefinition { DisplayName = "First", ExecutablePath = executable, Arguments = "--one" },
            new CustomAppDefinition { DisplayName = "Duplicate", ExecutablePath = executable.ToUpperInvariant(), Arguments = "--two" }
        ]
    };
    var candidates = new AppDiscovery(() => settings).GetCandidatesForExtension(".egtest", false);
    Assert(candidates.Count(c => string.Equals(c.ExecutablePath, executable, StringComparison.OrdinalIgnoreCase)) == 1, "duplicate custom executable paths must produce one candidate");
    var candidate = candidates.Single(c => string.Equals(c.ExecutablePath, executable, StringComparison.OrdinalIgnoreCase));
    Assert(candidate.DisplayName == "First" && candidate.Arguments == "--one", "first custom definition must win after de-duplication");
}

static void UnavailableCustomAppsAreViewOnly()
{
    var settings = new UserSettings();
    settings.FileTypes[".egtest"] = new FileTypePreference
    {
        CustomApps = [new CustomAppDefinition { DisplayName = "Missing", ExecutablePath = "Z:\\missing-eousgate.exe" }]
    };
    var discovery = new AppDiscovery(() => settings);
    var runtimeCandidates = discovery.GetCandidatesForExtension(".egtest", true);
    Assert(runtimeCandidates.All(candidate => !candidate.ExecutablePath.EndsWith("missing-eousgate.exe", StringComparison.OrdinalIgnoreCase)), "unavailable custom apps must not enter runtime candidates");
    var viewCandidates = discovery.GetCandidatesForExtension(".egtest", false);
    Assert(viewCandidates.Any(candidate => candidate.ExecutablePath.EndsWith("missing-eousgate.exe", StringComparison.OrdinalIgnoreCase) && !candidate.IsAvailable), "settings view must retain unavailable custom apps");
}

static void OpenCustomArgumentsAreAppended()
{
    ProcessStartInfo? captured = null;
    var starts = 0;
    using var process = new System.Diagnostics.Process();
    var service = new OpenFileService(info => { starts++; captured = info; return process; });
    var candidate = new AppCandidate("custom", "Custom", Environment.ProcessPath!, "--custom-flag", true);
    var result = service.Open(["C:\\first file.txt", "C:\\second file.txt"], candidate);
    Assert(result.Success && captured is not null, "custom candidate should be started");
    var firstIndex = captured!.Arguments.IndexOf("\"C:\\first file.txt\"", StringComparison.Ordinal);
    var secondIndex = captured.Arguments.IndexOf("\"C:\\second file.txt\"", StringComparison.Ordinal);
    var customIndex = captured.Arguments.IndexOf("--custom-flag", StringComparison.Ordinal);
    Assert(starts == 1 && firstIndex >= 0 && secondIndex > firstIndex && customIndex > secondIndex, "all file paths must be passed in order to one process before custom arguments");
}

static void EdgeBandPositionRoundTrips()
{
    var path = TestPath();
    try
    {
        var store = new JsonSettingsStore(path);
        store.Save(new UserSettings { EdgeBandPosition = "Right,Top" });
        Assert(store.Load().EdgeBandPosition == "Right,Top", "selected edge band positions must persist as a combination");
    }
    finally { Delete(path); }
}

static void DpiPixelConversionPreservesCoordinates()
{
    Assert(Math.Abs(DpiCoordinateConverter.PixelsToDip(1500, 1.5) - 1000) < 0.001, "150% pixels must convert to DIPs");
    Assert(Math.Abs(DpiCoordinateConverter.PixelsToDip(-1920, 2) + 960) < 0.001, "negative screen coordinates must convert correctly");
}

static void OverlayPlacementClampsAllEdges()
{
    var area = new System.Windows.Rect(0, 0, 2560, 1440);
    var left = OverlayPlacementMath.Calculate(area, new System.Windows.Point(0, 10), 404, 320, OverlayPlacementSide.Left);
    var right = OverlayPlacementMath.Calculate(area, new System.Windows.Point(2560, 700), 404, 320, OverlayPlacementSide.Right);
    var top = OverlayPlacementMath.Calculate(area, new System.Windows.Point(1280, 0), 404, 320, OverlayPlacementSide.Top);
    Assert(left.Left >= area.Left && left.Top >= area.Top, "left placement must stay in the work area");
    Assert(right.Right <= area.Right && right.Bottom <= area.Bottom, "right placement must stay in the work area");
    Assert(top.Left >= area.Left && top.Right <= area.Right && top.Top >= area.Top, "top placement must stay in the work area");
}

static void AutoDismissDelayDefaultsAndClones()
{
    var settings = new UserSettings();
    Assert(settings.AutoDismissDelayMs == 500, "auto-dismiss delay must default to 500ms");
    var clone = settings.Clone();
    clone.AutoDismissDelayMs = 10000;
    Assert(settings.AutoDismissDelayMs == 500 && clone.AutoDismissDelayMs == 10000, "auto-dismiss delay must clone independently");
}

static void AutoDismissDelayRoundTripsAndLegacyDefaults()
{
    var path = TestPath();
    try
    {
        var store = new JsonSettingsStore(path);
        store.Save(new UserSettings { AutoDismissDelayMs = 0 });
        Assert(store.Load().AutoDismissDelayMs == 0, "zero auto-dismiss delay must survive JSON round-trip");
        store.Save(new UserSettings { AutoDismissDelayMs = 20000 });
        Assert(store.Load().AutoDismissDelayMs == 10000, "persisted delays above the supported range must clamp to 10000ms");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"CandidateCount\":4}");
        Assert(new JsonSettingsStore(path).Load().AutoDismissDelayMs == 500, "legacy settings must use the auto-dismiss default");
    }
    finally { Delete(path); }
}

static void OverlayDismissalSafeAreaCoversPanelAndEdge()
{
    var area = new System.Drawing.Rectangle(0, 0, 2560, 1440);
    var panel = new System.Drawing.Rectangle(20, 400, 404, 320);
    var leftEdge = EdgeBandLayoutMath.CalculateBounds(area, OverlayPlacementSide.Left, new EdgeBandLayout(), 8);
    var rightEdge = EdgeBandLayoutMath.CalculateBounds(area, OverlayPlacementSide.Right, new EdgeBandLayout(), 8);
    var topEdge = EdgeBandLayoutMath.CalculateBounds(area, OverlayPlacementSide.Top, new EdgeBandLayout(), 8);
    Assert(OverlayDismissalPolicy.IsWithinSafeArea(new System.Drawing.Point(100, 500), panel, leftEdge), "panel points must be safe");
    Assert(OverlayDismissalPolicy.IsWithinSafeArea(new System.Drawing.Point(2, 500), panel, leftEdge), "left edge points must be safe");
    Assert(OverlayDismissalPolicy.IsWithinSafeArea(new System.Drawing.Point(2558, 500), panel, rightEdge), "right edge points must be safe");
    Assert(OverlayDismissalPolicy.IsWithinSafeArea(new System.Drawing.Point(1000, 2), panel, topEdge), "top edge points must be safe");
    Assert(!OverlayDismissalPolicy.IsWithinSafeArea(new System.Drawing.Point(1000, 900), panel, leftEdge), "points outside panel and edge must be unsafe");
}

static void OverlayDismissalTimingHonorsDelay()
{
    var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    Assert(!OverlayDismissalPolicy.HasElapsed(null, start, 0), "without an outside timestamp dismissal must not start");
    Assert(OverlayDismissalPolicy.HasElapsed(start, start, 0), "zero delay must dismiss immediately");
    Assert(!OverlayDismissalPolicy.HasElapsed(start, start.AddMilliseconds(499), 500), "configured delay must not elapse early");
    Assert(OverlayDismissalPolicy.HasElapsed(start, start.AddMilliseconds(500), 500), "configured delay must elapse at its boundary");
    Assert(OverlayDismissalPolicy.HasElapsed(start, start.AddSeconds(10), 20000), "delays above the supported range must clamp to 10000ms");
}

static void EdgeBandLayoutsDefaultAndClone()
{
    var settings = new UserSettings();
    Assert(settings.LeftEdgeBand is not null && settings.RightEdgeBand is not null && settings.TopEdgeBand is not null, "new settings must include per-side edge layouts");
    Assert(settings.LeftEdgeBand!.LengthPercent == 100 && settings.LeftEdgeBand.WidthPercent == 0.5 && settings.LeftEdgeBand.OffsetPixels == 0, "edge layout defaults must be 100%, 0.5%, and 0px");
    var clone = settings.Clone();
    clone.LeftEdgeBand!.LengthPercent = 40;
    clone.RightEdgeBand!.OffsetPixels = 80;
    Assert(settings.LeftEdgeBand!.LengthPercent == 100 && settings.RightEdgeBand!.OffsetPixels == 0, "edge layout objects must clone independently");
}

static void EdgeBandLayoutsRoundTripAndLegacyFallback()
{
    var path = TestPath();
    try
    {
        var store = new JsonSettingsStore(path);
        store.Save(new UserSettings
        {
            LeftEdgeBand = new EdgeBandLayout { LengthPercent = 40, WidthPercent = 1.2, OffsetPixels = -30 },
            RightEdgeBand = new EdgeBandLayout { LengthPercent = 70, WidthPercent = 0.8, OffsetPixels = 45 },
            TopEdgeBand = new EdgeBandLayout { LengthPercent = 55, WidthPercent = 0.6, OffsetPixels = 12 }
        });
        var loaded = store.Load();
        Assert(loaded.LeftEdgeBand!.LengthPercent == 40 && loaded.RightEdgeBand!.OffsetPixels == 45 && loaded.TopEdgeBand!.WidthPercent == 0.6, "per-side edge layouts must survive JSON round-trip");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"EdgeBandWidth\":12}");
        var legacy = new JsonSettingsStore(path).Load();
        Assert(legacy.LeftEdgeBand is null && legacy.RightEdgeBand is null && legacy.TopEdgeBand is null, "legacy settings must keep per-side layouts unset");
        var bounds = EdgeBandLayoutMath.CalculateBounds(new System.Drawing.Rectangle(0, 0, 1920, 1080), OverlayPlacementSide.Left, legacy.LeftEdgeBand, legacy.EdgeBandWidth);
        Assert(bounds.Width == 12 && bounds.Height == 1080, "legacy edge width must remain the fallback when no per-side layout exists");
    }
    finally { Delete(path); }
}

static void EdgeBandLayoutBoundsHonorConfiguration()
{
    var area = new System.Drawing.Rectangle(0, 0, 2560, 1440);
    var left = EdgeBandLayoutMath.CalculateBounds(area, OverlayPlacementSide.Left, new EdgeBandLayout { LengthPercent = 50, WidthPercent = 0.5, OffsetPixels = 100 }, 8);
    Assert(left.Width == 7 && left.Height == 720 && left.Top == 460, "left layout must use short-edge width, half length, and vertical offset");

    var right = EdgeBandLayoutMath.CalculateBounds(area, OverlayPlacementSide.Right, new EdgeBandLayout { LengthPercent = 70, WidthPercent = 1, OffsetPixels = -200 }, 8);
    Assert(right.Right == area.Right && right.Height == 1008 && right.Top == 16, "right layout must anchor to the right edge and offset from center");

    var top = EdgeBandLayoutMath.CalculateBounds(area, OverlayPlacementSide.Top, new EdgeBandLayout { LengthPercent = 40, WidthPercent = 1, OffsetPixels = 5000 }, 8);
    Assert(top.Width == 1024 && top.Height == 14 && top.Right == area.Right, "top layout must clamp an oversized horizontal offset to the work area");
}

static void ThemePalettesAreDistinct()
{
    var light = ThemePalette.Create("Light", true, false);
    var dark = ThemePalette.Create("Dark", false, false);
    Assert(light.Background != dark.Background && light.Foreground != dark.Foreground && light.SurfaceSoft != dark.SurfaceSoft, "light and dark palettes must differ across surfaces");
}

static void HighContrastUsesSystemColors()
{
    var palette = ThemePalette.Create("Dark", true, true);
    Assert(palette.Background == "SystemWindow" && palette.Foreground == "SystemWindowText" && palette.Accent == "SystemHighlight", "high contrast must use Windows system colors");
}

static void SettingsAppearanceDefaultsAreBounded()
{
    var settings = new UserSettings();
    Assert(settings.PanelWidth is >= 240 and <= 560, "panel width default must fit the supported range");
    Assert(settings.PanelMaxHeight is >= 300 and <= 900, "panel height default must fit the supported range");
    Assert(settings.CandidateGap is >= 0 and <= 16, "candidate gap default must fit the supported range");
    Assert(settings.AnimationDurationMs is >= 80 and <= 600 && settings.AutoDismissDelayMs is >= 0 and <= 10000 && settings.EdgePulseEnabled && settings.EdgeBandPosition == "Left,Right", "animation, auto-dismiss and edge position defaults must be enabled and bounded");
}

static void PanelDesignSchemeOneIsDefault()
{
    var settings = new UserSettings();
    Assert(settings.DesignSchemeId == PanelDesignCatalog.DefaultId, "new settings must select the default design scheme");
    Assert(PanelDesignCatalog.All.Count == 3, "design center must expose three schemes");
    Assert(PanelDesignCatalog.All[0].Sequence == "方案一" && PanelDesignCatalog.All[0].IsDefault, "scheme one must remain the first default entry");
    Assert(PanelDesignCatalog.Normalize("compact-list") == "compact-list", "compact list scheme must normalize by stable id");
    Assert(PanelDesignCatalog.Normalize("icon-grid") == "icon-grid", "icon grid scheme must normalize by stable id");
    Assert(PanelDesignCatalog.Normalize("unknown") == PanelDesignCatalog.DefaultId, "unknown saved schemes must fall back safely");
}

static void SettingsNormalizeUnknownDesignScheme()
{
    var path = TestPath();
    try
    {
        var store = new JsonSettingsStore(path);
        store.Save(new UserSettings { DesignSchemeId = "removed-scheme", EdgeBandPosition = "diagonal" });
        var loaded = store.Load();
        Assert(loaded.DesignSchemeId == PanelDesignCatalog.DefaultId && loaded.EdgeBandPosition == "Left,Right", "unknown design and edge positions must load safely");
    }
    finally { Delete(path); }
}

static void SkinCatalogNormalizesIds()
{
    var ids = SkinCatalog.All.Select(skin => skin.Id).ToArray();
    Assert(ids.Length == 3 && ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3, "skin catalog must contain three unique ids");
    Assert(SkinCatalog.All[0].Id == SkinCatalog.DefaultId && SkinCatalog.All[0].IsDefault, "Win11 Mica must remain the default skin");
    Assert(SkinCatalog.Normalize("acrylic-glass") == "acrylic-glass", "acrylic skin must normalize by stable id");
    Assert(SkinCatalog.Normalize("clear-surface") == "clear-surface", "clear skin must normalize by stable id");
    Assert(SkinCatalog.Normalize("unknown") == SkinCatalog.DefaultId, "unknown skins must fall back safely");
}

static void SkinPalettesAreDistinct()
{
    var mica = ThemePalette.Create("Light", false, false, "win11-mica");
    var acrylic = ThemePalette.Create("Light", false, false, "acrylic-glass");
    var clear = ThemePalette.Create("Light", false, false, "clear-surface");
    Assert(mica.Background != acrylic.Background && acrylic.Background != clear.Background, "skin surfaces must have distinct backgrounds");
    Assert(acrylic.Border != clear.Border && acrylic.SurfaceSoft != clear.SurfaceSoft, "glass and clear skins must differ in supporting surfaces");
    Assert(!acrylic.Background.StartsWith("#FF", StringComparison.OrdinalIgnoreCase) && !clear.Background.StartsWith("#FF", StringComparison.OrdinalIgnoreCase), "glass skins must use transparent background layers");
}

static void SkinBackdropTypesAreDistinct()
{
    Assert(MicaWindowHelper.GetBackdropTypeForSkin("win11-mica") == 2, "Mica must use DWMSBT_MAINWINDOW");
    Assert(MicaWindowHelper.GetBackdropTypeForSkin("acrylic-glass") == 4, "Acrylic must use DWMSBT_TRANSIENTWINDOW");
    Assert(MicaWindowHelper.GetBackdropTypeForSkin("clear-surface") == 1, "Clear must disable the DWM backdrop");
    Assert(MicaWindowHelper.GetBackdropTypeForSkin("unknown") == 2, "unknown skins must use the safe Mica fallback");
}

static void SettingsNormalizeUnknownSkin()
{
    var path = TestPath();
    try
    {
        var store = new JsonSettingsStore(path);
        store.Save(new UserSettings { SkinId = "removed-skin" });
        var loaded = store.Load();
        Assert(loaded.SkinId == SkinCatalog.DefaultId, "unknown saved skins must load as the default");
    }
    finally { Delete(path); }
}

static string TestPath() => Path.Combine(Path.GetTempPath(), "EousGate-tests", Guid.NewGuid().ToString("N"), "settings.json");

static void Delete(string path)
{
    var directory = Path.GetDirectoryName(path);
    if (File.Exists(path)) File.Delete(path);
    if (directory is not null && Directory.Exists(directory)) Directory.Delete(directory, true);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
