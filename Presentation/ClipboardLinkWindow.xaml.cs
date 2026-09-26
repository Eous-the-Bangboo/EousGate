using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using EousGate.Infrastructure;
using Button = System.Windows.Controls.Button;

namespace EousGate;

public partial class ClipboardLinkWindow : Window
{
    private readonly Func<UserSettings> _settings;
    private readonly WebLinkOpener _opener;
    private readonly NotificationLifetime _lifetime = new();
    private readonly DispatcherTimer _timer;
    private long _lastTick;
    private bool _allowClose;

    public ClipboardLinkWindow(Func<UserSettings> settings, WebLinkOpener? opener = null)
    {
        InitializeComponent();
        _settings = settings;
        _opener = opener ?? new WebLinkOpener();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += Tick;
        Closed += (_, _) => { _timer.Stop(); _timer.Tick -= Tick; };
        Closing += (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            Dismiss();
        };
        SizeChanged += (_, _) => { if (IsVisible) PositionAtEdge(); };
    }

    public void ShowLinks(IReadOnlyList<Uri> links)
    {
        if (links.Count == 0) { Dismiss(); return; }
        Links.ItemsSource = links;
        LinkScroller.ScrollToTop();
        Subtitle.Text = links.Count == 1 ? "点击链接，用默认浏览器打开" : $"检测到 {links.Count} 个链接，点击即可打开";
        ErrorText.Visibility = Visibility.Collapsed;
        RestartLifetime();
        PositionAtEdge();
        if (!IsVisible) Show(); // ShowActivated=false keeps typing focus in the source app.
        UpdateLayout();
        PositionAtEdge();
        Slide.BeginAnimation(TranslateTransform.XProperty, null);
        Slide.X = 0;
        if (SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast)
        {
            var offset = _settings().ClipboardPopupSide == "Left" ? -24d : 24d;
            Slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(offset, 0,
                TimeSpan.FromMilliseconds(Math.Clamp(_settings().AnimationDurationMs, 80, 600)))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
    }

    public void RefreshSettings()
    {
        if (!IsVisible) return;
        RestartLifetime();
        PositionAtEdge();
    }

    private void RestartLifetime()
    {
        _lifetime.Restart(_settings().ClipboardPopupDurationSeconds);
        _lastTick = Stopwatch.GetTimestamp();
        Countdown.Text = $"{Math.Ceiling(_lifetime.RemainingSeconds):0} 秒后收起 · 移入暂停";
        _timer.Start();
    }

    private void Tick(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        var interacting = IsMouseOver || IsKeyboardFocusWithin;
        var expired = _lifetime.Advance(Stopwatch.GetElapsedTime(_lastTick, now), interacting);
        _lastTick = now;
        Countdown.Text = interacting ? "已暂停收起 · 移出后继续" : $"{Math.Ceiling(_lifetime.RemainingSeconds):0} 秒后收起 · 移入暂停";
        if (expired) Dismiss();
    }

    private void PositionAtEdge()
    {
        var area = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);
        var scale = DpiCoordinateConverter.GetScaleForPoint(new System.Drawing.Point(area.Left, area.Top));
        var work = DpiCoordinateConverter.ToDip(area, scale);
        Width = Math.Min(380, work.Width);
        MaxHeight = work.Height;
        LinkScroller.MaxHeight = Math.Max(60, Math.Min(280, work.Height - 190));
        Left = _settings().ClipboardPopupSide == "Left" ? work.Left : Math.Max(work.Left, work.Right - Width);
        Top = Math.Max(work.Top, work.Bottom - (ActualHeight > 0 ? ActualHeight : 230) - 8);
    }

    private void OpenLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Uri uri }) return;
        var result = _opener.Open(uri);
        if (result.Success) Dismiss();
        else
        {
            ErrorText.Text = result.Error;
            ErrorText.Visibility = Visibility.Visible;
            RestartLifetime();
        }
    }

    private void DismissClick(object sender, RoutedEventArgs e) => Dismiss();
    private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Dismiss(); e.Handled = true; }
    }

    public void Dismiss()
    {
        _timer.Stop();
        Hide();
        Links.ItemsSource = null;
        ErrorText.Text = "";
    }

    public void Shutdown()
    {
        _allowClose = true;
        Close();
    }
}
