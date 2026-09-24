namespace EousGate.Infrastructure;

/// <summary>Pure geometry and timing rules for dismissing an open panel.</summary>
public static class OverlayDismissalPolicy
{
    public static bool IsWithinSafeArea(
        System.Drawing.Point cursor,
        System.Drawing.Rectangle panelBounds,
        System.Drawing.Rectangle edgeBandBounds)
        => panelBounds.Contains(cursor) || edgeBandBounds.Contains(cursor);

    public static bool HasElapsed(DateTime? outsideSince, DateTime now, int delayMs)
    {
        if (outsideSince is null) return false;
        var delay = Math.Clamp(delayMs, 0, 10_000);
        return now - outsideSince.Value >= TimeSpan.FromMilliseconds(delay);
    }
}
