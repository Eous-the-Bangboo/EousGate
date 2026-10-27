using EousGate;
using EousGate.Infrastructure;
using System.Text.Json;

internal static class AssistantTests
{
    internal static void FolderHistory()
    {
        var root = Path.Combine(Path.GetTempPath(), "EousAssistant-folder-" + Guid.NewGuid());
        var old = new EousGate.FolderHop.HistoryStore(Path.Combine(root, "old"));
        var first = old.Touch(@"C:\中文 路径\folder [1]"); first.Favorite = true; first.VisitedUtc = 100;
        old.Touch(@"C:\another").VisitedUtc = 200;
        Check(old.Save(), old.Warning);
        var source = Path.Combine(old.DirectoryPath, "history.json"); var bytes = File.ReadAllBytes(source);
        var current = new EousGate.FolderHop.HistoryStore(Path.Combine(root, "new"));
        current.Touch(@"c:\中文 路径\folder [1]").VisitedUtc = 300;
        Check(current.Import(source) == 2 && current.Data.Folders.Count == 2, "import must merge and deduplicate");
        var merged = current.Data.Folders.Single(x => x.Favorite);
        Check(merged.VisitedUtc == 300 && current.Query(1, "中文", []).Count == 1, "merge keeps favorites, latest visit and Unicode search");
        Check(bytes.SequenceEqual(File.ReadAllBytes(source)), "import source must not be modified");
        current.ClearRecent(); Check(current.Save(), current.Warning);
        var restored = new EousGate.FolderHop.HistoryStore(current.DirectoryPath);
        Check(restored.Data.Folders.Count == 1 && restored.Data.Folders[0].Favorite, "clear preserves favorites across restart");
        Check(!File.Exists(Path.Combine(current.DirectoryPath, "history.json.bak")), "clear removes stale history backup");
        Check(EousGate.FolderHop.HistoryStore.Normalize("shell:AppsFolder") is null, "reject virtual paths");
        Check(EousGate.FolderHop.HistoryStore.Normalize(@"\\server\share\") == @"\\server\share\", "UNC root preserved");
        var invalid = Path.Combine(root, "invalid.json"); File.WriteAllText(invalid, "broken");
        try { current.Import(invalid); throw new Exception("invalid import accepted"); }
        catch (System.Runtime.Serialization.SerializationException) { }
        Check(current.Data.Folders.Count == 1, "invalid import must not change current data");
    }
    internal static void Lifecycle()
    {
        var manager = new ToolManager();
        var starts = 0; var stops = 0;
        var good = new ManagedTool(new("good", "Good", "正常工具", "", ""),
            () => { starts++; return Task.CompletedTask; }, () => { stops++; return Task.CompletedTask; }, () => { });
        var bad = new ManagedTool(new("bad", "Bad", "失败工具", "", ""),
            () => throw new InvalidOperationException("模拟启动失败"), () => Task.CompletedTask, () => { });
        manager.Register(bad); manager.Register(good);
        manager.RunAsync(true, true).GetAwaiter().GetResult();
        Check(bad.State == ToolState.Failed && bad.Error!.Contains("模拟"), "failure must be visible");
        Check(good.State == ToolState.Running && starts == 1, "a failed tool must not block the next one");
        good.StartAsync().GetAwaiter().GetResult();
        Check(starts == 1, "start must be idempotent");
        good.Selected = false;
        manager.RunAsync(false, true).GetAwaiter().GetResult();
        Check(good.State == ToolState.Running, "selected stop must leave unselected tools running");
        manager.RunAsync(false, false).GetAwaiter().GetResult();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var concurrentStarts = 0; var concurrentStops = 0;
        var concurrent = new ManagedTool(new("concurrent", "Concurrent", "并发工具", "", ""),
            async () => { concurrentStarts++; await release.Task; }, () => { concurrentStops++; return Task.CompletedTask; }, () => { });
        var firstStart = concurrent.StartAsync(); var repeatedStart = concurrent.StartAsync(); var queuedStop = concurrent.StopAsync();
        Check(concurrent.State == ToolState.Starting, "pending start must expose transition state");
        release.SetResult(); Task.WhenAll(firstStart, repeatedStart, queuedStop).GetAwaiter().GetResult();
        Check(concurrentStarts == 1 && concurrentStops == 1 && concurrent.State == ToolState.Stopped, "concurrent repeated actions must serialize without duplicate resources");
        good.StopAsync().GetAwaiter().GetResult();
        Check(good.State == ToolState.Stopped && stops == 1, "stop all and repeated stop");
        bad.Selected = false;
        manager.RunAsync(true, true).GetAwaiter().GetResult();
        Check(starts == 1, "empty selection is a no-op");
        manager.RunAsync(true, false).GetAwaiter().GetResult();
        Check(starts == 2 && good.State == ToolState.Running, "start all ignores selection");
        manager.RunAsync(false, false).GetAwaiter().GetResult();
    }
    internal static void Migration()
    {
        var root = Path.Combine(Path.GetTempPath(), "EousAssistant-migration-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var legacy = Path.Combine(root, "legacy.json"); var current = Path.Combine(root, "settings.json");
        var original = new UserSettings { Theme = "Dark", ClipboardLinksEnabled = false, Paused = false };
        original.FileTypes[".txt"] = new() { CandidateOrder = ["notepad"], CustomApps = [new() { ExecutablePath = Environment.ProcessPath!, DisplayName = "Mine" }] };
        File.WriteAllText(legacy, JsonSerializer.Serialize(original));
        var bytes = File.ReadAllBytes(legacy);
        var store = new JsonSettingsStore(current, legacyPath: legacy);
        var settings = store.Load();
        Check(File.Exists(current) && settings.Theme == "Dark" && !settings.ClipboardLinksEnabled, "legacy migration: " + store.LastError);
        Check(!settings.ToolSelections["web-hop"] && settings.ToolSelections["file-hop"], "respect old disabled settings");
        Check(bytes.SequenceEqual(File.ReadAllBytes(legacy)), "legacy source must remain unchanged");
        Check(settings.FileTypes[".txt"].CandidateOrder[0] == "notepad" && settings.FileTypes[".txt"].CustomApps.Count == 1, "preserve app preferences");
        var clone = settings.Clone(); clone.ToolSelections["file-hop"] = false;
        Check(settings.ToolSelections["file-hop"], "selection clone must be independent");
        store.Save(clone);
        Check(!store.Load().ToolSelections["file-hop"], "selection round trip");
        File.WriteAllText(legacy, "invalid");
        Check(store.Load().Theme == "Dark", "new settings must take precedence over legacy");
        File.WriteAllText(current, "broken settings");
        var damaged = new JsonSettingsStore(current);
        var fallback = damaged.Load(); damaged.Save(fallback);
        Check(damaged.LastError is not null && File.ReadAllText(current) == "broken settings", "failed reads must not overwrite original data");
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
}
