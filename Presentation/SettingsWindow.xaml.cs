using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using EousGate.Infrastructure;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Forms = System.Windows.Forms;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;
using SystemColors = System.Windows.SystemColors;

namespace EousGate;

public partial class SettingsWindow : Window
{
    internal event EventHandler? SectionChanged;
    internal int CurrentSection => SettingsTabs.SelectedIndex;
    internal string DrawerTitle => CurrentSection switch { 1 or 3 => "文件快开", 2 => "网页快开", 4 => "关于 Eous", _ => "通用设置" };
    internal void SelectSection(int index)
    {
        foreach (TabItem item in SettingsTabs.Items) item.Visibility = Visibility.Visible;
        SettingsTabs.SelectedIndex = index;
        if (Resources.Contains("WorkbenchDialogMode")) UseWorkbenchLayout();
        SectionChanged?.Invoke(this, EventArgs.Empty);
    }
    internal void UseWorkbenchLayout()
    {
        BrandHeader.Visibility = Visibility.Collapsed; BrandHeaderRow.Height = new GridLength(0);
        SettingsFooterRow.Height = new GridLength(92); SettingsTabs.Margin = new Thickness(0);
        SettingsFooter.Visibility = CurrentSection == 4 ? Visibility.Collapsed : Visibility.Visible;
        if (CurrentSection == 4) SettingsFooterRow.Height = new GridLength(0);
        var file = CurrentSection is 1 or 3;
        SettingsTabs.Template = (ControlTemplate)FindResource(file ? "WorkbenchFileTabs" : "WorkbenchContentTabs");
        SettingsTabs.TabStripPlacement = Dock.Top;
        SettingsTabs.ItemContainerStyle = (Style)FindResource("WorkbenchTabItem");
        for (var i = 0; i < SettingsTabs.Items.Count; i++)
        {
            var tab = (TabItem)SettingsTabs.Items[i];
            tab.Visibility = (file ? i is 1 or 3 : i == CurrentSection) ? Visibility.Visible : Visibility.Collapsed;
            if (tab.Content is ScrollViewer { Content: StackPanel stack } && stack.Children.OfType<TextBlock>().FirstOrDefault() is { } title)
                title.Visibility = Visibility.Collapsed;
            if (tab.Content is ScrollViewer scroll) scroll.Margin = new Thickness(18, 0, 12, 0);
            else if (tab.Content is FrameworkElement page) page.Margin = new Thickness(24, 12, 24, 18);
        }
        ((TabItem)SettingsTabs.Items[1]).Header = "触发与面板";
        ((TabItem)SettingsTabs.Items[3]).Header = "打开方式";
        AppearanceTitle.Visibility = AppearanceHint.Visibility = FluentSkinCard.Visibility = LegacySkinTitle.Visibility = Skins.Visibility = Visibility.Collapsed;
        Theme.IsEnabled = true;
        AuxiliaryAppearance.Visibility = PreviewCard.Visibility = Visibility.Visible;
        if (PreviewCard.Parent is Grid previewHost) { previewHost.Children.Remove(PreviewCard); AuxiliaryAppearanceContent.Children.Add(PreviewCard); }
        PreviewCard.Margin = new Thickness(0, 12, 0, 0);
        PreviewGap.Width = PreviewWidth.Width = new GridLength(0);
        ThemeHint.Text = "主题在应用后更新。";
        AppearanceGrid.Margin = new Thickness(24, 10, 24, 20);
        ThemeCard.Padding = ReadingCard.Padding = new Thickness(0);
        ThemeCard.BorderThickness = ReadingCard.BorderThickness = new Thickness(0);
        ThemeCard.Background = ReadingCard.Background = System.Windows.Media.Brushes.Transparent;
        if (DiagnosticsSection.Parent is StackPanel host && host != AppearanceControls)
        {
            host.Children.Remove(DiagnosticsSection); AppearanceControls.Children.Add(DiagnosticsSection);
            DiagnosticsSection.Margin = new Thickness(0, 24, 0, 0); DiagnosticsSection.Padding = new Thickness(0);
            DiagnosticsSection.BorderThickness = new Thickness(0); DiagnosticsSection.Background = System.Windows.Media.Brushes.Transparent;
        }
        SettingsFooterLayout.RowDefinitions.Clear();
        SettingsFooterLayout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        SettingsFooterLayout.RowDefinitions.Add(new() { Height = new GridLength(36) });
        SettingsFooterLayout.Margin = new Thickness(24, 8, 24, 8);
        foreach (UIElement child in SettingsFooterLayout.Children) Grid.SetRow(child, child == ApplyStatusText ? 0 : 1);
        Grid.SetColumn(ApplyStatusText, 0); Grid.SetColumnSpan(ApplyStatusText, 6);
        ApplyStatusText.Margin = new Thickness(0, 0, 0, 6);
    }
    private readonly UserSettings _settings;
    private readonly ISettingsStore _store;
    private readonly AppDiscovery _discovery;
    private readonly Action<UserSettings> _applied;
    private readonly Action<UserSettings>? _beforeSave;
    private string? _loadedExtension;
    private IReadOnlyList<AppCandidate> _loadedCandidates = [];
    private string _selectedColor;
    private readonly FluentMotion _motion = new();
    private readonly Style _legacyButtons;
    private readonly Style _legacyChecks;
    private bool _fluent;
    private string _savedControls = "", _savedFileTypes = "";
    private UserSettings _savedDraft = new();
    private string FileTypesFingerprint() => System.Text.Json.JsonSerializer.Serialize(_settings.FileTypes.Where(p => p.Value.CandidateOrder.Count > 0 || p.Value.HiddenCandidateIds.Count > 0 || p.Value.CustomApps.Count > 0 || p.Value.DisplayCount is not null).ToDictionary(p => p.Key, p => p.Value));
    private string ControlFingerprint() => string.Join("\u001f", new object?[] {
        BandOpacity.Text, _selectedColor, CandidateCount.Text, TriggerDelay.Text, AutoDismissDelay.Text,
        ClipboardEnabled.IsChecked, ClipboardAutoOpen.IsChecked, ClipboardCopyCount.Text, ClipboardDuration.Text, ClipboardSide.SelectedIndex,
        PanelWidth.Text, PanelMaxHeight.Text, CandidateGap.Text, AnimationDuration.Text,
        LeftBandStart.Text, LeftBandWidth.Text, LeftBandEnd.Text, RightBandStart.Text, RightBandWidth.Text, RightBandEnd.Text,
        TopBandStart.Text, TopBandWidth.Text, TopBandEnd.Text, EdgeBandLeft.IsChecked, EdgeBandRight.IsChecked, EdgeBandTop.IsChecked,
        Theme.SelectedIndex, Skins.SelectedValue, LargeText.IsChecked, EdgePulseEnabled.IsChecked, Diagnostics.IsChecked });
    private void AcceptSavedDraft()
    {
        _savedControls = ControlFingerprint();
        _savedFileTypes = FileTypesFingerprint();
        _savedDraft = _settings.Clone();
    }
    internal bool HasUnsavedEdits
    {
        get
        {
            if (ControlFingerprint() != _savedControls || FileTypesFingerprint() != _savedFileTypes || _orderEdited || _visibilityEdited) return true;
            var previous = _loadedExtension is null ? null : _savedDraft.FileTypes.GetValueOrDefault(_loadedExtension)?.DisplayCount;
            return PerTypeCountEnabled.IsChecked == true ? !int.TryParse(PerTypeCount.Text, out var current) || current != previous : previous is not null;
        }
    }
    internal bool ApplyBeforeClose() { TryApplySettings(false); return !HasUnsavedEdits; }

