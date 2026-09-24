using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Forms = System.Windows.Forms;
using System.IO;
using EousGate.Infrastructure;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using System.Windows.Threading;

namespace EousGate;

public partial class EdgeDropWindow : Window
{
    public enum EdgeSide { Left, Right, Top }

    private readonly OverlayWindow _overlay;
    private readonly IAppDiscovery _discovery;
    private readonly Func<bool> _isPaused;
    private readonly Func<UserSettings> _settings;
    private readonly DispatcherTimer _triggerTimer;
    private readonly EdgeSide _side;
    private IReadOnlyList<string>? _pendingFiles;
    private System.Windows.Media.Brush _normalBackground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.RoyalBlue);

    public EdgeDropWindow(OverlayWindow overlay, IAppDiscovery discovery, Func<bool> isPaused, Func<UserSettings> settings, EdgeSide side = EdgeSide.Left)
    {
        InitializeComponent();
        _overlay = overlay;
        _discovery = discovery;
        _isPaused = isPaused;
        _settings = settings;
        _side = side;
        _triggerTimer = new DispatcherTimer();
        _triggerTimer.Tick += (_, _) =>
        {
            // 拖拽进入接收条后延时触发，使用触发时的屏幕坐标作为面板锚点。
            _triggerTimer.Stop();
            var filePaths = _pendingFiles;
            _pendingFiles = null;
            if (filePaths is null || _isPaused()) return;
            var session = new DragSession(filePaths) { State = DragSessionState.PanelShown };
            var cursor = Forms.Cursor.Position;
            _overlay.BeginSession(session, _discovery.GetCandidates(filePaths), cursor, _side);
        };
        Loaded += (_, _) => PositionAtPrimaryEdge();
        DragEnter += OnDragEnter;
        DragOver += OnDragOver;
        DragLeave += (_, _) =>
        {
            CancelPendingTrigger();
            StopBandPulse();
            ResetDragAppearance();
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) _overlay.CancelSession(); };
    }

    public void ApplyAppearance(UserSettings settings)
    {
        Opacity = Math.Clamp(settings.EdgeBandOpacity, 0.05, 1);
        try
        {
            _normalBackground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(settings.EdgeBandColor));
            Surface.Background = _normalBackground;
        }
        catch { Surface.Background = _normalBackground; }
        Surface.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x66, 0x93, 0xDF, 0xFF));
        var show = (settings.EdgeBandPosition ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(position => position.Equals("Both", StringComparison.OrdinalIgnoreCase)
                ? _side is EdgeSide.Left or EdgeSide.Right
                : position.Equals(_side.ToString(), StringComparison.OrdinalIgnoreCase));
        IsEnabled = show && !settings.Paused;
        if (show && !IsVisible && IsLoaded) Show();
        if (!show && IsVisible) Hide();
        if (IsLoaded) PositionAtPrimaryEdge();
    }

    private void PositionAtPrimaryEdge()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);
        var scale = DpiCoordinateConverter.GetScaleForPoint(new System.Drawing.Point(area.Left + 1, area.Top + 1));
        var settings = _settings();
        var side = (OverlayPlacementSide)_side;
        var configured = _side switch
        {
            EdgeSide.Right => settings.RightEdgeBand,
            EdgeSide.Top => settings.TopEdgeBand,
            _ => settings.LeftEdgeBand
        };
        var bounds = EdgeBandLayoutMath.CalculateBounds(area, side, configured, settings.EdgeBandWidth);
        Width = DpiCoordinateConverter.PixelsToDip(bounds.Width, scale);
        Height = DpiCoordinateConverter.PixelsToDip(bounds.Height, scale);
        Left = DpiCoordinateConverter.PixelsToDip(bounds.Left, scale);
        Top = DpiCoordinateConverter.PixelsToDip(bounds.Top, scale);

        // Window.Left/Top are DPI-sensitive; SetWindowPos keeps the edge hit target in physical pixels.
        NativeWindowPlacement.TrySetPosition(this, bounds.Left, bounds.Top);
    }

    private void OnDragEnter(object sender, System.Windows.DragEventArgs e)
    {
        // 接收条通过 DragEnter 响应“拖着文件移动到边缘”，不是普通 MouseEnter。
        CancelPendingTrigger();
        if (_isPaused()) return;
        StartBandPulse();
        Surface.Background = (System.Windows.Media.Brush)FindResource("EdgeBandActive");
        Surface.BorderBrush = (System.Windows.Media.Brush)FindResource("PanelAccent");
        e.Effects = WpfDragDropEffects.None;
        if (!e.Data.GetDataPresent(WpfDataFormats.FileDrop)) return;
        var files = e.Data.GetData(WpfDataFormats.FileDrop) as string[];
        if (!FileDropPolicy.TryGetFiles(files, Directory.Exists, out var filePaths))
        {
            _overlay.ShowUnsupported(_side);
            return;
        }
        _pendingFiles = filePaths;
        _triggerTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(_settings().TriggerDelayMs, 0, 1000));
        _triggerTimer.Start();
        e.Effects = WpfDragDropEffects.Copy;
    }

    private void OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (_pendingFiles is not null || _overlay.IsVisible) e.Effects = WpfDragDropEffects.Copy;
    }

    private void CancelPendingTrigger()
    {
        _triggerTimer.Stop();
        _pendingFiles = null;
    }

    private void StartBandPulse()
    {
        if (!_settings().EdgePulseEnabled) return;
        BandScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 1.45, TimeSpan.FromMilliseconds(420))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });
        Surface.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.62, 1, TimeSpan.FromMilliseconds(420))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });
    }

    private void StopBandPulse()
    {
        BandScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        BandScale.ScaleX = 1;
        Surface.BeginAnimation(UIElement.OpacityProperty, null);
        Surface.Opacity = 1;
    }

    private void ResetDragAppearance()
    {
        Surface.Background = _normalBackground;
        Surface.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x66, 0x93, 0xDF, 0xFF));
    }
}
