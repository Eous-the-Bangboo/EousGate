using EousGate;
using EousGate.Infrastructure;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using Button = System.Windows.Controls.Button;

internal static class AssistantVisualSmoke
{
    internal static int Run(string[] args)
    {
        var output = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(Path.GetTempPath(), "EousAssistant-visual"));
        Directory.CreateDirectory(output);
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new EousGate.App(false); app.InitializeComponent();
            try
            {
                var manager = new ToolManager();
                foreach (var (id, english, chinese, description, glyph) in new[]
                {
                    ("file-hop", "EousFileHop", "文件快开", "把文件拖到屏幕边缘，选择软件打开。", "\uE8A7"),
                    ("web-hop", "EousWebHop", "网页快开", "复制网页地址后，从侧边提示快速打开。", "\uE774"),
                    ("folder-hop", "EousFolderHop", "文件夹快跳", "记录资源管理器目录，在打开和保存窗口快速跳转。", "\uE8B7")
                }) manager.Register(new ManagedTool(new(id, english, chinese, description, glyph), () => Task.CompletedTask, () => Task.CompletedTask, () => { }));
                foreach (var definition in ExternalProgramCatalog.All)
                    manager.Register(new ManagedTool(definition.Descriptor, () => Task.CompletedTask, () => Task.CompletedTask, () => { }));
                var panel = new AssistantWindow(manager, () => { }, "mica");
                var content = (FrameworkElement)panel.Content;
                panel.Content = null;
                var root = new Border { Child = content };
                root.SetResourceReference(Border.BackgroundProperty, "PanelBackground");
                panel.Content = root;
                void Layout(double width = 980, double height = 720)
                {
                    root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
                }
                Layout();
                var toolScroll = (ScrollViewer)panel.FindName("ToolArea");
                Capture(root, Path.Combine(output, "workbench-initial.png"), 1);
                Check(toolScroll.ViewportHeight > 400 && toolScroll.ExtentWidth <= toolScroll.ViewportWidth + 1, "workbench must preserve a useful scrolling workspace without horizontal overflow");
                VerifyMotion();
                ToolSwitchSmoke.Verify();
                Check(((ItemsControl)panel.FindName("ToolList")).Items.Count == 3 && ((ItemsControl)panel.FindName("ExternalToolList")).Items.Count == 3, "three native and three external tools must be grouped separately");
                Check(Descendants<TextBlock>(root).Any(t => t.Text == "Eous原生工具") && Descendants<TextBlock>(root).Any(t => t.Text == "非Eous原生工具"), "both requested section headings must be visible in the document");
                Check(manager.Tools.All(t => t.State == ToolState.Stopped), "panel must not auto-start tools");
                var buttons = Descendants<Button>(root).ToArray();
                void Click(string text) => buttons.Single(b => Equals(b.Content, text)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                manager.Tools[0].Selected = false;
                Check(manager.Tools.All(t => t.State == ToolState.Stopped), "selection is separate from actual state");
                Check(!Descendants<CheckBox>(root).Any() && !buttons.Any(b => Equals(b.Content, "启动勾选") || Equals(b.Content, "关闭勾选")), "batch selection controls must be hidden in the workbench");
                Click("全部启动"); Check(manager.Tools.All(t => t.State == ToolState.Running), "start all button wiring");
                Check(Descendants<ToolSwitch>(root).Count() == 6 && Descendants<ToolSwitch>(root).All(t => t.IsChecked == true), "batch start must update all six switches");
                Click("全部关闭"); Check(manager.Tools.All(t => t.State == ToolState.Stopped), "stop all button wiring");
                Check(Descendants<ToolSwitch>(root).All(t => t.IsChecked == false), "batch stop must update every switch");
                panel.Report(string.Join("；", Enumerable.Repeat("测试工具失败，请查看详细原因后重试。", 8)));
                Layout();
                var feedback = (TextBlock)panel.FindName("Feedback");
                Check(feedback.TransformToAncestor(root).Transform(new Point()).Y >= toolScroll.TransformToAncestor(root).Transform(new Point(0, toolScroll.ActualHeight)).Y,
                    "multiline feedback must reserve space instead of covering the tool list");
                foreach (var theme in new[] { "Light", "Dark" })
                {
                    app.ApplyTheme(new UserSettings { Theme = theme }); Layout();
                    foreach (var scale in new[] { 1d, 1.25, 1.5 }) Capture(root, Path.Combine(output, $"assistant-{theme}-{scale:0.##}.png"), scale);
                    var scroll = (ScrollViewer)panel.FindName("ToolArea");
                    scroll.ScrollToEnd(); Layout();
                    Capture(root, Path.Combine(output, $"assistant-{theme}-external.png"), 1);
                    scroll.ScrollToHome(); Layout();
                    foreach (var button in Descendants<Button>(root).Where(b => b.IsVisible))
                        Check(button.ActualHeight >= 30 && button.DesiredSize.Width <= button.ActualWidth + 1, "button clipped: " + button.Content);
                    Layout(840, 540);
                    Check(Descendants<Button>(root).Where(b => b.IsVisible).All(b => b.ActualWidth >= 30), "minimum panel size must preserve buttons");
                    Layout();
                }
                SkinVisualSmoke.Verify(app, manager, output);
                var folderStore = new EousGate.FolderHop.HistoryStore(Path.Combine(output, "folder-data"));
                folderStore.Data.Paused = true;
                // No real user clipboard, foreground input, file dialogs or Explorer navigation in this check.
                var frame = new DispatcherFrame();
                app.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        for (var i = 0; i < 3; i++)
                        {
                            var session = new EousGate.FolderHop.FolderSession(folderStore, _ => { });
                            Check(!session.Picker.Visible && !session.Button.Visible, "starting FolderHop must not display a picker");
                            await session.StopAsync(); await session.StopAsync();
                            Check(session.IsStopped && session.Picker.IsDisposed && session.Button.IsDisposed, "stop must release native windows");
                        }
                    }
                    catch (Exception ex) { failure = ex; }
                    finally { frame.Continue = false; }
                }));
                Dispatcher.PushFrame(frame);
                panel.Close();
                if (failure is not null) throw failure;
                Console.WriteLine("PASS assistant UI: native/external tool lists, all actions, hidden batch selection, manual startup, light/dark 100/125/150% rendering, external group scrolling, minimum layout and three FolderHop start-stop cycles (no desktop input).");
            }
            catch (Exception ex) { failure = ex; }
            finally { app.Shutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60))) { Console.Error.WriteLine("Assistant UI check timed out"); return 1; }
        if (failure is not null) { Console.Error.WriteLine(failure); return 1; }
        return 0;
    }
    private static void VerifyMotion()
    {
        using var motion = new FluentMotion();
        var target = new Border();
        motion.Enter(target, 160, 0, 160, x: -12, y: 0, enabled: true);
        // A detached visual has no render ticks. Seek the same WPF timeline explicitly.
        var clock = FluentMotion.Animation(0, 1, 160).CreateClock();
        target.ApplyAnimationClock(UIElement.OpacityProperty, clock);
        clock.Controller!.SeekAlignedToLastTick(TimeSpan.FromMilliseconds(50), TimeSeekOrigin.BeginTime);
        Check(target.Opacity is > 0 and < 1, "entrance must interpolate before settling");
        motion.StopAll();
        Check(target.Opacity == 1 && ((TranslateTransform)target.RenderTransform).X == 0 && !target.HasAnimatedProperties,
            "cancel must release animation clocks and expose the final layout");
        motion.Enter(target, 160, 0, 40, y: 6, enabled: true);
        clock = FluentMotion.Animation(0, 1, 40).CreateClock();
        target.ApplyAnimationClock(UIElement.OpacityProperty, clock);
        clock.Controller!.SeekAlignedToLastTick(TimeSpan.FromMilliseconds(100), TimeSeekOrigin.BeginTime);
        Check(target.Opacity == 1 && ((TranslateTransform)target.RenderTransform).Y == 0, "entry must settle once without looping");
        motion.Enter(target, 600, 120, 200, x: -12, enabled: false);
        Check(target.Opacity == 1 && !target.HasAnimatedProperties && ((TranslateTransform)target.RenderTransform).X == 0,
            "disabled animation must immediately expose all controls without translation or stale clocks");
        Console.WriteLine("PASS A motion: interpolation, one-shot endpoints, interrupted entrance, repeated entry and reduced-motion cleanup.");
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void Capture(FrameworkElement root, string path, double scale)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * scale), (int)Math.Ceiling(root.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
