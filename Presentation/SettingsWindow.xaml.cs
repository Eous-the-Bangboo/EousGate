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

namespace EousGate;

public partial class SettingsWindow : Window
{
    private readonly UserSettings _settings;
    private readonly ISettingsStore _store;
    private readonly AppDiscovery _discovery;
    private readonly Action<UserSettings> _applied;
    private string? _loadedExtension;
    private IReadOnlyList<AppCandidate> _loadedCandidates = [];
    private string _selectedColor;

    public SettingsWindow(UserSettings settings, ISettingsStore store, AppDiscovery discovery, Action<UserSettings> applied)
    {
        InitializeComponent(); _settings = settings.Clone(); _store = store; _discovery = new AppDiscovery(() => _settings); _applied = applied; _selectedColor = IsValidColor(_settings.EdgeBandColor) ? _settings.EdgeBandColor : new UserSettings().EdgeBandColor;
        MicaWindowHelper.Enable(this, settings.SkinId);
        DesignSchemes.ItemsSource = PanelDesignCatalog.All;
        DesignSchemes.SelectedValue = PanelDesignCatalog.Normalize(settings.DesignSchemeId);
        Skins.ItemsSource = SkinCatalog.All;
        Skins.SelectedValue = SkinCatalog.Normalize(settings.SkinId);
        BandOpacity.Text = settings.EdgeBandOpacity.ToString(CultureInfo.InvariantCulture);
        BandColorPreview.Background = ToBrush(_selectedColor); BuildColorSwatches();
        CandidateCount.Text = settings.CandidateCount.ToString(CultureInfo.InvariantCulture);
        TriggerDelay.Text = settings.TriggerDelayMs.ToString(CultureInfo.InvariantCulture);
        AutoDismissDelay.Text = settings.AutoDismissDelayMs.ToString(CultureInfo.InvariantCulture);
        PanelWidth.Text = settings.PanelWidth.ToString(CultureInfo.InvariantCulture);
        PanelMaxHeight.Text = settings.PanelMaxHeight.ToString(CultureInfo.InvariantCulture);
        CandidateGap.Text = settings.CandidateGap.ToString(CultureInfo.InvariantCulture);
        AnimationDuration.Text = settings.AnimationDurationMs.ToString(CultureInfo.InvariantCulture);
        SetEdgeBandLayoutEditors(settings);
        SetEdgeBandPositionChecks(settings.EdgeBandPosition);
        Theme.SelectedIndex = settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        LargeText.IsChecked = settings.LargeText; EdgePulseEnabled.IsChecked = settings.EdgePulseEnabled; Diagnostics.IsChecked = settings.DiagnosticsEnabled;
        Opacity = 0;
        Loaded += (_, _) =>
        {
            BeginAnimation(Window.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            WindowTranslation.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        };
        PerTypeCountEnabled.Checked += (_, _) => PerTypeCount.IsEnabled = true;
        PerTypeCountEnabled.Unchecked += (_, _) => PerTypeCount.IsEnabled = false;
    }

    private void SetEdgeBandLayoutEditors(UserSettings settings)
    {
        var area = Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);
        SetEdgeBandLayoutEditors(EdgeBandLayoutMath.GetEditableLayout(settings, OverlayPlacementSide.Left, area), LeftBandLength, LeftBandWidth, LeftBandOffset);
        SetEdgeBandLayoutEditors(EdgeBandLayoutMath.GetEditableLayout(settings, OverlayPlacementSide.Right, area), RightBandLength, RightBandWidth, RightBandOffset);
        SetEdgeBandLayoutEditors(EdgeBandLayoutMath.GetEditableLayout(settings, OverlayPlacementSide.Top, area), TopBandLength, TopBandWidth, TopBandOffset);
    }

