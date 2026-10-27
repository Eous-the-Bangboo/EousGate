using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Principal;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace EousGate.Infrastructure;

public sealed class WindowsExternalProgramController : IExternalProgramController
{
    private readonly int _session = Process.GetCurrentProcess().SessionId;
    private static readonly string CurrentUserSid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("无法确定当前用户。");
    private readonly Func<IntPtr, int> _requestClose;
    private readonly Func<ExternalProgramDefinition, ExternalProgramSnapshot, Task>? _elevatedStop;
    private readonly Func<ExternalProgramDefinition, string, IReadOnlyList<string>> _targets;

    public WindowsExternalProgramController() : this(RequestClose, ElevatedExternalStop.RunAsync) { }
    internal WindowsExternalProgramController(Func<ExternalProgramDefinition, string, IReadOnlyList<string>> targets) : this(RequestClose, null, targets) { }
    internal WindowsExternalProgramController(Func<IntPtr, int> requestClose,
        Func<ExternalProgramDefinition, ExternalProgramSnapshot, Task>? elevatedStop = null,
        Func<ExternalProgramDefinition, string, IReadOnlyList<string>>? targets = null)
    { _requestClose = requestClose; _elevatedStop = elevatedStop; _targets = targets ?? ((definition, path) => LauncherProcessTargets.Resolve(definition, path)); }

    private static int RequestClose(IntPtr window)
    {
        if (PostMessage(window, 0x10, IntPtr.Zero, IntPtr.Zero)) return 0;
        var error = Marshal.GetLastWin32Error();
        return error == 0 ? 31 : error;
    }

    public bool IsValidExecutable(ExternalProgramDefinition definition, string path)
        => File.Exists(path) && string.Equals(Path.GetFileName(path), definition.ExecutableName, StringComparison.OrdinalIgnoreCase);

