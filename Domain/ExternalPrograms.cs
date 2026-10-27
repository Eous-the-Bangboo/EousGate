namespace EousGate;

public sealed class ExternalProgramOptions
{
    public string? ExecutablePath { get; set; }
    public ExternalProgramOptions Clone() => new() { ExecutablePath = ExecutablePath };
}

public sealed record ExternalProgramDefinition(ToolDescriptor Descriptor, string ExecutableName,
    string RegistryName, string[] DefaultRelativePaths, string[] StartArguments, string[] ExitArguments,
    bool ForceTerminateOnTimeout = true, string? RawStartArguments = null);

public sealed class CustomMonitoredProgram
{
    public string Id { get; set; } = "custom-" + Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string? StartArguments { get; set; }
    public bool ForceTerminateOnTimeout { get; set; }
    public CustomMonitoredProgram Clone() => new() { Id = Id, Name = Name, ExecutablePath = ExecutablePath, StartArguments = StartArguments, ForceTerminateOnTimeout = ForceTerminateOnTimeout };
}

public static class ExternalProgramCatalog
{
    public static IReadOnlyList<ExternalProgramDefinition> All { get; } = new[]
    {
        new ExternalProgramDefinition(new("external-ditto", "Ditto", "Ditto", "剪贴板历史管理；监控当前用户的应用进程。", "\uE77F", ToolKind.External),
            "Ditto.exe", "Ditto", [@"Ditto\Ditto.exe"], [], ["/Exit"]),
        new ExternalProgramDefinition(new("external-everything", "Everything", "Everything", "文件搜索；仅控制桌面程序，保留后台索引服务。", "\uE721", ToolKind.External),
            "Everything.exe", "Everything", [@"Everything\Everything.exe"], ["-startup"], ["-exit"]),
        new ExternalProgramDefinition(new("external-sayall", "Sayall", "无线麦 Sayall", "语音输入；停止时关闭应用及同路径辅助进程。", "\uE720", ToolKind.External),
            "sayall-windows-app.exe", "无线麦 SayAll", [@"无线麦 SayAll\sayall-windows-app.exe", @"Programs\无线麦 SayAll\sayall-windows-app.exe"], [], [])
    };
}

public sealed record ExternalProcessIdentity(int Id, long StartedUtcTicks, string ExecutablePath, int SessionId);
public sealed record ExternalProgramSnapshot(string? ExecutablePath, IReadOnlyList<ExternalProcessIdentity> Processes, string? Error = null, bool ExecutableMissing = false);

public interface IExternalProgramController
{
    ExternalProgramSnapshot Inspect(ExternalProgramDefinition definition, string? configuredPath);
    bool IsValidExecutable(ExternalProgramDefinition definition, string path);
    Task StartAsync(ExternalProgramDefinition definition, string executablePath);
    Task StopAsync(ExternalProgramDefinition definition, ExternalProgramSnapshot snapshot);
}
