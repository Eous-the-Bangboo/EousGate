namespace EousGate;

public sealed record OpenPanelPlan(IReadOnlyList<AppCandidate> VisibleCandidates, bool CanExpand)
{
    public static OpenPanelPlan Create(IReadOnlyList<AppCandidate> candidates, int displayCount)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var count = Math.Clamp(displayCount, 1, 12);
        return new OpenPanelPlan(candidates.Take(count).ToArray(), candidates.Count > count);
    }
}
