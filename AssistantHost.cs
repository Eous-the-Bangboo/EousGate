using System.IO;
using System.Windows;
using EousGate.FolderHop;
using EousGate.Infrastructure;

namespace EousGate;

public partial class App
{
    private readonly ToolManager _tools = new();
    private JsonSettingsStore? _settingsStore;
    private AssistantWindow? _panel;
    private SettingsWindow? _settingsWindow;
    private FolderSettingsWindow? _folderSettings;
    private HistoryStore? _history;
    private FolderSession? _folderSession;
    private LegacyInstanceGuard? _legacyGate;
    private LegacyInstanceGuard? _legacyFolder;
    private EventWaitHandle? _showPanel;
    private RegisteredWaitHandle? _panelWait;
    private bool _exiting;
    private bool _hostReleased;
    private IExternalProgramController _externalController = null!;
    private readonly Dictionary<string, ExternalProgramSettingsWindow> _externalSettingsWindows = new();
    private readonly Dictionary<string, CustomProgramWindow> _customProgramWindows = new();
    private readonly HashSet<IAssistantTool> _selectionSubscriptions = [];
    private CustomProgramService? _customPrograms;
    private System.Windows.Threading.DispatcherTimer? _externalMonitor;
    private bool _externalMonitoring;
    private bool _externalRefreshRunning;
    private bool _externalRefreshPending;
    private int _externalMonitorGeneration;
    internal ToolManager Tools => _tools;
    internal CustomProgramService? CustomPrograms => _customPrograms;
    internal bool ExternalRefreshInProgress => _externalRefreshRunning;
    internal bool ExternalMonitorEnabled => _externalMonitor?.IsEnabled == true;

    private Func<bool> _fileDropElevationProbe = FileDropPermissionGuard.IsProcessElevated;

