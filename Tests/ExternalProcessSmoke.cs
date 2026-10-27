using System.Diagnostics;
using EousGate;
using EousGate.Infrastructure;
using System.Windows.Threading;

internal static class ExternalProcessSmoke
{
    internal static int MonitorHost()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new EousGate.App(false); app.InitializeComponent();
            var frame = new DispatcherFrame();
            app.Exit += (_, _) => frame.Continue = false;
            app.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    app.InitializeAssistant(Path.Combine(Path.GetTempPath(), "EousAssistant-monitor-" + Guid.NewGuid()), tray: false, showPanel: false);
                    await Task.Delay(2500);
                    Check(app.Tools.Tools.Count == 6, "host must register six tools");
                    Check(app.Tools.Tools.OfType<ExternalProgramTool>().All(t => t.StateText == "检测中"), "headless host must not inspect external programs");
                    Check(app.Windows.Count == 0, "headless host must not display windows");
                    app.ShowPanel();
                    for (var i = 0; i < 100 && app.Tools.Tools.OfType<ExternalProgramTool>().Any(t => t.StateText == "检测中"); i++) await Task.Delay(50);
                    Check(app.Tools.Tools.Where(t => t.Descriptor.Kind == ToolKind.BuiltIn).All(t => t.State == ToolState.Stopped), "monitor must not start native modules");
                    Check(app.Tools.Tools.OfType<ExternalProgramTool>().All(t => t.State is ToolState.Running or ToolState.Stopped), "external monitoring must resolve installed programs without commands");
                    Check(app.Windows.OfType<AssistantWindow>().Count() == 1, "visible monitor host must display one panel");
                    foreach (var tool in app.Tools.Tools.OfType<ExternalProgramTool>()) Console.WriteLine($"PASS monitor host {tool.Descriptor.ChineseName}: {tool.StateText}; no startup/shutdown commands sent.");
                }
                catch (Exception ex) { failure = ex; }
                finally { app.Shutdown(); }
            }));
            Dispatcher.PushFrame(frame);
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(20))) return 1;
        if (failure is not null) { Console.Error.WriteLine(failure); return 1; }
        return 0;
    }
    internal static int InspectInstalled()
    {
        try
        {
            var controller = new WindowsExternalProgramController();
            foreach (var definition in ExternalProgramCatalog.All)
            {
                var snapshot = controller.Inspect(definition, null);
                Check(snapshot.Error is null, definition.Descriptor.ChineseName + ": " + snapshot.Error);
                Check(snapshot.Processes.All(p => p.SessionId == Process.GetCurrentProcess().SessionId && string.Equals(p.ExecutablePath, snapshot.ExecutablePath, StringComparison.OrdinalIgnoreCase)), "matched wrong session or path");
                Console.WriteLine($"PASS read-only {definition.Descriptor.ChineseName}: located={snapshot.ExecutablePath is not null}, current-session processes={snapshot.Processes.Count}; no commands sent.");
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    internal static int Run(string[] args)
    {
        try { TestAsync(args[0]).GetAwaiter().GetResult(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static async Task TestAsync(string fixtureDirectory)
    {
        var root = Path.Combine(Path.GetTempPath(), "EousAssistant-process-" + Guid.NewGuid());
        var first = Path.Combine(root, "first"); var second = Path.Combine(root, "second");
        foreach (var directory in new[] { first, second })
        {
            Directory.CreateDirectory(directory);
            foreach (var file in Directory.GetFiles(fixtureDirectory)) File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
        }
        const string name = "EousAssistant.ExternalFixture.exe";
        var path = Path.Combine(first, name); var peerPath = Path.Combine(second, name);
        var definition = new ExternalProgramDefinition(new("fixture", "Fixture", "测试进程", "", "", ToolKind.External), name, "Fixture", [], ["--spawn-helper"], ["--exit"]);
        var controller = new WindowsExternalProgramController();
        var tool = new ExternalProgramTool(definition, controller, () => new() { ExecutablePath = path }, () => { });
        using var peer = Process.Start(new ProcessStartInfo(peerPath) { UseShellExecute = false, CreateNoWindow = true })!;
        try
        {
            await tool.StartAsync(); Check(tool.State == ToolState.Running, tool.Error ?? "fixture did not start");
            for (int i = 0; i < 30 && controller.Inspect(definition, path).Processes.Count < 2; i++) await Task.Delay(100);
            var original = controller.Inspect(definition, path);
            Check(original.Processes.Count == 2, "main and helper must be grouped");
            await tool.StartAsync();
            Check(controller.Inspect(definition, path).Processes.Select(p => p.Id).Order().SequenceEqual(original.Processes.Select(p => p.Id).Order()), "start duplicated a running process");
            var wrongIdentity = original with { Processes = original.Processes.Select(p => p with { StartedUtcTicks = p.StartedUtcTicks + 1 }).ToArray() };
            await controller.StopAsync(definition, wrongIdentity);
            Check(controller.Inspect(definition, path).Processes.Count == 2, "creation-time mismatch must not terminate any process");
            var wrongSession = original with { Processes = original.Processes.Select(p => p with { SessionId = -1 }).ToArray() };
            await controller.StopAsync(definition, wrongSession);
            Check(controller.Inspect(definition, path).Processes.Count == 2, "other sessions must be excluded");
            await tool.StopAsync(); Check(tool.State == ToolState.Stopped, tool.Error ?? "graceful stop failed");
            Check(!peer.HasExited, "same-name executable at another path must remain running");
            var forcedDefinition = definition with { StartArguments = ["--wait"], ExitArguments = [] };
            var customDefinition = CustomProgramConfiguration.Definition(new CustomMonitoredProgram { Name = "测试自定义", ExecutablePath = path, StartArguments = "--spawn-helper --label \"two words\"" });
            await controller.StartAsync(customDefinition, path);
            ExternalProgramSnapshot forced = controller.Inspect(forcedDefinition, path);
            for (int i = 0; i < 30 && forced.Processes.Count < 2; i++) { await Task.Delay(100); forced = controller.Inspect(forcedDefinition, path); }
            Check(forced.Processes.Count == 2, "custom raw start arguments must spawn the requested helper");
            try { await controller.StopAsync(customDefinition, forced); throw new Exception("custom default forcibly terminated a process"); }
            catch (InvalidOperationException) { }
            Check(controller.Inspect(customDefinition, path).Processes.Count == 2, "custom default must preserve processes that ignore normal close");
            await controller.StopAsync(forcedDefinition, forced);
            Check(controller.Inspect(forcedDefinition, path).Processes.Count == 0 && !peer.HasExited, "fallback stop must target only the pinned executable processes");
            var windowDefinition = customDefinition with { RawStartArguments = "--window" };
            await controller.StartAsync(windowDefinition, path);
            var requests = 0;
            var deniedController = new WindowsExternalProgramController(_ => { requests++; return 5; });
            var deniedTool = new ExternalProgramTool(windowDefinition, deniedController, () => new() { ExecutablePath = path }, () => { });
            for (var i = 0; i < 50 && requests == 0; i++)
            {
                await deniedTool.StopAsync();
                if (requests == 0) await Task.Delay(50);
            }
            Check(requests > 0 && deniedTool.State == ToolState.Failed && deniedTool.CanStop && !deniedTool.CanStart
                && deniedTool.Error!.Contains("管理员权限") && !peer.HasExited,
                "Windows close-message denial must report permissions and preserve the live switch/retry without forcing exit");
            // The real adapter can normally close the same hidden fixture at equal permissions.
            await controller.StopAsync(windowDefinition, controller.Inspect(windowDefinition, path));
            Check(controller.Inspect(windowDefinition, path).Processes.Count == 0, "successful close-message delivery must still exit normally");
            Console.WriteLine("PASS close-message permissions: denied WM_CLOSE is actionable, state and retry retained, equal-permission normal close succeeds; only hidden test fixture controlled.");
            Console.WriteLine("PASS real process adapter: main/helper grouping, no duplicate start, graceful exit command, fallback termination, creation-time/session protection and same-name different-path isolation. Only test fixtures controlled.");
        }
        finally
        {
            foreach (var ownPath in new[] { path, peerPath })
            {
                var own = controller.Inspect(definition, ownPath);
                if (own.Processes.Count > 0) await controller.StopAsync(definition, own);
            }
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