    public ExternalProgramSnapshot Inspect(ExternalProgramDefinition definition, string? configuredPath)
    {
        try
        {
            var path = string.IsNullOrWhiteSpace(configuredPath) ? Locate(definition) : Path.GetFullPath(configuredPath);
            if (path is null) return new(null, [], "未找到程序，请在设置中选择程序文件。");
            if (!string.Equals(Path.GetFileName(path), definition.ExecutableName, StringComparison.OrdinalIgnoreCase))
                return new(path, [], "程序文件不匹配，请选择 " + definition.ExecutableName + "。");
            var targetPaths = _targets(definition, path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var matches = new List<ExternalProcessIdentity>(); string? error = null;
            foreach (var process in targetPaths.Select(p => Path.GetFileNameWithoutExtension(p)).Distinct(StringComparer.OrdinalIgnoreCase)
                .SelectMany(Process.GetProcessesByName))
            {
                using (process)
                {
                    try
                    {
                        if (process.SessionId != _session) continue; // Never manage Windows services or another login session.
                        using var handle = OpenProcess(0x100000 | 0x1000, false, process.Id);
                        if (handle.IsInvalid) { if (!process.HasExited) error = "无法读取应用进程，请检查程序权限后重试。"; continue; }
                        var identity = ReadIdentity(handle, process.Id, _session);
                        if (identity is not null && targetPaths.Contains(identity.ExecutablePath)) matches.Add(identity);
                    }
                    catch (InvalidOperationException) { /* A process may exit during inspection. */ }
                    catch (Win32Exception) { error = "无法读取应用进程，请检查程序权限后重试。"; }
                }
            }
            var missing = error is null && matches.Count == 0 && !IsValidExecutable(definition, path);
            if (missing) error = "程序路径已失效，请在设置中重新选择。";
            return new(path, matches, error, missing);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        { return new(configuredPath, [], "无法定位程序，请在设置中重新选择程序文件。"); }
    }

    private string? Locate(ExternalProgramDefinition definition)
    {
        var runningPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(definition.ExecutableName)))
        {
            using (process)
            {
                try
                {
                    if (process.SessionId != _session) continue;
                    using var handle = OpenProcess(0x100000 | 0x1000, false, process.Id);
                    if (!handle.IsInvalid && ReadIdentity(handle, process.Id, _session) is { } identity && IsValidExecutable(definition, identity.ExecutablePath)) runningPaths.Add(identity.ExecutablePath);
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { /* Continue with installed locations. */ }
            }
        }
        if (runningPaths.Count == 1) return runningPaths.Single();
        if (runningPaths.Count > 1) throw new IOException("检测到多个安装位置，请选择要管理的程序。");
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
            foreach (var relative in definition.DefaultRelativePaths)
            {
                var path = Path.Combine(root, relative);
                if (IsValidExecutable(definition, path)) return path;
            }
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;
                foreach (var name in uninstall.GetSubKeyNames())
                {
                    using var entry = uninstall.OpenSubKey(name);
                    if (entry?.GetValue("DisplayName") is not string display || !display.StartsWith(definition.RegistryName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (entry.GetValue("InstallLocation") is string location)
                    {
                        var path = Path.Combine(location.Trim('"', ' '), definition.ExecutableName);
                        if (IsValidExecutable(definition, path)) return path;
                    }
                    if (entry.GetValue("DisplayIcon") is string icon)
                    {
                        var path = icon.Trim().StartsWith('"') ? icon.Split('"')[1] : icon.Split(',')[0].Trim();
                        if (IsValidExecutable(definition, path)) return path;
                    }
                }
            }
        return null;
    }

    public Task StartAsync(ExternalProgramDefinition definition, string executablePath)
    {
        if (!IsValidExecutable(definition, executablePath)) throw new InvalidOperationException("程序文件无效，请重新选择。");
        // Shell authorization can wait for the user's UAC response. Keep the UI responsive.
        return Task.Run(() => Launch(executablePath, definition.StartArguments, definition.RawStartArguments, allowElevation: true));
    }
    internal static void Launch(string path, IEnumerable<string> arguments, string? rawArguments = null,
        bool allowElevation = false, Action<ProcessStartInfo>? startProcess = null)
    {
        var start = new ProcessStartInfo(path) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(path)! };
        if (rawArguments is not null) start.Arguments = rawArguments;
        else foreach (var argument in arguments) start.ArgumentList.Add(argument);
        startProcess ??= info =>
        {
            using var process = Process.Start(info);
            // Shell may hand off to an existing instance. Real process inspection confirms success.
            if (process is null && !info.UseShellExecute) throw new InvalidOperationException("无法启动程序。");
        };
        try
        {
            try { startProcess(start); }
            catch (Win32Exception ex) when (allowElevation && ex.NativeErrorCode == 740)
            {
                // Only retry an explicit elevation-required failure. Shell honors the EXE
                // manifest/compatibility settings and asks Windows for consent as needed.
                start.UseShellExecute = true;
                start.Verb = "open";
                startProcess(start);
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { throw new InvalidOperationException("已取消管理员权限授权，程序未启动；可重新点击开关并在 Windows 提示中允许。", ex); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 740)
        { throw new InvalidOperationException("此程序需要管理员权限，请检查程序的兼容性设置或以相同权限手动操作。", ex); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
        { throw new InvalidOperationException("Windows 拒绝访问此程序，请检查文件权限或安全软件的拦截。", ex); }
    }
    public async Task StopAsync(ExternalProgramDefinition definition, ExternalProgramSnapshot snapshot)
    {
        try { await StopCoreAsync(definition, snapshot); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5 && _elevatedStop is not null && !FileDropPermissionGuard.IsProcessElevated())
        { await _elevatedStop(definition, snapshot); }
    }

    private async Task StopCoreAsync(ExternalProgramDefinition definition, ExternalProgramSnapshot snapshot)
    {
        if (snapshot.Processes.Count == 0 || snapshot.ExecutablePath is null) return;
        if (!string.Equals(Path.GetFileName(snapshot.ExecutablePath), definition.ExecutableName, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("程序文件不匹配，已停止操作。");
        var targetPaths = _targets(definition, snapshot.ExecutablePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Only operate on the observed processes. Pinned handles and creation times prevent PID reuse mistakes.
        var handles = new List<(ExternalProcessIdentity Identity, SafeProcessHandle Handle)>();
        try
        {
            foreach (var identity in snapshot.Processes)
            {
                if (identity.SessionId != _session || !targetPaths.Contains(identity.ExecutablePath)) continue;
                var handle = OpenProcess(0x100000u | 0x1000u | (definition.ForceTerminateOnTimeout ? 0x1u : 0u), false, identity.Id);
                if (handle.IsInvalid)
                {
                    var code = Marshal.GetLastWin32Error(); handle.Dispose();
                    if (code == 87) continue; // Already exited.
                    throw StopFailure(code);
                }
                if (ReadIdentity(handle, identity.Id, _session) != identity) { handle.Dispose(); continue; }
                handles.Add((identity, handle));
            }
            if (handles.Count == 0) return;
            if (definition.ExitArguments.Length > 0 && File.Exists(snapshot.ExecutablePath)) Launch(snapshot.ExecutablePath, definition.ExitArguments);
            else
            {
                var closeError = 0;
                var enumerated = EnumWindows((window, _) =>
                {
                    GetWindowThreadProcessId(window, out var pid);
                    if (handles.Any(p => p.Identity.Id == pid && WaitForSingleObject(p.Handle, 0) == 258))
                    {
                        var error = _requestClose(window);
                        // The window may disappear between enumeration and posting.
                        if (error != 0 && error != 1400 && (closeError == 0 || error == 5)) closeError = error;
                    }
                    return true;
                }, IntPtr.Zero);
                if (!enumerated) throw StopFailure(Marshal.GetLastWin32Error());
                if (closeError != 0) throw StopFailure(closeError);
            }
            if (await WaitForExit(handles, TimeSpan.FromSeconds(4))) return;
            if (!definition.ForceTerminateOnTimeout) throw new InvalidOperationException("程序未正常退出，仍在运行；可自行退出或在程序设置中允许超时强制结束。");
            foreach (var (_, handle) in handles)
                if (WaitForSingleObject(handle, 0) == 258 && !TerminateProcess(handle, 0)) throw StopFailure(Marshal.GetLastWin32Error());
            if (!await WaitForExit(handles, TimeSpan.FromSeconds(3))) throw new InvalidOperationException("应用仍在退出中，请稍后重试。");
        }
        finally { foreach (var (_, handle) in handles) handle.Dispose(); }
    }
    internal static Win32Exception StopFailure(int code) => new(code, code == 5
        ? "Windows 拒绝关闭此程序，可能是管理员权限高于助手；请从程序自身退出，或退出助手后以管理员身份重新运行再重试。开启强制结束也不能绕过权限限制。"
        : "无法向此程序请求退出，请从程序自身退出后重试。");
    private static async Task<bool> WaitForExit(List<(ExternalProcessIdentity Identity, SafeProcessHandle Handle)> handles, TimeSpan timeout)
    {
        var elapsed = Stopwatch.StartNew();
        do
        {
            if (handles.All(p => WaitForSingleObject(p.Handle, 0) == 0)) return true;
            await Task.Delay(100);
        } while (elapsed.Elapsed < timeout);
        return false;
    }
    private static ExternalProcessIdentity? ReadIdentity(SafeProcessHandle handle, int id, int session)
    {
        if (WaitForSingleObject(handle, 0) == 0) return null;
        if (!OpenProcessToken(handle, 0x8, out var token)) throw new Win32Exception(Marshal.GetLastWin32Error());
        using (token)
        {
            GetTokenInformation(token, 1, IntPtr.Zero, 0, out var size);
            if (size <= 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!GetTokenInformation(token, 1, buffer, size, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (new SecurityIdentifier(Marshal.ReadIntPtr(buffer)).Value != CurrentUserSid) return null;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        var text = new StringBuilder(32768); var length = text.Capacity;
        if (!QueryFullProcessImageName(handle, 0, text, ref length) || !GetProcessTimes(handle, out var creation, out _, out _, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(id, DateTime.FromFileTimeUtc(creation).Ticks, Path.GetFullPath(text.ToString()), session);
    }
    private delegate bool WindowCallback(IntPtr window, IntPtr data);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int id);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int type, IntPtr buffer, int length, out int needed);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder name, ref int size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(SafeProcessHandle process, uint code);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EnumWindows(WindowCallback callback, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
}
