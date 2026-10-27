using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EousGate;

// Figma A: cubic-bezier(.2,0,0,1). Timings are ratios of the 160 ms reference.
// Base values always describe the final layout; stopping a clock reveals that layout.
internal sealed class FluentMotion : IDisposable
{
    private readonly HashSet<FrameworkElement> _targets = [];
    private bool _disposed;
    internal static bool IsEnabled => SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
    internal static double Scale(int milliseconds) => Math.Clamp(milliseconds, 80, 600) / 160d;

    internal FluentMotion() => SystemParameters.StaticPropertyChanged += SystemChanged;

    internal void Enter(FrameworkElement target, int milliseconds, double delay = 0, double duration = 160, double x = 0, double y = 6, bool? enabled = null)
    {
        if (_disposed) return;
        Stop(target);
        var shift = new TranslateTransform();
        target.RenderTransform = shift;
        target.Opacity = 1;
        if (!(enabled ?? IsEnabled)) return;
        _targets.Add(target);
        var scale = Scale(milliseconds);
        target.BeginAnimation(UIElement.OpacityProperty, Animation(0, 1, duration * scale, delay * scale));
        shift.BeginAnimation(TranslateTransform.XProperty, Animation(x, 0, duration * scale, delay * scale));
        shift.BeginAnimation(TranslateTransform.YProperty, Animation(y, 0, duration * scale, delay * scale));
        // Immediate interaction exposes the target, including during a delayed entrance.
        target.MouseEnter += Reveal;
        target.GotKeyboardFocus += Reveal;
        target.Unloaded += Unloaded;
    }

    internal static DoubleAnimationUsingKeyFrames Animation(double from, double to, double duration, double delay = 0)
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(delay + duration), FillBehavior = FillBehavior.Stop };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        if (delay > 0) animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay))));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay + duration)), new KeySpline(.2, 0, 0, 1)));
        return animation;
    }

    private void Reveal(object sender, RoutedEventArgs e) => Stop((FrameworkElement)sender);
    private void Unloaded(object sender, RoutedEventArgs e) => Stop((FrameworkElement)sender);
    private void SystemChanged(object? sender, PropertyChangedEventArgs e) { if (!IsEnabled) StopAll(); }

    internal void Stop(FrameworkElement target)
    {
        if (!_targets.Remove(target)) return;
        target.MouseEnter -= Reveal; target.GotKeyboardFocus -= Reveal; target.Unloaded -= Unloaded;
        target.BeginAnimation(UIElement.OpacityProperty, null);
        if (target.RenderTransform is TranslateTransform shift)
        {
            shift.BeginAnimation(TranslateTransform.XProperty, null);
            shift.BeginAnimation(TranslateTransform.YProperty, null);
            shift.X = shift.Y = 0;
        }
        target.Opacity = 1;
    }
    internal void StopAll() { foreach (var target in _targets.ToArray()) Stop(target); }
    public void Dispose() { StopAll(); _disposed = true; SystemParameters.StaticPropertyChanged -= SystemChanged; }
}
