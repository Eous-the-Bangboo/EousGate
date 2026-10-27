using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CheckBox = System.Windows.Controls.CheckBox;
using Button = System.Windows.Controls.Button;

namespace EousGate;

public partial class SettingsWindow
{
    private readonly List<CheckBox> _candidateRows = [];
    private bool _updatingFileBrowser;
    private bool _orderEdited;
    private bool _visibilityEdited;
    private bool _showAllExtensions;

    private void ChooseExtensionClick(object sender, RoutedEventArgs e)
    {
        var opening = ExtensionBrowser.Visibility != Visibility.Visible;
        ExtensionBrowser.Visibility = opening ? Visibility.Visible : Visibility.Collapsed;
        ExtensionArrow.RenderTransform = new System.Windows.Media.RotateTransform(opening ? 180 : 0);
        System.Windows.Automation.AutomationProperties.SetHelpText(ChooseExtensionButton, opening ? "分类与后缀搜索已展开" : "分类与后缀搜索已收起");
        if (opening) Extension.Focus();
        else ChooseExtensionButton.Focus();
    }

    private void InitializeFileTypeBrowser()
    {
        FileTypeCategory[] categories =
        [
            new("all", "全部常见", []),
            .. FileTypeCatalog.All,
            new("configured", "已自定义", [])
        ];
        FileCategories.ItemsSource = categories;
        FileCategories.SelectedValue = "all";
        LoadFileType(".mp4", false);
    }