    private static void SetEdgeBandLayoutEditors(EdgeBandLayout layout, System.Windows.Controls.TextBox length, System.Windows.Controls.TextBox width, System.Windows.Controls.TextBox offset)
    {
        length.Text = layout.LengthPercent.ToString("0.##", CultureInfo.InvariantCulture);
        width.Text = layout.WidthPercent.ToString("0.##", CultureInfo.InvariantCulture);
        offset.Text = layout.OffsetPixels.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryReadEdgeBandLayout(string side, System.Windows.Controls.TextBox length, System.Windows.Controls.TextBox width, System.Windows.Controls.TextBox offset, out EdgeBandLayout layout)
    {
        layout = new EdgeBandLayout();
        if (!double.TryParse(length.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lengthPercent)
            || lengthPercent < EdgeBandLayoutMath.MinLengthPercent || lengthPercent > EdgeBandLayoutMath.MaxLengthPercent)
        {
            MessageBox.Show($"{side}接收条长度请输入 10 到 100。", "设置");
            return false;
        }
        if (!double.TryParse(width.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var widthPercent)
            || widthPercent < EdgeBandLayoutMath.MinWidthPercent || widthPercent > EdgeBandLayoutMath.MaxWidthPercent)
        {
            MessageBox.Show($"{side}接收条宽度请输入 0.1 到 5。", "设置");
            return false;
        }
        if (!int.TryParse(offset.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offsetPixels)
            || offsetPixels < EdgeBandLayoutMath.MinOffsetPixels || offsetPixels > EdgeBandLayoutMath.MaxOffsetPixels)
        {
            MessageBox.Show($"{side}接收条偏移请输入 -10000 到 10000。", "设置");
            return false;
        }
        layout = new EdgeBandLayout { LengthPercent = lengthPercent, WidthPercent = widthPercent, OffsetPixels = offsetPixels };
        return true;
    }

    private void LoadClick(object sender, RoutedEventArgs e)
    {
        var ext = Extension.Text.Trim(); if (string.IsNullOrWhiteSpace(ext)) return;
        if (!ext.StartsWith('.')) ext = "." + ext;
        _loadedExtension = ext.ToLowerInvariant(); _loadedCandidates = _discovery.GetCandidatesForExtension(_loadedExtension, false);
        var pref = _settings.FileTypes.FirstOrDefault(p => string.Equals(p.Key, _loadedExtension, StringComparison.OrdinalIgnoreCase)).Value;
        var ordered = _loadedCandidates.OrderBy(c => pref?.CandidateOrder.FindIndex(id => string.Equals(id, c.Id, StringComparison.OrdinalIgnoreCase)) is var i && i >= 0 ? i : int.MaxValue).ThenBy(c => c.DisplayName);
        Apps.Items.Clear();
        foreach (var candidate in ordered)
            Apps.Items.Add(CreateCandidateRow(candidate, pref));
        LoadedLabel.Text = $"{_loadedExtension}：{_loadedCandidates.Count} 个候选软件";
        AddAppButton.IsEnabled = true;
        RemoveCustomButton.IsEnabled = false;
        if (pref?.DisplayCount is int perTypeCount) { PerTypeCountEnabled.IsChecked = true; PerTypeCount.Text = perTypeCount.ToString(); }
        else { PerTypeCountEnabled.IsChecked = false; PerTypeCount.Text = ""; }
    }

    private System.Windows.Controls.CheckBox CreateCandidateRow(AppCandidate candidate, FileTypePreference? preference)
    {
        var isCustom = preference?.CustomApps.Any(app => string.Equals(AppDiscovery.NormalizeExecutablePath(app.ExecutablePath), candidate.ExecutablePath, StringComparison.OrdinalIgnoreCase)) == true;
        var content = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock { Text = candidate.DisplayName, VerticalAlignment = VerticalAlignment.Center });
        if (isCustom)
        {
            content.Children.Add(new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("PanelAccentSoft"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(8, 0, 0, 0),
                Child = new TextBlock { Text = "自定义", FontSize = 11, Foreground = (System.Windows.Media.Brush)FindResource("PanelAccent") }
            });
        }
        if (!candidate.IsAvailable)
        {
            content.Children.Add(new TextBlock { Text = "应用已不可用", Margin = new Thickness(8, 0, 0, 0), Foreground = (System.Windows.Media.Brush)FindResource("PanelDanger"), FontSize = 11 });
        }
        return new System.Windows.Controls.CheckBox
        {
            Content = content,
            ToolTip = candidate.IsAvailable ? $"{candidate.ExecutablePath}\n{candidate.Arguments}" : $"应用已不可用\n{candidate.ExecutablePath}",
            Tag = candidate,
            IsChecked = candidate.IsAvailable && (preference is null || !preference.HiddenCandidateIds.Contains(candidate.Id, StringComparer.OrdinalIgnoreCase)),
            IsEnabled = candidate.IsAvailable,
            Margin = new Thickness(4)
        };
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
        LoadClick(sender, e);
        Apps.SelectedItem = Apps.Items.OfType<System.Windows.Controls.CheckBox>().FirstOrDefault(item => item.Tag is AppCandidate candidate && string.Equals(candidate.ExecutablePath, path, StringComparison.OrdinalIgnoreCase));
    }

