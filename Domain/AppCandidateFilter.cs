namespace EousGate;

public static class AppCandidateFilter
{
    public static IReadOnlyList<AppCandidate> Filter(
        IEnumerable<AppCandidate> candidates,
        Predicate<string> executableExists,
        Func<string, string> normalizePath)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(executableExists);
        ArgumentNullException.ThrowIfNull(normalizePath);

        return candidates
            .Where(candidate => candidate.IsAvailable && !string.IsNullOrWhiteSpace(candidate.ExecutablePath))
            .Select(candidate =>
            {
                if (candidate.IsPackaged) return candidate;
                var normalizedPath = normalizePath(candidate.ExecutablePath);
                return candidate with { Id = normalizedPath, ExecutablePath = normalizedPath };
            })
            .Where(candidate => candidate.IsPackaged || executableExists(candidate.ExecutablePath))
            .GroupBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }
}