    public SettingsWindow(UserSettings settings, ISettingsStore store, AppDiscovery discovery, Action<UserSettings> applied, Action<UserSettings>? beforeSave = null)
    {
        InitializeComponent(); _settings = settings.Clone(); _store = store; _discovery = new AppDiscovery(() => _settings); _applied = applied; _selectedColor = IsValidColor(_settings.EdgeBandColor) ? _settings.EdgeBandColor : new UserSettings().EdgeBandColor;
        _legacyButtons = (Style)Resources[typeof(System.Windows.Controls.Button)];
        _legacyChecks = (Style)Resources[typeof(System.Windows.Controls.CheckBox)];
        _beforeSave = beforeSave;
        AppVersion.Text = $"EousAssistant {FileVersionInfo.GetVersionInfo(typeof(SettingsWindow).Assembly.Location).ProductVersion?.Split('+')[0]}";
        MicaWindowHelper.Enable(this, settings.SkinId);
        Skins.ItemsSource = SkinCatalog.All;
        Skins.SelectedValue = SkinCatalog.Normalize(settings.SkinId);
        BandOpacity.Text = settings.EdgeBandOpacity.ToString(CultureInfo.InvariantCulture);
        BandColorPreview.Background = ToBrush(_selectedColor); BuildColorSwatches();
        CandidateCount.Text = settings.CandidateCount.ToString(CultureInfo.InvariantCulture);
        TriggerDelay.Text = settings.TriggerDelayMs.ToString(CultureInfo.InvariantCulture);
        AutoDismissDelay.Text = settings.AutoDismissDelayMs.ToString(CultureInfo.InvariantCulture);
        ClipboardEnabled.IsChecked = settings.ClipboardLinksEnabled;
        ClipboardAutoOpen.IsChecked = settings.ClipboardAutoOpenEnabled;
        ClipboardCopyCount.Text = settings.ClipboardAutoOpenCopyCount.ToString(CultureInfo.InvariantCulture);
        ClipboardDuration.Text = settings.ClipboardPopupDurationSeconds.ToString(CultureInfo.InvariantCulture);
        ClipboardSide.SelectedIndex = settings.ClipboardPopupSide == "Left" ? 1 : 0;
        PanelWidth.Text = settings.PanelWidth.ToString(CultureInfo.InvariantCulture);
        PanelMaxHeight.Text = settings.PanelMaxHeight.ToString(CultureInfo.InvariantCulture);
        CandidateGap.Text = settings.CandidateGap.ToString(CultureInfo.InvariantCulture);
        AnimationDuration.Text = settings.AnimationDurationMs.ToString(CultureInfo.InvariantCulture);
        SetEdgeBandLayoutEditors(settings);
        SetEdgeBandPositionChecks(settings.EdgeBandPosition);
        Theme.SelectedIndex = settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        LargeText.IsChecked = settings.LargeText; EdgePulseEnabled.IsChecked = settings.EdgePulseEnabled; Diagnostics.IsChecked = settings.DiagnosticsEnabled;
        Loaded += (_, _) =>
        {
            if (Resources.Contains("WorkbenchDialogMode"))
            {
                BeginAnimation(Window.OpacityProperty, null); Opacity = 1;
                WindowTranslation.BeginAnimation(TranslateTransform.YProperty, null); WindowTranslation.Y = 0;
                return;
            }
            if (_fluent) AnimateAppearance();
            else
            {
                BeginAnimation(Window.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                WindowTranslation.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            }
        };
        Closed += (_, _) => _motion.Dispose();
        AppearanceGrid.SizeChanged += (_, _) => UpdateAppearanceWidth();
        IsVisibleChanged += (_, _) => { if (!IsVisible) _motion.StopAll(); };
        PerTypeCountEnabled.Checked += (_, _) => PerTypeCount.IsEnabled = true;
        PerTypeCountEnabled.Unchecked += (_, _) => PerTypeCount.IsEnabled = false;
        Skins.SelectionChanged += (_, _) => RefreshAppearancePreview();
        Theme.SelectionChanged += (_, _) => RefreshAppearancePreview();
        LargeText.Checked += (_, _) => RefreshAppearancePreview();
        LargeText.Unchecked += (_, _) => RefreshAppearancePreview();
        RefreshAppearancePreview();
        InitializeFileTypeBrowser();
        AcceptSavedDraft();
    }

    private void RefreshAppearancePreview()
    {
        var draft = _settings.Clone();
        draft.SkinId = SkinCatalog.Normalize(Skins.SelectedValue?.ToString());
        draft.Theme = (Theme.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";
        draft.LargeText = LargeText.IsChecked == true;
        var fixedDark = draft.SkinId == SkinCatalog.FocusDarkId;
        Theme.IsEnabled = !fixedDark;
        ThemeHint.Text = fixedDark ? "B 固定使用深色；切回 A 后保留原来的主题选择。" : "A 支持跟随系统，也可手动选择浅色或深色。";
        // Preview locally: cancel never changes the running tools or the persisted settings.
        ThemeResources.Apply(Resources, ThemePalette.Create(draft.Theme, SystemColors.WindowColor.R < 128, SystemParameters.HighContrast, draft.SkinId), draft.SkinId);
        LivePreview.Update(draft);
        ApplyAppearanceLayout(!fixedDark);
        WorkbenchTheme.Apply(Resources, draft.Theme);
        Resources["SettingsNavBackground"] = FindResource("PanelBackground");
        var labels = new Style(typeof(System.Windows.Controls.Label));
        labels.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, FindResource("PanelForeground")));
        Resources[typeof(System.Windows.Controls.Label)] = labels;
        Resources[typeof(System.Windows.Controls.CheckBox)] = FindResource("WorkbenchCheckBox");
        Resources[typeof(System.Windows.Controls.Button)] = FindResource("WorkbenchButton");
        SaveButton.Style = (Style)FindResource("WorkbenchPrimary");
        AppearanceTitle.Text = "打开面板外观";
        AppearanceHint.Text = "调整主题、阅读与动效。";
        PreviewNote.Text = "应用或保存后更新正在使用的界面。";
        if (Resources.Contains("WorkbenchDialogMode")) UseWorkbenchLayout();
        MicaWindowHelper.Apply(this, draft.SkinId);
    }

