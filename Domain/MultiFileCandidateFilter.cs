namespace EousGate;

public static class MultiFileCandidateFilter
{
    public static IReadOnlyList<AppCandidate> Intersect(IReadOnlyList<IReadOnlyList<AppCandidate>> candidateSets)
    {
        ArgumentNullException.ThrowIfNull(candidateSets);
        if (candidateSets.Count == 0) return [];

        return candidateSets[0]
            .Where(candidate => candidateSets.Skip(1).All(set => set.Any(other => IsSameInvocation(candidate, other))))
            .ToArray();
    }

    private static bool IsSameInvocation(AppCandidate left, AppCandidate right)
        => string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Arguments, right.Arguments, StringComparison.Ordinal);
}
