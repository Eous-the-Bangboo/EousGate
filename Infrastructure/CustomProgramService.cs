using System.Diagnostics;
using System.IO;

namespace EousGate.Infrastructure;

internal static class CustomProgramConfiguration
{
    internal static CustomMonitoredProgram Validate(CustomMonitoredProgram value)
    {
        var result = value.Clone();
        if (!result.Id.StartsWith("custom-", StringComparison.Ordinal) || !Guid.TryParseExact(result.Id[7..], "N", out _)) throw new ArgumentException("自定义程序标识无效。");
        result.Name = result.Name.Trim();
        if (result.Name.Length is < 1 or > 80) throw new ArgumentException("程序名称请输入 1 到 80 个字符。");
        var path = Environment.ExpandEnvironmentVariables(result.ExecutablePath.Trim().Trim('"'));
        if (!Path.IsPathFullyQualified(path) || !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("请选择完整路径的 EXE 程序文件。");
        result.ExecutablePath = Path.GetFullPath(path);
        result.StartArguments = string.IsNullOrWhiteSpace(result.StartArguments) ? null : result.StartArguments.Trim();
        if (result.StartArguments?.Length > 8192 || result.StartArguments?.IndexOfAny(['\0', '\r', '\n']) >= 0) throw new ArgumentException("启动参数过长或包含无效字符。");
        return result;
    }
    internal static ExternalProgramDefinition Definition(CustomMonitoredProgram program) => new(
        new(program.Id, Path.GetFileNameWithoutExtension(program.ExecutablePath), program.Name,
            "自定义程序 · 监控实际运行状态", "\uE8A5", ToolKind.External, IconPath: program.ExecutablePath),
        Path.GetFileName(program.ExecutablePath), "", [], [], [], program.ForceTerminateOnTimeout, program.StartArguments);
    internal static string DisplayName(string path)
    {
        try { if (FileVersionInfo.GetVersionInfo(path).FileDescription is { Length: > 0 } name) return name.Trim(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception) { }
        return Path.GetFileNameWithoutExtension(path);
    }
    internal static bool Normalize(UserSettings settings)
    {
        var valid = new List<CustomMonitoredProgram>(); var ids = new HashSet<string>(); var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var invalid = false;
        foreach (var value in settings.CustomPrograms ?? [])
        {
            try
            {
                if (value is null) { invalid = true; continue; }
                var item = Validate(value);
                if (!ids.Add(item.Id) || !paths.Add(item.ExecutablePath)) { invalid = true; continue; }
                valid.Add(item);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or NullReferenceException) { invalid = true; }
        }
        settings.CustomPrograms = valid; return !invalid;
    }
}

internal sealed class CustomProgramService(ToolManager tools, IExternalProgramController controller,
    Func<UserSettings> settings, Action<UserSettings> commit, Action<string> showSettings, Action changed)
{
    private readonly SemaphoreSlim _editing = new(1, 1);
    internal void RegisterSaved()
    {
        foreach (var program in settings().CustomPrograms) tools.Register(Create(program));
    }
    private ExternalProgramTool Create(CustomMonitoredProgram program) => new(CustomProgramConfiguration.Definition(program), controller,
        () => new() { ExecutablePath = program.ExecutablePath }, () => showSettings(program.Id),
        settings().ToolSelections.GetValueOrDefault(program.Id, true));
    internal async Task SaveAsync(CustomMonitoredProgram input, string? existingId = null)
    {
        await _editing.WaitAsync();
        try
        {
            CheckIdle();
            var candidate = input.Clone();
            candidate.Id = existingId ?? "custom-" + Guid.NewGuid().ToString("N");
            candidate = CustomProgramConfiguration.Validate(candidate);
            if (string.Equals(candidate.ExecutablePath, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("不能把助手自身作为外部程序管理。");
            var definition = CustomProgramConfiguration.Definition(candidate);
            if (!controller.IsValidExecutable(definition, candidate.ExecutablePath)) throw new InvalidOperationException("程序文件不存在或已失效，请重新选择。");
            if (settings().CustomPrograms.Any(p => p.Id != existingId && string.Equals(p.ExecutablePath, candidate.ExecutablePath, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("此程序路径已添加，请编辑已有条目。");
            foreach (var preset in tools.Tools.OfType<ExternalProgramTool>().Where(t => ExternalProgramCatalog.All.Any(p => p.Descriptor.Id == t.Descriptor.Id)).ToArray())
            {
                var path = preset.ExecutablePath ?? (await Task.Run(() => controller.Inspect(preset.Definition, null))).ExecutablePath;
                if (string.Equals(path, candidate.ExecutablePath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("此程序已由预设条目管理，请使用已有条目。");
            }
            var previous = existingId is null ? null : tools.Tools.OfType<ExternalProgramTool>().SingleOrDefault(t => t.Descriptor.Id == existingId);
            if (existingId is not null && previous is null) throw new InvalidOperationException("程序已被移除，请重新打开。");
            var saved = settings().CustomPrograms.SingleOrDefault(p => p.Id == existingId);
            if (previous is not null && saved is not null && IsStopPolicyOnlyChange(saved, candidate))
            {
                await previous.UpdateStopPolicyAsync(candidate.ForceTerminateOnTimeout, () =>
                {
                    CheckIdle();
                    var policyDraft = settings().Clone();
                    policyDraft.CustomPrograms[policyDraft.CustomPrograms.FindIndex(p => p.Id == existingId)] = candidate;
                    commit(policyDraft);
                });
                changed();
                return;
            }
            if (previous is not null) await previous.EnsureEditableAsync();
            CheckIdle();
            if (tools.Tools.OfType<ExternalProgramTool>().Any(t => ExternalProgramCatalog.All.Any(p => p.Descriptor.Id == t.Descriptor.Id)
                && string.Equals(t.ExecutablePath, candidate.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("此程序已由预设条目管理，请使用已有条目。");
            var draft = settings().Clone(); // Read again after awaits to preserve concurrent general settings and selections.
            if (existingId is null) draft.CustomPrograms.Add(candidate);
            else draft.CustomPrograms[draft.CustomPrograms.FindIndex(p => p.Id == existingId)] = candidate;
            draft.ToolSelections.TryAdd(candidate.Id, true);
            commit(draft); // A failed save must leave both runtime and settings unchanged.
            var replacement = Create(candidate);
            if (previous is null) tools.Register(replacement); else tools.Replace(replacement);
            changed();
        }
        finally { _editing.Release(); }
    }
    private static bool IsStopPolicyOnlyChange(CustomMonitoredProgram previous, CustomMonitoredProgram candidate)
        => previous.Id == candidate.Id && previous.Name == candidate.Name
            && string.Equals(previous.ExecutablePath, candidate.ExecutablePath, StringComparison.OrdinalIgnoreCase)
            && previous.StartArguments == candidate.StartArguments;
    internal async Task RemoveAsync(string id)
    {
        await _editing.WaitAsync();
        try
        {
            CheckIdle();
            var draft = settings().Clone();
            if (draft.CustomPrograms.RemoveAll(p => p.Id == id) == 0) throw new InvalidOperationException("程序已被移除。");
            draft.ToolSelections.Remove(id);
            commit(draft);
            tools.Remove(id); // Removing management never calls StopAsync or deletes the executable.
            changed();
        }
        finally { _editing.Release(); }
    }
    private void CheckIdle()
    {
        if (tools.Tools.Any(t => t.State is ToolState.Starting or ToolState.Stopping)) throw new InvalidOperationException("正在启停程序，请完成后再修改列表。");
    }
}
