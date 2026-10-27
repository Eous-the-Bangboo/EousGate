using System.Windows;
using SystemColors = System.Windows.SystemColors;

namespace EousGate;

public partial class AppearancePreview : System.Windows.Controls.UserControl
{
    private readonly DataTemplate _legacyRows;
    private sealed record PreviewRow(string Name, string Glyph, bool Selected);

    public AppearancePreview()
    {
        InitializeComponent();
        _legacyRows = Rows.ItemTemplate;
        Rows.ItemsSource = new[]
        {
            new PreviewRow("记事本", "\uE70B", true), new PreviewRow("Visual Studio Code", "\uE943", false),
            new PreviewRow("Microsoft Word", "\uE8A5", false), new PreviewRow("写字板", "\uE8A4", false)
        };
    }

    internal void Update(UserSettings settings)
    {
        ThemeResources.Apply(Resources, ThemePalette.Create(settings.Theme, SystemColors.WindowColor.R < 128, SystemParameters.HighContrast, settings.SkinId), settings.SkinId);
        Resources["CandidateRowHeight"] = settings.LargeText ? 68d : SkinCatalog.Normalize(settings.SkinId) == SkinCatalog.FocusDarkId ? 52d : 60d;
        var fluent = SkinCatalog.Normalize(settings.SkinId) == SkinCatalog.DefaultId;
        Rows.ItemTemplate = fluent ? (DataTemplate)FindResource("FluentPreviewRow") : _legacyRows;
        PreviewHeader.Margin = fluent ? new Thickness(16, 0, 16, 0) : new Thickness(16, 16, 16, 14);
        PreviewHeader.Height = fluent ? 80 : double.NaN;
        PreviewHeader.ColumnDefinitions[0].Width = new GridLength(fluent ? 48 : 38);
        PreviewBadge.Width = fluent ? 36 : 30; PreviewBadge.Height = fluent ? 36 : 34;
        PreviewHeading.FontSize = fluent ? 16 : 15;
        PreviewLabels.VerticalAlignment = fluent ? VerticalAlignment.Center : VerticalAlignment.Stretch;
        PreviewLabels.Margin = fluent ? new Thickness(0, 0, 40, 0) : new Thickness(0);
        PreviewContext.Text = fluent ? "2 个 TXT 文件 · 本机应用" : "2 个 TXT 文件";
        PreviewContext.FontSize = fluent ? 11 : 12;
        PreviewContext.Margin = fluent ? new Thickness(0, 4, 0, 0) : new Thickness(0, 3, 0, 0);
        PreviewContext.TextTrimming = fluent ? TextTrimming.CharacterEllipsis : TextTrimming.None;
        PreviewCount.Visibility = fluent ? Visibility.Visible : Visibility.Collapsed;
        Rows.Margin = fluent ? new Thickness(12, 12, 12, 8) : new Thickness(8, 0, 8, 8);
        PreviewFooter.Height = fluent ? 44 : double.NaN;
    }
}
