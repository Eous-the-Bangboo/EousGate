namespace EousGate;

public static class FileDropPolicy
{
    public static bool TryGetFiles(
        IReadOnlyList<string>? paths,
        Predicate<string> isDirectory,
        out IReadOnlyList<string>? filePaths)
    {
        ArgumentNullException.ThrowIfNull(isDirectory);
        filePaths = null;
        if (paths is not { Count: > 0 }) return false;

        var accepted = new string[paths.Count];
        for (var index = 0; index < paths.Count; index++)
        {
            var candidate = paths[index];
            if (string.IsNullOrWhiteSpace(candidate) || isDirectory(candidate)) return false;
            accepted[index] = candidate;
        }

        filePaths = Array.AsReadOnly(accepted);
        return true;
    }
}
