namespace EousGate.Infrastructure;

/// <summary>Calculates the physical receive-strip bounds for each screen edge.</summary>
public static class EdgeBandLayoutMath
{
    public static bool IsValidRange(double start, double end)
        => double.IsFinite(start) && double.IsFinite(end) && start >= 0 && start < end && end <= 100;

    public const double MinLengthPercent = 10;
    public const double MaxLengthPercent = 100;
    public const double MinWidthPercent = 0.1;
    public const double MaxWidthPercent = 5;
    public const int MinOffsetPixels = -10_000;
    public const int MaxOffsetPixels = 10_000;

    public static EdgeBandLayout Normalize(EdgeBandLayout? layout)
    {
        var source = layout ?? new EdgeBandLayout();
        var hasRange = source.StartPercent.HasValue || source.EndPercent.HasValue;
        var validRange = source.StartPercent is { } start && source.EndPercent is { } end && IsValidRange(start, end);
        return new EdgeBandLayout
        {
            StartPercent = hasRange ? validRange ? source.StartPercent : 0 : null,
            EndPercent = hasRange ? validRange ? source.EndPercent : 100 : null,
            LengthPercent = double.IsFinite(source.LengthPercent) ? Math.Clamp(source.LengthPercent, MinLengthPercent, MaxLengthPercent) : 100,
            WidthPercent = double.IsFinite(source.WidthPercent) ? Math.Clamp(source.WidthPercent, MinWidthPercent, MaxWidthPercent) : 0.5,
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
        var shortEdge = Math.Max(1, Math.Min(workingArea.Width, workingArea.Height));
        var layout = Normalize(configured);
        if (layout.StartPercent.HasValue && layout.EndPercent.HasValue) return layout;
        var edgeLength = side == OverlayPlacementSide.Top ? workingArea.Width : workingArea.Height;
        if (edgeLength <= 0) return new EdgeBandLayout { StartPercent = 0, EndPercent = 100, WidthPercent = layout.WidthPercent };
        var bounds = CalculateBounds(workingArea, side, configured, settings.EdgeBandWidth);
        var start = side == OverlayPlacementSide.Top ? bounds.Left - workingArea.Left : bounds.Top - workingArea.Top;
        var length = side == OverlayPlacementSide.Top ? bounds.Width : bounds.Height;
        return Normalize(new EdgeBandLayout
        {
            StartPercent = start * 100d / edgeLength,
            EndPercent = (start + length) * 100d / edgeLength,
            WidthPercent = configured is null ? (side == OverlayPlacementSide.Top ? bounds.Height : bounds.Width) / (double)shortEdge * 100 : layout.WidthPercent
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
        int startOffset, length;
        if (layout.StartPercent is { } start && layout.EndPercent is { } end)
        {
            startOffset = Math.Clamp((int)Math.Round(edgeLength * start / 100d), 0, edgeLength - 1);
            var endOffset = Math.Clamp((int)Math.Round(edgeLength * end / 100d), startOffset + 1, edgeLength);
            length = endOffset - startOffset;
        }
        else
        {
            // Preserve the actual old position, including boundary clamping, before editing migrates it.
            length = Math.Clamp((int)Math.Round(edgeLength * layout.LengthPercent / 100d), 1, edgeLength);
            startOffset = Math.Clamp((edgeLength - length) / 2 + layout.OffsetPixels, 0, edgeLength - length);
        }
        var thickness = configured is null
            ? Math.Clamp((int)Math.Round(double.IsFinite(legacyWidthPixels) ? legacyWidthPixels : 8), 1, Math.Min(64, Math.Min(workingArea.Width, workingArea.Height)))
            : Math.Clamp((int)Math.Round(Math.Min(workingArea.Width, workingArea.Height) * layout.WidthPercent / 100d), 1, Math.Min(workingArea.Width, workingArea.Height));

        if (side == OverlayPlacementSide.Top)
        {
            var left = workingArea.Left + startOffset;
            return new System.Drawing.Rectangle(left, workingArea.Top, length, thickness);
        }

        var top = workingArea.Top + startOffset;
        var x = side == OverlayPlacementSide.Right ? workingArea.Right - thickness : workingArea.Left;
        return new System.Drawing.Rectangle(x, top, thickness, length);
    }
}
