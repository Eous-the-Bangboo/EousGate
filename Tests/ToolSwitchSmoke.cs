using EousGate;
using EousGate.Infrastructure;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows.Threading;

internal static class ToolSwitchSmoke
{
    internal static void Verify()
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        try { VerifyCore(); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
    private static void VerifyCore()
    {
        var startGate = new TaskCompletionSource();
        var stopGate = new TaskCompletionSource();
        var starts = 0; var stops = 0;
        var tool = new ManagedTool(new("switch-test", "SwitchTest", "开关测试", "", "", ToolKind.External),
            async () => { starts++; await startGate.Task; }, async () => { stops++; await stopGate.Task; }, () => { }, selected: false);
        var manager = new ToolManager(); manager.Register(tool);
        var window = new AssistantWindow(manager, () => { }, SkinCatalog.DefaultId);
        var root = (FrameworkElement)window.Content;
        void Layout() { root.Measure(new Size(1024, 710)); root.Arrange(new Rect(0, 0, 1024, 710)); root.UpdateLayout(); }
        Layout();
        var control = Descendants<ToolSwitch>(root).Single();
        var peer = UIElementAutomationPeer.CreatePeerForElement(control)!;
        var provider = (IToggleProvider)peer.GetPattern(PatternInterface.Toggle);
        Check(control.IsChecked == false && control.IsEnabled && !tool.Selected, "stopped switch must be off without changing batch selection");
        provider.Toggle(); Layout();
        Check(tool.State == ToolState.Starting && control.IsBusy && !control.IsEnabled && provider.ToggleState == ToggleState.On && starts == 1,
            "start must immediately request on while the model is still starting");
        Check(Equals(Descendants<System.Windows.Controls.Button>(root).Single(b => ReferenceEquals(b.Tag, tool)).Content, "设置"), "pending external tool must retain its settings label instead of offering path repair");
        try { provider.Toggle(); throw new Exception("busy UIA control accepted repeated input"); } catch (ElementNotEnabledException) { }
        Check(starts == 1, "repeated input must not start twice");
        startGate.SetResult(); Pump(); Layout();
        Check(tool.State == ToolState.Running && control.IsChecked == true && control.IsEnabled && !control.IsBusy && !tool.Selected,
            "successful start must move right without changing selection");
        provider.Toggle(); Layout();
        Check(tool.State == ToolState.Stopping && control.IsBusy && control.IsChecked == false && stops == 1, "stop must immediately request off while pending");
        stopGate.SetException(new InvalidOperationException("测试停止失败")); Pump(); Layout();
        Check(tool.State == ToolState.Failed && control.IsChecked == true && control.IsEnabled && !control.RequestsStart, "failed stop must retain on and offer stop retry");
        window.Close();

        var failure = new ManagedTool(new("failure", "Failure", "启动失败", "", ""), () => throw new InvalidOperationException("测试启动失败"), () => Task.CompletedTask, () => { });
        var failedSwitch = Bind(failure);
        failedSwitch.Click += async (_, _) => await failure.StartAsync();
        ((IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(failedSwitch)!.GetPattern(PatternInterface.Toggle)).Toggle(); Pump();
        Check(failure.State == ToolState.Failed && failedSwitch.IsChecked == false && failedSwitch.IsEnabled, "failed start must remain off and permit retry");

        var observed = new ObservedTool();
        var observedSwitch = Bind(observed);
        observed.Observe(ToolState.Running, false, true);
        Check(observedSwitch.IsChecked == true, "external manual start must update the switch");
        observed.Observe(ToolState.Failed, false, true);
        Check(observedSwitch.IsChecked == true && observedSwitch.IsEnabled, "failed external stop with live processes must remain on");
        observed.Observe(ToolState.Stopped, true, false);
        Check(observedSwitch.IsChecked == false, "external manual exit must turn the switch off");
        observed.Observe(ToolState.Failed, false, false);
        Check(!observedSwitch.IsEnabled, "unavailable or undetected tools must not accept requests");
        VerifyAnimation();
        Console.WriteLine("PASS apple-green switches: real UIA requests, selection isolation, pending/repeated input, start failure, stop failure and external observation.");
    }
    private static void VerifyAnimation()
    {
        var observed = new ObservedTool();
        var control = Bind(observed);
        control.AnimationDuration = 600;
        // A hidden native host loads the visual without activating a desktop window.
        using var source = new HwndSource(new HwndSourceParameters("Switch animation regression") { Width = 100, Height = 60, WindowStyle = 0 });
        source.RootVisual = control;
        Pump();
        Check(control.IsLoaded && control.IsVisible, "native host must load the switch for real animation eligibility");
        var knob = (FrameworkElement)control.Template.FindName("PART_Knob", control);
        var track = (FrameworkElement)control.Template.FindName("PART_OnTrack", control);
        var shift = (TranslateTransform)knob.RenderTransform;
        observed.Observe(ToolState.Running, false, true);
        if (FluentMotion.IsEnabled)
        {
            Check(shift.HasAnimatedProperties && track.HasAnimatedProperties, "confirmed state must animate both knob and track");
            // Seek to a stable mid-frame, then send redundant monitor/capability notifications.
            var clock = FluentMotion.Animation(0, 20, 600).CreateClock();
            shift.ApplyAnimationClock(TranslateTransform.XProperty, clock);
            clock.Controller!.SeekAlignedToLastTick(TimeSpan.FromMilliseconds(80), TimeSeekOrigin.BeginTime);
            var position = shift.X;
            Check(position is > 0 and < 20, "knob must interpolate during the transition");
            observed.Observe(ToolState.Running, false, true);
            Check(Math.Abs(shift.X - position) < .001, "duplicate notifications must preserve the current animation clock and position");
            clock.Controller.SeekAlignedToLastTick(TimeSpan.FromMilliseconds(700), TimeSeekOrigin.BeginTime);
            Check(shift.X == 20, "switch must settle at the confirmed running position");
            observed.Observe(ToolState.Stopped, true, false);
            Check(shift.HasAnimatedProperties && !control.IsChecked!.Value, "reverse transition must animate while semantics update immediately");
        }
        else Check(shift.X == 20 && track.Opacity == 1, "system reduced motion must immediately expose final state");
        control.Visibility = Visibility.Hidden;
        Check(!shift.HasAnimatedProperties && !track.HasAnimatedProperties, "hiding must release animation clocks");
        observed.Observe(ToolState.Stopped, true, false);
        control.Visibility = Visibility.Visible;
        Check(shift.X == 0 && track.Opacity == 0, "restoring must show the current state without replaying motion");
        Console.WriteLine("PASS switch animation: interpolated knob/track, duplicate observation continuity, reverse transition and hide/restore cleanup.");
    }
    private static ToolSwitch Bind(IAssistantTool tool)
    {
        var control = new ToolSwitch { DataContext = tool, Style = (Style)System.Windows.Application.Current.FindResource("AppleToolSwitch") };
        control.SetBinding(ToolSwitch.StateProperty, new Binding(nameof(tool.State)));
        control.SetBinding(ToolSwitch.CanStartProperty, new Binding(nameof(tool.CanStart)));
        control.SetBinding(ToolSwitch.CanStopProperty, new Binding(nameof(tool.CanStop)));
        control.ApplyTemplate(); return control;
    }
    private sealed class ObservedTool : IAssistantTool
    {
        public ToolDescriptor Descriptor => new("observed", "Observed", "外部测试", "", "", ToolKind.External);
        public ToolState State { get; private set; }
        public string? Error => null;
        public bool Selected { get; set; }
        public string StateText => State.ToString();
        public bool CanStart { get; private set; } = true;
        public bool CanStop { get; private set; }
        public bool CanOpenSettings => true;
        public event PropertyChangedEventHandler? PropertyChanged;
        internal void Observe(ToolState state, bool start, bool stop)
        {
            State = state; CanStart = start; CanStop = stop;
            foreach (var name in new[] { nameof(State), nameof(CanStart), nameof(CanStop) }) PropertyChanged?.Invoke(this, new(name));
        }
        public Task StartAsync() => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void OpenSettings() { }
    }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
