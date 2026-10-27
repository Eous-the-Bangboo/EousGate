using System.ComponentModel;

namespace EousGate.Infrastructure;

public sealed class ExternalProgramTool(ExternalProgramDefinition definition, IExternalProgramController controller,
    Func<ExternalProgramOptions> options, Action settings, bool selected = true) : IAssistantTool
{
    private readonly SemaphoreSlim _transition = new(1, 1);
    private bool _selected = selected;
    private bool _observed;
    private bool _operationFailed;
    private bool _failedActive;
    private string? _resolvedPath;
    private ExternalProgramSnapshot _snapshot = new(null, []);
    public ExternalProgramDefinition Definition => definition;
    public ToolDescriptor Descriptor => definition.Descriptor;
    public ToolState State { get; private set; }
    public string? Error { get; private set; }
    public string? ExecutablePath => options().ExecutablePath ?? _resolvedPath;
    public bool Selected { get => _selected; set { if (_selected == value) return; _selected = value; Changed(nameof(Selected)); } }
    public string StateText => !_observed ? "检测中" : State switch { ToolState.Running => "运行中", ToolState.Starting => "启动中", ToolState.Stopping => "停止中", ToolState.Failed => "失败", _ => "未运行" };
    public bool CanStart => _observed && State is not (ToolState.Starting or ToolState.Stopping) && _snapshot.Error is null && _snapshot.Processes.Count == 0 && ExecutablePath is not null;
    public bool CanStop => _observed && State is not (ToolState.Starting or ToolState.Stopping) && _snapshot.Processes.Count > 0;
    public bool CanOpenSettings => true;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string name) => PropertyChanged?.Invoke(this, new(name));
    private void Notify()
    {
        foreach (var name in new[] { nameof(State), nameof(StateText), nameof(Error), nameof(CanStart), nameof(CanStop), nameof(ExecutablePath) }) Changed(name);
    }
    private async Task InspectAsync()
    {
        var path = options().ExecutablePath ?? _resolvedPath;
        _snapshot = await Task.Run(() => controller.Inspect(definition, path));
        _resolvedPath = _snapshot.ExecutablePath; _observed = true;
    }
    private void ApplyObservation()
    {
        if (_snapshot.Error is { } error) { State = ToolState.Failed; Error = error; }
        else if (_operationFailed && (_snapshot.Processes.Count > 0) == _failedActive) State = ToolState.Failed;
        else { State = _snapshot.Processes.Count > 0 ? ToolState.Running : ToolState.Stopped; Error = null; _operationFailed = false; }
        Notify();
    }
    public async Task RefreshAsync()
    {
        if (!await _transition.WaitAsync(0)) return;
        try { await InspectAsync(); ApplyObservation(); }
        catch (Exception) { _snapshot = _snapshot with { Error = "暂时无法检测程序状态。" }; _observed = true; State = ToolState.Failed; Error = _snapshot.Error; Notify(); }
        finally { _transition.Release(); }
    }
    public async Task StartAsync()
    {
        await _transition.WaitAsync();
        try
        {
            await InspectAsync();
            if (_snapshot.Processes.Count > 0 && _snapshot.Error is null) { _operationFailed = false; ApplyObservation(); return; }
            if (_snapshot.Error is not null || _resolvedPath is null) throw new InvalidOperationException(_snapshot.Error ?? "请先选择程序文件。");
            State = ToolState.Starting; Error = null; _operationFailed = false; Notify();
            await controller.StartAsync(definition, _resolvedPath);
            for (int i = 0; i < 30; i++)
            {
                await InspectAsync();
                if (_snapshot.Error is not null) throw new InvalidOperationException(_snapshot.Error);
                if (_snapshot.Processes.Count > 0) { ApplyObservation(); return; }
                await Task.Delay(150);
            }
            throw new InvalidOperationException("启动后未检测到程序，请检查程序路径或稍后重试。");
        }
        catch (Exception ex) { _observed = true; _failedActive = _snapshot.Processes.Count > 0; _operationFailed = true; State = ToolState.Failed; Error = ex.Message; Notify(); }
        finally { _transition.Release(); }
    }
    public async Task StopAsync()
    {
        await _transition.WaitAsync();
        try
        {
            await InspectAsync();
            if (_snapshot.Error is not null) throw new InvalidOperationException(_snapshot.Error);
            if (_snapshot.Processes.Count == 0) { _operationFailed = false; ApplyObservation(); return; }
            State = ToolState.Stopping; Error = null; Notify();
            await controller.StopAsync(definition, _snapshot);
            for (int i = 0; i < 20; i++)
            {
                await InspectAsync();
                if (_snapshot.Error is not null || _snapshot.Processes.Count == 0) break;
                await Task.Delay(100);
            }
            if (_snapshot.Error is not null || _snapshot.Processes.Count > 0) throw new InvalidOperationException(_snapshot.Error ?? "程序仍在运行，请重试停止。");
            _operationFailed = false; ApplyObservation();
        }
        catch (Exception ex) { _observed = true; _failedActive = _snapshot.Processes.Count > 0; _operationFailed = true; State = ToolState.Failed; Error = ex.Message; Notify(); }
        finally { _transition.Release(); }
    }
    public async Task ConfigurePathAsync(string? path, Action<string?> save)
    {
        await _transition.WaitAsync();
        try
        {
            await InspectAsync();
            if (_snapshot.Processes.Count > 0 && !string.Equals(path, ExecutablePath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("请先停止此工具，再更改程序路径。");
            if (path is not null && !controller.IsValidExecutable(definition, path)) throw new InvalidOperationException("请选择 " + definition.ExecutableName + "。");
            save(path); _resolvedPath = null; _operationFailed = false;
            await InspectAsync(); ApplyObservation();
        }
        finally { _transition.Release(); }
    }
    internal async Task EnsureEditableAsync()
    {
        if (State is ToolState.Starting or ToolState.Stopping) throw new InvalidOperationException("正在启停，请完成后再修改。");
        await _transition.WaitAsync();
        try
        {
            await InspectAsync(); ApplyObservation();
            if (_snapshot.Error is not null && !_snapshot.ExecutableMissing) throw new InvalidOperationException(_snapshot.Error);
            if (_snapshot.Processes.Count > 0) throw new InvalidOperationException("请先停止此程序，再修改设置。");
        }
        finally { _transition.Release(); }
    }
    internal async Task UpdateStopPolicyAsync(bool forceTerminateOnTimeout, Action save)
    {
        if (State is ToolState.Starting or ToolState.Stopping) throw new InvalidOperationException("正在启停，请完成后再修改。");
        await _transition.WaitAsync();
        try
        {
            // The service has checked that target, name and startup arguments are unchanged.
            // Save before updating runtime behavior; keep the same observed tool and selection.
            save();
            definition = definition with { ForceTerminateOnTimeout = forceTerminateOnTimeout };
            Notify();
        }
        finally { _transition.Release(); }
    }
    public void OpenSettings() => settings();
}
