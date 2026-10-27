using EousGate;
using EousGate.Infrastructure;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;

internal static class FileHopSettingsSmoke
{
    internal static int Run(string[] args)
    {
        var output = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(Path.GetTempPath(), "Eous-file-hop"));
        Directory.CreateDirectory(output);
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new EousGate.App(false); app.InitializeComponent();
            try
            {
                VerifyEditing(app, output);
                VerifyRanges(app, output);
                VerifyRendering(app, output);
                Console.WriteLine("PASS File-hop WPF: range defaults, invalid endpoints retain draft without saving, independent fractional endpoints apply/reopen; category/search, draft switching, validation, order/visibility, reset, save failure/cancel, A light/dark and B at minimum size and 100/150/200% offscreen.");
            }
            catch (Exception ex) { failure = ex; }
            finally { app.Shutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60))) { Console.Error.WriteLine("File-hop UI timed out"); return 1; }
        if (failure is not null) { Console.Error.WriteLine(failure); return 1; }
        return 0;
    }

    private static void VerifyRanges(EousGate.App app, string output)
    {
        var settings = new UserSettings(); app.ApplyTheme(settings);
        var store = new JsonSettingsStore(Path.Combine(output, "edge-range-settings.json"));
        store.Save(settings);
        UserSettings? applied = null;
        var editor = new SettingsWindow(settings, store, new AppDiscovery(), value => applied = value);
        editor.Resources["WorkbenchDialogMode"] = true;
        editor.SelectSection(1);
        TextBox Input(string name) => (TextBox)editor.FindName(name);
        void Apply() => ((Button)editor.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        foreach (var side in new[] { "Left", "Right", "Top" })
            Check(Input(side + "BandStart").Text == "0" && Input(side + "BandEnd").Text == "100", "new range editors must show full coverage");
        foreach (var (start, end) in new[] { ("80", "20"), ("20", "20"), ("-1", "80"), ("0", "101"), ("NaN", "100"), ("", "100") })
        {
            Input("LeftBandStart").Text = start; Input("LeftBandEnd").Text = end;
            editor.SelectSection(2);
            Apply();
            Check(applied is null && store.Load().LeftEdgeBand!.StartPercent == 0, "invalid ranges must not apply or write settings");
            Check(((TextBlock)editor.FindName("EdgeRangeError")).Visibility == Visibility.Visible
                && Input("LeftBandStart").Text == start && Input("LeftBandEnd").Text == end, "invalid ranges retain input and show an inline error");
            var tabs = (TabControl)editor.FindName("SettingsTabs");
            Check(editor.CurrentSection == 1 && ((TabItem)tabs.Items[1]).Visibility == Visibility.Visible
                && ((TabItem)tabs.Items[3]).Visibility == Visibility.Visible, "invalid range saved from another section must restore the complete file settings tab group");
        }
        foreach (var (side, start, end) in new[] { ("Left", "20", "80"), ("Right", "12.5", "87.5"), ("Top", "30", "90") })
        { Input(side + "BandStart").Text = start; Input(side + "BandEnd").Text = end; }
        Check(editor.HasUnsavedEdits && settings.LeftEdgeBand!.StartPercent == 0, "range editing must stay in the unsaved draft");
        Apply();
        Check(applied?.LeftEdgeBand?.StartPercent == 20 && applied.RightEdgeBand?.EndPercent == 87.5 && applied.TopEdgeBand?.StartPercent == 30
            && !editor.HasUnsavedEdits && ((TextBlock)editor.FindName("EdgeRangeError")).Visibility == Visibility.Collapsed, "valid independent ranges apply and clear validation");
        var reopened = new SettingsWindow(store.Load(), store, new AppDiscovery(), _ => { });
        Check(((TextBox)reopened.FindName("LeftBandEnd")).Text == "80" && ((TextBox)reopened.FindName("RightBandStart")).Text == "12.5"
            && ((TextBox)reopened.FindName("TopBandEnd")).Text == "90", "saved endpoints must reopen exactly");
        reopened.Close(); editor.Close();
    }

    private static UserSettings Seed()
    {
        var settings = new UserSettings();
        foreach (var extension in new[] { ".eg-one", ".eg-two" })
            settings.FileTypes[extension] = new()
            {
                CustomApps =
                [
                    new() { Id = Environment.ProcessPath!, ExecutablePath = Environment.ProcessPath!, DisplayName = "Alpha Runner" },
                    new() { Id = Path.Combine(Environment.SystemDirectory, "cmd.exe"), ExecutablePath = Path.Combine(Environment.SystemDirectory, "cmd.exe"), DisplayName = "Beta Shell" }
                ]
            };
        settings.FileTypes[".mkv"] = new() { CandidateOrder = ["stale"], HiddenCandidateIds = ["stale"] };
        return settings;
    }

    private static void VerifyEditing(EousGate.App app, string output)
    {
        var source = Seed(); app.ApplyTheme(source);
        var path = Path.Combine(output, "settings.json");
        var store = new JsonSettingsStore(path);
        store.Save(source);
        var applied = 0;
        var editor = new SettingsWindow(source, store, new AppDiscovery(), _ => applied++);
        T Find<T>(string name) => (T)editor.FindName(name);
        Check(Find<StackPanel>("ExtensionBrowser").Visibility == Visibility.Collapsed, "software list leads; suffix browser starts collapsed");
        Find<Button>("ChooseExtensionButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Find<StackPanel>("ExtensionBrowser").Visibility == Visibility.Visible, "change suffix exposes category/search without losing the current editor");
        Find<ListBox>("FileCategories").SelectedValue = "audio";
        Check(Find<ListBox>("ExtensionSuggestions").Items.Cast<string>().Contains(".flac"), "category shows common suffixes");
        Find<TextBox>("Extension").Text = "FLAC";
        Check(Find<ListBox>("ExtensionSuggestions").Items.Count == 1, "category search filters case-insensitively");
        Check(editor.LoadFileType(".eg-one"), "load custom suffix");
        var apps = Find<ListBox>("Apps");
        Check(apps.Items.Count >= 2 && apps.Items.OfType<CheckBox>().Take(2).Select(row => ((AppCandidate)row.Tag).DisplayName).SequenceEqual(["Alpha Runner", "Beta Shell"]), "custom apps precede other system handlers");
        ((CheckBox)apps.Items[0]).IsChecked = false;
        apps.SelectedIndex = 1;
        var moved = (CheckBox)apps.Items[1]; var checkedBeforeMove = moved.IsChecked;
        Descendants<Button>(moved.Content as DependencyObject ?? throw new Exception("candidate content missing"))
            .Single(b => Equals(b.Content, "↑")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(ReferenceEquals(apps.Items[0], moved) && moved.IsChecked == checkedBeforeMove, "inline arrow reorders the candidate without toggling its visibility");
        Find<CheckBox>("PerTypeCountEnabled").IsChecked = true;
        Find<TextBox>("PerTypeCount").Text = "5";
        Check(editor.LoadFileType(".eg-two") && editor.LoadFileType(".eg-one"), "switch suffix and back");
        Check(source.FileTypes[".eg-one"].HiddenCandidateIds.Count == 0, "draft does not mutate active settings");
        Check(((AppCandidate)((CheckBox)apps.Items[0]).Tag).DisplayName == "Beta Shell"
            && ((CheckBox)apps.Items[1]).IsChecked == false && Find<TextBox>("PerTypeCount").Text == "5", "order, hidden and count survive switching");
        Find<TextBox>("PerTypeCount").Text = "99";
        Check(!editor.LoadFileType(".eg-two") && Find<TextBlock>("LoadedLabel").Text.StartsWith(".eg-one"), "invalid count prevents navigation and retains current editor");
        Find<TextBox>("PerTypeCount").Text = "5";
        Find<Button>("ApplyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(applied == 1 && store.Load().FileTypes[".eg-one"].HiddenCandidateIds.Count == 1, "apply persists draft");
        var reopened = new SettingsWindow(store.Load(), store, new AppDiscovery(), _ => { });
        reopened.LoadFileType(".eg-one");
        Check(((ListBox)reopened.FindName("Apps")).Items.OfType<CheckBox>().Count(row => row.IsChecked == false) == 1, "reopen restores visibility");
        reopened.Close();
        Find<Button>("ResetTypeButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(apps.Items.OfType<CheckBox>().All(row => row.IsChecked == true) && Find<TextBox>("PerTypeCount").Text == "5", "reset keeps custom apps/count and clears visibility/order");
        Find<Button>("ResetCategoryButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); // audio only
        Find<ListBox>("FileCategories").SelectedValue = "video";
        Find<Button>("ResetCategoryButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Find<Button>("ApplyButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!store.Load().FileTypes.ContainsKey(".mkv"), "category reset affects all suffixes in category without preloading them");
        Check(!store.Load().FileTypes.ContainsKey(".mp4"), "browsing default suffix never pins auto order");
        ((CheckBox)apps.Items[0]).IsChecked = false;
        editor.Close();
        Check(store.Load().FileTypes[".eg-one"].HiddenCandidateIds.Count == 0, "cancel discards uncommitted changes");

        var blockedPath = Path.Combine(output, "blocked-settings"); Directory.CreateDirectory(blockedPath);
        var blocked = new JsonSettingsStore(blockedPath);
        var failureApplied = false;
        var failed = new SettingsWindow(Seed(), blocked, new AppDiscovery(), _ => failureApplied = true);
        failed.LoadFileType(".eg-one");
        ((CheckBox)((ListBox)failed.FindName("Apps")).Items[0]).IsChecked = false;
        ((Button)failed.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(blocked.LastError is not null && !failureApplied
            && ((CheckBox)((ListBox)failed.FindName("Apps")).Items[0]).IsChecked == false
            && ((TextBlock)failed.FindName("ApplyStatusText")).Text.Contains("失败"), "disk failure retains current draft without applying");
        failed.Close();
    }

    private static void VerifyRendering(EousGate.App app, string output)
    {
        foreach (var (skin, theme, name) in new[]
        {
            (SkinCatalog.DefaultId, "Light", "a-light"),
            (SkinCatalog.DefaultId, "Dark", "a-dark"),
            (SkinCatalog.FocusDarkId, "Light", "b-dark")
        })
        {
            var settings = Seed(); settings.SkinId = skin; settings.Theme = theme;
            app.ApplyTheme(settings);
            var editor = new SettingsWindow(settings, new MemoryStore(), new AppDiscovery(), _ => { });
            var tabs = (TabControl)editor.FindName("SettingsTabs");
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(tab => Equals(tab.Header, "软件选项"));
            ((ListBox)editor.FindName("FileCategories")).SelectedValue = "video";
            editor.LoadFileType(".mp4");
            ((Button)editor.FindName("ChooseExtensionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var content = (FrameworkElement)editor.Content;
            editor.Content = null; content.RenderTransform = Transform.Identity;
            var root = new Border { Child = content }; root.SetResourceReference(Border.BackgroundProperty, "PanelBackground"); editor.Content = root;
            foreach (var (width, height, size) in new[] { (1164d, 752d, "normal"), (944d, 592d, "min") })
            {
                root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
                Check(((ListBox)editor.FindName("FileCategories")).ActualWidth >= 120, "category navigation retains width");
                foreach (var button in Descendants<Button>(root).Where(b => b.IsVisible))
                    Check(button.ActualWidth >= 50 && button.DesiredSize.Width <= button.ActualWidth + button.Margin.Left + button.Margin.Right + 1, "button clipped: " + button.Content);
                foreach (var scale in size == "normal" ? new[] { 1d, 1.5, 2d } : new[] { 1d })
                {
                    var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                    bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(output, $"file-hop-{name}-{size}-{scale:0.##}.png")); encoder.Save(stream);
                }
                var categories = (ListBox)editor.FindName("FileCategories");
                var last = categories.Items[categories.Items.Count - 1];
                categories.ScrollIntoView(last);
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                root.UpdateLayout();
                var container = (ListBoxItem)categories.ItemContainerGenerator.ContainerFromItem(last);
                var bounds = container.TransformToAncestor(categories).TransformBounds(new Rect(container.RenderSize));
                Check(bounds.Top >= -1 && bounds.Bottom <= categories.ActualHeight + 1, $"last category remains reachable: {name}/{size}, bounds {bounds}, list height {categories.ActualHeight}");
                categories.ScrollIntoView(categories.SelectedItem); root.UpdateLayout();
            }
            editor.Close();
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private sealed class MemoryStore : ISettingsStore
    {
        public UserSettings Load() => new();
        public void Save(UserSettings settings) { }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
