using EousGate;
using EousGate.Infrastructure;
using System.ComponentModel;
using System.Diagnostics;

internal static class ExternalProgramTests
{
    internal static void PermissionAwareLaunch()
    {
        const string path = @"C:\Program Files\测试工具\TrafficMonitor.exe";
        var calls = 0;
        void CheckInfo(ProcessStartInfo info)
        {
            Check(info.FileName == path && info.WorkingDirectory == Path.GetDirectoryName(path), "exact executable and working directory must survive retry");
            Check(info.ArgumentList.SequenceEqual(new[] { "--name", "含 空格", "a\"b", @"C:\with space\" }), "structured arguments must remain separate and unchanged");
        }
        var arguments = new[] { "--name", "含 空格", "a\"b", @"C:\with space\" };
        WindowsExternalProgramController.Launch(path, arguments, allowElevation: true, startProcess: info =>
        {
            CheckInfo(info); calls++;
            Check(!info.UseShellExecute, "ordinary launch must not request elevation");
        });
        Check(calls == 1, "normal launch must run once");
        calls = 0;
        WindowsExternalProgramController.Launch(path, arguments, allowElevation: true, startProcess: info =>
        {
            CheckInfo(info); calls++;
            if (calls == 1) { Check(!info.UseShellExecute, "try normal launch first"); throw new Win32Exception(740); }
            Check(info.UseShellExecute && info.Verb == "open", "740 must use Windows shell authorization for the same EXE");
        });
        Check(calls == 2, "elevation-required launch must retry exactly once");
        foreach (var (code, text, expectedCalls, elevate) in new[]
        {
            (1223, "已取消管理员权限授权", 2, true),
            (5, "拒绝访问", 1, true),
            (740, "需要管理员权限", 1, false)
        })
        {
            calls = 0;
            try
            {
                WindowsExternalProgramController.Launch(path, [], "--name \"含 空格\"", elevate, info =>
                {
                    calls++;
                    Check(info.Arguments == "--name \"含 空格\"" && info.ArgumentList.Count == 0, "raw custom arguments must be identical on retry");
                    throw new Win32Exception(calls == 1 && expectedCalls == 2 ? 740 : code);
                });
                throw new Exception("expected launch error");
            }
            catch (InvalidOperationException ex)
            {
                Check(ex.Message.Contains(text) && !ex.Message.Contains(path), "permission failures must have actionable Chinese text without leaking the raw launch exception");
                Check(ex.InnerException is Win32Exception && calls == expectedCalls, "preserve underlying error; never retry access denial or elevate exit commands");
            }
        }
    }

    internal static void MonitoringAndIdempotence()
    {
        var backend = new FakeController(); var tool = Create(backend);
        Check(!tool.CanStart && tool.StateText == "检测中", "do not claim a state before observing");
        tool.RefreshAsync().GetAwaiter().GetResult();
        Check(tool.State == ToolState.Stopped && tool.CanStart && !tool.CanStop, "initial stopped state");
        backend.Running = true; tool.RefreshAsync().GetAwaiter().GetResult();
        Check(tool.State == ToolState.Running && !tool.CanStart && tool.CanStop, "track externally started process");
        tool.StartAsync().GetAwaiter().GetResult(); Check(backend.Starts == 0, "existing process must not launch twice");
        backend.Running = false; tool.RefreshAsync().GetAwaiter().GetResult();
        Check(tool.State == ToolState.Stopped, "track manual exit");
        Task.WhenAll(tool.StartAsync(), tool.StartAsync()).GetAwaiter().GetResult();
        Check(backend.Starts == 1 && tool.State == ToolState.Running, "concurrent start must be idempotent");
        tool.StopAsync().GetAwaiter().GetResult(); tool.StopAsync().GetAwaiter().GetResult();
        Check(backend.Stops == 1 && tool.State == ToolState.Stopped, "stop verifies the process really exited");
    }
    internal static void FailuresAndRecovery()
    {
        var backend = new FakeController { Running = true, FailStop = true }; var tool = Create(backend);
        tool.StopAsync().GetAwaiter().GetResult();
        Check(tool.State == ToolState.Failed && tool.CanStop && !tool.CanStart, "failed stop must not report stopped");
        tool.RefreshAsync().GetAwaiter().GetResult(); Check(tool.State == ToolState.Failed, "keep action error until recovery");
        backend.Running = false; tool.RefreshAsync().GetAwaiter().GetResult();
        Check(tool.State == ToolState.Stopped && tool.Error is null, "manual exit clears stale failure");
        backend.FailStart = true; tool.StartAsync().GetAwaiter().GetResult(); tool.RefreshAsync().GetAwaiter().GetResult();
        Check(tool.State == ToolState.Failed && tool.Error is not null, "preserve failed launch when still stopped");
        backend.Error = "无法确认路径"; tool.RefreshAsync().GetAwaiter().GetResult();
        Check(!tool.CanStart, "uncertain identity must not permit duplicate launch");
        backend.Error = null; backend.FailStart = false; tool.StartAsync().GetAwaiter().GetResult();
        Check(tool.State == ToolState.Running, "retry recovers");
    }
    internal static void BatchAndExitPolicy()
    {
        var manager = new ToolManager(); var backend = new FakeController { Running = true }; var external = Create(backend);
        var builtInStops = 0;
        var builtIn = new ManagedTool(new("builtin", "Builtin", "内置", "", ""), () => Task.CompletedTask, () => { builtInStops++; return Task.CompletedTask; }, () => { });
        manager.Register(builtIn); manager.Register(external);
        builtIn.StartAsync().GetAwaiter().GetResult(); external.RefreshAsync().GetAwaiter().GetResult();
        manager.RunAsync(false, false, ToolKind.BuiltIn).GetAwaiter().GetResult();
        Check(builtInStops == 1 && backend.Stops == 0 && external.State == ToolState.Running, "assistant exit must preserve external programs");
        external.Selected = false;
        manager.RunAsync(false, true).GetAwaiter().GetResult(); Check(backend.Stops == 0, "selected batch respects external checkbox");
        manager.RunAsync(false, false).GetAwaiter().GetResult(); Check(backend.Stops == 1, "explicit stop all includes external programs");
    }
    internal static void Configuration()
    {
        var backend = new FakeController(); var options = new ExternalProgramOptions();
        var definition = ExternalProgramCatalog.All[0];
        var tool = new ExternalProgramTool(definition, backend, () => options, () => { });
        tool.ConfigurePathAsync(@"C:\Tools\Ditto.exe", p => options.ExecutablePath = p).GetAwaiter().GetResult();
        Check(tool.ExecutablePath == options.ExecutablePath, "configured path");
        backend.Running = true;
        try { tool.ConfigurePathAsync(@"D:\Other\Ditto.exe", p => options.ExecutablePath = p).GetAwaiter().GetResult(); throw new Exception("running path changed"); }
        catch (InvalidOperationException) { }
        var settings = new UserSettings(); settings.ExternalPrograms[definition.Descriptor.Id] = options;
        var clone = settings.Clone(); clone.ExternalPrograms[definition.Descriptor.Id].ExecutablePath = "changed";
        Check(settings.ExternalPrograms[definition.Descriptor.Id].ExecutablePath != "changed", "deep clone external settings");
        var path = Path.Combine(Path.GetTempPath(), "EousAssistant-external-" + Guid.NewGuid() + ".json");
        var store = new JsonSettingsStore(path); store.Save(settings);
        Check(store.Load().ExternalPrograms[definition.Descriptor.Id].ExecutablePath == options.ExecutablePath, "external path round trip");
        Check(ExternalProgramCatalog.All.Count == 3 && ExternalProgramCatalog.All.All(p => p.Descriptor.Kind == ToolKind.External), "three registered external profiles");
    }
    private static ExternalProgramTool Create(FakeController backend) => new(ExternalProgramCatalog.All[0], backend, () => new(), () => { });
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class FakeController : IExternalProgramController
    {
        public bool Running, FailStart, FailStop; public int Starts, Stops; public string? Error;
        public ExternalProgramSnapshot Inspect(ExternalProgramDefinition definition, string? configuredPath)
        {
            var path = configuredPath ?? @"C:\Tools\Ditto.exe";
            return new(path, Running ? [new(123, 456, path, 1), new(124, 457, path, 1)] : [], Error);
        }
        public bool IsValidExecutable(ExternalProgramDefinition definition, string path) => true;
        public Task StartAsync(ExternalProgramDefinition definition, string path)
        { Starts++; if (FailStart) throw new IOException("launch failed"); Running = true; return Task.CompletedTask; }
        public Task StopAsync(ExternalProgramDefinition definition, ExternalProgramSnapshot snapshot)
        { Stops++; if (FailStop) throw new IOException("stop failed"); Running = false; return Task.CompletedTask; }
    }
}
