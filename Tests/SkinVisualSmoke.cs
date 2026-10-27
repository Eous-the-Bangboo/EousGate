using EousGate;
using EousGate.Infrastructure;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Button = System.Windows.Controls.Button;

internal static class SkinVisualSmoke
{
    internal static void Verify(EousGate.App app, ToolManager manager, string output)
    {
        foreach (var (skin, theme, name) in new[]
        {
            (SkinCatalog.DefaultId, "Light", "skin-a"),
            (SkinCatalog.DefaultId, "Dark", "skin-a-dark"),
            (SkinCatalog.FocusDarkId, "Light", "skin-b")
        })
        {
            var settings = new UserSettings { SkinId = skin, Theme = theme };
            app.ApplyTheme(settings);
            var before = ((SolidColorBrush)app.Resources["PanelBackground"]).Color;
            var store = new MemoryStore(settings);
            var editor = new SettingsWindow(settings, store, new AppDiscovery(() => settings), app.ApplyTheme);
            var root = Wrap(editor);
            Layout(root, 1164, 752);
            if (skin == SkinCatalog.DefaultId)
            {
                var reading = (Border)editor.FindName("ReadingCard");
                Check(reading.TransformToAncestor(root).Transform(new Point(0, reading.ActualHeight)).Y < 696,
                    "A reading controls must fit above the footer at the default settings size");
                var picker = (ComboBox)editor.FindName("SkinPicker");
                Check(Equals(picker.SelectedValue, SkinCatalog.DefaultId) && picker.ItemTemplate is not null, "A skin picker must show a named skin and preserve selection");
            }
            foreach (var scale in new[] { 1d, 1.25, 1.5 }) Capture(root, Path.Combine(output, $"{name}-settings-{scale:0.##}.png"), scale);
            var skins = (ListBox)editor.FindName("Skins");
            var themes = (ComboBox)editor.FindName("Theme");
            Check(skins.Items.Cast<SkinDefinition>().Select(s => s.Id).SequenceEqual(new[] { SkinCatalog.DefaultId, SkinCatalog.FocusDarkId }) && themes.IsEnabled == (skin != SkinCatalog.FocusDarkId), "only A/B skin choices and fixed-dark control");
            Check(editor.FindName("DesignSchemes") is null, "the single remaining layout must not expose a redundant selector");
            Layout(root, 944, 592);
            Capture(root, Path.Combine(output, $"{name}-settings-min.png"), 1);
            var footer = Descendants<Button>(root).Where(b => new[] { "恢复全部设置", "帮助", "取消", "应用", "保存" }.Contains(b.Content?.ToString())).ToArray();
            Check(footer.Length == 5 && footer.All(b => b.ActualWidth >= 70 && b.ActualHeight >= 32 && b.DesiredSize.Width <= b.ActualWidth + b.Margin.Left + b.Margin.Right + 1), "minimum settings layout must preserve footer actions: " + string.Join("; ", footer.Select(b => $"{b.Content} {b.ActualWidth}x{b.ActualHeight} desired {b.DesiredSize.Width}")));
            // Selection previews locally, without committing or changing running tools.
            skins.SelectedValue = SkinCatalog.FocusDarkId;
            Layout(root, 1164, 752);
            Check(store.Saves == 0 && settings.SkinId == skin && ((SolidColorBrush)app.Resources["PanelBackground"]).Color == before, "preview must not leak into active settings");
            Check(!themes.IsEnabled, "B disables the independent theme control");
            footer.Single(b => Equals(b.Content, "应用")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(store.Saves == 1 && store.Value.SkinId == SkinCatalog.FocusDarkId && store.Value.Theme == theme, "apply must persist B and preserve the theme choice");
            var reopened = new SettingsWindow(store.Value, store, new AppDiscovery(() => store.Value), app.ApplyTheme);
            Check(Equals(((ListBox)reopened.FindName("Skins")).SelectedValue, SkinCatalog.FocusDarkId), "reopening must restore B");
            ((ListBox)reopened.FindName("Skins")).SelectedValue = SkinCatalog.DefaultId;
            Check(((ComboBox)reopened.FindName("Theme")).IsEnabled, "returning to A enables the preserved theme");
            reopened.Close();
            Check(store.Saves == 1 && store.Value.SkinId == SkinCatalog.FocusDarkId, "cancel must not save the preview");
            editor.Close();

            app.ApplyTheme(settings);
            var assistant = new AssistantWindow(manager, () => { }, skin);
            var assistantRoot = Wrap(assistant);
            Layout(assistantRoot, 1024, 710);
            Capture(assistantRoot, Path.Combine(output, $"{name}-assistant.png"), 1);
            if (skin == SkinCatalog.DefaultId)
            {
                manager.Tools[0].StartAsync().GetAwaiter().GetResult();
                manager.Tools[3].StartAsync().GetAwaiter().GetResult();
                Layout(assistantRoot, 1024, 710);
                foreach (var scale in new[] { 1d, 1.25, 1.5 }) Capture(assistantRoot, Path.Combine(output, $"{name}-switches-{scale:0.##}.png"), scale);
                manager.Tools[0].StopAsync().GetAwaiter().GetResult();
                manager.Tools[3].StopAsync().GetAwaiter().GetResult();
            }
            assistant.Close();
        }
        Console.WriteLine("PASS A/B skins: preview isolation, apply/reopen/cancel, preserved theme, minimum footer layout, A light/dark and B rendering at 100/125/150% (no desktop input).");
    }

    private sealed class MemoryStore(UserSettings value) : ISettingsStore
    {
        internal UserSettings Value = value.Clone();
        internal int Saves;
        public UserSettings Load() => Value.Clone();
        public void Save(UserSettings settings) { Value = settings.Clone(); Saves++; }
    }
    private static Border Wrap(Window window)
    {
        var content = (FrameworkElement)window.Content; window.Content = null;
        content.RenderTransform = Transform.Identity;
        var root = new Border { Child = content };
        root.SetResourceReference(Border.BackgroundProperty, "PanelBackground");
        window.Content = root; return root;
    }
    private static void Layout(FrameworkElement root, double width, double height)
    {
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
    }
    private static void Capture(FrameworkElement root, string path, double scale)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * scale), (int)Math.Ceiling(root.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
