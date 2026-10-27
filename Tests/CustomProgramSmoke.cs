using EousGate;
using EousGate.Infrastructure;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;

internal static class CustomProgramSmoke
{
    internal static int Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new App(false); app.InitializeComponent(); var frame = new DispatcherFrame();
            app.Exit += (_, _) => frame.Continue = false;
            app.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await Verify(app); }
                catch (Exception ex) { failure = ex; }
                finally { app.Shutdown(); }
            }));
            Dispatcher.PushFrame(frame);
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(40))) { Console.Error.WriteLine("custom program smoke timeout"); return 1; }
        if (failure is not null) { Console.Error.WriteLine(failure); return 1; }
        Console.WriteLine("PASS custom host/editor: live add, green switch, running edit rejection, edit/selection, stale general settings preservation, live removal without stopping and restart persistence.");
        return 0;
    }
    private static async Task Verify(App app)
    {
        var data = Path.Combine(Path.GetTempPath(), "EousAssistant-custom-" + Guid.NewGuid());
        Directory.CreateDirectory(data);
        var fixturePath = Path.Combine(data, "Example.exe");
        File.Copy(Environment.ProcessPath!, fixturePath); // Metadata/icon fixture only; the fake backend never executes it.
        var backend = new CustomProgramTests.Backend();
        app.InitializeAssistant(data, tray: false, showPanel: true, externalController: backend);
        var panel = app.Windows.OfType<AssistantWindow>().Single();
        panel.ShowActivated = false;
        await Until(() => app.Tools.Tools.OfType<ExternalProgramTool>().All(t => t.StateText != "检测中"));
        // Leave a general-settings draft open before custom additions to test merge-on-apply.
        Click(panel, "NavGeneral"); var general = app.Windows.OfType<SettingsWindow>().Single();
        Check(ReferenceEquals(general.Owner, panel) && general.AllowsTransparency, "general settings must open within the real workbench");
        app.Tools.Tools.Single(t => t.Descriptor.Id == "file-hop").OpenSettings();
        Check(((TabControl)general.FindName("SettingsTabs")).SelectedIndex == 1, "file settings entry must select its own section");
        app.Tools.Tools.Single(t => t.Descriptor.Id == "web-hop").OpenSettings();
        Check(((TabControl)general.FindName("SettingsTabs")).SelectedIndex == 2, "web settings entry must select its own section");
        app.Tools.Tools.Single(t => t.Descriptor.Id == "folder-hop").OpenSettings();
        var folderSettings = app.Windows.OfType<FolderSettingsWindow>().Single();
        Check(ReferenceEquals(folderSettings.Owner, panel) && folderSettings.AllowsTransparency, "folder settings must share the workbench modal shell");
        folderSettings.Close();
        await Until(() => !app.Windows.OfType<FolderSettingsWindow>().Any());
        Click(panel, "AddProgramButton"); var editor = app.Windows.OfType<CustomProgramWindow>().Single();
        ((TextBox)editor.FindName("ProgramName")).Text = "自定义测试程序";
        ((TextBox)editor.FindName("ProgramPath")).Text = fixturePath;
        ((TextBox)editor.FindName("StartArguments")).Text = "--label \"two words\"";
        Click(editor, "SaveButton");
        try { await Until(() => app.Tools.Tools.Count == 7 && !app.Windows.OfType<CustomProgramWindow>().Any()); }
        catch (Exception ex) { throw new Exception($"custom add: tools={app.Tools.Tools.Count}, editors={app.Windows.OfType<CustomProgramWindow>().Count()}, status={((TextBlock)editor.FindName("EditorStatus")).Text}", ex); }
        var custom = app.Tools.Tools.OfType<ExternalProgramTool>().Single(t => t.Descriptor.Id.StartsWith("custom-"));
        await Until(() => custom.State == ToolState.Stopped && custom.CanStart);
        Check(((ItemsControl)panel.FindName("ExternalToolList")).Items.Count == 4 && backend.Starts == 0, "add must refresh the existing list without starting");
        app.Tools.Tools.Single(t => t.Descriptor.Id == "external-ditto").OpenSettings();
        var presetEditor = app.Windows.OfType<ExternalProgramSettingsWindow>().Single();
        ((TextBox)presetEditor.FindName("ExecutablePath")).Text = fixturePath; Click(presetEditor, "SaveButton");
        await Until(() => ((TextBlock)presetEditor.FindName("Status")).Text.Contains("自定义程序管理"));
        presetEditor.Close();
        var apply = Descendants<Button>((DependencyObject)general.Content).Single(b => Equals(b.Content, "应用")); apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(new JsonSettingsStore(Path.Combine(data, "settings.json")).Load().CustomPrograms.Count == 1, "applying an older general draft must preserve newly added custom definitions");
        general.Close();
        custom.Selected = false;
        await custom.StartAsync();
        panel.UpdateLayout();
        var control = Descendants<ToolSwitch>((DependencyObject)panel.Content).Single(t => ReferenceEquals(t.Tag, custom));
        Check(control.IsChecked == true, "custom process state must drive the green switch");
        Check(Descendants<System.Windows.Controls.Image>((DependencyObject)panel.Content).Any(image => image.Source is not null), "custom EXE icons must render from the selected file");
        custom.OpenSettings(); editor = app.Windows.OfType<CustomProgramWindow>().Single();
        ((CheckBox)editor.FindName("ForceStop")).IsChecked = true;
        Click(editor, "SaveButton");
        await Until(() => !app.Windows.OfType<CustomProgramWindow>().Any());
        Check(app.Tools.Tools.Contains(custom) && custom.Definition.ForceTerminateOnTimeout && custom.State == ToolState.Running && !custom.Selected,
            "running stop policy save must take effect without replacing the tool or losing state/selection");
        custom.OpenSettings(); editor = app.Windows.OfType<CustomProgramWindow>().Single();
        Check(((CheckBox)editor.FindName("ForceStop")).IsChecked == true, "running stop policy must survive editor reopen");
        ((Expander)editor.FindName("Advanced")).IsExpanded = true; editor.UpdateLayout();
        Capture(editor, "custom-program-editor.png");
        app.ApplyTheme(new UserSettings { SkinId = SkinCatalog.FocusDarkId }); editor.UpdateLayout();
        Check(Descendants<Label>((DependencyObject)editor.Content).All(label => ((SolidColorBrush)label.Foreground).Color == ((SolidColorBrush)editor.FindResource("PanelForeground")).Color),
            "dark editor labels must use readable theme foreground");
        Capture(editor, "custom-program-editor-dark.png");
        panel.Width = 900; panel.Height = 600; editor.UpdateLayout();
        Check(((Button)editor.FindName("SaveButton")).ActualWidth >= 84 && ((Button)editor.FindName("CancelButton")).ActualWidth >= 84,
            "minimum editor size must preserve save and cancel");
        Capture(editor, "custom-program-editor-min.png");
        panel.Width = 1040; panel.Height = 820; app.ApplyTheme(new UserSettings()); editor.UpdateLayout();
        ((TextBox)editor.FindName("ProgramName")).Text = "编辑后的程序"; Click(editor, "SaveButton");
        await Until(() => ((TextBlock)editor.FindName("EditorStatus")).Text.Contains("请先停止"));
        Check(app.Tools.Tools.Contains(custom), "running edit must keep the existing tool");
        await custom.StopAsync(); Click(editor, "SaveButton");
        await Until(() => !app.Tools.Tools.Contains(custom));
        var edited = app.Tools.Tools.OfType<ExternalProgramTool>().Single(t => t.Descriptor.Id == custom.Descriptor.Id);
        Check(edited.Descriptor.ChineseName == "编辑后的程序" && !edited.Selected, "edit keeps stable identity and batch selection");
        await edited.StartAsync();
        panel.UpdateLayout();
        await Task.Delay(900); // Let A entrance and knob motion settle before the rendered capture.
        Capture(panel, "custom-program-home.png");
        var stops = backend.Stops;
        edited.OpenSettings(); editor = app.Windows.OfType<CustomProgramWindow>().Single(); Click(editor, "RemoveButton");
        await Until(() => app.Tools.Tools.Count == 6);
        Check(backend.Stops == stops && backend.Running.ContainsKey(fixturePath), "removing a running program must not stop it");
        Check(new JsonSettingsStore(Path.Combine(data, "settings.json")).Load().CustomPrograms.Count == 0, "removal persists");
        await app.CustomPrograms!.SaveAsync(new() { Name = "重开保留", ExecutablePath = @"C:\Fixture\Persist.exe" });
        panel.Close(); app.ShowPanel();
        Check(((ItemsControl)app.Windows.OfType<AssistantWindow>().Single().FindName("ExternalToolList")).Items.Count == 4, "reopening the panel retains custom programs");
        Check(new JsonSettingsStore(Path.Combine(data, "settings.json")).Load().CustomPrograms.Single().Name == "重开保留", "restart settings retain custom definitions");
    }
    private static void Capture(Window visual, string name)
    {
        var bitmap = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(Path.GetTempPath(), name)); encoder.Save(stream);
    }
    private static void Click(Window window, string name) => ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static async Task Until(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition()) { if (clock.Elapsed.TotalSeconds > 6) throw new Exception("custom condition timeout"); await Task.Delay(20); }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
