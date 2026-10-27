using EousGate;
using EousGate.Infrastructure;
using System.Collections.Concurrent;

internal static class CustomProgramTests
{
    internal static void Configuration()
    {
        var value = new CustomMonitoredProgram { Name = " 测试应用 ", ExecutablePath = @"C:\Apps\Demo.exe", StartArguments = "--label \"hello world\"" };
        var settings = new UserSettings { CustomPrograms = [value] }; settings.ToolSelections[value.Id] = false;
        var clone = settings.Clone(); clone.CustomPrograms[0].Name = "Changed";
        Check(settings.CustomPrograms[0].Name != "Changed", "custom definitions must deep-clone");
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"); var store = new JsonSettingsStore(path);
        store.Save(settings); var loaded = store.Load();
        Check(store.LastError is null && loaded.CustomPrograms.Single().Id == value.Id && loaded.CustomPrograms[0].Name == "测试应用" && loaded.CustomPrograms[0].StartArguments == value.StartArguments && !loaded.CustomPrograms[0].ForceTerminateOnTimeout && !loaded.ToolSelections[value.Id], "round trip identity, name, args, safe stop and selection");
        File.WriteAllText(path, "{\"Theme\":\"Dark\"}");
        Check(store.Load().CustomPrograms.Count == 0, "legacy settings start with an empty custom list");
        File.WriteAllText(path, "{\"Theme\":\"Dark\",\"CustomPrograms\":[null,{\"Id\":\"file-hop\",\"Name\":\"bad\",\"ExecutablePath\":\"C:\\\\bad.exe\"}]}");
        var damaged = store.Load();
        Check(damaged.Theme == "Dark" && damaged.CustomPrograms.Count == 0 && store.LastError is not null, "malformed custom items must not break other settings or shadow builtin IDs");
    }
    internal static void Management() => ManagementAsync().GetAwaiter().GetResult();
    internal static void StopPolicyWhileRunning() => StopPolicyWhileRunningAsync().GetAwaiter().GetResult();
    private static async Task StopPolicyWhileRunningAsync()
    {
        var current = new UserSettings(); var manager = new ToolManager();
        var backend = new Backend { IgnoreNormalClose = true }; var failSave = false;
        var service = new CustomProgramService(manager, backend, () => current, draft =>
        {
            if (failSave) throw new IOException("save failure");
            current = draft;
        }, _ => { }, () => { });
        await service.SaveAsync(new() { Name = "常驻测试", ExecutablePath = @"C:\Apps\Resident.exe" });
        var tool = (ExternalProgramTool)manager.Tools.Single();
        tool.Selected = false; current.ToolSelections[tool.Descriptor.Id] = false;
        await tool.StartAsync(); await tool.StopAsync();
        Check(tool.State == ToolState.Failed && tool.CanStop && !tool.Definition.ForceTerminateOnTimeout,
            "default normal-close timeout must preserve the live process and allow retry");
        var policy = current.CustomPrograms.Single().Clone(); policy.ForceTerminateOnTimeout = true;
        failSave = true;
        await Reject(() => service.SaveAsync(policy, policy.Id));
        Check(!current.CustomPrograms.Single().ForceTerminateOnTimeout && !tool.Definition.ForceTerminateOnTimeout
            && ReferenceEquals(tool, manager.Tools.Single()), "failed policy save must preserve both persisted and live policy");
        failSave = false;
        foreach (var change in new Action<CustomMonitoredProgram>[]
        {
            p => p.Name = "改名", p => p.ExecutablePath = @"C:\Apps\Other.exe", p => p.StartArguments = "--other"
        })
        {
            var invalidEdit = policy.Clone(); change(invalidEdit);
            await Reject(() => service.SaveAsync(invalidEdit, policy.Id));
        }
        await service.SaveAsync(policy, policy.Id);
        Check(ReferenceEquals(tool, manager.Tools.Single()) && tool.State == ToolState.Failed && tool.CanStop && !tool.Selected
            && tool.Definition.ForceTerminateOnTimeout && current.CustomPrograms.Single().ForceTerminateOnTimeout,
            "running policy update must preserve observed state, retry, identity and selection without replacing or stopping the tool");
        await tool.StopAsync();
        Check(tool.State == ToolState.Stopped && !backend.Running.Any(), "retry must use the newly saved force policy");
    }
    private static async Task ManagementAsync()
    {
        var current = new UserSettings(); var manager = new ToolManager(); var backend = new Backend(); var failSave = false;
        var service = new CustomProgramService(manager, backend, () => current, draft => { if (failSave) throw new IOException("save failure"); current = draft; }, _ => { }, () => { });
        var input = new CustomMonitoredProgram { Name = "Demo", ExecutablePath = @"C:\Apps\Demo.exe" };
        await service.SaveAsync(input);
        var first = manager.Tools.OfType<ExternalProgramTool>().Single(); var id = first.Descriptor.Id;
        Check(current.CustomPrograms.Single().Id == id && first.StateText == "检测中" && backend.Starts == 0, "adding registers without starting and persists stable identity");
        first.Selected = false; current.ToolSelections[id] = false;
        await Reject(() => service.SaveAsync(new() { Name = "Duplicate", ExecutablePath = @"c:\apps\DEMO.exe" }));
        await first.StartAsync();
        await Reject(() => service.SaveAsync(new() { Name = "Renamed", ExecutablePath = input.ExecutablePath }, id));
        Check(current.CustomPrograms[0].Name == "Demo", "running definitions cannot be replaced");
        await first.StopAsync();
        var edit = current.CustomPrograms[0].Clone(); edit.Name = "Renamed"; edit.StartArguments = "--label \"a b\""; edit.ForceTerminateOnTimeout = true;
        backend.Missing[input.ExecutablePath] = true;
        edit.ExecutablePath = @"C:\Apps\Fixed.exe";
        await service.SaveAsync(edit, id);
        var replacement = manager.Tools.OfType<ExternalProgramTool>().Single();
        Check(replacement != first && replacement.Descriptor.Id == id && !replacement.Selected && replacement.Definition.RawStartArguments == edit.StartArguments && replacement.Definition.ForceTerminateOnTimeout, "edit preserves identity/selection and updates behavior");
        failSave = true;
        await Reject(() => service.RemoveAsync(id));
        await Reject(() => service.SaveAsync(new() { Name = "Other", ExecutablePath = @"C:\Apps\Other.exe" }));
        Check(manager.Tools.Count == 1 && current.CustomPrograms.Count == 1 && current.CustomPrograms[0].Name == "Renamed", "failed persistence must leave runtime and configuration unchanged");
        failSave = false;
        await replacement.StartAsync(); var stops = backend.Stops;
        await service.RemoveAsync(id);
        Check(manager.Tools.Count == 0 && current.CustomPrograms.Count == 0 && !current.ToolSelections.ContainsKey(id) && backend.Stops == stops && backend.Running.ContainsKey(edit.ExecutablePath), "removal only drops management and selection; it must not stop a live process");
        await Reject(() => service.RemoveAsync("external-ditto"));
        await service.SaveAsync(edit);
        var restored = new ToolManager(); var starts = backend.Starts;
        var reopened = new CustomProgramService(restored, backend, () => current, _ => { }, _ => { }, () => { }); reopened.RegisterSaved();
        var restoredTool = (ExternalProgramTool)restored.Tools.Single();
        Check(restoredTool.Descriptor.Id == current.CustomPrograms[0].Id && backend.Starts == starts && restoredTool.StateText == "检测中", "startup registers persisted programs without starting or polling");
        await restoredTool.RefreshAsync();
        Check(restoredTool.State == ToolState.Running, "persisted custom targets must observe already-running processes");
        manager.Register(new ExternalProgramTool(ExternalProgramCatalog.All[0], backend, () => new() { ExecutablePath = @"C:\Preset\Ditto.exe" }, () => { }));
        await Reject(() => service.SaveAsync(new() { Name = "重复预设", ExecutablePath = @"C:\Preset\Ditto.exe" }));
    }
    private static async Task Reject(Func<Task> action)
    {
        try { await action(); } catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException) { return; }
        throw new Exception("expected operation rejection");
    }
    internal sealed class Backend : IExternalProgramController
    {
        internal readonly ConcurrentDictionary<string, bool> Running = new(StringComparer.OrdinalIgnoreCase);
        internal readonly ConcurrentDictionary<string, bool> Missing = new(StringComparer.OrdinalIgnoreCase);
        internal int Starts, Stops;
        internal bool IgnoreNormalClose;
        public ExternalProgramSnapshot Inspect(ExternalProgramDefinition definition, string? configuredPath)
        {
            var path = configuredPath ?? @"C:\Presets\" + definition.ExecutableName;
            if (Missing.ContainsKey(path)) return new(path, [], "程序路径已失效", ExecutableMissing: true);
            return new(path, Running.ContainsKey(path) ? [new(100, 200, path, 1)] : []);
        }
        public bool IsValidExecutable(ExternalProgramDefinition definition, string path) => !path.Contains("invalid", StringComparison.OrdinalIgnoreCase) && !Missing.ContainsKey(path);
        public Task StartAsync(ExternalProgramDefinition definition, string executablePath) { Starts++; Running[executablePath] = true; return Task.CompletedTask; }
        public Task StopAsync(ExternalProgramDefinition definition, ExternalProgramSnapshot snapshot)
        {
            Stops++;
            if (IgnoreNormalClose && !definition.ForceTerminateOnTimeout) throw new InvalidOperationException("程序未正常退出，仍在运行。");
            Running.TryRemove(snapshot.ExecutablePath!, out _); return Task.CompletedTask;
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