    private void FileCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingFileBrowser || _settings is null) return;
        Extension.Text = "";
        _showAllExtensions = false;
        RefreshExtensionSuggestions();
    }

    private void ExtensionSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFileBrowser && _settings is not null) RefreshExtensionSuggestions();
    }

    private void RefreshExtensionSuggestions()
    {
        _updatingFileBrowser = true;
        var category = FileCategories.SelectedValue?.ToString() ?? "all";
        var extensions = FileTypeCatalog.Browse(category, _settings.FileTypes.Keys, Extension.Text);
        var canExpand = category == "all" && string.IsNullOrWhiteSpace(Extension.Text) && extensions.Count > 18;
        MoreExtensions.Visibility = canExpand ? Visibility.Visible : Visibility.Collapsed;
        MoreExtensions.Content = _showAllExtensions ? "收起后缀" : $"展开全部 {extensions.Count} 种后缀";
        ExtensionSuggestions.ItemsSource = canExpand && !_showAllExtensions ? extensions.Take(18).ToArray() : extensions;
        ExtensionSuggestions.SelectedItem = extensions.FirstOrDefault(ext => ext == _loadedExtension);
        ExtensionEmptyState.Visibility = extensions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResetCategoryButton.IsEnabled = FileTypeCatalog.All.Any(c => c.Id == category);
        ResetCategoryButton.Content = FileCategories.SelectedItem is FileTypeCategory selected && ResetCategoryButton.IsEnabled
            ? $"恢复整类{selected.Name}推荐" : "整类恢复推荐";
        _updatingFileBrowser = false;
    }

    private void MoreExtensionsClick(object sender, RoutedEventArgs e)
    {
        _showAllExtensions = !_showAllExtensions;
        RefreshExtensionSuggestions();
    }

    private void ExtensionSuggestionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingFileBrowser || ExtensionSuggestions.SelectedItem is not string extension) return;
        if (!LoadFileType(extension))
        {
            _updatingFileBrowser = true;
            ExtensionSuggestions.SelectedItem = _loadedExtension;
            _updatingFileBrowser = false;
        }
        else
        {
            ExtensionBrowser.Visibility = Visibility.Collapsed;
            ExtensionArrow.RenderTransform = System.Windows.Media.Transform.Identity;
            ChooseExtensionButton.Focus();
            FileTypeScroll.ScrollToTop();
        }
    }

    private void ExtensionKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        LoadClick(sender, e); e.Handled = true;
    }

    internal bool LoadFileType(string extension, bool captureCurrent = true)
    {
        var ext = FileTypeCatalog.NormalizeExtension(extension);
        if (ext.Length == 0 || (captureCurrent && !TryCaptureCurrentFileType())) return false;
        _loadedExtension = ext;
        _loadedCandidates = _discovery.GetCandidatesForExtension(ext, false);
        var preference = _settings.FileTypes.GetValueOrDefault(ext);
        Apps.Items.Clear(); _candidateRows.Clear();
        foreach (var candidate in _loadedCandidates) _candidateRows.Add(CreateCandidateRow(candidate, preference));
        _orderEdited = _visibilityEdited = false;
        LoadedLabel.Text = ext;
        LoadedCategory.Text = FileTypeCatalog.Find(ext)?.Name ?? "自定义";
        var selectedCount = _candidateRows.Count(row => row.IsChecked == true);
        RecommendationSummary.Text = $"已启用 {selectedCount} 个软件";
        RecommendationSummary.ToolTip = $"共 {_loadedCandidates.Count} 个关联软件；系统默认与类型匹配优先。";
        AddAppButton.IsEnabled = true; RemoveCustomButton.IsEnabled = false;
        PerTypeCountEnabled.IsChecked = preference?.DisplayCount is not null;
        PerTypeCount.Text = preference?.DisplayCount?.ToString() ?? "";
        FileTypeStatus.Text = "";
        RefreshCandidateRows(); RefreshExtensionSuggestions();
        return true;
    }

    private void RefreshCandidateRows()
    {
        Apps.Items.Clear();
        foreach (var row in _candidateRows)
        {
            if (row.Tag is not AppCandidate candidate) continue;
            if (ShowOtherApps.IsChecked == true || row.IsChecked == true
                || _discovery.IsSuggestedForExtension(_loadedExtension!, candidate)) Apps.Items.Add(row);
        }
        NoAppsLabel.Visibility = Apps.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        for (var i = 0; i < Apps.Items.Count; i++)
            if (Apps.Items[i] is CheckBox { Content: Grid grid })
                foreach (var button in grid.Children.OfType<StackPanel>().SelectMany(panel => panel.Children.OfType<Button>()))
                    button.IsEnabled = (int)button.Tag < 0 ? i > 0 : i < Apps.Items.Count - 1;
    }

    private void ShowOtherAppsChanged(object sender, RoutedEventArgs e)
    {
        if (_loadedExtension is not null) RefreshCandidateRows();
    }

    private void CandidateVisibilityChanged()
    {
        _visibilityEdited = true;
        RecommendationSummary.Text = $"已启用 {_candidateRows.Count(row => row.IsChecked == true)} 个软件";
    }

    private bool TryCaptureCurrentFileType()
    {
        int? count = null;
        if (_loadedExtension is null) return true;
        if (PerTypeCountEnabled.IsChecked == true)
        {
            if (!int.TryParse(PerTypeCount.Text, out var parsed) || parsed is < 1 or > 12)
            {
                FileTypeStatus.Text = $"请先将 {_loadedExtension} 的显示数量设为 1–12，再切换后缀。";
                PerTypeCount.Focus(); return false;
            }
            count = parsed;
        }
        CaptureCurrentExtensionPreference(count);
        return true;
    }

    private void ResetTypeRecommendationClick(object sender, RoutedEventArgs e)
    {
        if (_loadedExtension is null || !TryCaptureCurrentFileType()) return;
        ResetRecommendation(_loadedExtension);
        LoadFileType(_loadedExtension, false);
        FileTypeStatus.Text = "已恢复此后缀的智能推荐；保留添加的应用和显示数量。点击应用或保存后生效。";
    }

    private void ResetCategoryRecommendationClick(object sender, RoutedEventArgs e)
    {
        if (FileCategories.SelectedItem is not FileTypeCategory category
            || !FileTypeCatalog.All.Any(c => c.Id == category.Id) || !TryCaptureCurrentFileType()) return;
        foreach (var extension in category.Extensions) ResetRecommendation(extension);
        if (_loadedExtension is not null) LoadFileType(_loadedExtension, false);
        FileTypeStatus.Text = $"已恢复{category.Name}类 {category.Extensions.Count} 个后缀的智能推荐，保留自定义应用和数量。点击应用或保存后生效。";
    }

    private void ResetRecommendation(string extension)
    {
        if (!_settings.FileTypes.TryGetValue(extension, out var preference)) return;
        preference.CandidateOrder.Clear(); preference.HiddenCandidateIds.Clear();
        if (preference.CustomApps.Count == 0 && preference.DisplayCount is null) _settings.FileTypes.Remove(extension);
    }
}
