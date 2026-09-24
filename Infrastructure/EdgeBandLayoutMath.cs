namespace EousGate.Infrastructure;

/// <summary>Calculates the physical receive-strip bounds for each screen edge.</summary>
public static class EdgeBandLayoutMath
{
    public const double MinLengthPercent = 10;
    public const double MaxLengthPercent = 100;
    public const double MinWidthPercent = 0.1;
    public const double MaxWidthPercent = 5;
    public const int MinOffsetPixels = -10_000;
    public const int MaxOffsetPixels = 10_000;

    public static EdgeBandLayout Normalize(EdgeBandLayout? layout)
    {
        var source = layout ?? new EdgeBandLayout();
        return new EdgeBandLayout
        {
            LengthPercent = Math.Clamp(source.LengthPercent, MinLengthPercent, MaxLengthPercent),
            WidthPercent = Math.Clamp(source.WidthPercent, MinWidthPercent, MaxWidthPercent),
            OffsetPixels = Math.Clamp(source.OffsetPixels, MinOffsetPixels, MaxOffsetPixels)
        };
    }

    public static EdgeBandLayout GetEditableLayout(UserSettings settings, OverlayPlacementSide side, System.Drawing.Rectangle workingArea)
    {
        var configured = side switch
        {
            OverlayPlacementSide.Right => settings.RightEdgeBand,
            OverlayPlacementSide.Top => settings.TopEdgeBand,
            _ => settings.LeftEdgeBand
        };
        if (configured is not null) return Normalize(configured);

        var shortEdge = Math.Max(1, Math.Min(workingArea.Width, workingArea.Height));
        var legacyWidth = Math.Clamp(settings.EdgeBandWidth, 1, 64);
        return Normalize(new EdgeBandLayout
        {
            LengthPercent = 100,
            WidthPercent = legacyWidth / shortEdge * 100,
            OffsetPixels = 0
        });
    }

    public static System.Drawing.Rectangle CalculateBounds(
        System.Drawing.Rectangle workingArea,
        OverlayPlacementSide side,
        EdgeBandLayout? configured,
        double legacyWidthPixels)
    {
        if (workingArea.Width <= 0 || workingArea.Height <= 0)
            return System.Drawing.Rectangle.Empty;

        var layout = Normalize(configured);
        var edgeLength = side == OverlayPlacementSide.Top ? workingArea.Width : workingArea.Height;
        var length = Math.Clamp((int)Math.Round(edgeLength * layout.LengthPercent / 100d), 1, edgeLength);
        var thickness = configured is null
            ? Math.Clamp((int)Math.Round(legacyWidthPixels), 1, 64)
            : Math.Clamp((int)Math.Round(Math.Min(workingArea.Width, workingArea.Height) * layout.WidthPercent / 100d), 1, Math.Min(workingArea.Width, workingArea.Height));

        if (side == OverlayPlacementSide.Top)
        {
            var left = workingArea.Left + (workingArea.Width - length) / 2 + layout.OffsetPixels;
            left = Math.Clamp(left, workingArea.Left, workingArea.Right - length);
            return new System.Drawing.Rectangle(left, workingArea.Top, length, thickness);
        }

        var top = workingArea.Top + (workingArea.Height - length) / 2 + layout.OffsetPixels;
        top = Math.Clamp(top, workingArea.Top, workingArea.Bottom - length);
        var x = side == OverlayPlacementSide.Right ? workingArea.Right - thickness : workingArea.Left;
        return new System.Drawing.Rectangle(x, top, thickness, length);
    }
}
