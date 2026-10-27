using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EousGate;
using EousGate.Infrastructure;

internal static class ClipboardSmoke
{
    // Opt-in UI/message integration test with a synthetic text reader.
    internal static int Run(string[] args)
    {
        if (args.Length != 1) { Console.Error.WriteLine("Supply an output directory for --clipboard-smoke."); return 1; }
        var result = 1;
        var thread = new Thread(() =>
        {
            var app = new EousGate.App(initializeServices: false);
            app.InitializeComponent();
            ApplyPalette(app, ThemePalette.Create("Light", false, false));
            var output = Path.GetFullPath(args[0]);
            Directory.CreateDirectory(output);
            var settings = new UserSettings { ClipboardPopupDurationSeconds = 2, AnimationDurationMs = 80 };
            var opens = new List<string>();
            // Controlled interaction state avoids depending on the user's pointer while testing expiry.
            var window = new ClipboardLinkWindow(() => settings, new WebLinkOpener(info => opens.Add(info.FileName)), () => false);
            ClipboardMonitor? monitor = null;
            var readAttempts = 0;
            var copied = 0;
            var error = "";
            const string sample = "测试链接 https://example.com/clipboard?source=eousgate 和 https://example.org/docs";
            var frame = new DispatcherFrame();
            var timers = new List<DispatcherTimer>();
            void Fail(Exception exception) { error = exception.Message; frame.Continue = false; }
            void Schedule(int milliseconds, Action action)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
                timers.Add(timer);
                timer.Tick += (_, _) => { timer.Stop(); try { action(); } catch (Exception exception) { Fail(exception); } };
                timer.Start();
            }
            try
            {
                monitor = new ClipboardMonitor(text =>
                {
                    if (text != sample) return;
                    copied++;
                    window.ShowLinks(ClipboardLinks.Extract(text));
                }, null, () => (++readAttempts > 2, sample));
                Check(monitor.SetEnabled(true), "clipboard listener registration failed");
                Schedule(100, () =>
                {
                    PostMessage(monitor.WindowHandle, 0x031D, IntPtr.Zero, IntPtr.Zero);
                });
                Schedule(600, () =>
                {
                    Check(copied == 1 && window.IsVisible, "first copy did not display the popup exactly once");
                    Capture(window, Path.Combine(output, "clipboard-light.png"));
                    foreach (var scale in new[] { 1.25, 1.5, 2.0 })
                        Capture(window, Path.Combine(output, $"clipboard-light-{scale * 100:0}.png"), scale);
                });
                Schedule(1300, () => PostMessage(monitor.WindowHandle, 0x031D, IntPtr.Zero, IntPtr.Zero));
                Schedule(2600, () =>
                {
                    Check(copied == 2 && window.IsVisible, "identical second copy must reset the popup lifetime");
                    ApplyPalette(app, ThemePalette.Create("Dark", true, false));
                    Capture(window, Path.Combine(output, "clipboard-dark.png"));
                    foreach (var scale in new[] { 1.25, 1.5, 2.0 })
                        Capture(window, Path.Combine(output, $"clipboard-dark-{scale * 100:0}.png"), scale);
                });
                var expiryPolls = 0;
                void VerifyExpired()
                {
                    // Rendering a cold WPF window can delay the second copy and
                    // its expiry timer. Observe dismissal before advancing.
                    if (window.IsVisible)
                    {
                        Check(++expiryPolls < 60, "popup did not expire after the second copy with interaction disabled");
                        Schedule(50, VerifyExpired); return;
                    }
                    window.ShowLinks(ClipboardLinks.Extract(sample));
                    window.UpdateLayout();
                    var link = Descendants<Button>(window).First(button => button.Tag is Uri);
                    link.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(opens.SequenceEqual(new[] { "https://example.com/clipboard?source=eousgate" }) && !window.IsVisible, "link click must launch its URL once and dismiss");
                    Check(monitor.SetEnabled(false), "listener did not disable");
                    PostMessage(monitor.WindowHandle, 0x031D, IntPtr.Zero, IntPtr.Zero);
                Schedule(500, () =>
                {
                    Check(copied == 2, "disabled clipboard listener still fired");
                    Check(monitor.SetEnabled(true), "listener did not re-enable");
                    PostMessage(monitor.WindowHandle, 0x031D, IntPtr.Zero, IntPtr.Zero);
                });
                Schedule(1000, () =>
                {
                    Check(copied == 3 && window.IsVisible, "same text after re-enable must display again");
                    window.Dismiss();
                    ApplyPalette(app, ThemePalette.Create("Light", false, false));
                    var path = Path.Combine(output, "smoke-settings.json");
                    UserSettings? applied = null;
                    var editor = new SettingsWindow(settings, new JsonSettingsStore(path), new AppDiscovery(), value => applied = value) { ShowActivated = false };
                    try
                    {
                        editor.Show();
                        var tabs = (TabControl)editor.FindName("SettingsTabs");
                        tabs.SelectedItem = editor.FindName("WebSettingsTab");
                        ((TextBox)editor.FindName("ClipboardDuration")).Text = "9";
                        ((ComboBox)editor.FindName("ClipboardSide")).SelectedIndex = 1;
                        ((CheckBox)editor.FindName("ClipboardAutoOpen")).IsChecked = true;
                        ((TextBox)editor.FindName("ClipboardCopyCount")).Text = "2";
                        editor.UpdateLayout();
                        editor.BeginAnimation(Window.OpacityProperty, null);
                        editor.Opacity = 1;
                        var translation = (TranslateTransform)editor.FindName("WindowTranslation");
                        translation.BeginAnimation(TranslateTransform.YProperty, null);
                        translation.Y = 0;
                        Capture(editor, Path.Combine(output, "clipboard-settings.png"));
                        Descendants<Button>(editor).Single(button => Equals(button.Content, "应用")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Check(applied is { ClipboardPopupDurationSeconds: 9, ClipboardPopupSide: "Left" } && editor.IsVisible, "Apply must save new preferences and keep settings open");
                        Check(new JsonSettingsStore(path).Load().ClipboardPopupDurationSeconds == 9, "duration was not persisted");
                        Check(applied is { ClipboardAutoOpenEnabled: true, ClipboardAutoOpenCopyCount: 2 }
                            && new JsonSettingsStore(path).Load() is { ClipboardAutoOpenEnabled: true, ClipboardAutoOpenCopyCount: 2 },
                            "the opt-in checkbox and copy threshold must save and read back");
                    }
                    finally { editor.Close(); }
                    Check(monitor.SetEnabled(false), "original listener did not stop before the rapid-copy check");
                    var quickSettings = new UserSettings { ClipboardAutoOpenEnabled = true, ClipboardAutoOpenCopyCount = 2 };
                    var quickOpens = new List<string>(); var rapidCopies = 0;
                    var quickActions = new ClipboardLinkActions(() => quickSettings, new WebLinkOpener(p => quickOpens.Add(p.FileName)),
                        (links, message) => window.ShowLinks(links, message), window.Dismiss);
                    using (var quickMonitor = new ClipboardMonitor(text => { rapidCopies++; quickActions.HandleCopy(text); }, null, () => (true, sample)))
                    {
                        Check(quickMonitor.SetEnabled(true), "rapid-copy listener did not enable");
                        for (var i = 0; i < 3; i++) SendMessage(quickMonitor.WindowHandle, 0x031D, IntPtr.Zero, IntPtr.Zero);
                        Check(rapidCopies == 3 && quickOpens.Count == 2 && !window.IsVisible,
                            "rapid separate notifications must trigger one complete URL round, without debouncing away copies");
                    }
                    uint sequence = 10;
                    var delivered = 0;
                    using (var versioned = new ClipboardMonitor(_ => delivered++, null, () => (true, sample), () => sequence))
                    {
                        Check(versioned.SetEnabled(true), "versioned listener did not enable");
                        for (var i = 0; i < 4; i++) SendMessage(versioned.WindowHandle, 0x031D, IntPtr.Zero, IntPtr.Zero);
                        Check(delivered == 1, "duplicate notifications for one clipboard version must count once");
                        sequence++;
                        SendMessage(versioned.WindowHandle, 0x031D, IntPtr.Zero, IntPtr.Zero);
                        Check(delivered == 2, "a new clipboard version with identical text must count again");
                        Check(versioned.SetEnabled(false) && versioned.SetEnabled(true), "versioned listener did not restart");
                        SendMessage(versioned.WindowHandle, 0x031D, IntPtr.Zero, IntPtr.Zero);
                        Check(delivered == 3, "listener restart must clear its delivered-version cache");
                    }
                    frame.Continue = false;
                });
                }
                Schedule(3800, VerifyExpired);
                Schedule(12000, () => throw new TimeoutException("Clipboard integration timed out"));
                Dispatcher.PushFrame(frame);
                if (error.Length > 0) throw new InvalidOperationException(error);
                result = 0;
                Console.WriteLine("PASS native clipboard message dispatch with synthetic reader, busy retries, identical copies, reset/expiry, disable/re-enable, click dispatch and settings Apply; rendered light/dark/settings.");
            }
            catch (Exception exception) { Console.Error.WriteLine(exception.Message); }
            finally
            {
                foreach (var timer in timers) timer.Stop();
                monitor?.Dispose();
                window.Shutdown();
                app.Shutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    private static void ApplyPalette(EousGate.App app, ThemePalette palette)
    {
        foreach (var (key, color) in new[] { ("PanelElevatedSurface", palette.ElevatedSurface), ("PanelForeground", palette.Foreground), ("PanelMutedForeground", palette.MutedForeground), ("PanelBorder", palette.Border), ("PanelSurfaceSoft", palette.SurfaceSoft), ("PanelAccent", palette.Accent), ("PanelAccentSoft", palette.AccentSoft) })
            app.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private static void Capture(Window window, string path, double scale = 1)
    {
        window.UpdateLayout();
        if (window is ClipboardLinkWindow)
        {
            var close = (Button)window.FindName("DismissButton");
            var presenter = Descendants<ContentPresenter>(close).Single();
            var text = new FormattedText("×", System.Globalization.CultureInfo.CurrentUICulture,
                close.FlowDirection, new Typeface(close.FontFamily, close.FontStyle, close.FontWeight, close.FontStretch),
                close.FontSize, close.Foreground, VisualTreeHelper.GetDpi(close).PixelsPerDip);
            Check(presenter.ActualWidth >= text.WidthIncludingTrailingWhitespace && presenter.ActualHeight >= text.Height,
                "close glyph must fit completely inside its content area");
            Check(close.IsEnabled && System.Windows.Automation.AutomationProperties.GetName(close) == "关闭链接提示",
                "close control must remain enabled and accessible");
        }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth * scale), (int)Math.Ceiling(window.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
}
