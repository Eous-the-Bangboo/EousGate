using System.Windows;
using System.IO;
using EousGate.Infrastructure;

namespace EousGate;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstance;
    private TrayService? _tray;
    private EdgeDropWindow? _edgeLeft;
    private EdgeDropWindow? _edgeRight;
    private EdgeDropWindow? _edgeTop;
    private OverlayWindow? _overlay;
    private ClipboardMonitor? _clipboardMonitor;
    private ClipboardLinkWindow? _clipboardWindow;
    private ClipboardLinkActions? _clipboardActions;
    private UserSettings _settings = new();
    private readonly DiagnosticsLogger _diagnostics;
    private readonly bool _initializeServices;

    public App() : this(true) { }

    internal App(bool initializeServices)
    {
        _initializeServices = initializeServices;
        _diagnostics = new DiagnosticsLogger(() => _settings.DiagnosticsEnabled);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (!_initializeServices) return;
        if (e.Args.Length == 1 && e.Args[0].StartsWith(ElevatedExternalStop.ArgumentPrefix, StringComparison.Ordinal))
        {
            Shutdown(ElevatedExternalStop.ExecuteWorker(e.Args[0][ElevatedExternalStop.ArgumentPrefix.Length..]));
            return;
        }
        _singleInstanceMutex = new Mutex(true, "Local\\EousAssistant.SingleInstance", out _ownsSingleInstance);
        if (!_ownsSingleInstance)
        {
            try { using var signal = EventWaitHandle.OpenExisting("Local\\EousAssistant.ShowPanel"); signal.Set(); } catch (WaitHandleCannotBeOpenedException) { }
            Shutdown();
            return;
        }
#if DEBUG
        if (e.Args.Contains("--preview-settings", StringComparer.OrdinalIgnoreCase))
        {
            ShowSettingsPreview(
                e.Args.Contains("--dark", StringComparer.OrdinalIgnoreCase),
                GetOptionValue(e.Args, "--capture-settings="));
            return;
        }
        if (e.Args.Contains("--preview-overlay", StringComparer.OrdinalIgnoreCase))
        {
            ShowOverlayPreview(
                e.Args.Contains("--dark", StringComparer.OrdinalIgnoreCase),
                GetOptionValue(e.Args, "--capture-overlay="));
            return;
        }
#endif
        InitializeAssistant();
    }

#if DEBUG
    private void ShowSettingsPreview(bool dark, string? capturePath)
    {
        _settings = new UserSettings { Theme = dark ? "Dark" : "Light" };
        ApplyTheme(_settings);
        var store = new PreviewSettingsStore(_settings);
        var window = new SettingsWindow(
            _settings,
            store,
            new AppDiscovery(() => _settings),
            updated =>
            {
                _settings = updated;
                ApplyTheme(updated);
            });
        window.Closed += (_, _) => Shutdown();
        window.Show();
        QueuePreviewCapture(window, capturePath);
    }

    private void ShowOverlayPreview(bool dark, string? capturePath)
    {
        _settings = new UserSettings
        {
            Theme = dark ? "Dark" : "Light",
            PanelWidth = 404,
            PanelMaxHeight = 640,
            CandidateGap = 5,
            AnimationDurationMs = 320
        };
        ApplyTheme(_settings);

        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var candidates = new[]
        {
            PreviewCandidate("notepad", "记事本", Path.Combine(system, "notepad.exe")),
            PreviewCandidate("code", "Visual Studio Code", Path.Combine(windows, "explorer.exe")),
            PreviewCandidate("word", "Microsoft Word", Path.Combine(system, "write.exe")),
            PreviewCandidate("paint", "画图", Path.Combine(system, "mspaint.exe"))
        };

        _overlay = new OverlayWindow(new AppDiscovery(), new PreviewOpenFileService(), () => _settings,
            monitorMouseButton: false, showInWindowSwitcher: true)
        {
            Topmost = false
        };
        _overlay.IsVisibleChanged += (_, _) =>
        {
            if (_overlay?.IsVisible == false) Shutdown();
        };
        _overlay.BeginSession(new DragSession([@"C:\Preview\sample.txt", @"C:\Preview\notes.txt"]) { State = DragSessionState.PanelShown }, candidates);
        QueuePreviewCapture(_overlay, capturePath);
    }

    private static string? GetOptionValue(IEnumerable<string> arguments, string prefix)
        => arguments.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];

    private static void QueuePreviewCapture(Window window, string? capturePath)
    {
        if (string.IsNullOrWhiteSpace(capturePath)) return;
        window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            window.UpdateLayout();
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
            var width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX));
            var height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY));
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                width,
                height,
                dpi.PixelsPerInchX,
                dpi.PixelsPerInchY,
                System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var stream = File.Create(capturePath)) encoder.Save(stream);
            window.Close();
        }));
    }

    private static AppCandidate PreviewCandidate(string id, string name, string executable)
        => new(id, name, executable, null, true, executable);

    private sealed class PreviewOpenFileService : IOpenFileService
    {
        public OpenResult Open(IReadOnlyList<string> filePaths, AppCandidate candidate) => new(false, "预览模式不会执行打开动作。");
    }

    private sealed class PreviewSettingsStore(UserSettings settings) : ISettingsStore
    {
        private UserSettings _settings = settings.Clone();
        public UserSettings Load() => _settings.Clone();
        public void Save(UserSettings settings) => _settings = settings.Clone();
    }
#endif

    internal void ApplyTheme(UserSettings settings)
    {
        var systemIsDark = System.Windows.SystemColors.WindowColor.R < 128;
        // A/B use opaque fallback surfaces, so their identity is independent of DWM support.
        var effectiveSkin = MicaWindowHelper.IsBackdropSupported || settings.SkinId == SkinCatalog.FocusDarkId ? settings.SkinId : SkinCatalog.DefaultId;
        var palette = ThemePalette.Create(settings.Theme, systemIsDark, SystemParameters.HighContrast, effectiveSkin);
        ThemeResources.Apply(Resources, palette, settings.SkinId);
        foreach (Window window in Windows)
        {
            if (window is AssistantWindow assistant) assistant.ApplyAppearance(settings.SkinId, settings.AnimationDurationMs, settings.LargeText, settings.Theme);
            if (window is SettingsWindow or CustomAppWindow or AssistantWindow or FolderSettingsWindow or ExternalProgramSettingsWindow or CustomProgramWindow)
                MicaWindowHelper.Apply(window, settings.SkinId);
        }
    }

    private void ApplyEdgeBands(UserSettings settings)
    {
        _edgeLeft?.ApplyAppearance(settings);
        _edgeRight?.ApplyAppearance(settings);
        _edgeTop?.ApplyAppearance(settings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ReleaseAssistantHost();
        _clipboardMonitor?.Dispose();
        _clipboardWindow?.Shutdown();
        _tray?.Dispose();
        _edgeLeft?.Close();
        _edgeRight?.Close();
        _edgeTop?.Close();
        _overlay?.Close();
        if (_ownsSingleInstance)
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
        base.OnExit(e);
    }
}