    private void ApplyAppearanceLayout(bool fluent)
    {
        _motion.StopAll();
        _fluent = fluent;
        BrandHeaderRow.Height = new GridLength(fluent ? 0 : 68);
        BrandHeader.Visibility = fluent ? Visibility.Collapsed : Visibility.Visible;
        SettingsFooterRow.Height = new GridLength(fluent ? 56 : 66);
        SettingsFooterLayout.Margin = new Thickness(24, fluent ? 10 : 12, 24, fluent ? 10 : 12);
        SettingsFooter.SetResourceReference(Border.BackgroundProperty, fluent ? "PanelElevatedSurface" : "PanelSurfaceSoft");
        SettingsTabs.Margin = fluent ? new Thickness(0, 12, 18, 0) : new Thickness(18, 12, 18, 0);
        Resources["SettingsNavWidth"] = new GridLength(fluent ? 188 : 170);
        Resources["SettingsNavBackground"] = FindResource(fluent ? "PanelBackground" : "PanelSurfaceSoft");
        Resources["SettingsContentMargin"] = new Thickness(fluent ? 24 : 20, 0, 0, 0);
        SettingsTabs.ItemContainerStyle = (Style)FindResource(fluent ? "FluentTabItem" : "TabItemStyle");
        AppearanceGrid.Margin = fluent ? new Thickness(0, 12, 6, 24) : new Thickness(4, 12, 8, 16);
        PreviewGap.Width = new GridLength(fluent ? 24 : 16);
        PreviewWidth.Width = new GridLength(fluent ? 336 : 260);
        AppearanceControls.Margin = fluent ? new Thickness(0, 0, 0, 8) : new Thickness(0, 0, 8, 8);
        AppearanceTitle.FontSize = fluent ? 24 : 20;
        AppearanceHint.Text = fluent ? "选择你习惯的外观，预览后再应用。" : "选择皮肤与主题，右侧即时预览。";
        AppearanceHint.Margin = new Thickness(0, 5, 0, fluent ? 24 : 20);
        LegacySkinTitle.Visibility = Skins.Visibility = fluent ? Visibility.Collapsed : Visibility.Visible;
        FluentSkinCard.Visibility = ReadingCard.Visibility = ThemeDivider.Visibility = MotionHint.Visibility = fluent ? Visibility.Visible : Visibility.Collapsed;
        ThemeCard.Margin = fluent ? new Thickness(0, 16, 0, 0) : new Thickness(0, 16, 8, 0);
        PreviewCard.Padding = new Thickness(fluent ? 16 : 12);
        PreviewCard.Margin = fluent ? new Thickness(0, 80, 0, 0) : new Thickness(0);
        PreviewNote.Text = fluent ? "点击应用或保存后，才更新运行中的界面。" : "皮肤同时用于助手、设置与打开面板。点击应用或保存后生效。";
        PreviewCaption.Text = fluent ? "2 个 TXT 文件 · 效果示意" : "2 个 TXT 文件 · 示意预览";
        ThemeOptions.Children.Remove(ReadingOptions); ReadingHost.Children.Remove(ReadingOptions);
        if (fluent) ReadingHost.Children.Add(ReadingOptions); else ThemeOptions.Children.Add(ReadingOptions);
        LargeText.Margin = fluent ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 0, 6);
        Resources[typeof(System.Windows.Controls.Button)] = fluent ? FindResource("FluentAButton") : _legacyButtons;
        Resources[typeof(System.Windows.Controls.CheckBox)] = fluent ? FindResource("FluentACheckBox") : _legacyChecks;
        SaveButton.Style = (Style)FindResource(fluent ? "FluentAPrimary" : "PrimaryActionButtonStyle");
        CancelButton.Style = (Style)FindResource(fluent ? "FluentAGhost" : "SecondaryActionButtonStyle");
        ApplyButton.Style = fluent ? (Style)FindResource("FluentAButton") : _legacyButtons;
        if (fluent)
        {
            BeginAnimation(Window.OpacityProperty, null); Opacity = 1;
            WindowTranslation.BeginAnimation(TranslateTransform.YProperty, null); WindowTranslation.Y = 0;
        }
        else WindowTranslation.Y = IsLoaded ? 0 : 10;
        UpdateAppearanceWidth();
    }

    private void UpdateAppearanceWidth()
    {
        if (Resources.Contains("WorkbenchDialogMode")) { PreviewGap.Width = PreviewWidth.Width = new GridLength(0); return; }
        if (_fluent) PreviewWidth.Width = new GridLength(AppearanceGrid.ActualWidth is > 0 and < 800 ? 280 : 336);
    }

    private void AnimateAppearance()
    {
        var duration = int.TryParse(AnimationDuration.Text, out var value) ? value : _settings.AnimationDurationMs;
        _motion.Enter(FluentSkinCard, duration, 40, 200);
        _motion.Enter(ThemeCard, duration, 80, 200);
        _motion.Enter(ReadingCard, duration, 120, 200);
        _motion.Enter(PreviewCard, duration, 80, 200);
    }

    private void SetEdgeBandLayoutEditors(UserSettings settings)
    {
        var area = Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);
        SetEdgeBandLayoutEditors(EdgeBandLayoutMath.GetEditableLayout(settings, OverlayPlacementSide.Left, area), LeftBandStart, LeftBandWidth, LeftBandEnd);
        SetEdgeBandLayoutEditors(EdgeBandLayoutMath.GetEditableLayout(settings, OverlayPlacementSide.Right, area), RightBandStart, RightBandWidth, RightBandEnd);
        SetEdgeBandLayoutEditors(EdgeBandLayoutMath.GetEditableLayout(settings, OverlayPlacementSide.Top, area), TopBandStart, TopBandWidth, TopBandEnd);
    }

    private static void SetEdgeBandLayoutEditors(EdgeBandLayout layout, System.Windows.Controls.TextBox start, System.Windows.Controls.TextBox width, System.Windows.Controls.TextBox end)
    {
        start.Text = layout.StartPercent!.Value.ToString("0.########", CultureInfo.InvariantCulture);
        width.Text = layout.WidthPercent.ToString("0.##", CultureInfo.InvariantCulture);
        end.Text = layout.EndPercent!.Value.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private bool TryReadEdgeBandLayout(string side, System.Windows.Controls.TextBox start, System.Windows.Controls.TextBox width, System.Windows.Controls.TextBox end, out EdgeBandLayout layout)
    {
        layout = new EdgeBandLayout();
        if (!double.TryParse(start.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var startPercent)
            || !double.TryParse(end.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var endPercent)
            || !EdgeBandLayoutMath.IsValidRange(startPercent, endPercent))
        {
            ShowEdgeRangeError($"{side}接收条：起点和终点请输入 0 到 100，且起点须小于终点。", start);
            return false;
        }
        if (!double.TryParse(width.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var widthPercent)
            || !double.IsFinite(widthPercent) || widthPercent < EdgeBandLayoutMath.MinWidthPercent || widthPercent > EdgeBandLayoutMath.MaxWidthPercent)
        {
            ShowEdgeRangeError($"{side}接收条宽度请输入 0.1 到 5。", width);
            return false;
        }
        layout = new EdgeBandLayout { StartPercent = startPercent, EndPercent = endPercent, WidthPercent = widthPercent };
        return true;
    }

    private void ShowEdgeRangeError(string message, System.Windows.Controls.TextBox input)
    {
        SelectSection(1);
        EdgeRangeError.Text = message;
        EdgeRangeError.Visibility = Visibility.Visible;
        ApplyStatusText.Text = message;
        ApplyStatusText.SetResourceReference(TextBlock.ForegroundProperty, "PanelDanger");
        input.Focus();
        EdgeRangeError.BringIntoView();
    }

    private void LoadClick(object sender, RoutedEventArgs e)
    {
        var ext = FileTypeCatalog.NormalizeExtension(Extension.Text);
        if (ext.Length == 0) { FileTypeStatus.Text = "请输入一个有效后缀，例如 .mp4 或 .txt。"; Extension.Focus(); return; }
        LoadFileType(ext);
    }

    private System.Windows.Controls.CheckBox CreateCandidateRow(AppCandidate candidate, FileTypePreference? preference)
    {
        var isCustom = preference?.CustomApps.Any(app => string.Equals(AppDiscovery.NormalizeExecutablePath(app.ExecutablePath), candidate.ExecutablePath, StringComparison.OrdinalIgnoreCase)) == true;
        var content = new Grid { VerticalAlignment = VerticalAlignment.Center };
        content.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        name.Children.Add(new TextBlock { Text = candidate.DisplayName, FontSize = 14, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(name);
        var reason = _discovery.GetRecommendationLabel(_loadedExtension!, candidate);
        var label = new TextBlock { Text = isCustom ? "自定义" : reason, FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0), Foreground = (System.Windows.Media.Brush)FindResource("PanelMutedForeground") };
        Grid.SetColumn(label, 1); content.Children.Add(label);
        if (!candidate.IsAvailable)
        {
            name.Children.Add(new TextBlock { Text = "应用已不可用", Foreground = (System.Windows.Media.Brush)FindResource("PanelDanger"), FontSize = 13 });
        }
        var row = new System.Windows.Controls.CheckBox
        {
            Content = content,
            ToolTip = candidate.IsAvailable ? $"{candidate.ExecutablePath}\n{candidate.Arguments}" : $"应用已不可用\n{candidate.ExecutablePath}",
            Tag = candidate,
            IsChecked = candidate.IsAvailable && preference?.HiddenCandidateIds.Contains(candidate.Id, StringComparer.OrdinalIgnoreCase) != true
                && (preference?.CandidateOrder.Contains(candidate.Id, StringComparer.OrdinalIgnoreCase) == true || _discovery.IsSuggestedForExtension(_loadedExtension!, candidate)),
            IsEnabled = candidate.IsAvailable,
            Margin = new Thickness(2), MinHeight = 45,
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch
        };
        var controls = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var delta in new[] { -1, 1 })
        {
            var button = new System.Windows.Controls.Button { Content = delta < 0 ? "↑" : "↓", Tag = delta,
                Style = (Style)FindResource("WorkbenchIconButton"), FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Width = 30, Height = 32, ToolTip = (delta < 0 ? "上移" : "下移") + candidate.DisplayName };
            System.Windows.Automation.AutomationProperties.SetName(button, button.ToolTip.ToString());
            button.Click += (_, e) => { Apps.SelectedItem = row; MoveSelected(delta); e.Handled = true; };
            controls.Children.Add(button);
        }
        Grid.SetColumn(controls, 2); content.Children.Add(controls);
        row.PreviewMouseDown += (_, _) => Apps.SelectedItem = row;
        row.GotKeyboardFocus += (_, _) => Apps.SelectedItem = row;
        System.Windows.Automation.AutomationProperties.SetName(row, $"{candidate.DisplayName}，{reason}");
        row.Checked += (_, _) => CandidateVisibilityChanged();
        row.Unchecked += (_, _) => CandidateVisibilityChanged();
        return row;
    }

    private void AppsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RemoveCustomButton.IsEnabled = Apps.SelectedItem is System.Windows.Controls.CheckBox { Tag: AppCandidate candidate }
            && IsCustomCandidate(candidate);
    }

    private bool IsCustomCandidate(AppCandidate candidate)
        => _loadedExtension is not null
            && _settings.FileTypes.TryGetValue(_loadedExtension, out var preference)
            && preference.CustomApps.Any(app => string.Equals(AppDiscovery.NormalizeExecutablePath(app.ExecutablePath), candidate.ExecutablePath, StringComparison.OrdinalIgnoreCase));

    private FileTypePreference CurrentPreference()
    {
        if (_loadedExtension is null) throw new InvalidOperationException("请先加载文件类型。");
        if (!_settings.FileTypes.TryGetValue(_loadedExtension, out var preference))
        {
            preference = new FileTypePreference();
            _settings.FileTypes[_loadedExtension] = preference;
        }
        return preference;
    }

    private void AddAppClick(object sender, RoutedEventArgs e)
    {
        if (_loadedExtension is null) return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择打开应用",
            Filter = "应用程序 (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        if (!TryCaptureCurrentFileType()) return;
        var path = AppDiscovery.NormalizeExecutablePath(dialog.FileName);
        var preference = CurrentPreference();
        if (preference.CustomApps.Any(app => string.Equals(AppDiscovery.NormalizeExecutablePath(app.ExecutablePath), path, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("这个应用已经添加到当前文件类型。", "添加打开方式", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var editor = new CustomAppWindow(path, AppDiscovery.GetDisplayName(path), skinId: _settings.SkinId) { Owner = this };
        if (editor.ShowDialog() != true || editor.Definition is null) return;
        preference.CustomApps.Add(editor.Definition);
        LoadFileType(_loadedExtension, false);
        Apps.SelectedItem = Apps.Items.OfType<System.Windows.Controls.CheckBox>().FirstOrDefault(item => item.Tag is AppCandidate candidate && string.Equals(candidate.ExecutablePath, path, StringComparison.OrdinalIgnoreCase));
    }

    private void RemoveCustomClick(object sender, RoutedEventArgs e)
    {
        if (_loadedExtension is null || Apps.SelectedItem is not System.Windows.Controls.CheckBox { Tag: AppCandidate candidate } || !IsCustomCandidate(candidate)) return;
        if (!TryCaptureCurrentFileType()) return;
        var preference = CurrentPreference();
        preference.CustomApps.RemoveAll(app => string.Equals(AppDiscovery.NormalizeExecutablePath(app.ExecutablePath), candidate.ExecutablePath, StringComparison.OrdinalIgnoreCase));
        preference.CandidateOrder.RemoveAll(id => string.Equals(id, candidate.Id, StringComparison.OrdinalIgnoreCase));
        preference.HiddenCandidateIds.RemoveAll(id => string.Equals(id, candidate.Id, StringComparison.OrdinalIgnoreCase));
        LoadFileType(_loadedExtension, false);
    }

    private void MoveUpClick(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void MoveDownClick(object sender, RoutedEventArgs e) => MoveSelected(1);
    private void MoveSelected(int delta)
    {
        var index = Apps.SelectedIndex; var next = index + delta;
        if (index < 0 || next < 0 || next >= Apps.Items.Count) return;
        var item = (System.Windows.Controls.CheckBox)Apps.Items[index];
        var target = (System.Windows.Controls.CheckBox)Apps.Items[next];
        var targetIndex = _candidateRows.IndexOf(target);
        _candidateRows.Remove(item); _candidateRows.Insert(targetIndex, item);
        _orderEdited = true;
        RefreshCandidateRows(); Apps.SelectedItem = item;
    }

    private void ApplyClick(object sender, RoutedEventArgs e) => TryApplySettings(false);

    private void SaveClick(object sender, RoutedEventArgs e) => TryApplySettings(true);

    private void TryApplySettings(bool closeAfterApply)
    {
        if (!double.TryParse(BandOpacity.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity) || opacity < .05 || opacity > 1) { MessageBox.Show("透明度请输入 0.05 到 1。", "设置"); return; }
        if (!int.TryParse(CandidateCount.Text, out var count) || count < 1 || count > 12) { MessageBox.Show("默认显示数量请输入 1 到 12。", "设置"); return; }
        if (!int.TryParse(TriggerDelay.Text, out var delay) || delay < 0 || delay > 1000) { MessageBox.Show("触发延迟请输入 0 到 1000。", "设置"); return; }
        if (!int.TryParse(AutoDismissDelay.Text, out var autoDismissDelay) || autoDismissDelay < 0 || autoDismissDelay > 10000) { MessageBox.Show("自动收起延迟请输入 0 到 10000。", "设置"); return; }
        if (!int.TryParse(ClipboardDuration.Text, out var clipboardDuration) || clipboardDuration < 2 || clipboardDuration > 60) { MessageBox.Show("剪贴板提示停留时间请输入 2 到 60 秒。", "设置"); ClipboardDuration.Focus(); return; }
        var clipboardCopies = _settings.ClipboardAutoOpenCopyCount;
        if (int.TryParse(ClipboardCopyCount.Text, out var parsedCopies) && parsedCopies is >= 2 and <= 10) clipboardCopies = parsedCopies;
        else if (ClipboardAutoOpen.IsChecked == true) { MessageBox.Show("连续复制次数请输入 2 到 10。", "设置"); ClipboardCopyCount.Focus(); return; }
        if (!double.TryParse(PanelWidth.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var panelWidth) || panelWidth < 240 || panelWidth > 560) { MessageBox.Show("面板宽度请输入 240 到 560。", "设置"); return; }
        if (!double.TryParse(PanelMaxHeight.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var panelHeight) || panelHeight < 300 || panelHeight > 900) { MessageBox.Show("面板最大高度请输入 300 到 900。", "设置"); return; }
        if (!double.TryParse(CandidateGap.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var gap) || gap < 0 || gap > 16) { MessageBox.Show("候选间距请输入 0 到 16。", "设置"); return; }
        if (!int.TryParse(AnimationDuration.Text, out var animationDuration) || animationDuration < 80 || animationDuration > 600) { MessageBox.Show("动效时长请输入 80 到 600。", "设置"); return; }
        if (!TryReadEdgeBandLayout("左侧", LeftBandStart, LeftBandWidth, LeftBandEnd, out var leftBand)
            || !TryReadEdgeBandLayout("右侧", RightBandStart, RightBandWidth, RightBandEnd, out var rightBand)
            || !TryReadEdgeBandLayout("上侧", TopBandStart, TopBandWidth, TopBandEnd, out var topBand)) return;
        EdgeRangeError.Visibility = Visibility.Collapsed;
        int? perTypeCount = null;
        if (PerTypeCountEnabled.IsChecked == true)
        {
            if (!int.TryParse(PerTypeCount.Text, out var parsedPerTypeCount) || parsedPerTypeCount < 1 || parsedPerTypeCount > 12)
            {
                MessageBox.Show("当前文件类型显示数量请输入 1 到 12。", "设置");
                return;
            }
            perTypeCount = parsedPerTypeCount;
        }

        CaptureCurrentExtensionPreference(perTypeCount);
        _settings.EdgeBandOpacity = opacity; _settings.EdgeBandColor = _selectedColor; _settings.CandidateCount = count; _settings.TriggerDelayMs = delay; _settings.AutoDismissDelayMs = autoDismissDelay;
        _settings.ClipboardLinksEnabled = ClipboardEnabled.IsChecked == true;
        _settings.ClipboardAutoOpenEnabled = ClipboardAutoOpen.IsChecked == true;
        _settings.ClipboardAutoOpenCopyCount = clipboardCopies;
        _settings.ClipboardPopupDurationSeconds = clipboardDuration;
        _settings.ClipboardPopupSide = ClipboardSide.SelectedIndex == 1 ? "Left" : "Right";
        _settings.LeftEdgeBand = leftBand; _settings.RightEdgeBand = rightBand; _settings.TopEdgeBand = topBand;
        _settings.PanelWidth = panelWidth; _settings.PanelMaxHeight = panelHeight; _settings.CandidateGap = gap; _settings.AnimationDurationMs = animationDuration;
        var edgeBandPosition = ReadEdgeBandPosition();
        if (edgeBandPosition is null)
        {
            MessageBox.Show("请至少选择一个接收条位置。", "设置");
            return;
        }
        _settings.EdgeBandPosition = edgeBandPosition;
        _settings.DesignSchemeId = PanelDesignCatalog.DefaultId;
        _settings.SkinId = SkinCatalog.Normalize(Skins.SelectedValue?.ToString());
        _settings.Theme = ((ComboBoxItem)Theme.SelectedItem)?.Tag?.ToString() ?? "System"; _settings.LargeText = LargeText.IsChecked == true; _settings.EdgePulseEnabled = EdgePulseEnabled.IsChecked == true; _settings.DiagnosticsEnabled = Diagnostics.IsChecked == true;
        _beforeSave?.Invoke(_settings);
        _store.Save(_settings);
        if (_store is JsonSettingsStore json && json.LastError is not null)
        {
            ApplyStatusText.Text = "保存失败，请重试";
            ApplyStatusText.SetResourceReference(TextBlock.ForegroundProperty, "PanelDanger");
            ApplyStatusText.ToolTip = json.LastError;
            FileTypeStatus.Text = json.LastError;
            return;
        }
        _applied(_settings.Clone());
        AcceptSavedDraft();
        if (closeAfterApply)
        {
            Close();
            return;
        }

        ApplyStatusText.Text = "已应用"; ApplyStatusText.ToolTip = null;
        ApplyStatusText.SetResourceReference(TextBlock.ForegroundProperty, "PanelSuccess");
        FileTypeStatus.Text = "";
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => { timer.Stop(); ApplyStatusText.Text = ""; };
        timer.Start();
    }

    private void CaptureCurrentExtensionPreference(int? perTypeCount)
    {
        if (_loadedExtension is null) return;
        var existing = _settings.FileTypes.GetValueOrDefault(_loadedExtension);
        if (!_orderEdited && !_visibilityEdited && existing?.DisplayCount == perTypeCount) return;
        var preference = CurrentPreference();
        if (_orderEdited) preference.CandidateOrder = _candidateRows.Select(row => ((AppCandidate)row.Tag).Id).ToList();
        foreach (var item in _candidateRows)
        {
            if (item.Tag is not AppCandidate candidate || !candidate.IsAvailable) continue;
            preference.HiddenCandidateIds.RemoveAll(id => string.Equals(id, candidate.Id, StringComparison.OrdinalIgnoreCase));
            if (item.IsChecked != true) preference.HiddenCandidateIds.Add(candidate.Id);
            else if (!_discovery.IsSuggestedForExtension(_loadedExtension, candidate)
                && !preference.CandidateOrder.Contains(candidate.Id, StringComparer.OrdinalIgnoreCase)) preference.CandidateOrder.Add(candidate.Id);
        }
        preference.DisplayCount = perTypeCount;
        preference.CustomApps = preference.CustomApps
            .Where(app => !string.IsNullOrWhiteSpace(app.ExecutablePath))
            .Select(app =>
            {
                app.ExecutablePath = AppDiscovery.NormalizeExecutablePath(app.ExecutablePath);
                app.Id = app.ExecutablePath;
                app.DisplayName = string.IsNullOrWhiteSpace(app.DisplayName) ? AppDiscovery.GetDisplayName(app.ExecutablePath) : app.DisplayName.Trim();
                app.Arguments = string.IsNullOrWhiteSpace(app.Arguments) ? null : app.Arguments.Trim();
                return app;
            })
            .GroupBy(app => app.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        _orderEdited = _visibilityEdited = false;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => Close();
    private void ResetClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show("恢复全部设置？", "Eous 助手", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        var defaults = new UserSettings();
        _settings.ClipboardLinksEnabled = defaults.ClipboardLinksEnabled;
        _settings.ClipboardAutoOpenEnabled = defaults.ClipboardAutoOpenEnabled;
        _settings.ClipboardAutoOpenCopyCount = defaults.ClipboardAutoOpenCopyCount;
        ClipboardAutoOpen.IsChecked = defaults.ClipboardAutoOpenEnabled;
        ClipboardCopyCount.Text = defaults.ClipboardAutoOpenCopyCount.ToString(CultureInfo.InvariantCulture);
        _settings.ClipboardPopupDurationSeconds = defaults.ClipboardPopupDurationSeconds;
        _settings.ClipboardPopupSide = defaults.ClipboardPopupSide;
        ClipboardEnabled.IsChecked = defaults.ClipboardLinksEnabled;
        ClipboardDuration.Text = defaults.ClipboardPopupDurationSeconds.ToString(CultureInfo.InvariantCulture);
        ClipboardSide.SelectedIndex = 0;
        _settings.DesignSchemeId = defaults.DesignSchemeId; _settings.SkinId = defaults.SkinId; _settings.Paused = defaults.Paused; _settings.CandidateCount = defaults.CandidateCount; _settings.EdgeBandWidth = defaults.EdgeBandWidth; _settings.EdgeBandOpacity = defaults.EdgeBandOpacity; _settings.EdgeBandColor = defaults.EdgeBandColor; _settings.EdgeBandPosition = defaults.EdgeBandPosition; _settings.LeftEdgeBand = defaults.LeftEdgeBand?.Clone(); _settings.RightEdgeBand = defaults.RightEdgeBand?.Clone(); _settings.TopEdgeBand = defaults.TopEdgeBand?.Clone(); _settings.TriggerDelayMs = defaults.TriggerDelayMs; _settings.AutoDismissDelayMs = defaults.AutoDismissDelayMs; _settings.Theme = defaults.Theme; _settings.LargeText = defaults.LargeText; _settings.DiagnosticsEnabled = defaults.DiagnosticsEnabled; _settings.FileTypes.Clear();
        Skins.SelectedValue = defaults.SkinId;
        BandOpacity.Text = defaults.EdgeBandOpacity.ToString(CultureInfo.InvariantCulture); _selectedColor = defaults.EdgeBandColor; BandColorPreview.Background = ToBrush(_selectedColor); CandidateCount.Text = defaults.CandidateCount.ToString(CultureInfo.InvariantCulture); TriggerDelay.Text = defaults.TriggerDelayMs.ToString(CultureInfo.InvariantCulture); AutoDismissDelay.Text = defaults.AutoDismissDelayMs.ToString(CultureInfo.InvariantCulture); PanelWidth.Text = defaults.PanelWidth.ToString(CultureInfo.InvariantCulture); PanelMaxHeight.Text = defaults.PanelMaxHeight.ToString(CultureInfo.InvariantCulture); CandidateGap.Text = defaults.CandidateGap.ToString(CultureInfo.InvariantCulture); AnimationDuration.Text = defaults.AnimationDurationMs.ToString(CultureInfo.InvariantCulture); SetEdgeBandLayoutEditors(defaults); Theme.SelectedIndex = 0; LargeText.IsChecked = false; EdgePulseEnabled.IsChecked = true; Diagnostics.IsChecked = false;
        SetEdgeBandPositionChecks(defaults.EdgeBandPosition);
        PerTypeCountEnabled.IsChecked = false; PerTypeCount.Text = "";
        if (_loadedExtension is not null) LoadFileType(_loadedExtension, false);
        RefreshExtensionSuggestions();
    }

    private void ChooseColorClick(object sender, RoutedEventArgs e) => ColorPopup.IsOpen = true;

    private void SetEdgeBandPositionChecks(string? position)
    {
        var positions = (position ?? "Left,Right")
            .Split([',', ';', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.Equals("Both", StringComparison.OrdinalIgnoreCase) ? "Left,Right" : value)
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        EdgeBandLeft.IsChecked = positions.Contains("Left");
        EdgeBandRight.IsChecked = positions.Contains("Right");
        EdgeBandTop.IsChecked = positions.Contains("Top");
    }

    private string? ReadEdgeBandPosition()
    {
        var positions = new List<string>();
        if (EdgeBandLeft.IsChecked == true) positions.Add("Left");
        if (EdgeBandRight.IsChecked == true) positions.Add("Right");
        if (EdgeBandTop.IsChecked == true) positions.Add("Top");
        return positions.Count == 0 ? null : string.Join(',', positions);
    }

    private void BuildColorSwatches()
    {
        foreach (var hex in new[] { "#2563EB", "#0EA5E9", "#06B6D4", "#10B981", "#84CC16", "#F59E0B", "#F97316", "#EF4444", "#EC4899", "#8B5CF6", "#64748B", "#111827" })
        {
            var button = new System.Windows.Controls.Button { Width = 30, MinWidth = 30, Height = 30, Margin = new Thickness(3), Padding = new Thickness(0), ToolTip = hex, Background = ToBrush(hex), BorderBrush = new SolidColorBrush(Colors.Transparent) };
            System.Windows.Automation.AutomationProperties.SetName(button, $"选择颜色 {hex}");
            button.Click += (_, _) => SetSelectedColor(hex);
            ColorSwatches.Children.Add(button);
        }
    }

    private void SetSelectedColor(string color)
    {
        _selectedColor = color;
        BandColorPreview.Background = ToBrush(color);
        ColorPopup.IsOpen = false;
    }

    private void CustomColorClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.ColorDialog();
        if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
        SetSelectedColor($"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
    }

    private static SolidColorBrush ToBrush(string color)
    {
        try { return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)); }
        catch { return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2563EB")); }
    }

    private static bool IsValidColor(string color)
    {
        try { _ = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color); return true; }
        catch { return false; }
    }
    private void HelpClick(object sender, RoutedEventArgs e)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "docs", "BEGINNER-GUIDE.md"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "docs", "BEGINNER-GUIDE.md"))
        };
        var help = candidates.FirstOrDefault(File.Exists);
        try { if (help is not null) Process.Start(new ProcessStartInfo(help) { UseShellExecute = true }); else System.Windows.MessageBox.Show("请参阅项目 docs/BEGINNER-GUIDE.md。", "帮助"); } catch { }
    }
}
