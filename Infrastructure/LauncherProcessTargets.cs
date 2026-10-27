using System.Diagnostics;
using System.IO;

namespace EousGate.Infrastructure;

internal static class LauncherProcessTargets
{
    internal static IReadOnlyList<string> Resolve(ExternalProgramDefinition definition, string launchPath,
        Func<string, string?>? company = null)
    {
        var paths = new List<string> { Path.GetFullPath(launchPath) };
        if (!definition.Descriptor.Id.StartsWith("custom-", StringComparison.Ordinal)
            || !string.Equals(Path.GetFileName(launchPath), "launcher.exe", StringComparison.OrdinalIgnoreCase)) return paths;
        company ??= path => FileVersionInfo.GetVersionInfo(path).CompanyName;
        try
        {
            if (!string.Equals(company(launchPath), "miHoYo", StringComparison.OrdinalIgnoreCase)) return paths;
            var root = Path.GetDirectoryName(paths[0])!;
            // Only the installer's immediate numeric version directories; never games, helpers or arbitrary descendants.
            foreach (var directory in Directory.EnumerateDirectories(root))
            {
                if (!Version.TryParse(Path.GetFileName(directory), out _)) continue;
                var target = Path.Combine(directory, "HYP.exe");
                if (File.Exists(target) && string.Equals(company(target), "miHoYo", StringComparison.OrdinalIgnoreCase)) paths.Add(target);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception)
        { throw new IOException("无法核对启动器的实际运行文件，请检查安装目录权限后重试。", ex); }
        return paths;
    }
}
