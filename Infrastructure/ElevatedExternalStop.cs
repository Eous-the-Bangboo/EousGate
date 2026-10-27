using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace EousGate.Infrastructure;

// An explicit Stop may authorize this short-lived worker; the desktop assistant stays unelevated.
internal static class ElevatedExternalStop
{
    internal const string ArgumentPrefix = "--external-stop-worker=";
    internal sealed record Request(string ToolId, string LaunchPath, ExternalProcessIdentity[] Processes, bool ForceTerminate);

    internal static string Encode(ExternalProgramDefinition definition, ExternalProgramSnapshot snapshot)
        => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new Request(definition.Descriptor.Id,
            snapshot.ExecutablePath ?? throw new InvalidOperationException("缺少程序路径。"), snapshot.Processes.ToArray(), definition.ForceTerminateOnTimeout)));

    internal static Request Decode(string payload)
    {
        if (payload.Length is 0 or > 30000) throw new ArgumentException("关闭请求无效。");
        var request = JsonSerializer.Deserialize<Request>(Convert.FromBase64String(payload)) ?? throw new ArgumentException("关闭请求无效。");
        if (string.IsNullOrWhiteSpace(request.ToolId) || request.Processes is null || request.Processes.Length is 0 or > 64
            || request.Processes.Select(p => p.Id).Distinct().Count() != request.Processes.Length) throw new ArgumentException("关闭请求无效。");
        ValidatePath(request.LaunchPath);
        foreach (var process in request.Processes)
        {
            if (process.Id <= 0 || process.Id == Environment.ProcessId || process.StartedUtcTicks <= 0
                || process.SessionId != Process.GetCurrentProcess().SessionId) throw new ArgumentException("关闭进程身份无效。");
            ValidatePath(process.ExecutablePath);
        }
        if (string.Equals(request.LaunchPath, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("不能关闭助手自身。");
        return request;
    }
    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFullPath(path), path, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("关闭程序路径无效。");
    }

    internal static ProcessStartInfo StartInfo(string executable, string payload)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Path.GetDirectoryName(executable)! };
        start.ArgumentList.Add(ArgumentPrefix + payload);
        return start;
    }
    internal static async Task RunAsync(ExternalProgramDefinition definition, ExternalProgramSnapshot snapshot)
    {
        try
        {
            var payload = Encode(definition, snapshot);
            _ = Decode(payload);
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("无法定位授权关闭程序。");
            using var worker = await Task.Run(() => Process.Start(StartInfo(executable, payload)))
                ?? throw new InvalidOperationException("未能启动授权关闭程序。");
            await worker.WaitForExitAsync();
            if (worker.ExitCode == 0) return;
            if (worker.ExitCode == 5) throw new InvalidOperationException("授权后仍无法关闭此程序，请从程序自身退出。");
            if (worker.ExitCode == 4 && !definition.ForceTerminateOnTimeout)
                throw new InvalidOperationException("程序未正常退出，可能只缩到了托盘；如需彻底结束，请在此程序设置中允许“超时强制结束”后重试。");
            throw new InvalidOperationException("授权关闭未完成，程序仍可能运行，请检查后重试。");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { throw new InvalidOperationException("已取消关闭授权，程序保持运行。", ex); }
    }
    internal static int ExecuteWorker(string payload)
    {
        try
        {
            var request = Decode(payload);
            var definition = new ExternalProgramDefinition(new(request.ToolId, "Stop worker", "授权关闭", "", "", ToolKind.External),
                Path.GetFileName(request.LaunchPath), "", [], [], [], request.ForceTerminate);
            // No startup/exit command execution, no recursive elevation, only pinned identities from the caller.
            var controller = new WindowsExternalProgramController(window =>
            {
                if (PostMessage(window, 0x10, IntPtr.Zero, IntPtr.Zero)) return 0;
                var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                return error == 0 ? 31 : error;
            });
            Task.Run(() => controller.StopAsync(definition, new(request.LaunchPath, request.Processes))).GetAwaiter().GetResult();
            return 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5) { return 5; }
        catch (InvalidOperationException) { return 4; }
        catch (Exception ex) when (ex is ArgumentException or FormatException or JsonException or IOException or NullReferenceException) { return 2; }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
