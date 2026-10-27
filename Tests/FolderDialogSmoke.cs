using EousGate;
using EousGate.FolderHop;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

internal static class FolderDialogSmoke
{
    internal static int Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new EousGate.App(false); app.InitializeComponent();
            var store = new HistoryStore(Path.Combine(Path.GetTempPath(), "Eous-dialog-" + Guid.NewGuid()));
            store.Data.Paused = true;
            var dialog = IntPtr.Zero;
            var foregroundOverride = IntPtr.Zero;
            // The user's active app may stay foreground under Windows foreground-lock rules.
            // Feed the real dialog handle to the production follower while observing native activation independently.
            var session = new FolderSession(store, _ => { }, () => foregroundOverride != IntPtr.Zero ? foregroundOverride : dialog != IntPtr.Zero ? dialog : Native.GetForegroundWindow());
            var owner = new CustomProgramWindow(null, _ => Task.CompletedTask, null, SkinCatalog.DefaultId);
            var frame = new DispatcherFrame();
            app.Dispatcher.BeginInvoke(new Action(async () =>
            {
                var samples = new List<string>();
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                var ticks = 0;
                timer.Tick += (_, _) =>
                {
                    try
                    {
                    if (dialog == IntPtr.Zero)
                        Native.EnumWindows((window, _) => { GetWindowThreadProcessId(window, out var pid); if (pid == Environment.ProcessId && Native.IsFileDialog(window)) dialog = window; return true; }, IntPtr.Zero);
                    if (dialog != IntPtr.Zero)
                    {
                        var front = Native.GetForegroundWindow();
                        GetWindowThreadProcessId(front, out var frontPid);
                        if (ticks is >= 25 and < 65 or >= 100)
                            samples.Add($"dialogFront={front == dialog},buttonFront={front == session.Button.Handle},visible={session.Button.Visible},enabled={IsWindowEnabled(session.Button.Handle)},frontClass={Native.Class(front)},own={frontPid == Environment.ProcessId}");
                        if (ticks == 70)
                        {
                            foregroundOverride = session.Picker.Handle;
                            var child = Native.Children(session.Button.Handle).First();
                            Native.SendMessage(child, 0xF5, IntPtr.Zero, null); // Click only our fixture's companion.
                        }
                        if (ticks == 78)
                        {
                            Check(session.Picker.Visible && session.Picker.Target == dialog, "companion click must open the picker for the real modal dialog");
                            session.Picker.Hide(); foregroundOverride = IntPtr.Zero;
                        }
                    }
                    if (++ticks >= 140 && dialog != IntPtr.Zero) Native.PostMessage(dialog, 0x10, IntPtr.Zero, IntPtr.Zero);
                    }
                    catch (Exception ex) { failure = ex; session.Picker.Hide(); if (dialog != IntPtr.Zero) Native.PostMessage(dialog, 0x10, IntPtr.Zero, IntPtr.Zero); }
                };
                try
                {
                    owner.Show();
                    for (var round = 0; round < 2; round++)
                    {
                    dialog = IntPtr.Zero; foregroundOverride = IntPtr.Zero; ticks = 0; samples.Clear();
                    timer.Start();
                    ((Button)owner.FindName("BrowseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    timer.Stop();
                    if (failure is not null) throw failure;
                    var settled = samples.ToArray();
                    foreach (var group in samples.GroupBy(x => x)) Console.WriteLine($"SAMPLE {group.Count()}x {group.Key}");
                    Check(settled.Length > 30, "real file dialog did not remain available for sampling");
                    Check(settled.All(s => s.Contains("buttonFront=False,visible=True,enabled=True,")), "same-process file dialog companion must remain visible and enabled without taking focus");
                    }
                    Console.WriteLine("PASS actual WPF file dialog with controlled foreground reader: stable companion, no foreground activation, click opens picker, two modal cycles and clean shutdown.");
                }
                catch (Exception ex) { failure = ex; }
                finally { timer.Stop(); await session.StopAsync(); owner.Close(); frame.Continue = false; }
            }));
            Dispatcher.PushFrame(frame); app.Shutdown();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(35))) { Console.Error.WriteLine("Folder dialog smoke timed out"); return 1; }
        if (failure is null) return 0;
        Console.Error.WriteLine(failure); return 1;
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr window);
}
