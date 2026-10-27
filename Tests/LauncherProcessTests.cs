using EousGate;
using EousGate.Infrastructure;
using System.Diagnostics;
using System.Text.Json;

internal static class LauncherProcessTests
{
    internal static int InspectLive(string path)
    {
        var definition = Definition(path);
        var snapshot = new WindowsExternalProgramController().Inspect(definition, path);
        Console.WriteLine($"READ ONLY launcher: matched={snapshot.Processes.Count}; names={string.Join(',', snapshot.Processes.Select(p => Path.GetFileName(p.ExecutablePath)).Distinct())}; error={snapshot.Error ?? "none"}; no commands sent.");
        return snapshot.Error is null && snapshot.Processes.Count > 0 ? 0 : 1;
    }
    internal static ExternalProgramDefinition Definition(string path) => new(new("custom-fixture", "Launcher", "测试启动器", "", "", ToolKind.External), Path.GetFileName(path), "", [], [], [], false);
    internal static void TargetBoundaries()
    {
        var root = Path.Combine(Path.GetTempPath(), "Eous-launch-target-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        var launch = Path.Combine(root, "launcher.exe"); File.WriteAllText(launch, "fixture");
        foreach (var dir in new[] { "1.0.0.1", "2.0.0.2", "games", "other", "3.0.0.3" })
        { Directory.CreateDirectory(Path.Combine(root, dir)); File.WriteAllText(Path.Combine(root, dir, "HYP.exe"), "fixture"); }
        var def = Definition(launch);
        var targets = LauncherProcessTargets.Resolve(def, launch, p => p.Contains("3.0.0.3") ? "Other" : "miHoYo");
        Check(targets.Count == 3 && targets.Contains(Path.Combine(root, "1.0.0.1", "HYP.exe")) && targets.Contains(Path.Combine(root, "2.0.0.2", "HYP.exe")), "only matching publisher in immediate version directories is a handoff target");
        Check(LauncherProcessTargets.Resolve(def, launch, _ => "Other").SequenceEqual([launch]), "other launchers retain exact-path behavior");
        Check(LauncherProcessTargets.Resolve(def with { Descriptor = def.Descriptor with { Id = "external-preset" } }, launch, _ => "miHoYo").Count == 1, "presets must not adopt custom launcher profiles");
    }
    internal static void StopRequests()
    {
        var path = Path.Combine(Path.GetTempPath(), "launcher.exe");
        var process = new ExternalProcessIdentity(12345, DateTime.UtcNow.Ticks, path, Process.GetCurrentProcess().SessionId);
        var def = Definition(path) with { ForceTerminateOnTimeout = true };
        var payload = ElevatedExternalStop.Encode(def, new(path, [process]));
        var decoded = ElevatedExternalStop.Decode(payload);
        Check(decoded.ForceTerminate && decoded.Processes.Single() == process && decoded.LaunchPath == path, "authorization preserves exact identity and explicit force choice");
        var start = ElevatedExternalStop.StartInfo(Environment.ProcessPath!, payload);
        Check(start.UseShellExecute && start.Verb == "runas" && start.WindowStyle == ProcessWindowStyle.Hidden
            && start.ArgumentList.Single() == ElevatedExternalStop.ArgumentPrefix + payload, "only the narrow worker is elevated, without shell command composition");
        foreach (var invalid in new[] { decoded with { LaunchPath = "relative.exe" }, decoded with { Processes = [process, process] }, decoded with { Processes = [process with { SessionId = -1 }] } })
        {
            try { ElevatedExternalStop.Decode(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(invalid))); throw new Exception("invalid stop request accepted"); }
            catch (ArgumentException) { }
        }
        Check(ElevatedExternalStop.ExecuteWorker("invalid-base64") == 2, "invalid worker requests fail without a window or control action");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
