using EousGate;
using EousGate.Infrastructure;
using System.Drawing;

internal static class EdgeBandRangeTests
{
    internal static void DefaultsAndBounds()
    {
        var settings = new UserSettings();
        foreach (var layout in new[] { settings.LeftEdgeBand!, settings.RightEdgeBand!, settings.TopEdgeBand! })
            Check(layout.StartPercent == 0 && layout.EndPercent == 100, "each new side must cover 0–100 by default");
        var copy = settings.Clone(); copy.LeftEdgeBand!.StartPercent = 20;
        Check(settings.LeftEdgeBand!.StartPercent == 0, "range drafts must clone independently");
        var area = new Rectangle(-1920, 40, 1920, 1080);
        var range = new EdgeBandLayout { StartPercent = 20, EndPercent = 80, WidthPercent = 1 };
        Check(EdgeBandLayoutMath.CalculateBounds(area, OverlayPlacementSide.Left, range, 8) == new Rectangle(-1920, 256, 11, 648), "left endpoints map down the edge");
        Check(EdgeBandLayoutMath.CalculateBounds(area, OverlayPlacementSide.Right, range, 8) == new Rectangle(-11, 256, 11, 648), "right endpoints map down the edge");
        Check(EdgeBandLayoutMath.CalculateBounds(area, OverlayPlacementSide.Top, range, 8) == new Rectangle(-1536, 40, 1152, 11), "top endpoints map across the edge");
        foreach (var side in Enum.GetValues<OverlayPlacementSide>())
        {
            var full = EdgeBandLayoutMath.CalculateBounds(area, side, new EdgeBandLayout { StartPercent = 0, EndPercent = 100 }, 8);
            Check(side == OverlayPlacementSide.Top ? full.Left == area.Left && full.Right == area.Right : full.Top == area.Top && full.Bottom == area.Bottom,
                "0–100 must cover the complete work-area edge");
            var tiny = EdgeBandLayoutMath.CalculateBounds(area, side, new EdgeBandLayout { StartPercent = 99.9999, EndPercent = 100 }, 8);
            Check(area.Contains(tiny) && tiny.Width >= 1 && tiny.Height >= 1, "fractional ranges must remain nonempty and inside the work area");
        }
        Check(EdgeBandLayoutMath.CalculateBounds(Rectangle.Empty, OverlayPlacementSide.Left, range, 8).IsEmpty, "empty screens are safe");
    }

    internal static void ValidationAndMigration()
    {
        foreach (var (start, end) in new[] { (-1d, 50d), (0d, 101d), (20d, 20d), (80d, 20d), (double.NaN, 100d), (0d, double.PositiveInfinity) })
        {
            Check(!EdgeBandLayoutMath.IsValidRange(start, end), "invalid intervals must be rejected");
            var safe = EdgeBandLayoutMath.Normalize(new EdgeBandLayout { StartPercent = start, EndPercent = end });
            Check(safe.StartPercent == 0 && safe.EndPercent == 100, "corrupt stored intervals fall back to full coverage");
        }
        Check(EdgeBandLayoutMath.IsValidRange(12.5, 87.5), "fractional endpoints are supported");
        var area = new Rectangle(32, 48, 2560, 1440);
        var settings = new UserSettings
        {
            LeftEdgeBand = new() { LengthPercent = 50, WidthPercent = 1.2, OffsetPixels = 100 },
            RightEdgeBand = new() { LengthPercent = 70, WidthPercent = 0.8, OffsetPixels = -200 },
            TopEdgeBand = new() { LengthPercent = 40, WidthPercent = 0.6, OffsetPixels = 5000 }
        };
        foreach (var (side, old) in new[] { (OverlayPlacementSide.Left, settings.LeftEdgeBand), (OverlayPlacementSide.Right, settings.RightEdgeBand), (OverlayPlacementSide.Top, settings.TopEdgeBand) })
        {
            var previous = EdgeBandLayoutMath.CalculateBounds(area, side, old, settings.EdgeBandWidth);
            var migrated = EdgeBandLayoutMath.GetEditableLayout(settings, side, area);
            Check(migrated.StartPercent is not null && migrated.EndPercent is not null, "old ranges must resolve to editable endpoints");
            Check(previous == EdgeBandLayoutMath.CalculateBounds(area, side, migrated, settings.EdgeBandWidth), "migration must preserve actual old bounds including clamped offsets");
        }
        settings.TopEdgeBand = null; settings.EdgeBandWidth = 12;
        var legacyTop = EdgeBandLayoutMath.GetEditableLayout(settings, OverlayPlacementSide.Top, area);
        Check(legacyTop.StartPercent == 0 && legacyTop.EndPercent == 100
            && EdgeBandLayoutMath.CalculateBounds(area, OverlayPlacementSide.Top, legacyTop, 12).Height == 12, "legacy top thickness must migrate independently from length");
    }

    internal static void Persistence()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Eous-range-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        var store = new JsonSettingsStore(path);
        store.Save(new UserSettings { LeftEdgeBand = new() { StartPercent = 12.5, EndPercent = 87.5 }, RightEdgeBand = new() { StartPercent = 30, EndPercent = 90 }, TopEdgeBand = new() { StartPercent = 10, EndPercent = 60 } });
        var loaded = store.Load();
        Check(store.LastError is null && loaded.LeftEdgeBand!.StartPercent == 12.5 && loaded.LeftEdgeBand.EndPercent == 87.5
            && loaded.RightEdgeBand!.StartPercent == 30 && loaded.TopEdgeBand!.EndPercent == 60, "independent endpoints must persist");
        File.WriteAllText(path, "{\"LeftEdgeBand\":{\"LengthPercent\":50,\"OffsetPixels\":100,\"WidthPercent\":1}}");
        loaded = store.Load();
        Check(loaded.LeftEdgeBand!.StartPercent is null && loaded.LeftEdgeBand.EndPercent is null, "missing endpoints must remain distinguishable from new defaults");
        var area = new Rectangle(0, 0, 1920, 1080);
        var old = EdgeBandLayoutMath.CalculateBounds(area, OverlayPlacementSide.Left, loaded.LeftEdgeBand, 8);
        loaded.LeftEdgeBand = EdgeBandLayoutMath.GetEditableLayout(loaded, OverlayPlacementSide.Left, area);
        store.Save(loaded);
        Check(EdgeBandLayoutMath.CalculateBounds(area, OverlayPlacementSide.Left, store.Load().LeftEdgeBand, 8) == old, "saved migration must retain the old physical range");
        File.WriteAllText(path, "{\"TopEdgeBand\":{\"StartPercent\":80,\"EndPercent\":20}}");
        loaded = store.Load();
        Check(loaded.TopEdgeBand!.StartPercent == 0 && loaded.TopEdgeBand.EndPercent == 100, "invalid JSON interval falls back safely");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
