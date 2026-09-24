using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using DrawingIcon = System.Drawing.Icon;
using System.IO;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using MediaBrush = System.Windows.Media.Brush;
using EousGate.Infrastructure;
using Forms = System.Windows.Forms;
using WpfDragDropEffects = System.Windows.DragDropEffects;

namespace EousGate;

public partial class OverlayWindow : Window
{
    private readonly IAppDiscovery _discovery;
    private readonly IOpenFileService _open;
    private readonly Func<UserSettings> _settings;
    private readonly bool _monitorMouseButton;
    private DragSession? _session;
    private readonly DispatcherTimer _sessionTimer;
    private readonly DispatcherTimer _autoDismissTimer;
    private DateTime? _outsideSafeAreaSince;
    private System.Drawing.Point? _anchorPoint;
    private bool _panelFromRight;
    private bool _panelBelowAnchor;
    private EdgeDropWindow.EdgeSide _sourceSide = EdgeDropWindow.EdgeSide.Left;
    private string _activeDesignSchemeId = PanelDesignCatalog.DefaultId;
    private System.Windows.Controls.Panel _candidateSurface = new StackPanel();

    public OverlayWindow(IAppDiscovery discovery, IOpenFileService open, Func<UserSettings> settings, bool monitorMouseButton = true)
    {
        InitializeComponent();
        _discovery = discovery;
        _open = open;
        _settings = settings;
        _monitorMouseButton = monitorMouseButton;
        _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _sessionTimer.Tick += (_, _) =>
        {
            if (_session is not null && (GetAsyncKeyState(1) & 0x8000) == 0) CancelSession();
        };
        _autoDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _autoDismissTimer.Tick += (_, _) => CheckAutoDismiss();
        DragOver += (_, e) => e.Effects = WpfDragDropEffects.Copy;
        Drop += (_, _) => CancelSession();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) CancelSession(); };
    }

    public void BeginSession(DragSession session, IReadOnlyList<AppCandidate> candidates, System.Drawing.Point? cursorPosition = null, EdgeDropWindow.EdgeSide sourceSide = EdgeDropWindow.EdgeSide.Left)
    {
        _session = session;
        _anchorPoint = cursorPosition;
        _sourceSide = sourceSide;
        _outsideSafeAreaSince = null;
        var settings = _settings();
        ApplyDesignScheme(settings.DesignSchemeId);
        Width = Math.Clamp(settings.PanelWidth, 240, 560);
        MaxHeight = Math.Clamp(settings.PanelMaxHeight, 300, 900);
        if (_monitorMouseButton) _sessionTimer.Start();
        Heading.FontSize = settings.LargeText ? 18 : 16;
        Heading.Foreground = (MediaBrush)FindResource("PanelForeground");
        var isMultiple = session.FileCount > 1;
        var extensions = session.FilePaths
            .Select(Path.GetExtension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var extension = Path.GetExtension(session.PrimaryFilePath).TrimStart('.').ToUpperInvariant();
        var sameType = extensions.Length == 1;
        Heading.Text = isMultiple ? "选择打开这些文件的软件" : "选择打开文件的软件";
        FileTypeMark.Text = isMultiple && !sameType
            ? "FILES"
            : extension is { Length: > 0 and <= 4 } ? extension : isMultiple ? "FILES" : "FILE";
        FileContext.Text = isMultiple
            ? sameType && extension.Length > 0
                ? $"{session.FileCount} 个 {extension} 文件 · 本机应用"
                : $"{session.FileCount} 个文件 · {extensions.Length} 种类型 · 共同软件"
            : extension.Length > 0 ? $"{extension} 文件 · 本机应用" : "本机应用";
        CandidateCountLabel.Text = $"{candidates.Count} 个";
        AccentSegments.Visibility = SystemParameters.HighContrast ? Visibility.Collapsed : Visibility.Visible;
        if (SystemParameters.HighContrast) AccentRail.Background = (MediaBrush)FindResource("PanelAccent");
        ResetCandidateSurface();
        Actions.Children.Clear();
        if (candidates.Count == 0)
        {
            Heading.Text = isMultiple ? "未找到可同时打开的软件" : "未找到可打开的软件";
            FileContext.Text = isMultiple ? $"{session.FileCount} 个文件没有共同的软件选项" : "可交给 Windows 选择其他应用";
            AddStatusMessage(isMultiple ? "这些文件没有共同可用的软件选项。" : "当前文件类型没有可用的软件选项。");
            if (!isMultiple) AddAction("选择其他软件", () => OpenWithFile(session.PrimaryFilePath), primary: true);
        }
        else
        {
            var ext = Path.GetExtension(session.PrimaryFilePath);
            var pref = settings.FileTypes.FirstOrDefault(p => string.Equals(p.Key, ext, StringComparison.OrdinalIgnoreCase)).Value;
            var plan = OpenPanelPlan.Create(candidates, pref?.DisplayCount ?? settings.CandidateCount);
            var index = 0;
            foreach (var candidate in plan.VisibleCandidates) AddCandidate(candidate, index++);
            if (plan.CanExpand) AddAction("更多应用", () => ShowCandidates(candidates));
        }
        AddAction("取消", CancelSession);
        PositionAndShow();
    }

    private void ApplyDesignScheme(string? designSchemeId)
    {
        _activeDesignSchemeId = PanelDesignCatalog.Normalize(designSchemeId);
        switch (_activeDesignSchemeId)
        {
            case PanelDesignCatalog.DefaultId:
                PanelCard.CornerRadius = new CornerRadius(8);
                AccentRail.Visibility = Visibility.Visible;
                break;
            case "compact-list":
                PanelCard.CornerRadius = new CornerRadius(8);
                AccentRail.Visibility = Visibility.Visible;
                break;
            case "icon-grid":
                PanelCard.CornerRadius = new CornerRadius(8);
                AccentRail.Visibility = Visibility.Visible;
                break;
        }
        ResetCandidateSurface();
    }

    private void ResetCandidateSurface()
    {
        _candidateSurface = _activeDesignSchemeId == "icon-grid"
            ? new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 }
            : new StackPanel();
        CandidatesHost.Children.Clear();
        CandidatesHost.Children.Add(_candidateSurface);
    }

    private void AddCandidate(AppCandidate candidate, int animationIndex = 0)
    {
        var settings = _settings();
        var large = settings.LargeText;
        var compact = _activeDesignSchemeId == "compact-list";
        var iconGrid = _activeDesignSchemeId == "icon-grid";
        var button = new System.Windows.Controls.Button
        {
            Style = (Style)FindResource("CandidateButtonStyle"),
            Height = iconGrid ? 88 : compact ? 52 : large ? 68 : 60,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            HorizontalContentAlignment = iconGrid ? System.Windows.HorizontalAlignment.Center : System.Windows.HorizontalAlignment.Stretch,
            Padding = iconGrid ? new Thickness(8) : compact ? new Thickness(8, 5, 8, 5) : new Thickness(10, 7, 10, 7),
            Tag = candidate,
            ToolTip = $"{candidate.DisplayName}\n{candidate.ExecutablePath}",
            AllowDrop = true
        };
        System.Windows.Automation.AutomationProperties.SetName(button, $"打开 {candidate.DisplayName}");
        var gap = Math.Clamp(settings.CandidateGap, 0, 16);
        button.Margin = iconGrid
            ? new Thickness(Math.Max(2, gap / 2), Math.Max(2, gap / 2), Math.Max(2, gap / 2), Math.Max(2, gap / 2))
            : new Thickness(0, 0, 0, Math.Max(compact ? 2 : 3, gap));
        var panel = new Grid();
        if (!iconGrid)
        {
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compact ? 34 : 40) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compact ? 9 : 11) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        }
        var icon = TryGetIcon(candidate.IconPath) ?? TryGetIcon(candidate.ExecutablePath);
        var iconSurface = new Border
        {
            Width = iconGrid ? 48 : compact ? 32 : 38,
            Height = iconGrid ? 48 : compact ? 32 : 38,
            CornerRadius = new CornerRadius(8),
            Background = (MediaBrush)FindResource("PanelSurfaceSoft"),
            Child = icon is null
                ? new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(candidate.DisplayName) ? "?" : candidate.DisplayName[..1].ToUpperInvariant(),
                    FontSize = iconGrid ? 18 : compact ? 13 : 15,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (MediaBrush)FindResource("PanelForeground"),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                }
                : new System.Windows.Controls.Image { Source = icon, Width = iconGrid ? 38 : compact ? 25 : 30, Height = iconGrid ? 38 : compact ? 25 : 30 }
        };
        if (!iconGrid) Grid.SetColumn(iconSurface, 0);
        panel.Children.Add(iconSurface);

        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock
        {
            Text = candidate.DisplayName,
            Foreground = (MediaBrush)FindResource("PanelForeground"),
            FontSize = compact || iconGrid ? 13 : large ? 14 : 13,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        labels.Children.Add(title);
        var subtitle = new TextBlock
        {
            Text = "桌面应用",
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = (MediaBrush)FindResource("PanelMutedForeground"),
            FontSize = 11
        };
        labels.Children.Add(subtitle);
        if (!iconGrid)
        {
            Grid.SetColumn(labels, 2);
            panel.Children.Add(labels);
        }

        var arrow = new TextBlock
        {
            Text = "↗",
            FontSize = 15,
            Foreground = (MediaBrush)FindResource("PanelMutedForeground"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!iconGrid)
        {
            Grid.SetColumn(arrow, 3);
            panel.Children.Add(arrow);
        }
        button.Content = panel;
        button.Opacity = 0;
        button.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        button.RenderTransform = new TranslateTransform(iconGrid ? 0 : -12, 0);
        var pointerOver = false;
        var dropTarget = false;

        void RefreshCandidateVisual()
        {
            var active = pointerOver || dropTarget || button.IsKeyboardFocusWithin;
            if (active)
            {
                button.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "PanelAccentSoft");
                button.SetResourceReference(System.Windows.Controls.Control.BorderBrushProperty, "PanelAccent");
            }
            else
            {
                button.Background = System.Windows.Media.Brushes.Transparent;
                button.BorderBrush = System.Windows.Media.Brushes.Transparent;
            }
            iconSurface.SetResourceReference(Border.BackgroundProperty, active ? "PanelAccentSoft" : "PanelSurfaceSoft");
            title.SetResourceReference(TextBlock.ForegroundProperty, active ? "PanelAccent" : "PanelForeground");
            subtitle.SetResourceReference(TextBlock.ForegroundProperty, active ? "PanelAccentHover" : "PanelMutedForeground");
            arrow.SetResourceReference(TextBlock.ForegroundProperty, active ? "PanelAccent" : "PanelMutedForeground");
        }

        button.MouseEnter += (_, _) =>
        {
            pointerOver = true;
            RefreshCandidateVisual();
        };
        button.MouseLeave += (_, _) =>
        {
            pointerOver = false;
            RefreshCandidateVisual();
        };
        button.GotKeyboardFocus += (_, _) => RefreshCandidateVisual();
        button.LostKeyboardFocus += (_, _) => RefreshCandidateVisual();
        button.Loaded += (_, _) =>
        {
            if (!SystemParameters.ClientAreaAnimation)
            {
                button.Opacity = 1;
                ((TranslateTransform)button.RenderTransform).X = 0;
                return;
            }
            var delay = TimeSpan.FromMilliseconds(Math.Min(animationIndex, 8) * 35);
            var duration = Math.Clamp(_settings().AnimationDurationMs, 80, 600);
            button.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(duration)) { BeginTime = delay, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            if (!iconGrid)
                ((TranslateTransform)button.RenderTransform).BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-12, 0, TimeSpan.FromMilliseconds(duration + 40)) { BeginTime = delay, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        };
        button.Click += (_, _) => OpenCandidate(candidate);
        button.MouseEnter += (_, _) => button.ToolTip = $"打开 {candidate.DisplayName}";
        button.DragOver += (_, e) =>
        {
            dropTarget = true;
            RefreshCandidateVisual();
            e.Effects = WpfDragDropEffects.Copy;
        };
        button.DragLeave += (_, _) =>
        {
            dropTarget = false;
            RefreshCandidateVisual();
        };
        button.Drop += (_, e) =>
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) OpenCandidate(candidate);
            e.Handled = true;
        };
        _candidateSurface.Children.Add(button);
    }

    private void AddAction(string text, Action action, bool primary = false)
    {
        var button = new System.Windows.Controls.Button
        {
            Content = text,
            Style = (Style)FindResource(primary ? "PrimaryActionButtonStyle" : "SecondaryActionButtonStyle")
        };
        button.Tag = text;
        button.Click += (_, _) => action();
        Grid.SetColumn(button, text == "取消" ? 2 : 0);
        Actions.Children.Add(button);
    }

    private void AddStatusMessage(string text)
    {
        CandidatesHost.Children.Clear();
        var message = new TextBlock
        {
            Text = text,
            Style = (Style)FindResource("StatusTextStyle"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var icon = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(8),
            Background = (MediaBrush)FindResource("PanelAccentSoft"),
            Child = new TextBlock
            {
                Text = "i",
                FontWeight = FontWeights.Bold,
                Foreground = (MediaBrush)FindResource("PanelAccent"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(message, 2);
        row.Children.Add(icon);
        row.Children.Add(message);
        var surface = new Border { MinHeight = 70, Padding = new Thickness(12, 10, 12, 14), Child = row };
        System.Windows.Automation.AutomationProperties.SetName(surface, text);
        CandidatesHost.Children.Add(surface);
    }

    private void ShowCandidates(IReadOnlyList<AppCandidate> candidates)
    {
        ResetCandidateSurface();
        var moreButton = Actions.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault(button => Equals(button.Tag, "更多应用"));
        if (moreButton is not null) Actions.Children.Remove(moreButton);
        CandidateCountLabel.Text = $"{candidates.Count} 个";
        var index = 0;
        foreach (var candidate in candidates) AddCandidate(candidate, index++);
    }

    private void OpenCandidate(AppCandidate candidate)
    {
        if (_session is null) return;
        _session.SelectedCandidate = candidate;
        _session.State = DragSessionState.Opening;
        var result = _open.Open(_session.FilePaths, candidate);
        if (result.Success)
        {
            _session.State = DragSessionState.Completed;
            _session = null;
            _anchorPoint = null;
            _sessionTimer.Stop();
            HideAnimated();
        }
        else
        {
            _session.State = DragSessionState.Failed;
            Heading.Text = _session.FileCount > 1 ? "无法打开这些文件" : "无法打开文件";
            FileContext.Text = "所选软件没有响应，请重试或取消";
            Heading.Foreground = (MediaBrush)FindResource("PanelDanger");
            Actions.Children.Clear();
            AddAction("重试", () => OpenCandidate(candidate), primary: true);
            AddAction("取消", CancelSession);
            AnimateFailure();
        }
    }

    public void ShowUnsupported(EdgeDropWindow.EdgeSide sourceSide = EdgeDropWindow.EdgeSide.Left)
    {
        _session = null;
        _anchorPoint = Forms.Cursor.Position;
        _sourceSide = sourceSide;
        _outsideSafeAreaSince = null;
        _sessionTimer.Stop();
        Heading.Text = "暂不支持文件夹";
        FileTypeMark.Text = "!";
        FileContext.Text = "请拖入一个或多个普通文件";
        CandidateCountLabel.Text = "不支持";
        CandidatesHost.Children.Clear();
        Actions.Children.Clear();
        AddStatusMessage("包含文件夹或无效项目的拖拽不会产生打开动作。");
        AddAction("取消", HideAnimated);
        PositionAndShow();
    }

    public void CancelSession()
    {
        if (_session is not null) _session.State = DragSessionState.Cancelled;
        _session = null;
        _anchorPoint = null;
        _sessionTimer.Stop();
        _outsideSafeAreaSince = null;
        _autoDismissTimer.Stop();
        HideAnimated();
    }

    public void ShowSettings() => OpenDefaultApps();

    private void OpenDefaultApps()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true }); }
        catch { }
    }

    private static void OpenWithFile(string filePath)
    {
        try
        {
            var escapedPath = filePath.Replace("\"", "\\\"");
            Process.Start(new ProcessStartInfo("rundll32.exe", $"shell32.dll,OpenAs_RunDLL \"{escapedPath}\"") { UseShellExecute = true });
        }
        catch { }
    }

    private void PositionAndShow()
    {
        // WinForms 返回物理像素；WPF 的窗口位置使用 DIP，先按锚点所在显示器换算。
        var cursor = _anchorPoint ?? Forms.Cursor.Position;
        var screen = Forms.Screen.FromPoint(cursor);
        var area = screen?.WorkingArea ?? Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);
        var scale = DpiCoordinateConverter.GetScaleForPoint(cursor);
        PanelCard.BeginAnimation(UIElement.OpacityProperty, null);
        PanelTranslation.BeginAnimation(TranslateTransform.XProperty, null);
        PanelTranslation.BeginAnimation(TranslateTransform.YProperty, null);
        PanelScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        PanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        PanelCard.Opacity = 0;
        PanelTranslation.X = _sourceSide == EdgeDropWindow.EdgeSide.Right ? 28 : -28;
        PanelTranslation.Y = _sourceSide == EdgeDropWindow.EdgeSide.Top ? -18 : 0;
        PanelScale.ScaleX = 0.96;
        PanelScale.ScaleY = 0.96;
        Show();
        _outsideSafeAreaSince = null;
        if (_monitorMouseButton) _autoDismissTimer.Start();
        UpdateLayout();
        // SizeToContent 在 Show/UpdateLayout 后才有可靠的实际尺寸；优先读取 HWND 物理像素尺寸。
        var panelWidth = NativeWindowPlacement.TryGetBounds(this, out var currentBounds) && currentBounds.Width > 0
            ? currentBounds.Width
            : DpiCoordinateConverter.DipToPixels(Width > 0 ? Width : 404, scale);
        var panelHeight = currentBounds.Height > 0
            ? currentBounds.Height
            : DpiCoordinateConverter.DipToPixels(ActualHeight > 0 ? ActualHeight : Math.Min(MaxHeight, 320), scale);
        _panelFromRight = _sourceSide == EdgeDropWindow.EdgeSide.Right;
        _panelBelowAnchor = _sourceSide == EdgeDropWindow.EdgeSide.Top;
        var placement = OverlayPlacementMath.CalculatePixels(
            area,
            cursor,
            panelWidth,
            panelHeight,
            (OverlayPlacementSide)_sourceSide);
        NativeWindowPlacement.TrySetPosition(this, placement.Left, placement.Top);
        Activate();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(FocusInitialControl));
        if (!SystemParameters.ClientAreaAnimation)
        {
            PanelCard.Opacity = 1;
            PanelTranslation.X = 0;
            PanelTranslation.Y = 0;
            PanelScale.ScaleX = 1;
            PanelScale.ScaleY = 1;
            return;
        }
        var duration = Math.Clamp(_settings().AnimationDurationMs, 80, 600);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        PanelCard.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(duration)) { EasingFunction = easing });
        PanelTranslation.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(_panelFromRight ? 28 : -28, 0, TimeSpan.FromMilliseconds(duration + 60)) { EasingFunction = easing });
        PanelTranslation.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(_panelBelowAnchor ? -18 : 0, 0, TimeSpan.FromMilliseconds(duration + 60)) { EasingFunction = easing });
        PanelScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(duration + 60)) { EasingFunction = easing });
        PanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(duration + 60)) { EasingFunction = easing });
    }

    private void FocusInitialControl()
    {
        var target = _candidateSurface.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault(button => button.IsEnabled)
            ?? Actions.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault(button => button.IsEnabled);
        if (target is not null)
        {
            target.Focusable = true;
            Keyboard.Focus(target);
        }
    }

    private void AnimateFailure()
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(260) };
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(7, KeyTime.FromPercent(0.25)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(-5, KeyTime.FromPercent(0.5)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(3, KeyTime.FromPercent(0.75)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        PanelTranslation.BeginAnimation(TranslateTransform.XProperty, shake);
    }

    private void HideAnimated()
    {
        _outsideSafeAreaSince = null;
        _autoDismissTimer.Stop();
        if (!IsVisible) return;
        if (!SystemParameters.ClientAreaAnimation)
        {
            Hide();
            return;
        }

        var fade = new DoubleAnimation(PanelCard.Opacity, 0, TimeSpan.FromMilliseconds(130))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) => Hide();
        PanelCard.BeginAnimation(UIElement.OpacityProperty, fade);
        PanelTranslation.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, _panelFromRight ? 12 : -12, TimeSpan.FromMilliseconds(130)));
        PanelTranslation.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, _panelBelowAnchor ? -10 : 0, TimeSpan.FromMilliseconds(130)));
    }

    private void CheckAutoDismiss()
    {
        if (!IsVisible)
        {
            _autoDismissTimer.Stop();
            _outsideSafeAreaSince = null;
            return;
        }

        if (!NativeWindowPlacement.TryGetBounds(this, out var panelBounds) || panelBounds.Width <= 0 || panelBounds.Height <= 0)
            return;

        var anchor = _anchorPoint ?? Forms.Cursor.Position;
        var screen = Forms.Screen.FromPoint(anchor) ?? Forms.Screen.PrimaryScreen;
        if (screen is null) return;
        var settings = _settings();
        var side = (OverlayPlacementSide)_sourceSide;
        var configured = _sourceSide switch
        {
            EdgeDropWindow.EdgeSide.Right => settings.RightEdgeBand,
            EdgeDropWindow.EdgeSide.Top => settings.TopEdgeBand,
            _ => settings.LeftEdgeBand
        };
        var edgeBounds = EdgeBandLayoutMath.CalculateBounds(
            screen.WorkingArea,
            side,
            configured,
            settings.EdgeBandWidth);
        var cursor = Forms.Cursor.Position;
        var now = DateTime.UtcNow;
        if (OverlayDismissalPolicy.IsWithinSafeArea(cursor, panelBounds, edgeBounds))
        {
            _outsideSafeAreaSince = null;
            return;
        }

        _outsideSafeAreaSince ??= now;
        if (!OverlayDismissalPolicy.HasElapsed(_outsideSafeAreaSince, now, settings.AutoDismissDelayMs)) return;

        // Hide the panel while retaining the drag session; re-entering the source edge starts a fresh trigger delay.
        HideAnimated();
    }

    private static ImageSource? TryGetIcon(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            using var icon = DrawingIcon.ExtractAssociatedIcon(path);
            if (icon is null) return null;
            var handle = icon.ToBitmap().GetHbitmap();
            try { return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(36, 36)); }
            finally { DeleteObject(handle); }
        }
        catch { return null; }
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
