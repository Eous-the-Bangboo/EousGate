using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using static EousGate.Infrastructure.AppDiscovery;

namespace EousGate.Infrastructure;

public sealed class OpenFileService : IOpenFileService
{
    private readonly Func<ProcessStartInfo, Process?> _startProcess;
    private readonly DiagnosticsLogger _logger;
    private readonly Func<string, string?> _resolveAppId;
    private readonly Func<string, IReadOnlyList<string>, int> _invokePackagedFiles;

    public OpenFileService(Func<ProcessStartInfo, Process?>? startProcess = null, DiagnosticsLogger? logger = null)
        : this(startProcess, logger, name => PackagedAppIdentity.Resolve(name), InvokePackagedFiles)
    {
    }

    internal OpenFileService(
        Func<ProcessStartInfo, Process?>? startProcess,
        DiagnosticsLogger? logger,
        Func<string, string?> resolveAppId,
        Func<string, IReadOnlyList<string>, int> invokePackagedFiles)
    {
        _startProcess = startProcess ?? (info => Process.Start(info));
        _logger = logger ?? new DiagnosticsLogger();
        _resolveAppId = resolveAppId;
        _invokePackagedFiles = invokePackagedFiles;
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
            var appId = _resolveAppId(handlerName);
            if (appId is not null && _invokePackagedFiles(appId, filePaths) == 0)
                return new OpenResult(true);
        }
        catch { } // Resolve/COM failures become a failed open action below.

        _logger.Log("packagedfileactivationfailed");
        return new OpenResult(false, "无法使用所选系统应用打开文件，请检查应用是否仍然可用。");
    }

    private static int InvokePackagedFiles(string appId, IReadOnlyList<string> filePaths)
    {
        var hr = SHAssocEnumHandlers(Path.GetExtension(filePaths[0]), AssocFilter.None, out var handlers);
        Marshal.ThrowExceptionForHR(hr);
        try
        {
            var item = new IAssocHandler[1];
            while (handlers.Next(1, item, out var fetched) == 0 && fetched == 1)
            {
                var handler = item[0];
                try
                {
                    var handlerAppId = PackagedAppIdentity.Resolve(GetAppUserModelId(handler));
                    if (handlerAppId is null)
                    {
                        try
                        {
                            handler.GetName(out var name);
                            handlerAppId = PackagedAppIdentity.Resolve(name);
                        }
                        catch (COMException) { continue; }
                    }
                    if (!string.Equals(appId, handlerAppId, StringComparison.OrdinalIgnoreCase)) continue;
                    return InvokeShellHandler(handler, filePaths);
                }
                finally
                {
                    Marshal.ReleaseComObject(handler);
                }
            }
            return unchecked((int)0x80070490); // Selected handler no longer registered.
        }
        finally
        {
            Marshal.ReleaseComObject(handlers);
        }
    }

    private static int InvokeShellHandler(IAssocHandler handler, IReadOnlyList<string> filePaths)
    {
        var itemArray = CreateShellItemArray(filePaths);
        var dataObject = IntPtr.Zero;
        try
        {
            var dataHandler = new Guid("B8C0BD9F-ED24-455C-83E6-D5390C4FE8C4"); // BHID_DataObject
            var dataInterface = new Guid("0000010E-0000-0000-C000-000000000046"); // IID_IDataObject
            itemArray.BindToHandler(IntPtr.Zero, ref dataHandler, ref dataInterface, out dataObject);
            // Pass the Shell-owned native IDataObject pointer. A managed WPF
            // DataObject/CCW is not interchangeable with this Shell payload.
            if (filePaths.Count == 1) return handler.Invoke(dataObject);

            handler.CreateInvoker(dataObject, out var invoker);
            try
            {
                var support = invoker.SupportsSelection();
                return support == 0 ? invoker.Invoke() : support;
            }
            finally
            {
                Marshal.ReleaseComObject(invoker);
            }
        }
        finally
        {
            if (dataObject != IntPtr.Zero) Marshal.Release(dataObject);
            Marshal.ReleaseComObject(itemArray);
        }
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

    [ComImport, Guid("b63ea76d-1f85-456f-a19c-48159efa858b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray
    {
        void BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr result);
    }

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
