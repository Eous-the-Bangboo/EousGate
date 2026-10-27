using EousGate;
using EousGate.Infrastructure;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class WorkbenchSmoke
{
    internal static int Run(string[] args)
    {
        var output = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(Path.GetTempPath(), "workbench-ui"));
        Directory.CreateDirectory(output);
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new App(false); app.InitializeComponent(); app.ApplyTheme(new UserSettings { Theme = "Light" });
            var frame = new DispatcherFrame();
            app.Exit += (_, _) => frame.Continue = false;
            app.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await Verify(output); }
                catch (Exception ex) { failure = ex; }
                finally { app.Shutdown(); }
            }));
            Dispatcher.PushFrame(frame);
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60))) { Console.Error.WriteLine("Workbench smoke timed out"); return 1; }
        if (failure is not null) { Console.Error.WriteLine(failure); return 1; }
        Console.WriteLine("PASS workbench: file tab borders/labels/gaps and ancestor clipping, range settings at minimum/normal sizes; three repeated optimistic switch/failure checks; actual owned dialogs, inside/outside clicks, async close, saving guard, failure retention, apply/readback, native settings, resize, 100/150/200% rendering. No user clipboard or processes touched.");
        return 0;
    }

    private static async Task Verify(string output)
    {
        var manager = new ToolManager();
        foreach (var (id, english, name, detail, glyph) in new[] {
            ("file-hop", "EousFileHop", "文件快开", "把文件拖到屏幕边缘，选择软件打开。", "\uE8A7"),
            ("web-hop", "EousWebHop", "网页快开", "复制含网页地址的文本，从侧边快速打开。", "\uE774"),
            ("folder-hop", "EousFolderHop", "文件夹快跳", "记录常用目录，在打开和保存窗口快速跳转。", "\uE8B7") })
            manager.Register(new ManagedTool(new(id, english, name, detail, glyph), () => Task.CompletedTask, () => Task.CompletedTask, () => { }));
        foreach (var definition in ExternalProgramCatalog.All)
            manager.Register(new ManagedTool(definition.Descriptor, () => Task.CompletedTask, () => Task.CompletedTask, () => { }));
        var owner = new AssistantWindow(manager, () => { }, SkinCatalog.DefaultId, 160, () => { })
            { WindowStartupLocation = WindowStartupLocation.Manual, Left = -16000, Top = -16000, ShowActivated = false };
        owner.Show();
        await Settled(owner);
        Check(!Descendants<CheckBox>(owner).Any(), "home must hide batch selection controls");
        var natives = (ItemsControl)owner.FindName("ToolList");
        var nativeRects = natives.Items.Cast<object>().Select(item => (FrameworkElement)natives.ItemContainerGenerator.ContainerFromItem(item)).Select(item => item.TransformToAncestor(owner).Transform(new Point())).ToArray();
        Check(nativeRects.Length == 3 && nativeRects.Select(p => p.X).Distinct().Count() == 1 && nativeRects[0].Y < nativeRects[1].Y && nativeRects[1].Y < nativeRects[2].Y, "native tools must form one aligned vertical list");
        Capture(owner, null, Path.Combine(output, "workbench-home.png"));
        Capture(owner, null, Path.Combine(output, "workbench-home-200.png"), 2);
        Check(((ScrollViewer)owner.FindName("ToolArea")).ScrollableHeight < 1, "all six standard tools must fit at the default workbench size");
        var switches = Descendants<ToolSwitch>(owner).ToArray();
        Check(switches.Length == 6 && switches.All(s => ((Border)s.Template.FindName("Track", s)).CornerRadius == new CornerRadius(12)), "all six switches must use the chosen rounded shape");
        Check(owner.FindName("EousCharacter") is Image { Source: not null } && owner.FindName("RunningCount") is null, "original character must live in the sidebar, without a profile heading or redundant running counter");

        var settings = new UserSettings { AnimationDurationMs = 160 };
        var store = new JsonSettingsStore(Path.Combine(output, "isolated-settings.json"));
        UserSettings? applied = null;
        for (var round = 0; round < 3; round++)
        {
            ToolSwitchSmoke.Verify();
            var editor = new SettingsWindow(settings, store, new AppDiscovery(), value => applied = value);
            WorkbenchDialog.Attach(editor, owner, 160); editor.ShowActivated = false; editor.Show();
            editor.SelectSection(2); await Settled(editor);
            if (round == 0) Capture(owner, editor, Path.Combine(output, "workbench-web-settings.png"));
            var backdrop = (Grid)editor.Content;
            var card = (Border)backdrop.Children[0];
            ClickOutside(card);
            Check(editor.IsVisible && card.IsHitTestVisible, "inside click must not close the settings");
            if (round == 0)
            {
                ((CheckBox)editor.FindName("ClipboardAutoOpen")).IsChecked = true;
                ((TextBox)editor.FindName("ClipboardCopyCount")).Text = "2";
                ((Button)editor.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(applied is { ClipboardAutoOpenEnabled: true, ClipboardAutoOpenCopyCount: 2 } && editor.IsVisible, "Apply must save and keep the dialog open");
                Check(store.Load().ClipboardAutoOpenCopyCount == 2, "saved copy count must read back");
                owner.Width = 900; owner.Height = 600; await Until(() => editor.ActualWidth == owner.ActualWidth && editor.ActualHeight == owner.ActualHeight - 40);
                Capture(owner, editor, Path.Combine(output, "workbench-settings-min.png"));
                Check(((Button)editor.FindName("SaveButton")).ActualWidth >= 86 && card.ActualWidth <= owner.ActualWidth - 50, "minimum window must retain dialog margins and save action");
                editor.SelectSection(1); await Settled(editor);
                VerifyFileTabHeaders(editor);
                Capture(owner, editor, Path.Combine(output, "workbench-file-range-min.png"));
                owner.Width = 1040; owner.Height = 820; await Settled(editor);
                VerifyFileTabHeaders(editor);
                Capture(owner, editor, Path.Combine(output, "workbench-file-range.png"));
                Capture(owner, editor, Path.Combine(output, "workbench-file-range-150.png"), 1.5);
                Capture(owner, editor, Path.Combine(output, "workbench-file-range-200.png"), 2);
                editor.SelectSection(3); await Settled(editor);
                VerifyFileTabHeaders(editor);
                Capture(owner, editor, Path.Combine(output, "workbench-file-types.png"));
                Check(((StackPanel)editor.FindName("ExtensionBrowser")).Visibility == Visibility.Collapsed, "software list must not be pushed below an expanded suffix browser by default");
                ((Button)editor.FindName("ChooseExtensionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Settled(editor);
                Capture(owner, editor, Path.Combine(output, "workbench-file-types-expanded.png"));
                ((Button)editor.FindName("ChooseExtensionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(((TextBlock)editor.FindName("LoadedLabel")).Text == ".mp4", "collapsing suffix chooser retains the current type and draft");
            }
            ClickOutside(backdrop);
            await Until(() => !editor.IsVisible);
            Check(owner.IsVisible, "outside close must retain the workbench");
        }

        var failingPath = Path.Combine(output, "blocked-store"); Directory.CreateDirectory(failingPath);
        var failedEditor = new SettingsWindow(settings, new JsonSettingsStore(failingPath), new AppDiscovery(), _ => throw new Exception("failed storage must not apply"));
        WorkbenchDialog.Attach(failedEditor, owner, 160); failedEditor.ShowActivated = false; failedEditor.Show(); failedEditor.SelectSection(2); await Settled(failedEditor);
        ((CheckBox)failedEditor.FindName("ClipboardAutoOpen")).IsChecked = true;
        ((TextBox)failedEditor.FindName("ClipboardCopyCount")).Text = "4";
        ((Button)failedEditor.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(failedEditor.IsVisible && ((TextBox)failedEditor.FindName("ClipboardCopyCount")).Text == "4" && ((TextBlock)failedEditor.FindName("ApplyStatusText")).Text.Contains("失败"), "save failure must retain draft and position");
        Capture(owner, failedEditor, Path.Combine(output, "workbench-save-failure.png"));
        failedEditor.Close();
        var discard = Descendants<Button>(failedEditor).Single(b => Equals(b.Content, "放弃修改"));
        discard.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Until(() => !failedEditor.IsVisible);

        var gate = new TaskCompletionSource();
        var program = new CustomProgramWindow(null, _ => gate.Task, null, SkinCatalog.DefaultId);
        WorkbenchDialog.Attach(program, owner, 160); program.ShowActivated = false; program.Show(); await Settled(program);
        Capture(owner, program, Path.Combine(output, "workbench-program.png"));
        ((TextBox)program.FindName("ProgramName")).Text = "保留输入";
        ((Button)program.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ClickOutside((Grid)program.Content);
        Check(program.IsVisible && !((Button)program.FindName("CancelButton")).IsEnabled, "saving must prevent accidental outside dismissal");
        owner.Close(); Check(owner.IsVisible && program.IsVisible, "closing the owner must also preserve an editor that is saving");
        gate.SetException(new IOException("无法写入配置文件"));
        await Until(() => ((Button)program.FindName("CancelButton")).IsEnabled);
        program.UpdateLayout();
        Check(((TextBox)program.FindName("ProgramName")).Text == "保留输入", "failed async save must retain input");
        var error = (TextBlock)program.FindName("EditorStatus");
        var scroll = Descendants<ScrollViewer>(program).First(s => s.Content is StackPanel);
        var errorBounds = error.TransformToAncestor(scroll).TransformBounds(new Rect(error.RenderSize));
        Check(errorBounds.Top >= -1 && errorBounds.Bottom <= scroll.ViewportHeight + 1, "full failure message must be visible inside the editor viewport");
        Capture(owner, program, Path.Combine(output, "workbench-program-failure.png"));
        ClickOutside((Grid)program.Content); await Until(() => !program.IsVisible);
        var backend = new CustomProgramTests.Backend();
        var preset = new ExternalProgramSettingsWindow(new ExternalProgramTool(ExternalProgramCatalog.All[0], backend, () => new(), () => { }), backend,
            _ => throw new IOException("测试路径保存失败"), SkinCatalog.DefaultId);
        WorkbenchDialog.Attach(preset, owner, 160); preset.ShowActivated = false; preset.Show(); await Settled(preset);
        ((TextBox)preset.FindName("ExecutablePath")).Text = @"C:\Fixture\Example.exe";
        ((Button)preset.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        preset.Close(); owner.Close(); Check(preset.IsVisible && owner.IsVisible, "preset async save must protect both drawer and owner");
        await Until(() => ((Button)preset.FindName("SaveButton")).IsEnabled);
        Check(((TextBox)preset.FindName("ExecutablePath")).Text == @"C:\Fixture\Example.exe" && ((TextBlock)preset.FindName("Status")).Text.Contains("保存失败"), "preset save failure retains editable path");
        Capture(owner, preset, Path.Combine(output, "workbench-preset-failure.png")); preset.Close(); await Until(() => !preset.IsVisible);
        var recording = new SettingsWindow(settings, store, new AppDiscovery(), _ => { });
        WorkbenchDialog.Attach(recording, owner, 220); recording.ShowActivated = false;
        var frames = new List<object>(); var clock = Stopwatch.StartNew();
        recording.Show(); recording.SelectSection(2);
        var motionOutput = Path.Combine(output, "dialog-motion"); Directory.CreateDirectory(motionOutput);
        async Task Sample(string phase, Func<bool> done)
        {
            var phaseClock = Stopwatch.StartNew(); var index = 0;
            do
            {
                var name = $"{phase}-{index++:000}.png";
                Capture(owner, recording.IsVisible ? recording : null, Path.Combine(motionOutput, name));
                frames.Add(new { file = name, timestampMs = clock.ElapsedMilliseconds, phase });
                await Task.Delay(30);
                Check(phaseClock.Elapsed.TotalSeconds < 5, "recorded native transition must complete");
            } while (!done());
            Capture(owner, recording.IsVisible ? recording : null, Path.Combine(motionOutput, $"{phase}-end.png"));
            frames.Add(new { file = $"{phase}-end.png", timestampMs = clock.ElapsedMilliseconds, phase });
        }
        var animatedCard = (Border)((Grid)recording.Content).Children[0];
        await Sample("open", () => !Animating(animatedCard));
        recording.Close();
        await Sample("close", () => !recording.IsVisible);
        File.WriteAllText(Path.Combine(motionOutput, "frames.json"), System.Text.Json.JsonSerializer.Serialize(frames));

        for (var round = 0; round < 3; round++)
        {
            var guarded = new SettingsWindow(settings, store, new AppDiscovery(), _ => { });
            WorkbenchDialog.Attach(guarded, owner, 120); guarded.ShowActivated = false; guarded.Show(); guarded.SelectSection(2); await Settled(guarded);
            ((TextBox)guarded.FindName("ClipboardDuration")).Text = "14";
            ClickOutside((Grid)guarded.Content);
            Check(guarded.IsVisible && Descendants<Button>(guarded).Any(b => Equals(b.Content, "继续编辑")), "outside click with a draft must offer save/discard/continue");
            Descendants<Button>(guarded).Single(b => Equals(b.Content, "继续编辑")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(((TextBox)guarded.FindName("ClipboardDuration")).Text == "14", "continue editing must preserve input");
            guarded.Close();
            if (round == 0) Capture(owner, guarded, Path.Combine(output, "workbench-unsaved.png"));
            Descendants<Button>(guarded).Single(b => Equals(b.Content, "保存修改")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => !guarded.IsVisible);
            Check(store.Load().ClipboardPopupDurationSeconds == 14, "save from close guard must persist and close");
        }

        var overview = new SettingsWindow(settings, store, new AppDiscovery(), _ => { });
        WorkbenchDialog.Attach(overview, owner, 160); overview.ShowActivated = false; overview.Show(); overview.SelectSection(0); await Settled(overview);
        Capture(owner, overview, Path.Combine(output, "workbench-general.png"));
        overview.SelectSection(4); await Settled(overview); Capture(owner, overview, Path.Combine(output, "workbench-about.png"));
        overview.Close(); await Until(() => !overview.IsVisible);
        var history = new EousGate.FolderHop.HistoryStore(Path.Combine(output, "isolated-folder-history"));
        var folder = new FolderSettingsWindow(history, () => null, SkinCatalog.DefaultId);
        WorkbenchDialog.Attach(folder, owner, 160); folder.ShowActivated = false; folder.Show(); await Settled(folder);
        Capture(owner, folder, Path.Combine(output, "workbench-folder-settings.png")); folder.Close(); await Until(() => !folder.IsVisible);
        history.Touch(@"D:\Work\年度报告"); history.Touch(@"D:\Design\品牌素材");
        using (var picker = new EousGate.FolderHop.PickerForm(history, showWithoutActivation: true))
        {
            picker.StartPosition = System.Windows.Forms.FormStartPosition.Manual; picker.Location = new System.Drawing.Point(-16000, -16000);
            picker.Show(); System.Windows.Forms.Application.DoEvents(); picker.PerformLayout();
            using var bitmap = new System.Drawing.Bitmap(picker.Width, picker.Height);
            picker.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(Path.Combine(output, "native-folder-picker-device.png"));
            using var logical = new System.Drawing.Bitmap((int)Math.Round(bitmap.Width * 96d / picker.DeviceDpi), (int)Math.Round(bitmap.Height * 96d / picker.DeviceDpi));
            using (var graphics = System.Drawing.Graphics.FromImage(logical)) { graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; graphics.DrawImage(bitmap, 0, 0, logical.Width, logical.Height); }
            logical.Save(Path.Combine(output, "native-folder-picker.png"));
            File.WriteAllText(Path.Combine(output, "folder-capture-dpi.txt"), $"Source DeviceDpi={picker.DeviceDpi}; source={bitmap.Width}x{bitmap.Height}; normalized to 96 DPI logical units={logical.Width}x{logical.Height} for comparison with WPF RenderTargetBitmap.");
        }
        owner.Close();
    }

    private static void VerifyFileTabHeaders(SettingsWindow editor)
    {
        var tabs = (TabControl)editor.FindName("SettingsTabs");
        var visible = tabs.Items.Cast<TabItem>().Where(t => t.Visibility == Visibility.Visible).ToArray();
        Check(visible.Length == 2, "file settings retain exactly two visible tabs");
        Rect? previous = null;
        foreach (var tab in visible)
        {
            var surface = (Border)tab.Template.FindName("Tab", tab);
            var presenter = (ContentPresenter)tab.Template.FindName("TabHeader", tab);
            var bounds = surface.TransformToAncestor(tabs).TransformBounds(new Rect(surface.RenderSize));
            Check(bounds.Left >= 0 && bounds.Right <= tabs.ActualWidth && bounds.Height >= 36, "whole tab surface must stay inside its header row");
            Check(previous is null || bounds.Left >= previous.Value.Right + 7, "file tabs must retain their gap without covering the neighboring border");
            previous = bounds;
            for (DependencyObject? element = surface; element is Visual && element != tabs; element = VisualTreeHelper.GetParent(element))
            {
                var local = surface.TransformToAncestor((Visual)element).TransformBounds(new Rect(surface.RenderSize));
                var clip = VisualTreeHelper.GetClip((Visual)element);
                Check(clip is null || new Rect(clip.Bounds.X - 1, clip.Bounds.Y - 1, clip.Bounds.Width + 2, clip.Bounds.Height + 2).Contains(local), "tab border must not be clipped by an ancestor");
            }
            Check(presenter.DesiredSize.Width <= presenter.ActualWidth + 1, "full tab label must be visible");
        }
    }

    private static bool Animating(Border card) => card.RenderTransform is TransformGroup transforms && transforms.Children.Any(t => t.HasAnimatedProperties);
    private static void ClickOutside(UIElement element) => element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });
    private static async Task Settled(Window window)
    {
        window.UpdateLayout();
        if (window.Content is Grid grid && grid.Children.OfType<Border>().FirstOrDefault() is { } card)
            await Until(() => !Animating(card) && window.Opacity >= .999);
        else await Task.Delay(500);
        window.UpdateLayout();
    }
    private static async Task Until(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition()) { if (timer.Elapsed.TotalSeconds > 5) throw new Exception("workbench condition timeout"); await Task.Delay(16); }
    }
    private static void Capture(Window owner, Window? overlay, string path, double scale = 1)
    {
        owner.UpdateLayout(); overlay?.UpdateLayout();
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, owner.ActualWidth, owner.ActualHeight);
            drawing.DrawRectangle(new VisualBrush(owner), null, bounds);
            if (overlay is not null) drawing.DrawRectangle(new VisualBrush(overlay), null, new Rect(0, 40, bounds.Width, bounds.Height - 40));
        }
        var bitmap = new RenderTargetBitmap((int)(owner.ActualWidth * scale), (int)(owner.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match; foreach (var item in Descendants<T>(child)) yield return item; }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