    internal void InitializeAssistant(string? dataDirectory = null, bool tray = true, bool showPanel = true, bool includeExternalTools = true,
        IExternalProgramController? externalController = null, Func<bool>? fileDropElevationProbe = null)
    {
        _fileDropElevationProbe = fileDropElevationProbe ?? FileDropPermissionGuard.IsProcessElevated;
        _externalController = externalController ?? new WindowsExternalProgramController();
        System.Windows.Forms.Application.EnableVisualStyles();
        if (tray)
        {
            _showPanel = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\EousAssistant.ShowPanel");
            _panelWait = ThreadPool.RegisterWaitForSingleObject(_showPanel, (_, _) => Dispatcher.BeginInvoke(ShowPanel), null, Timeout.Infinite, false);
        }
        _settingsStore = new JsonSettingsStore(dataDirectory is null ? null : Path.Combine(dataDirectory, "settings.json"), logger: _diagnostics);
        _settings = _settingsStore.Load();
        _settings.Paused = false; // Legacy global pause is migrated to selection, never to live state.
        ApplyTheme(_settings);
        _history = new HistoryStore(Path.Combine(dataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EousAssistant"), "FolderHop"));
        Register("file-hop", "EousFileHop", "文件快开", "把文件拖到屏幕边缘，选择软件打开。", "\uE8A7", StartFileHop, StopFileHop, () => ShowModuleSettings(1));
        Register("web-hop", "EousWebHop", "网页快开", "复制含网页地址的文本，从侧边快速打开。", "\uE774", StartWebHop, StopWebHop, () => ShowModuleSettings(2));
        Register("folder-hop", "EousFolderHop", "文件夹快跳", "记录资源管理器目录，在打开和保存窗口快速跳转。", "\uE8B7", StartFolderHop, StopFolderHop, ShowFolderSettings);
        if (includeExternalTools)
            foreach (var definition in ExternalProgramCatalog.All)
            {
                _settings.ExternalPrograms.TryAdd(definition.Descriptor.Id, new());
                var tool = new ExternalProgramTool(definition, _externalController,
                    () => _settings.ExternalPrograms[definition.Descriptor.Id], () => ShowExternalSettings(definition.Descriptor.Id),
                    _settings.ToolSelections.GetValueOrDefault(definition.Descriptor.Id, true));
                _tools.Register(tool);
            }
        if (includeExternalTools)
        {
            _customPrograms = new CustomProgramService(_tools, _externalController, () => _settings, draft =>
            {
                if (_exiting || _hostReleased) throw new InvalidOperationException("助手正在退出，请稍后重新打开。");
                _settingsStore.Save(draft);
                if (_settingsStore.LastError is { } error) throw new IOException(error);
                _settings = draft;
            }, id => ShowCustomProgramSettings(id), RefreshCustomProgramMonitoring);
            _customPrograms.RegisterSaved();
        }
        _tools.CollectionChanged += ToolsCollectionChanged;
        SyncSelectionSubscriptions();
        if (tray) _tray = new TrayService(ShowPanel, ExitAssistant);
        if (includeExternalTools)
        {
            _externalMonitor = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _externalMonitor.Tick += ExternalMonitorTick;
        }
        if (showPanel) ShowPanel();
        if (_settingsStore.LastError is { } warning) _panel?.Report(warning);
    }
    private bool CanMonitorExternalPrograms => !_hostReleased && !_exiting &&
        _panel is { IsVisible: true, WindowState: not WindowState.Minimized };

    private void UpdateExternalMonitoring()
    {
        if (_externalMonitor is null) return;
        var enabled = CanMonitorExternalPrograms;
        if (enabled == _externalMonitoring) return;
        _externalMonitoring = enabled;
        _externalMonitorGeneration++;
        _externalRefreshPending = enabled;
        if (enabled)
        {
            _externalMonitor.Start();
            ExternalMonitorTick(this, EventArgs.Empty);
        }
        else _externalMonitor.Stop();
    }
    private async void ExternalMonitorTick(object? sender, EventArgs e)
    {
        if (_externalRefreshRunning || !CanMonitorExternalPrograms) return;
        _externalRefreshRunning = true;
        try
        {
            do
            {
                _externalRefreshPending = false;
                var generation = _externalMonitorGeneration;
                foreach (var tool in _tools.Tools.OfType<ExternalProgramTool>().ToArray())
                {
                    // An inspection already in flight may finish, but a hidden panel starts no more.
                    if (!CanMonitorExternalPrograms || generation != _externalMonitorGeneration) break;
                    if (!_tools.Tools.Contains(tool)) continue;
                    await tool.RefreshAsync();
                }
                // A reopen during an in-flight inspection requests just one fresh complete sweep.
            } while (_externalRefreshPending && CanMonitorExternalPrograms);
        }
        finally { _externalRefreshRunning = false; }
    }
    private void ToolsCollectionChanged(object? sender, EventArgs e) => SyncSelectionSubscriptions();
    private void SyncSelectionSubscriptions()
    {
        foreach (var old in _selectionSubscriptions.Where(t => !_tools.Tools.Contains(t)).ToArray())
        { old.PropertyChanged -= ToolSelectionChanged; _selectionSubscriptions.Remove(old); }
        foreach (var tool in _tools.Tools)
            if (_selectionSubscriptions.Add(tool)) tool.PropertyChanged += ToolSelectionChanged;
    }
    private void ToolSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IAssistantTool.Selected) || sender is not IAssistantTool tool) return;
        _settings.ToolSelections[tool.Descriptor.Id] = tool.Selected;
        _settingsStore!.Save(_settings);
        if (_settingsStore.LastError is { } error) _panel?.Report(error);
    }
    private void RefreshCustomProgramMonitoring()
    {
        _externalMonitorGeneration++; _externalRefreshPending = CanMonitorExternalPrograms;
        if (CanMonitorExternalPrograms) ExternalMonitorTick(this, EventArgs.Empty);
    }
    private void ShowCustomProgramSettings(string? id)
    {
        if (_customPrograms is null) return;
        var key = id ?? "add";
        if (!_customProgramWindows.TryGetValue(key, out var window))
        {
            var program = id is null ? null : _settings.CustomPrograms.SingleOrDefault(p => p.Id == id);
            if (id is not null && program is null) return;
            window = new CustomProgramWindow(program, draft => _customPrograms.SaveAsync(draft, id),
                id is null ? null : () => _customPrograms.RemoveAsync(id), _settings.SkinId);
            _customProgramWindows[key] = window;
            window.Closed += (_, _) => _customProgramWindows.Remove(key);
            ShowWorkbenchDialog(window);
        }
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }
    private void ShowExternalSettings(string id)
    {
        if (!_externalSettingsWindows.TryGetValue(id, out var window))
        {
            var tool = _tools.Tools.OfType<ExternalProgramTool>().Single(t => t.Descriptor.Id == id);
            window = new ExternalProgramSettingsWindow(tool, _externalController, path =>
            {
                if (path is not null && _settings.CustomPrograms.Any(p => string.Equals(p.ExecutablePath, path, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("此路径已由自定义程序管理，请使用已有条目。");
                var previous = _settings.ExternalPrograms[id].Clone();
                _settings.ExternalPrograms[id] = new() { ExecutablePath = path };
                _settingsStore!.Save(_settings);
                if (_settingsStore.LastError is { } error) { _settings.ExternalPrograms[id] = previous; throw new IOException(error); }
            }, _settings.SkinId);
            _externalSettingsWindows[id] = window;
            window.Closed += (_, _) => _externalSettingsWindows.Remove(id);
            ShowWorkbenchDialog(window);
        }
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }
    private void Register(string id, string english, string chinese, string description, string glyph,
        Func<Task> start, Func<Task> stop, Action settings)
        => _tools.Register(new ManagedTool(new(id, english, chinese, description, glyph), start, stop, settings,
            _settings.ToolSelections.GetValueOrDefault(id, true)));

    internal void ShowPanel()
    {
        if (_exiting || _hostReleased) return;
        if (_panel is null)
        {
            _panel = new AssistantWindow(_tools, () => ShowModuleSettings(0), _settings.SkinId, _settings.AnimationDurationMs,
                _customPrograms is null ? null : () => ShowCustomProgramSettings(null), _settings.LargeText, _settings.Theme, () => ShowModuleSettings(4));
            _panel.MinWidth = Math.Min(_panel.MinWidth, SystemParameters.WorkArea.Width);
            _panel.MinHeight = Math.Min(_panel.MinHeight, SystemParameters.WorkArea.Height);
            _panel.Width = Math.Min(_panel.Width, SystemParameters.WorkArea.Width);
            _panel.Height = Math.Min(_panel.Height, SystemParameters.WorkArea.Height);
            _panel.IsVisibleChanged += PanelVisibilityChanged;
            _panel.StateChanged += PanelStateChanged;
            _panel.Closed += PanelClosed;
        }
        if (!_panel.IsVisible) _panel.Show();
        if (_panel.WindowState == WindowState.Minimized) _panel.WindowState = WindowState.Normal;
        _panel.Activate();
        UpdateExternalMonitoring();
    }
    private void PanelVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateExternalMonitoring();
    private void PanelStateChanged(object? sender, EventArgs e) => UpdateExternalMonitoring();
    private void PanelClosed(object? sender, EventArgs e)
    {
        if (sender is not AssistantWindow panel) return;
        DetachPanelMonitoring(panel);
        if (ReferenceEquals(_panel, panel)) _panel = null;
        UpdateExternalMonitoring();
    }
    private void DetachPanelMonitoring(AssistantWindow panel)
    {
        panel.IsVisibleChanged -= PanelVisibilityChanged;
        panel.StateChanged -= PanelStateChanged;
        panel.Closed -= PanelClosed;
    }
    private void EnsureLegacyGateAbsent() => _legacyGate ??= new LegacyInstanceGuard("Local\\EousGate.SingleInstance", "EousGate");
    private Task StartFileHop()
    {
        FileDropPermissionGuard.EnsureSupported(_fileDropElevationProbe());
        EnsureLegacyGateAbsent();
        var discovery = new AppDiscovery(() => _settings, _diagnostics);
        _overlay = new OverlayWindow(discovery, new OpenFileService(logger: _diagnostics), () => _settings);
        _edgeLeft = new EdgeDropWindow(_overlay, discovery, () => false, () => _settings, EdgeDropWindow.EdgeSide.Left);
        _edgeRight = new EdgeDropWindow(_overlay, discovery, () => false, () => _settings, EdgeDropWindow.EdgeSide.Right);
        _edgeTop = new EdgeDropWindow(_overlay, discovery, () => false, () => _settings, EdgeDropWindow.EdgeSide.Top);
        ApplyEdgeBands(_settings);
        foreach (var edge in new[] { _edgeLeft, _edgeRight, _edgeTop }) if (edge.IsEnabled) edge.Show();
        return Task.CompletedTask;
    }
    private Task StopFileHop()
    {
        _edgeLeft?.Close(); _edgeLeft = null; _edgeRight?.Close(); _edgeRight = null; _edgeTop?.Close(); _edgeTop = null;
        _overlay?.Close(); _overlay = null;
        ReleaseLegacyGateIfIdle();
        return Task.CompletedTask;
    }
    private Task StartWebHop()
    {
        EnsureLegacyGateAbsent();
        _settings.ClipboardLinksEnabled = true;
        var opener = new WebLinkOpener(logger: _diagnostics);
        _clipboardWindow = new ClipboardLinkWindow(() => _settings, opener);
        _clipboardActions = new ClipboardLinkActions(() => _settings, opener,
            (links, error) => _clipboardWindow?.ShowLinks(links, error), () => _clipboardWindow?.Dismiss());
        _clipboardMonitor = new ClipboardMonitor(_clipboardActions.HandleCopy, _diagnostics);
        if (!_clipboardMonitor.SetEnabled(true)) throw new InvalidOperationException("暂时无法监听剪贴板，请停止后重新启动网页快开。");
        return Task.CompletedTask;
    }
    private Task StopWebHop()
    {
        _clipboardMonitor?.Dispose(); _clipboardMonitor = null;
        _clipboardActions?.Reset(); _clipboardActions = null;
        _clipboardWindow?.Shutdown(); _clipboardWindow = null;
        ReleaseLegacyGateIfIdle();
        return Task.CompletedTask;
    }
    private void ReleaseLegacyGateIfIdle()
    {
        if (_overlay is not null || _clipboardMonitor is not null) return;
        _legacyGate?.Dispose(); _legacyGate = null;
    }
    private Task StartFolderHop()
    {
        _legacyFolder ??= new LegacyInstanceGuard("Local\\FolderHop.CleanRoom.1", "FolderHop");
        _folderSession = new FolderSession(_history!, warning => _panel?.Report(warning));
        return Task.CompletedTask;
    }
    private async Task StopFolderHop()
    {
        if (_folderSession is { } session)
        {
            try { await session.StopAsync(); }
            finally { if (session.IsStopped) _folderSession = null; }
        }
        _legacyFolder?.Dispose(); _legacyFolder = null;
    }
    private void ShowWorkbenchDialog(Window window)
    {
        ShowPanel();
        WorkbenchDialog.Attach(window, _panel!, _settings.AnimationDurationMs);
        window.Show();
    }
    private void ShowModuleSettings(int section)
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settings, _settingsStore!, new AppDiscovery(() => _settings), updated =>
            {
                updated.ToolSelections = new(_settings.ToolSelections);
                updated.ExternalPrograms = _settings.ExternalPrograms.ToDictionary(p => p.Key, p => p.Value.Clone());
                updated.CustomPrograms = _settings.CustomPrograms.Select(p => p.Clone()).ToList();
                updated.Paused = false; _settings = updated;
                ApplyTheme(updated); ApplyEdgeBands(updated);
                if (_clipboardMonitor is not null && !updated.ClipboardLinksEnabled)
                    _ = _tools.Tools.Single(t => t.Descriptor.Id == "web-hop").StopAsync();
                _clipboardActions?.Reset();
                _clipboardWindow?.RefreshSettings();
                _folderSession?.Picker.RefreshTheme();
                _settingsStore!.Save(_settings);
            }, draft => { draft.ToolSelections = new(_settings.ToolSelections); draft.ExternalPrograms = _settings.ExternalPrograms.ToDictionary(p => p.Key, p => p.Value.Clone()); draft.CustomPrograms = _settings.CustomPrograms.Select(p => p.Clone()).ToList(); });
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            ShowWorkbenchDialog(_settingsWindow);
        }
        _settingsWindow.SelectSection(section);
        if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }
    private void ShowFolderSettings()
    {
        if (_folderSettings is null)
        {
            _folderSettings = new FolderSettingsWindow(_history!, () => _folderSession, _settings.SkinId);
            _folderSettings.Closed += (_, _) => _folderSettings = null;
            ShowWorkbenchDialog(_folderSettings);
        }
        if (_folderSettings.WindowState == WindowState.Minimized) _folderSettings.WindowState = WindowState.Normal;
        _folderSettings.Activate();
    }
    private async void ExitAssistant()
    {
        if (_exiting) return;
        _exiting = true;
        UpdateExternalMonitoring();
        await _tools.RunAsync(false, false, ToolKind.BuiltIn);
        if (_tools.Tools.Any(t => t.Descriptor.Kind == ToolKind.BuiltIn && t.State != ToolState.Stopped))
        {
            _exiting = false; ShowPanel(); _panel?.Report("有工具未能完整停止，请查看失败原因后重试退出。"); return;
        }
        if (_history is not null && !_history.Save() && !ConfirmUnsavedExit(_history.Warning))
        {
            _exiting = false; ShowPanel(); _panel?.Report(_history.Warning); return;
        }
        _settingsStore?.Save(_settings);
        if (_settingsStore?.LastError is { } error && !ConfirmUnsavedExit(error))
        {
            _exiting = false; ShowPanel(); _panel?.Report(error); return;
        }
        Shutdown();
    }
    private bool ConfirmUnsavedExit(string error)
        => System.Windows.MessageBox.Show(error + "\n本次未保存的改动可能丢失。仍要退出助手吗？", "Eous 助手", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    private void ReleaseAssistantHost()
    {
        _hostReleased = true;
        _tools.CollectionChanged -= ToolsCollectionChanged;
        foreach (var tool in _selectionSubscriptions) tool.PropertyChanged -= ToolSelectionChanged;
        _selectionSubscriptions.Clear();
        UpdateExternalMonitoring();
        if (_panel is { } panel) DetachPanelMonitoring(panel);
        if (_externalMonitor is not null) { _externalMonitor.Stop(); _externalMonitor.Tick -= ExternalMonitorTick; }
        _panelWait?.Unregister(null); _showPanel?.Dispose();
        _legacyGate?.Dispose(); _legacyFolder?.Dispose();
        _history?.Save();
    }
}
