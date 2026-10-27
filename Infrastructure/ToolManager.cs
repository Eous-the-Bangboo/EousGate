using System.ComponentModel;

namespace EousGate.Infrastructure;

public sealed class ManagedTool(ToolDescriptor descriptor, Func<Task> start, Func<Task> stop,
    Action settings, bool selected = true) : IAssistantTool
{
    private readonly SemaphoreSlim _transition = new(1, 1);
    private bool _selected = selected;
    private bool _needsCleanup;
    public ToolDescriptor Descriptor { get; } = descriptor;
    public ToolState State { get; private set; }
    public string? Error { get; private set; }
    public bool Selected { get => _selected; set { if (_selected == value) return; _selected = value; Changed(nameof(Selected)); } }
    public string StateText => State switch
    {
        ToolState.Starting => "启动中", ToolState.Running => "运行中", ToolState.Stopping => "停止中",
        ToolState.Failed => "失败", _ => "未运行"
    };
    public bool CanStart => Descriptor.Capabilities.HasFlag(ToolCapabilities.Start) && State is ToolState.Stopped or ToolState.Failed;
    public bool CanStop => Descriptor.Capabilities.HasFlag(ToolCapabilities.Stop) && State is ToolState.Running or ToolState.Failed;
    public bool CanOpenSettings => Descriptor.Capabilities.HasFlag(ToolCapabilities.Settings);
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string name) => PropertyChanged?.Invoke(this, new(name));
    private void Update(ToolState state, string? error = null)
    {
        State = state; Error = error;
        foreach (var name in new[] { nameof(State), nameof(Error), nameof(StateText), nameof(CanStart), nameof(CanStop) }) Changed(name);
    }
    public async Task StartAsync()
    {
        if (!Descriptor.Capabilities.HasFlag(ToolCapabilities.Start)) return;
        await _transition.WaitAsync();
        try
        {
            if (State == ToolState.Running) return;
            Update(ToolState.Starting);
            try
            {
                if (_needsCleanup) { await stop(); _needsCleanup = false; }
                _needsCleanup = true;
                await start();
                Update(ToolState.Running);
            }
            catch (Exception ex)
            {
                var error = ex.Message;
                try { await stop(); _needsCleanup = false; }
                catch (Exception cleanup) { error += "；释放资源失败：" + cleanup.Message; }
                Update(ToolState.Failed, error);
            }
        }
        finally { _transition.Release(); }
    }
    public async Task StopAsync()
    {
        if (!Descriptor.Capabilities.HasFlag(ToolCapabilities.Stop)) return;
        await _transition.WaitAsync();
        try
        {
            if (State == ToolState.Stopped) return;
            Update(ToolState.Stopping);
            try { if (_needsCleanup) await stop(); _needsCleanup = false; Update(ToolState.Stopped); }
            catch (Exception ex) { Update(ToolState.Failed, ex.Message); }
        }
        finally { _transition.Release(); }
    }
    public void OpenSettings() { if (CanOpenSettings) settings(); }
}

public sealed class ToolManager
{
    private readonly List<IAssistantTool> _tools = [];
    public List<string> BatchErrors { get; } = [];
    public IReadOnlyList<IAssistantTool> Tools => _tools.AsReadOnly();
    public event EventHandler? CollectionChanged;
    public void Register(IAssistantTool tool)
    {
        if (_tools.Any(t => t.Descriptor.Id == tool.Descriptor.Id)) throw new ArgumentException("工具 ID 重复。");
        _tools.Add(tool);
        CollectionChanged?.Invoke(this, EventArgs.Empty);
    }
    public void Replace(IAssistantTool tool)
    {
        var index = _tools.FindIndex(t => t.Descriptor.Id == tool.Descriptor.Id);
        if (index < 0) throw new InvalidOperationException("程序已被移除。");
        _tools[index] = tool; CollectionChanged?.Invoke(this, EventArgs.Empty);
    }
    public void Remove(string id)
    {
        if (_tools.RemoveAll(t => t.Descriptor.Id == id) > 0) CollectionChanged?.Invoke(this, EventArgs.Empty);
    }
    public async Task RunAsync(bool start, bool selectedOnly, ToolKind? kind = null)
    {
        BatchErrors.Clear();
        // Snapshot the selection so changing checkboxes cannot alter a batch already in progress.
        var targets = _tools.Where(t => (!selectedOnly || t.Selected) && (kind is null || t.Descriptor.Kind == kind)).ToArray();
        foreach (var tool in targets)
        {
            if (!_tools.Contains(tool)) continue;
            try { if (start) await tool.StartAsync(); else await tool.StopAsync(); }
            catch (Exception ex) { BatchErrors.Add(tool.Descriptor.ChineseName + "：" + ex.Message); }
        }
    }
}
