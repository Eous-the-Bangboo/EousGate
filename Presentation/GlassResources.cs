using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using ColorConverter = System.Windows.Media.ColorConverter;
using SystemColors = System.Windows.SystemColors;

namespace EousGate;

// Material is painted independently of the foreground: labels never inherit its alpha or blur.
internal static class GlassResources
{
    internal static void Apply(ResourceDictionary resources, ThemePalette palette, string? skin)
    {
        var highContrast = palette.Background == "SystemWindow";
        var dark = !highContrast && ((Color)ColorConverter.ConvertFromString(palette.Background)).R < 128;
        var flat = highContrast || SkinCatalog.Normalize(skin) == SkinCatalog.FocusDarkId;
        var paper = flat ? (Brush)resources["PanelElevatedSurface"] : Surface(dark, 250);
        resources["GlassWindowSurface"] = flat ? paper : Surface(dark, 148);
        resources["GlassPanelSurface"] = paper;
        resources["GlassControlSurface"] = flat ? (Brush)resources["PanelSurfaceSoft"] : Solid(dark ? "#503E5145" : "#B8FFFEF9");
        resources["GlassSidebarSurface"] = flat ? (Brush)resources["PanelBackground"] : new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(dark ? "#303C5F4C" : "#408FAA88"), Colors.Transparent, 65);
        resources["GlassFooterSurface"] = flat ? paper : Solid(dark ? "#18202B26" : "#38FFFEF9");
        resources["GlassRim"] = flat ? (Brush)resources["PanelBorder"] : new LinearGradientBrush(
            new GradientStopCollection {
                new(dark ? Color.FromArgb(105, 226, 240, 229) : Color.FromArgb(230, 255, 255, 251), 0),
                new(dark ? Color.FromArgb(25, 226, 240, 229) : Color.FromArgb(65, 255, 255, 251), .48),
                new(dark ? Color.FromArgb(65, 194, 219, 202) : Color.FromArgb(90, 92, 117, 97), 1)
            }, new Point(0, 0), new Point(.7, 1));
        resources["GlassSelectedSurface"] = highContrast ? (Brush)resources["PanelSelection"] : Solid(dark ? "#A126352D" : "#BCFFFEF9");
        resources["GlassSelectedForeground"] = highContrast ? SystemColors.HighlightTextBrush : (Brush)resources["PanelSuccess"];
        resources["GlassTransparent"] = System.Windows.Media.Brushes.Transparent;
    }

    private static SolidColorBrush Solid(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze(); return brush;
    }

    private static DrawingBrush Surface(bool dark, byte alpha)
    {
        var group = new DrawingGroup();
        var rect = new RectangleGeometry(new Rect(0, 0, 1000, 760));
        var paper = dark ? Color.FromArgb(alpha, 27, 39, 32) : Color.FromArgb(alpha, 255, 254, 249);
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(paper), null, rect));
        void Glow(string color, Point center, double radius)
        {
            var tint = (Color)ColorConverter.ConvertFromString(color);
            var radial = new RadialGradientBrush(tint, Color.FromArgb(0, tint.R, tint.G, tint.B))
                { Center = center, GradientOrigin = center, RadiusX = radius, RadiusY = radius };
            group.Children.Add(new GeometryDrawing(radial, null, rect));
        }
        Glow(dark ? "#304E7456" : "#36B2C4AD", new Point(.02, .58), .8);
        Glow(dark ? "#284F391F" : "#38EFD9B5", new Point(.96, .02), .85);
        Glow(dark ? "#30315F5B" : "#3895B7B5", new Point(.88, 1), .8);
        var brush = new DrawingBrush(group) { Stretch = Stretch.Fill };
        brush.Freeze(); return brush;
    }
}
