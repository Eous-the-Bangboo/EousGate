using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using EousGate;
using EousGate.Infrastructure;

internal static class ExternalMonitorSmoke
{
    internal static int Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new App(false); app.InitializeComponent();
            var backend = new CountingController();
            var frame = new DispatcherFrame();
            var exited = false;
            app.Exit += (_, _) => { exited = true; frame.Continue = false; };
            app.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await VerifyAsync(app, backend); }
                catch (Exception ex)
                {
                    failure = ex;
                    var panel = app.Windows.OfType<AssistantWindow>().FirstOrDefault();
                    Console.Error.WriteLine($"INFO lifecycle failure: count={backend.Inspections}, state={panel?.WindowState}, visible={panel?.IsVisible}, timer={app.ExternalMonitorEnabled}, refreshing={app.ExternalRefreshInProgress}");
                }
                finally { backend.ReleaseAll(); app.Shutdown(); }
            }));
            Dispatcher.PushFrame(frame);
            if (!exited) failure ??= new Exception("test must wait for the application Exit event");
            if (app.ExternalMonitorEnabled) failure ??= new Exception("shutdown must stop the monitor timer");
            var afterExit = backend.Inspections;
            // No worker inspections may start after the application dispatcher has shut down.
            Thread.Sleep(2200);
            if (backend.Inspections != afterExit) failure ??= new Exception("shutdown started another inspection");
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(90))) { Console.Error.WriteLine("External monitor lifecycle check timed out"); return 1; }
        if (failure is not null) { Console.Error.WriteLine(failure); return 1; }
        Console.WriteLine("PASS external monitor host: visibility/state gating, immediate resume, background refresh, coalescing, in-flight pause, action confirmation and shutdown; only fake processes controlled.");
        return 0;
    }

    private static async Task VerifyAsync(App app, CountingController backend)
    {
        app.InitializeAssistant(Path.Combine(Path.GetTempPath(), "EousAssistant-monitor-lifecycle-" + Guid.NewGuid()),
            tray: false, showPanel: false, externalController: backend);
        await NoInspectionsAsync(backend, 0, "unopened panel");
        Check(app.Windows.Count == 0, "headless initialization must not create windows");
        app.ShowPanel();
        var panel = app.Windows.OfType<AssistantWindow>().Single();
        ConfigureLifecyclePanel(panel);
        await UntilAsync(() => backend.Inspections == 3 && Observed(app) && !app.ExternalRefreshInProgress, "initial immediate sweep");
        await UntilAsync(() => backend.Inspections >= 6 && !app.ExternalRefreshInProgress, "visible periodic refresh");
        var beforeMaximize = backend.Inspections;
        panel.WindowState = WindowState.Maximized;
        await UntilAsync(() => backend.Inspections >= beforeMaximize + 3 && !app.ExternalRefreshInProgress, "maximized periodic refresh");
        panel.WindowState = WindowState.Normal;

        // Another window becoming active must not stop the timer.
        var other = new Window { Width = 180, Height = 100, ShowInTaskbar = false, Title = "Monitor test focus" };
        other.Show(); other.Activate();
        Check(!panel.IsActive, "test requires the panel to lose focus");
        var beforeFocus = backend.Inspections;
        await UntilAsync(() => backend.Inspections >= beforeFocus + 3, "inactive panel keeps refreshing");
        other.Close();
        await Task.Delay(150); // Let the closing window's native activation messages settle before minimizing.

        panel.WindowState = WindowState.Minimized;
        await IdleAsync(app);
        Check(!app.ExternalMonitorEnabled, "minimizing must stop the timer");
        var paused = backend.Inspections;
        backend.Running = true;
        await NoInspectionsAsync(backend, paused, "minimized panel");
        Check(app.Tools.Tools.OfType<ExternalProgramTool>().All(t => t.State == ToolState.Stopped), "paused panel retains observations");
        panel.WindowState = WindowState.Normal;
        await UntilAsync(() => backend.Inspections == paused + 3 && AllExternal(app, ToolState.Running), "restore refreshes changed status immediately", 1500);

        panel.Hide(); await IdleAsync(app);
        paused = backend.Inspections;
        await NoInspectionsAsync(backend, paused, "hidden panel");
        app.ShowPanel();
        await UntilAsync(() => backend.Inspections == paused + 3 && Observed(app), "show existing hidden panel", 1500);
        panel.Close(); await IdleAsync(app);
        paused = backend.Inspections;
        await NoInspectionsAsync(backend, paused, "closed panel");
        app.ShowPanel(); panel = app.Windows.OfType<AssistantWindow>().Single(); ConfigureLifecyclePanel(panel);
        await UntilAsync(() => backend.Inspections == paused + 3, "recreated panel immediate sweep", 1500);

        // Hold the first inspection while several timer ticks and reopen requests arrive.
        panel.Hide(); await IdleAsync(app);
        var blocked = backend.BlockNextInspection();
        paused = backend.Inspections;
        panel.Show();
        await blocked.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (var i = 0; i < 4; i++)
        {
            panel.WindowState = WindowState.Minimized;
            await Task.Delay(30);
            panel.WindowState = WindowState.Normal;
            await Task.Delay(30);
        }
        await Task.Delay(4300);
        Check(backend.Inspections == paused + 1 && backend.MaxConcurrent == 1, "ticks and rapid resumes must not overlap or queue sweeps");
        blocked.Release.Set();
        try { await UntilAsync(() => backend.Inspections == paused + 4 && !app.ExternalRefreshInProgress, "reopen during inspection requests one complete fresh sweep", 1500); }
        catch
        {
            Console.Error.WriteLine($"INFO resume sweep: expected {paused + 4}, actual {backend.Inspections}, state={panel.WindowState}, visible={panel.IsVisible}, timer={app.ExternalMonitorEnabled}, refreshing={app.ExternalRefreshInProgress}");
            throw;
        }
        await Task.Delay(250);
        Check(backend.Inspections == paused + 4, "resume requests must coalesce");

        panel.Hide(); await IdleAsync(app);
        blocked = backend.BlockNextInspection();
        panel.Show(); await blocked.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        panel.WindowState = WindowState.Minimized;
        paused = backend.Inspections;
        blocked.Release.Set();
        await NoInspectionsAsync(backend, paused, "pause during inspection stops remaining tools");

        // Explicit actions must finish their own verification even with the monitor paused.
        backend.Running = false;
        panel.WindowState = WindowState.Normal;
        await UntilAsync(() => AllExternal(app, ToolState.Stopped), "restore after interrupted sweep", 1500);
        var tool = app.Tools.Tools.OfType<ExternalProgramTool>().First();
        backend.StartGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = tool.StartAsync();
        await UntilAsync(() => tool.State == ToolState.Starting, "start underway");
        panel.WindowState = WindowState.Minimized;
        backend.StartGate.SetResult(); await start;
        Check(tool.State == ToolState.Running && backend.Starts == 1, "minimized start confirms actual state");
        await NoInspectionsAsync(backend, backend.Inspections, "completed start must not restart timer");

        panel.WindowState = WindowState.Normal;
        await UntilAsync(() => AllExternal(app, ToolState.Running), "resume before stop", 1500);
        backend.StopGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var stop = tool.StopAsync();
        await UntilAsync(() => tool.State == ToolState.Stopping, "stop underway");
        panel.WindowState = WindowState.Minimized;
        backend.StopGate.SetResult(); await stop;
        Check(tool.State == ToolState.Stopped && backend.Stops == 1, "minimized stop confirms actual state");
        await NoInspectionsAsync(backend, backend.Inspections, "completed stop must not restart timer");

        for (var i = 0; i < 3; i++)
        {
            panel.Close();
            app.ShowPanel(); panel = app.Windows.OfType<AssistantWindow>().Single(); ConfigureLifecyclePanel(panel);
            await UntilAsync(() => AllExternal(app, ToolState.Stopped), "repeated reopen");
        }
        await IdleAsync(app);
        paused = backend.Inspections;
        await UntilAsync(() => backend.Inspections >= paused + 3, "single surviving periodic monitor");
        await Task.Delay(250);
        Check(backend.Inspections == paused + 3 && backend.MaxConcurrent == 1, "reopens must not duplicate timers");
        Check(app.Tools.Tools.Where(t => t.Descriptor.Kind == ToolKind.BuiltIn).All(t => t.State == ToolState.Stopped), "monitor must not start native modules");
    }

    private static bool AllExternal(App app, ToolState state) => app.Tools.Tools.OfType<ExternalProgramTool>().All(t => t.State == state);
    private static void ConfigureLifecyclePanel(AssistantWindow panel)
    {
        // Rendering/layout has a separate smoke test. Keep lifecycle timing independent of rendering load.
        panel.Content = null;
        panel.MinWidth = panel.MinHeight = 0;
        panel.Width = 240; panel.Height = 160;
    }
    private static bool Observed(App app) => app.Tools.Tools.OfType<ExternalProgramTool>().All(t => t.StateText != "检测中");
    private static Task IdleAsync(App app) => UntilAsync(() => !app.ExternalRefreshInProgress, "finish in-flight monitor sweep");
    private static async Task NoInspectionsAsync(CountingController backend, int expected, string scenario)
    {
        await Task.Delay(2200);
        Check(backend.Inspections == expected, $"{scenario} must start no inspections (expected {expected}, actual {backend.Inspections})");
    }
    private static async Task UntilAsync(Func<bool> condition, string scenario, int timeoutMs = 10000)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.ElapsedMilliseconds > timeoutMs) throw new Exception("Timed out: " + scenario);
            await Task.Delay(15);
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    private sealed class InspectionGate
    {
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly ManualResetEventSlim Release = new(false);
    }
    private sealed class CountingController : IExternalProgramController
    {
        private int _inspections, _active, _maxConcurrent;
        private InspectionGate? _nextGate;
        private readonly ConcurrentBag<InspectionGate> _gates = [];
        public volatile bool Running;
        public int Starts, Stops;
        public TaskCompletionSource? StartGate, StopGate;
        public int Inspections => Volatile.Read(ref _inspections);
        public int MaxConcurrent => Volatile.Read(ref _maxConcurrent);
        public InspectionGate BlockNextInspection()
        {
            var gate = new InspectionGate(); _gates.Add(gate); _nextGate = gate; return gate;
        }
        public void ReleaseAll() { foreach (var gate in _gates) gate.Release.Set(); StartGate?.TrySetResult(); StopGate?.TrySetResult(); }
        public ExternalProgramSnapshot Inspect(ExternalProgramDefinition definition, string? configuredPath)
        {
            var active = Interlocked.Increment(ref _active);
            int previous;
            do { previous = Volatile.Read(ref _maxConcurrent); }
            while (active > previous && Interlocked.CompareExchange(ref _maxConcurrent, active, previous) != previous);
            Interlocked.Increment(ref _inspections);
            try
            {
                if (Interlocked.Exchange(ref _nextGate, null) is { } gate)
                {
                    gate.Entered.SetResult();
                    if (!gate.Release.Wait(TimeSpan.FromSeconds(15))) throw new Exception("blocked inspection timed out");
                }
                var path = @"C:\Fake\" + definition.ExecutableName;
                return new(path, Running ? [new(123, 456, path, 1)] : []);
            }
            finally { Interlocked.Decrement(ref _active); }
        }
        public bool IsValidExecutable(ExternalProgramDefinition definition, string path) => true;
        public async Task StartAsync(ExternalProgramDefinition definition, string path)
        { Starts++; if (StartGate is { } gate) await gate.Task; Running = true; }
        public async Task StopAsync(ExternalProgramDefinition definition, ExternalProgramSnapshot snapshot)
        { Stops++; if (StopGate is { } gate) await gate.Task; Running = false; }
    }
}
