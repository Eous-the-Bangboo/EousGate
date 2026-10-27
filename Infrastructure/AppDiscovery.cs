using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;

namespace EousGate.Infrastructure;

public sealed class AppDiscovery : IAppDiscovery
{
    private readonly Func<UserSettings>? _settings;
    private readonly DiagnosticsLogger _logger;
    private readonly Dictionary<string, string> _defaults = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _shellRecommended = new(StringComparer.OrdinalIgnoreCase);

    public AppDiscovery(Func<UserSettings>? settings = null, DiagnosticsLogger? logger = null)
    {
        _settings = settings;
        _logger = logger ?? new DiagnosticsLogger();
    }

    public IReadOnlyList<AppCandidate> GetCandidates(IReadOnlyList<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        if (filePaths.Count == 0) return [];

        var extensions = filePaths
            .Select(path => Path.GetExtension(path) ?? "")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (extensions.Any(string.IsNullOrWhiteSpace)) return [];

        var candidateSets = extensions
            .Select(extension => GetCandidatesForExtension(extension, true))
            .ToArray();
        return MultiFileCandidateFilter.Intersect(candidateSets);
    }

    public IReadOnlyList<AppCandidate> GetCandidatesForExtension(string extension, bool applyPreferences)
    {
        var result = new List<AppCandidate>();
        var ext = FileTypeCatalog.NormalizeExtension(extension);
        if (ext.Length == 0) return result;
        _defaults.Remove(ext);
        _shellRecommended[ext] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var customCandidates = GetCustomCandidates(ext);
        result.AddRange(customCandidates);
        try
        {
            var defaultExecutable = QueryDefaultExecutable(ext);
            if (!string.IsNullOrWhiteSpace(defaultExecutable) && File.Exists(defaultExecutable))
            {
                var candidate = CreateCandidate(defaultExecutable);
                result.Add(candidate);
                _defaults[ext] = candidate.Id;
            }

            // Some applications (notably Microsoft Office) register the file
            // type's default ProgID but do not add themselves to OpenWithProgids.
            // Resolve that ProgID as well so an installed handler is discoverable.
            using var extensionKey = Registry.ClassesRoot.OpenSubKey(ext);
            using var userChoice = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{ext}\UserChoice");
            var defaultProgId = userChoice?.GetValue("ProgId")?.ToString() ?? extensionKey?.GetValue(null)?.ToString();
            if (!string.IsNullOrWhiteSpace(defaultProgId))
            {
                var executable = ResolveExecutable(defaultProgId);
                if (executable is not null)
                {
                    var candidate = CreateCandidate(executable);
                    result.Add(candidate);
                    _defaults.TryAdd(ext, candidate.Id);
                }
            }

            var keyPath = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{ext}\OpenWithList";
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            var names = key?.GetValueNames().Where(n => n.Length == 1).Select(n => key.GetValue(n)?.ToString()).Where(v => !string.IsNullOrWhiteSpace(v)).ToList() ?? [];
            using var classes = Registry.ClassesRoot.OpenSubKey($@"{ext}\OpenWithProgids");
            names.AddRange(classes?.GetValueNames() ?? []);
            foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var executable = ResolveExecutable(name!);
                if (executable is null) continue;
                result.Add(CreateCandidate(executable));
            }

            // Packaged apps (such as the current Windows Notepad) are exposed
            // by Shell association handlers rather than OpenWithProgids.
            AddShellHandlers(ext, result);
            var defaultAppId = PackagedAppIdentity.Resolve(defaultProgId);
            var packagedDefault = defaultAppId is null ? null : result.FirstOrDefault(c =>
                string.Equals(c.AssociationHandlerName, defaultAppId, StringComparison.OrdinalIgnoreCase));
            if (packagedDefault is not null) _defaults[ext] = packagedDefault.Id;
        }
        catch { _logger.Log("appdiscoveryfailed"); }
        result = AppCandidateFilter.Filter(result, File.Exists, NormalizePath).ToList();
        if (!applyPreferences)
            result.AddRange(customCandidates.Where(candidate => !candidate.IsAvailable));
        var preference = GetPreference(ext);
        var defaultId = _defaults.GetValueOrDefault(ext);
        var recommended = _shellRecommended.GetValueOrDefault(ext);
        var ordered = AppRecommendationPolicy.Order(ext, result, preference, defaultId, recommended);
        return applyPreferences
            ? ordered.Where(c => AppRecommendationPolicy.IsVisible(ext, c, preference, defaultId, recommended)).ToArray()
            : ordered;
    }

    private FileTypePreference? GetPreference(string extension)
        => _settings?.Invoke().FileTypes.FirstOrDefault(p => string.Equals(p.Key, extension, StringComparison.OrdinalIgnoreCase)).Value;

    public bool IsSuggestedForExtension(string extension, AppCandidate candidate)
        => AppRecommendationPolicy.IsSuggested(extension, candidate, GetPreference(extension),
            _defaults.GetValueOrDefault(extension), _shellRecommended.GetValueOrDefault(extension));

    public string GetRecommendationLabel(string extension, AppCandidate candidate)
    {
        if (string.Equals(_defaults.GetValueOrDefault(extension), candidate.Id, StringComparison.OrdinalIgnoreCase)) return "系统默认";
        var score = AppRecommendationPolicy.Score(extension, candidate, GetPreference(extension),
            _defaults.GetValueOrDefault(extension), _shellRecommended.GetValueOrDefault(extension));
        return score switch { >= 900 => "自定义", >= 80 => "类型匹配", < 0 => "按需启用", _ => "系统关联" };
    }

    private static AppCandidate CreateCandidate(string executable, string? preferredDisplayName = null)
    {
        var normalized = NormalizePath(executable);
        var displayName = string.IsNullOrWhiteSpace(preferredDisplayName) ? GetDisplayName(normalized) : preferredDisplayName.Trim();
        return new AppCandidate(normalized, displayName, normalized, null, File.Exists(normalized), normalized);
    }

    private IReadOnlyList<AppCandidate> GetCustomCandidates(string extension)
    {
        if (_settings is null) return [];
        var preference = _settings().FileTypes.FirstOrDefault(pair => string.Equals(pair.Key, extension, StringComparison.OrdinalIgnoreCase)).Value;
        if (preference?.CustomApps is null) return [];
        return preference.CustomApps
            .Where(app => !string.IsNullOrWhiteSpace(app.ExecutablePath))
            .Select(app =>
            {
                var executable = NormalizePath(app.ExecutablePath);
                var displayName = string.IsNullOrWhiteSpace(app.DisplayName) ? GetDisplayName(executable) : app.DisplayName.Trim();
                return new AppCandidate(executable, displayName, executable, app.Arguments, File.Exists(executable), executable);
            })
            .GroupBy(candidate => candidate.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    public static string NormalizeExecutablePath(string path)
    {
        var trimmed = path.Trim().Trim('"');
        try { return Path.GetFullPath(trimmed); }
        catch { return trimmed; }
    }

    private static string NormalizePath(string path) => NormalizeExecutablePath(path);

    public static string GetDisplayName(string executable)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(executable);
            var name = info.FileDescription;
            if (string.IsNullOrWhiteSpace(name)) name = info.ProductName;
            if (!string.IsNullOrWhiteSpace(name))
            {
                name = name.Trim();
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = Path.GetFileNameWithoutExtension(name);
                if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, "Microsoft® Windows® Operating System", StringComparison.OrdinalIgnoreCase)) return name;
            }
        }
        catch { }
        return Path.GetFileNameWithoutExtension(executable);
    }

    private void AddShellHandlers(string extension, ICollection<AppCandidate> result)
    {
        try
        {
            var hr = SHAssocEnumHandlers(extension, AssocFilter.None, out var handlers);
            if (hr != 0 || handlers is null) return;
            var item = new IAssocHandler[1];
            while (handlers.Next(1, item, out var fetched) == 0 && fetched == 1)
            {
                var handler = item[0];
                try
                {
                    handler.GetName(out var name);
                    handler.GetUIName(out var uiName);
                    handler.GetIconLocation(out var iconLocation, out _);
                    var candidate = CreateShellCandidate(name, uiName, iconLocation, GetAppUserModelId(handler));
                    if (candidate is not null)
                    {
                        result.Add(candidate);
                        if (handler.IsRecommended() == 0) _shellRecommended[extension].Add(candidate.Id);
                    }
                }
                catch { }
                finally
                {
                    if (handler is not null) Marshal.ReleaseComObject(handler);
                }
            }
            Marshal.ReleaseComObject(handlers);
        }
        catch { _logger.Log("shellhandlerdiscoveryfailed"); }
    }

    private static string? ExtractIconExecutable(string? iconLocation)
    {
        if (string.IsNullOrWhiteSpace(iconLocation) || iconLocation.StartsWith('@')) return null;
        var comma = iconLocation.LastIndexOf(',');
        return NormalizePath(comma > 0 ? iconLocation[..comma] : iconLocation);
    }

    internal static string? GetAppUserModelId(IAssocHandler handler)
    {
        try
        {
            if (handler is IObjectWithAppUserModelID identity)
            {
                identity.GetAppID(out var appId);
                return appId;
            }
        }
        catch (COMException) { } // Desktop handlers may not have an explicit AppID.
        return null;
    }

    internal static AppCandidate? CreateShellCandidate(string name, string? displayName, string? iconLocation, string? appId)
    {
        // GetName may be a localized label ("照片") or an executable path.
        // An indirect icon is only a resource reference, never an app identity.
        var resolvedAppId = PackagedAppIdentity.Resolve(appId) ?? PackagedAppIdentity.Resolve(name);
        if (resolvedAppId is not null)
            return CreatePackagedCandidate(name, displayName, iconLocation, resolvedAppId);

        var executable = File.Exists(name) ? name : ResolveExecutable(name);
        executable ??= ExtractIconExecutable(iconLocation);
        return executable is not null && File.Exists(executable)
            ? CreateCandidate(executable, displayName)
            : null;
    }

    private static AppCandidate CreatePackagedCandidate(string handlerName, string? displayName, string? iconPath, string appId)
    {
        var normalizedName = handlerName.Trim();
        var label = string.IsNullOrWhiteSpace(displayName) ? normalizedName : displayName.Trim();
        var shellTarget = $"shell:AppsFolder\\{appId}";
        return new AppCandidate(
            $"packaged:{normalizedName}",
            label,
            shellTarget,
            null,
            true,
            iconPath,
            appId);
    }

    private static string? QueryDefaultExecutable(string extension)
    {
        var length = 0u;
        AssocQueryString(AssocF.Executable, AssocStr.Executable, extension, null, null, ref length);
        if (length == 0) return null;
        var buffer = new System.Text.StringBuilder((int)length);
        return AssocQueryString(AssocF.Executable, AssocStr.Executable, extension, null, buffer, ref length) == 0 ? buffer.ToString() : null;
    }

    private enum AssocF { Executable = 2 }
    private enum AssocStr { Executable = 2 }
    internal enum AssocFilter { None = 0, Recommended = 1 }
    [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(AssocF flags, AssocStr str, string pszAssoc, string? pszExtra, System.Text.StringBuilder? pszOut, ref uint pcchOut);

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHAssocEnumHandlers(string pszExtra, AssocFilter filter, out IEnumAssocHandlers handlers);

    [ComImport, Guid("973810AE-9599-4B88-9E4D-6EE98C9552DA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IEnumAssocHandlers
    {
        [PreserveSig] int Next(uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0, ArraySubType = UnmanagedType.Interface)] IAssocHandler[] handlers, out uint fetched);
        [PreserveSig] int Skip(uint count);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumAssocHandlers handlers);
    }

    [ComImport, Guid("F04061AC-1659-4A3F-A954-775AA57FC083"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAssocHandler
    {
        void GetName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetUIName([MarshalAs(UnmanagedType.LPWStr)] out string uiName);
        void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] out string iconLocation, out int index);
        [PreserveSig] int IsRecommended();
        void MakeDefault([MarshalAs(UnmanagedType.LPWStr)] string description);
        [PreserveSig] int Invoke(IntPtr dataObject);
        void CreateInvoker(IntPtr dataObject, out IAssocHandlerInvoker invoker);
    }

    [ComImport, Guid("92218CAB-ECAA-4335-8133-807FD234C2EE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAssocHandlerInvoker
    {
        [PreserveSig] int SupportsSelection();
        [PreserveSig] int Invoke();
    }

    [ComImport, Guid("36DB0196-9665-46D1-9BA7-D3709EECF9ED"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IObjectWithAppUserModelID
    {
        void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);
        void GetAppID([MarshalAs(UnmanagedType.LPWStr)] out string appId);
    }

    private static string? ResolveExecutable(string name)
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey($@"Applications\{name}\shell\open\command")
                ?? Registry.ClassesRoot.OpenSubKey($@"{name}\shell\open\command");
            var command = key?.GetValue(null)?.ToString();
            if (string.IsNullOrWhiteSpace(command)) return null;
            var expanded = Environment.ExpandEnvironmentVariables(command.Trim());
            if (expanded.StartsWith('"')) return expanded[1..expanded.IndexOf('"', 1)];
            return expanded.Split(' ', 2)[0];
        }
        catch { return null; }
    }
}
