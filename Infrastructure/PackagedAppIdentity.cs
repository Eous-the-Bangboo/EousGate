using Microsoft.Win32;

namespace EousGate.Infrastructure;

internal static class PackagedAppIdentity
{
    internal static string? Resolve(string? name, Func<string, string?>? readProgIdAppId = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = name.Trim();
        if (IsPackagedAppId(name)) return name;
        if (!name.StartsWith("AppX", StringComparison.OrdinalIgnoreCase)
            || name.Any(character => !char.IsLetterOrDigit(character))) return null;

        var appId = (readProgIdAppId ?? ReadProgIdAppId)(name)?.Trim();
        return IsPackagedAppId(appId) ? appId : null;
    }

    private static bool IsPackagedAppId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var separator = value.IndexOf('!');
        return separator > 0 && separator < value.Length - 1
            && separator == value.LastIndexOf('!')
            && !value.Any(character => char.IsWhiteSpace(character) || character is '\\' or '/' or ':' or '"');
    }

    private static string? ReadProgIdAppId(string progId)
    {
        using var key = Registry.ClassesRoot.OpenSubKey($@"{progId}\Application");
        return key?.GetValue("AppUserModelID") as string;
    }
}
