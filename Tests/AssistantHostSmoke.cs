using EousGate;
using EousGate.FolderHop;
using System.Windows.Threading;

internal static class AssistantHostSmoke
{
    internal static int Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new EousGate.App(false); app.InitializeComponent();
            var directory = Path.Combine(Path.GetTempPath(), "EousAssistant-host-" + Guid.NewGuid());
            var history = new HistoryStore(Path.Combine(directory, "FolderHop"));
            history.Data.Paused = true; history.Dirty = true;
            if (!history.Save()) throw new IOException(history.Warning);
            var frame = new DispatcherFrame();
            app.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    app.InitializeAssistant(directory, tray: false, showPanel: false, includeExternalTools: false);
                    Check(app.Tools.Tools.All(t => t.State == ToolState.Stopped), "host must not initialize any module before start");
                    Check(app.Windows.Count == 0, "host must not create module windows before start");
                    var file = app.Tools.Tools.Single(t => t.Descriptor.Id == "file-hop");
                    var web = app.Tools.Tools.Single(t => t.Descriptor.Id == "web-hop");
                    var folder = app.Tools.Tools.Single(t => t.Descriptor.Id == "folder-hop");
                    for (int cycle = 0; cycle < 2; cycle++)
                    {
                        await app.Tools.RunAsync(true, false);
                        Check(app.Tools.Tools.All(t => t.State == ToolState.Running), string.Join(";", app.Tools.Tools.Select(t => t.Error)));
                        Check(app.Windows.OfType<EdgeDropWindow>().Count() == 3, "exactly three edge windows per start");
                        Check(app.Windows.OfType<ClipboardLinkWindow>().Count() == 1, "exactly one clipboard window per start");
                        if (cycle == 0)
                        {
                            using var process = System.Diagnostics.Process.GetCurrentProcess();
                            var cpu = process.TotalProcessorTime; var elapsed = System.Diagnostics.Stopwatch.StartNew();
                            await Task.Delay(2000); process.Refresh();
                            var percent = (process.TotalProcessorTime - cpu).TotalMilliseconds / elapsed.Elapsed.TotalMilliseconds / Environment.ProcessorCount * 100;
                            Console.WriteLine($"INFO isolated test host, all modules running with folder sampling paused: working set {process.WorkingSet64 / 1048576d:0.0} MiB; machine CPU {percent:0.00}% over {elapsed.Elapsed.TotalSeconds:0.0}s. Not a production benchmark.");
                        }
                        await web.StopAsync();
                        Check(file.State == ToolState.Running && folder.State == ToolState.Running, "stopping web must leave other tools running");
                        Check(!app.Windows.OfType<ClipboardLinkWindow>().Any(), "web stop must close clipboard window");
                        await web.StartAsync();
                        await file.StopAsync();
                        Check(!app.Windows.OfType<EdgeDropWindow>().Any() && !app.Windows.OfType<OverlayWindow>().Any(), "file stop must release all WPF utility windows");
                        Check(web.State == ToolState.Running && folder.State == ToolState.Running, "file stop must be independent");
                        await app.Tools.RunAsync(false, false);
                        Check(app.Tools.Tools.All(t => t.State == ToolState.Stopped), "all real modules must stop cleanly");
                        Check(app.Windows.Count == 0, "no module windows may remain after stop");
                    }
                    // Successful acquisition proves the legacy FolderHop marker was released by Stop.
                    using var marker = new EousGate.Infrastructure.LegacyInstanceGuard("Local\\FolderHop.CleanRoom.1", "test");
                    Console.WriteLine("PASS assistant real host: manual startup, two complete start-stop cycles, independent FileHop/WebHop stops, native window cleanup and legacy marker release; no desktop input or history sampling.");
                }
                catch (Exception ex) { failure = ex; }
                finally
                {
                    await app.Tools.RunAsync(false, false);
                    frame.Continue = false;
                }
            }));
            Dispatcher.PushFrame(frame); app.Shutdown();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) { Console.Error.WriteLine("Real host check timed out"); return 1; }
        if (failure is not null) { Console.Error.WriteLine(failure); return 1; }
        return 0;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