    private void RemoveCustomClick(object sender, RoutedEventArgs e)
    {
        if (_loadedExtension is null || Apps.SelectedItem is not System.Windows.Controls.CheckBox { Tag: AppCandidate candidate } || !IsCustomCandidate(candidate)) return;
        var preference = CurrentPreference();
        preference.CustomApps.RemoveAll(app => string.Equals(AppDiscovery.NormalizeExecutablePath(app.ExecutablePath), candidate.ExecutablePath, StringComparison.OrdinalIgnoreCase));
        preference.CandidateOrder.RemoveAll(id => string.Equals(id, candidate.Id, StringComparison.OrdinalIgnoreCase));
        preference.HiddenCandidateIds.RemoveAll(id => string.Equals(id, candidate.Id, StringComparison.OrdinalIgnoreCase));
        LoadClick(sender, e);
    }

    private void MoveUpClick(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void MoveDownClick(object sender, RoutedEventArgs e) => MoveSelected(1);
    private void MoveSelected(int delta)
    {
        var index = Apps.SelectedIndex; var next = index + delta;
        if (index < 0 || next < 0 || next >= Apps.Items.Count) return;
        var item = Apps.Items[index]; Apps.Items.RemoveAt(index); Apps.Items.Insert(next, item); Apps.SelectedIndex = next;
    }

    private void ApplyClick(object sender, RoutedEventArgs e) => TryApplySettings(false);

    private void SaveClick(object sender, RoutedEventArgs e) => TryApplySettings(true);

    private void TryApplySettings(bool closeAfterApply)
    {
        if (!double.TryParse(BandOpacity.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity) || opacity < .05 || opacity > 1) { MessageBox.Show("透明度请输入 0.05 到 1。", "设置"); return; }
        if (!int.TryParse(CandidateCount.Text, out var count) || count < 1 || count > 12) { MessageBox.Show("默认显示数量请输入 1 到 12。", "设置"); return; }
        if (!int.TryParse(TriggerDelay.Text, out var delay) || delay < 0 || delay > 1000) { MessageBox.Show("触发延迟请输入 0 到 1000。", "设置"); return; }
        if (!int.TryParse(AutoDismissDelay.Text, out var autoDismissDelay) || autoDismissDelay < 0 || autoDismissDelay > 10000) { MessageBox.Show("自动收起延迟请输入 0 到 10000。", "设置"); return; }
        if (!double.TryParse(PanelWidth.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var panelWidth) || panelWidth < 240 || panelWidth > 560) { MessageBox.Show("面板宽度请输入 240 到 560。", "设置"); return; }
        if (!double.TryParse(PanelMaxHeight.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var panelHeight) || panelHeight < 300 || panelHeight > 900) { MessageBox.Show("面板最大高度请输入 300 到 900。", "设置"); return; }
        if (!double.TryParse(CandidateGap.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var gap) || gap < 0 || gap > 16) { MessageBox.Show("候选间距请输入 0 到 16。", "设置"); return; }
        if (!int.TryParse(AnimationDuration.Text, out var animationDuration) || animationDuration < 80 || animationDuration > 600) { MessageBox.Show("动效时长请输入 80 到 600。", "设置"); return; }
        if (!TryReadEdgeBandLayout("左侧", LeftBandLength, LeftBandWidth, LeftBandOffset, out var leftBand)
            || !TryReadEdgeBandLayout("右侧", RightBandLength, RightBandWidth, RightBandOffset, out var rightBand)
            || !TryReadEdgeBandLayout("上侧", TopBandLength, TopBandWidth, TopBandOffset, out var topBand)) return;
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
        _settings.LeftEdgeBand = leftBand; _settings.RightEdgeBand = rightBand; _settings.TopEdgeBand = topBand;
        _settings.PanelWidth = panelWidth; _settings.PanelMaxHeight = panelHeight; _settings.CandidateGap = gap; _settings.AnimationDurationMs = animationDuration;
        var edgeBandPosition = ReadEdgeBandPosition();
        if (edgeBandPosition is null)
        {
            MessageBox.Show("请至少选择一个接收条位置。", "设置");
            return;
        }
        _settings.EdgeBandPosition = edgeBandPosition;
        _settings.DesignSchemeId = PanelDesignCatalog.Normalize(DesignSchemes.SelectedValue?.ToString());
        _settings.SkinId = SkinCatalog.Normalize(Skins.SelectedValue?.ToString());
        _settings.Theme = ((ComboBoxItem)Theme.SelectedItem)?.Tag?.ToString() ?? "System"; _settings.LargeText = LargeText.IsChecked == true; _settings.EdgePulseEnabled = EdgePulseEnabled.IsChecked == true; _settings.DiagnosticsEnabled = Diagnostics.IsChecked == true;
        _store.Save(_settings);
        _applied(_settings.Clone());
        if (closeAfterApply)
        {
            Close();
            return;
        }

        ApplyStatusText.Text = "已应用";
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => { timer.Stop(); ApplyStatusText.Text = ""; };
        timer.Start();
    }

    private void CaptureCurrentExtensionPreference(int? perTypeCount)
    {
        if (_loadedExtension is null) return;
        var preference = CurrentPreference();
        preference.CandidateOrder.Clear();
        preference.HiddenCandidateIds.Clear();
        foreach (var item in Apps.Items.OfType<System.Windows.Controls.CheckBox>())
        {
            if (item.Tag is not AppCandidate candidate) continue;
            preference.CandidateOrder.Add(candidate.Id);
            if (item.IsChecked != true) preference.HiddenCandidateIds.Add(candidate.Id);
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
    }

    private void CancelClick(object sender, RoutedEventArgs e) => Close();
    private void ResetClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show("恢复全部设置？", "EousGate", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        var defaults = new UserSettings();
        _settings.DesignSchemeId = defaults.DesignSchemeId; _settings.SkinId = defaults.SkinId; _settings.Paused = defaults.Paused; _settings.CandidateCount = defaults.CandidateCount; _settings.EdgeBandWidth = defaults.EdgeBandWidth; _settings.EdgeBandOpacity = defaults.EdgeBandOpacity; _settings.EdgeBandColor = defaults.EdgeBandColor; _settings.EdgeBandPosition = defaults.EdgeBandPosition; _settings.LeftEdgeBand = defaults.LeftEdgeBand?.Clone(); _settings.RightEdgeBand = defaults.RightEdgeBand?.Clone(); _settings.TopEdgeBand = defaults.TopEdgeBand?.Clone(); _settings.TriggerDelayMs = defaults.TriggerDelayMs; _settings.AutoDismissDelayMs = defaults.AutoDismissDelayMs; _settings.Theme = defaults.Theme; _settings.LargeText = defaults.LargeText; _settings.DiagnosticsEnabled = defaults.DiagnosticsEnabled; _settings.FileTypes.Clear();
        DesignSchemes.SelectedValue = defaults.DesignSchemeId;
        Skins.SelectedValue = defaults.SkinId;
        BandOpacity.Text = defaults.EdgeBandOpacity.ToString(CultureInfo.InvariantCulture); _selectedColor = defaults.EdgeBandColor; BandColorPreview.Background = ToBrush(_selectedColor); CandidateCount.Text = defaults.CandidateCount.ToString(CultureInfo.InvariantCulture); TriggerDelay.Text = defaults.TriggerDelayMs.ToString(CultureInfo.InvariantCulture); AutoDismissDelay.Text = defaults.AutoDismissDelayMs.ToString(CultureInfo.InvariantCulture); PanelWidth.Text = defaults.PanelWidth.ToString(CultureInfo.InvariantCulture); PanelMaxHeight.Text = defaults.PanelMaxHeight.ToString(CultureInfo.InvariantCulture); CandidateGap.Text = defaults.CandidateGap.ToString(CultureInfo.InvariantCulture); AnimationDuration.Text = defaults.AnimationDurationMs.ToString(CultureInfo.InvariantCulture); SetEdgeBandLayoutEditors(defaults); Theme.SelectedIndex = 0; LargeText.IsChecked = false; EdgePulseEnabled.IsChecked = true; Diagnostics.IsChecked = false;
        SetEdgeBandPositionChecks(defaults.EdgeBandPosition);
        PerTypeCountEnabled.IsChecked = false; PerTypeCount.Text = "";
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
