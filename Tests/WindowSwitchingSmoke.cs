using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using EousGate;
using EousGate.Infrastructure;

internal static class WindowSwitchingSmoke
{
    private const int ToolWindow = 0x80;
    private const int AppWindow = 0x40000;
    private const int NoActivate = 0x08000000;
    private const int Transparent = 0x20;

    internal static int Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new EousGate.App(initializeServices: false);
            app.InitializeComponent();
            var windows = new List<Window>();
            ClipboardLinkWindow? clipboard = null;
            try
            {
                var settings = new UserSettings { EdgeBandPosition = "Left,Right,Top" };
                var discovery = new AppDiscovery(() => settings);
                var opener = new OpenFileService(_ => throw new InvalidOperationException("Unexpected file launch"));
                var overlay = new OverlayWindow(discovery, opener, () => settings, monitorMouseButton: false);
                windows.Add(overlay);
                CheckUtility(overlay);
                VerifyFluentPanel(overlay, settings);

                foreach (var side in Enum.GetValues<EdgeDropWindow.EdgeSide>())
                {
                    var edge = new EdgeDropWindow(overlay, discovery, () => settings.Paused, () => settings, side);
                    windows.Add(edge);
                    edge.ApplyAppearance(settings);
                    CheckUtility(edge);
                    VerifyEdgeRanges(edge, settings, side);
                    settings.Paused = true;
                    edge.ApplyAppearance(settings);
                    Check(!edge.IsEnabled, $"{side}: pause must disable input");
                    AssertExcluded(edge);
                    settings.Paused = false;
                    edge.ApplyAppearance(settings);
                    Check(edge.IsEnabled, $"{side}: resume must enable input");
                    settings.EdgeBandPosition = "";
                    edge.ApplyAppearance(settings);
                    Check(!edge.IsVisible, $"{side}: disabling the position must hide it");
                    settings.EdgeBandPosition = "Left,Right,Top";
                    edge.ApplyAppearance(settings);
                    Check(edge.IsVisible, $"{side}: enabling the position must show it");
                    AssertExcluded(edge);
                }

                clipboard = new ClipboardLinkWindow(() => settings,
                    new WebLinkOpener(_ => throw new InvalidOperationException("Unexpected browser launch")));
                windows.Add(clipboard);
                CheckUtility(clipboard);
                Check(!clipboard.ShowActivated, "clipboard must retain ShowActivated=false");
                clipboard.Dismiss();
                clipboard.ShowLinks([new Uri("https://example.com/")]);
                AssertExcluded(clipboard);
                clipboard.Dismiss();

                var editor = new SettingsWindow(settings, new MemorySettingsStore(settings), discovery, _ => { });
                windows.Add(editor);
                CheckApplicationWindow(editor);
                var custom = new CustomAppWindow(Environment.ProcessPath!, "Test") { Owner = editor };
                windows.Add(custom);
                CheckUtility(custom);
                Check(GetWindow(new WindowInteropHelper(custom).Handle, 4) == new WindowInteropHelper(editor).Handle,
                    "custom editor must retain the settings HWND as owner");

                var preview = new OverlayWindow(discovery, opener, () => settings,
                    monitorMouseButton: false, showInWindowSwitcher: true);
                windows.Add(preview);
                CheckApplicationWindow(preview);

                // Compare native styles with an otherwise identical ordinary window.
                var baseline = new Window { ShowInTaskbar = false, WindowStyle = WindowStyle.None, Topmost = true };
                windows.Add(baseline);
                var utility = new Window { ShowInTaskbar = false, WindowStyle = WindowStyle.None, Topmost = true };
                windows.Add(utility);
                WindowSwitchingHelper.ExcludeFromWindowSwitching(utility);
                new WindowInteropHelper(baseline).EnsureHandle();
                new WindowInteropHelper(utility).EnsureHandle();
                Check(Style(utility) == ((Style(baseline) | ToolWindow) & ~AppWindow),
                    "helper must preserve every unrelated extended style");

                Console.WriteLine("PASS window switching: three edges with immediate HWND range/size updates at 0–100%, 20–80%, 5–30%, pause/resume, position toggles, overlay, clipboard, settings, owned editor, preview, first-show/re-show and unrelated native styles.");
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                clipboard?.Shutdown();
                foreach (var window in windows.AsEnumerable().Reverse())
                    if (window != clipboard) window.Close();
                app.Shutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is null) return 0;
        Console.Error.WriteLine(failure);
        return 1;
    }

    private static void VerifyEdgeRanges(EdgeDropWindow edge, UserSettings settings, EdgeDropWindow.EdgeSide side)
    {
        var area = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        foreach (var (start, end) in new[] { (0d, 100d), (20d, 80d), (5d, 30d), (0d, 100d) })
        {
            var range = new EdgeBandLayout { StartPercent = start, EndPercent = end, WidthPercent = 0.5 };
            switch (side)
            {
                case EdgeDropWindow.EdgeSide.Left: settings.LeftEdgeBand = range; break;
                case EdgeDropWindow.EdgeSide.Right: settings.RightEdgeBand = range; break;
                default: settings.TopEdgeBand = range; break;
            }
            edge.ApplyAppearance(settings); Pump(edge);
            var expected = EdgeBandLayoutMath.CalculateBounds(area, (OverlayPlacementSide)side, range, settings.EdgeBandWidth);
            Check(NativeWindowPlacement.TryGetBounds(edge, out var actual) && Math.Abs(actual.X - expected.X) <= 1 && Math.Abs(actual.Y - expected.Y) <= 1
                && Math.Abs(actual.Width - expected.Width) <= 1 && Math.Abs(actual.Height - expected.Height) <= 1,
                $"{side}: HWND bounds must follow {start}–{end}% immediately; expected {expected}, actual {actual}");
        }
    }

    private static void VerifyFluentPanel(OverlayWindow overlay, UserSettings settings)
    {
        settings.AnimationDurationMs = 80;
        var choices = Enumerable.Range(0, 6).Select(i => new AppCandidate($"test-{i}", $"测试软件 {i + 1}", "unavailable-fixture.exe", null, true)).ToArray();
        var session = new DragSession(["private-first.txt", "private-second.txt"]);
        overlay.BeginSession(session, choices);
        Pump(overlay);
        var heading = (System.Windows.Controls.TextBlock)overlay.FindName("Heading");
        Check(heading.Text == "打开面板" && ((System.Windows.Controls.TextBlock)overlay.FindName("FileContext")).Text == "2 个 TXT 文件 · 本机应用", "A uses compact context without filenames");
        var host = (System.Windows.Controls.Grid)overlay.FindName("CandidatesHost");
        var rows = (System.Windows.Controls.StackPanel)host.Children[0];
        Check(rows.Children.Count == 4 && rows.Children.OfType<System.Windows.Controls.Button>().All(b => b.Height == 60), "A initially shows four 60px software options");
        var actions = (System.Windows.Controls.Grid)overlay.FindName("Actions");
        actions.Children.OfType<System.Windows.Controls.Button>().Single(b => Equals(b.Tag, "更多应用")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        rows = (System.Windows.Controls.StackPanel)host.Children[0];
        Check(rows.Children.Count == 6, "expanded options must retain every candidate");
        overlay.CancelSession();
        Check(session.State == DragSessionState.Cancelled, "cancel must end the session without opening files");
        // A pending exit completion must never hide a newer drag session.
        overlay.BeginSession(new DragSession(["next.txt"]), choices.Take(4).ToArray());
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
        Check(overlay.IsVisible && ((System.Windows.Controls.Border)overlay.FindName("PanelCard")).IsHitTestVisible, "rapid reopen must survive the previous exit completion");
        var card = (System.Windows.Controls.Border)overlay.FindName("PanelCard");
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(card.ActualWidth), (int)Math.Ceiling(card.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(card);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(Path.GetTempPath(), "figma-a-panel.png"))) encoder.Save(stream);
        overlay.BeginSession(new DragSession(["a.txt", "b.pdf"]), []);
        Check(heading.Text == "未找到可同时打开的软件" && actions.Children.OfType<System.Windows.Controls.Button>().Count() == 1, "mixed-file empty state must allow only cancel");
        overlay.CancelSession();
        Console.WriteLine("PASS A panel: file context, four-row density, expansion, cancel, rapid session replacement and mixed-file empty state.");
    }

    private static void CheckUtility(Window window)
    {
        var allowsDrop = window.AllowDrop;
        var showActivated = window.ShowActivated;
        window.SourceInitialized += (_, _) => AssertExcluded(window);
        window.Show();
        Pump(window);
        AssertExcluded(window);
        var initial = Style(window);
        Check((initial & (NoActivate | Transparent)) == 0,
            $"{window.GetType().Name}: input must remain interactive");
        window.Hide();
        window.Show();
        Pump(window);
        AssertExcluded(window);
        Check(Style(window) == initial, "native styles must survive Hide/Show");
        Check(window.AllowDrop == allowsDrop && window.ShowActivated == showActivated,
            "drag/drop and activation preferences must be preserved");
    }

    private static void CheckApplicationWindow(Window window)
    {
        window.Show();
        Pump(window);
        Check(window.ShowInTaskbar && (Style(window) & ToolWindow) == 0 && (Style(window) & AppWindow) != 0,
            $"{window.GetType().Name}: expected a normal switchable application window");
        window.Hide();
        window.Show();
        Pump(window);
        Check((Style(window) & ToolWindow) == 0 && (Style(window) & AppWindow) != 0,
            "application window must remain switchable after Hide/Show");
    }

    private static void AssertExcluded(Window window)
        => Check(!window.ShowInTaskbar && (Style(window) & ToolWindow) != 0 && (Style(window) & AppWindow) == 0,
            $"{window.GetType().Name}: expected TOOLWINDOW without APPWINDOW, got 0x{Style(window):X8}");

    private static int Style(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        Check(handle != IntPtr.Zero, "expected a real HWND");
        return GetWindowLong(handle, -20);
    }

    private static void Pump(Window window)
        => window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class MemorySettingsStore(UserSettings settings) : ISettingsStore
    {
        public UserSettings Load() => settings.Clone();
        public void Save(UserSettings value) => throw new InvalidOperationException("Unexpected settings write");
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr handle, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr handle, uint command);
}
