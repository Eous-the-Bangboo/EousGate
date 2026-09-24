using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace EousGate.Infrastructure;

public sealed class OpenFileService : IOpenFileService
{
    private readonly Func<ProcessStartInfo, Process?> _startProcess;
    private readonly DiagnosticsLogger _logger;

    public OpenFileService(Func<ProcessStartInfo, Process?>? startProcess = null, DiagnosticsLogger? logger = null)
    {
        _startProcess = startProcess ?? (info => Process.Start(info));
        _logger = logger ?? new DiagnosticsLogger();
    }

    public OpenResult Open(IReadOnlyList<string> filePaths, AppCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        if (filePaths.Count == 0)
            return new OpenResult(false, "没有可打开的文件。");

        if (!candidate.IsAvailable)
            return new OpenResult(false, "所选软件已不可用，请重新选择。");

        if (candidate.IsPackaged)
            return OpenPackagedAssociation(filePaths, candidate);

        if (string.IsNullOrWhiteSpace(candidate.ExecutablePath) || !File.Exists(candidate.ExecutablePath))
            return new OpenResult(false, "所选软件已不可用，请重新选择。");

        try
        {
            var process = _startProcess(new ProcessStartInfo(candidate.ExecutablePath)
            {
                Arguments = $"{BuildFileArguments(filePaths)} {candidate.Arguments}".Trim(),
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(candidate.ExecutablePath) ?? Environment.CurrentDirectory
            });
            if (process is null) return new OpenResult(false, "无法启动所选软件。");
            return new OpenResult(true);
        }
        catch
        {
            _logger.Log("processstartfailed");
            return new OpenResult(false, "无法启动所选软件，请检查软件是否仍然可用。");
        }
    }

    private OpenResult OpenPackagedAssociation(IReadOnlyList<string> filePaths, AppCandidate candidate)
    {
        var handlerName = candidate.AssociationHandlerName;
        if (string.IsNullOrWhiteSpace(handlerName))
            return new OpenResult(false, "无法启动所选系统应用。");

        try
        {
            // AUMIDs (the normal MSIX handler form) have a stable activation
            // API. This avoids passing a managed/COM IDataObject into the
            // Shell handler, which can terminate the host process on some
            // Windows builds.
            if (handlerName.Contains('!', StringComparison.Ordinal))
            {
                try
                {
                    var manager = (IApplicationActivationManager)(object)new ApplicationActivationManager();
                    try
                    {
                        var itemArray = CreateShellItemArray(filePaths);
                        try
                        {
                            var hr = manager.ActivateForFile(handlerName, itemArray, "open", out _);
                            if (hr >= 0) return new OpenResult(true);
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(itemArray);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(manager);
                    }
                }
                catch
                {
                    _logger.Log("packagedfileactivationfailed");
                }
            }

            // AppX ProgID names do not contain an AUMID. Explorer still knows
            // how to activate these Shell entries, and keeps the failure
            // contained in a separate process if the package disappeared.
            var process = _startProcess(new ProcessStartInfo("explorer.exe")
            {
                Arguments = $"{QuoteArgument($"shell:AppsFolder\\{handlerName}")} {BuildFileArguments(filePaths)}",
                UseShellExecute = true
            });
            return process is null
                ? new OpenResult(false, "无法启动所选系统应用。")
                : new OpenResult(true);
        }
        catch
        {
            _logger.Log("packagedprocessstartfailed");
        }
        return new OpenResult(false, "无法启动所选系统应用，请检查应用是否仍然可用。");
    }

    private static string QuoteArgument(string value)
        => $"\"{value.Replace("\"", "\\\"")}\"";

    private static string BuildFileArguments(IEnumerable<string> filePaths)
        => string.Join(" ", filePaths.Select(QuoteArgument));

    private static IShellItemArray CreateShellItemArray(IReadOnlyList<string> filePaths)
    {
        var itemIds = new IntPtr[filePaths.Count];
        try
        {
            for (var index = 0; index < filePaths.Count; index++)
            {
                var hr = SHParseDisplayName(filePaths[index], IntPtr.Zero, out itemIds[index], 0, out _);
                Marshal.ThrowExceptionForHR(hr);
            }

            var arrayHr = SHCreateShellItemArrayFromIDLists((uint)itemIds.Length, itemIds, out var itemArray);
            Marshal.ThrowExceptionForHR(arrayHr);
            return itemArray;
        }
        finally
        {
            foreach (var itemId in itemIds)
                if (itemId != IntPtr.Zero) Marshal.FreeCoTaskMem(itemId);
        }
    }

    private enum ActivateOptions { None = 0 }

    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"), ClassInterface(ClassInterfaceType.None)]
    private sealed class ApplicationActivationManager { }

    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, [MarshalAs(UnmanagedType.LPWStr)] string arguments, ActivateOptions options, out uint processId);
        [PreserveSig] int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, [MarshalAs(UnmanagedType.Interface)] IShellItemArray itemArray, [MarshalAs(UnmanagedType.LPWStr)] string verb, out uint processId);
    }

    [ComImport, Guid("b63ea76d-1f85-456f-a19c-48159efa858b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray { }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHParseDisplayName(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        IntPtr bindContext,
        out IntPtr itemIdList,
        uint attributesIn,
        out uint attributesOut);

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHCreateShellItemArrayFromIDLists(
        uint itemCount,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] itemIdLists,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemArray itemArray);
}
