using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace EousGate;

// Requested position responds immediately; observed state remains on the tool.
public sealed class ToolSwitch : ToggleButton
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(nameof(State), typeof(ToolState), typeof(ToolSwitch), new PropertyMetadata(ToolState.Stopped, Changed));
    public static readonly DependencyProperty CanStartProperty = DependencyProperty.Register(nameof(CanStart), typeof(bool), typeof(ToolSwitch), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty CanStopProperty = DependencyProperty.Register(nameof(CanStop), typeof(bool), typeof(ToolSwitch), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty AnimationDurationProperty = DependencyProperty.Register(nameof(AnimationDuration), typeof(int), typeof(ToolSwitch), new PropertyMetadata(160));
    private static readonly DependencyPropertyKey IsBusyPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsBusy), typeof(bool), typeof(ToolSwitch), new PropertyMetadata(false));
    public static readonly DependencyProperty IsBusyProperty = IsBusyPropertyKey.DependencyProperty;

    public ToolState State { get => (ToolState)GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public bool CanStart { get => (bool)GetValue(CanStartProperty); set => SetValue(CanStartProperty, value); }
    public bool CanStop { get => (bool)GetValue(CanStopProperty); set => SetValue(CanStopProperty, value); }
    public int AnimationDuration { get => (int)GetValue(AnimationDurationProperty); set => SetValue(AnimationDurationProperty, value); }
    public bool IsBusy => (bool)GetValue(IsBusyProperty);
    public bool RequestsStart => _requestedOn ?? !_confirmedOn;

    private bool _confirmedOn;
    private bool? _requestedOn;
    private bool _dispatchingRequest;
    private TranslateTransform? _shift;
    private FrameworkElement? _onLayer;
    private RotateTransform? _spinner;
    private bool _listening;
    private bool? _visualOn;
    private bool _spinning;

    public ToolSwitch()
    {
        IsVisibleChanged += (_, _) => { if (IsVisible) Refresh(false); else StopAnimations(); };
        Loaded += (_, _) =>
        {
            if (!_listening) { SystemParameters.StaticPropertyChanged += SystemChanged; _listening = true; }
            Refresh(false);
        };
        Unloaded += (_, _) =>
        {
            if (_listening) { SystemParameters.StaticPropertyChanged -= SystemChanged; _listening = false; }
            StopAnimations();
        };
    }

    public override void OnApplyTemplate()
    {
        StopAnimations();
        base.OnApplyTemplate();
        var knob = GetTemplateChild("PART_Knob") as FrameworkElement;
        _shift = knob is null ? null : new TranslateTransform();
        if (knob is not null) knob.RenderTransform = _shift!;
        _onLayer = GetTemplateChild("PART_OnTrack") as FrameworkElement;
        var spinner = GetTemplateChild("PART_Spinner") as FrameworkElement;
        _spinner = spinner is null ? null : new RotateTransform();
        if (spinner is not null) spinner.RenderTransform = _spinner!;
        Refresh(false);
    }

    protected override void OnToggle() { } // The tool model is never changed by ToggleButton.
    protected override AutomationPeer OnCreateAutomationPeer() => new ToolSwitchAutomationPeer(this);
    internal void RequestToggle() => OnClick();
    protected override void OnClick()
    {
        if (IsBusy || !IsEnabled || (RequestsStart ? !CanStart : !CanStop)) return;
        _requestedOn = RequestsStart;
        _dispatchingRequest = true;
        Refresh(true);
        try { base.OnClick(); }
        finally { _dispatchingRequest = false; Refresh(true); }
    }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((ToolSwitch)sender).Refresh(true);
    private void SystemChanged(object? sender, PropertyChangedEventArgs e) => Refresh(false);

    private void Refresh(bool animate)
    {
        // Read one coherent model snapshot: State and capability notifications arrive separately.
        var tool = DataContext as IAssistantTool;
        var state = tool?.State ?? State;
        var canStart = tool?.CanStart ?? CanStart;
        var canStop = tool?.CanStop ?? CanStop;
        if (state is ToolState.Starting or ToolState.Stopping) _requestedOn = state == ToolState.Starting;
        else if (!_dispatchingRequest) _requestedOn = null;
        if (state == ToolState.Running) _confirmedOn = true;
        else if (state == ToolState.Stopped) _confirmedOn = false;
        // A failed external stop can still have live processes. Prefer the controller's capabilities.
        else if (state == ToolState.Failed)
        {
            if (canStop && !canStart) _confirmedOn = true;
            else if (canStart && !canStop) _confirmedOn = false;
        }
        var displayOn = _requestedOn ?? _confirmedOn;
        SetCurrentValue(IsCheckedProperty, displayOn);
        SetValue(IsBusyPropertyKey, _dispatchingRequest || state is ToolState.Starting or ToolState.Stopping);
        SetCurrentValue(IsEnabledProperty, !IsBusy && (_confirmedOn ? canStop : canStart));
        // State and capabilities notify separately. Do not restart the same transition
        // (or spinner) before WPF has even rendered its first frame.
        var motion = animate && IsLoaded && IsVisible && FluentMotion.IsEnabled;
        if (_visualOn != displayOn || !motion) UpdatePosition(motion, displayOn);
        var spin = _spinner is not null && IsBusy && IsLoaded && IsVisible && FluentMotion.IsEnabled;
        if (_spinning != spin)
        {
            _spinner?.BeginAnimation(RotateTransform.AngleProperty, spin
                ? new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(800)) { RepeatBehavior = RepeatBehavior.Forever }
                : null);
            _spinning = spin;
        }
    }

    private void UpdatePosition(bool motion, bool displayOn)
    {
        _visualOn = displayOn;
        var duration = Math.Clamp(AnimationDuration, 80, 600);
        if (_shift is not null)
        {
            var from = _shift.X;
            _shift.BeginAnimation(TranslateTransform.XProperty, null);
            _shift.X = displayOn ? 20 : 0;
            if (motion && Math.Abs(from - _shift.X) > .01)
                _shift.BeginAnimation(TranslateTransform.XProperty, FluentMotion.Animation(from, _shift.X, duration));
        }
        if (_onLayer is not null)
        {
            var from = _onLayer.Opacity;
            _onLayer.BeginAnimation(UIElement.OpacityProperty, null);
            _onLayer.Opacity = displayOn ? 1 : 0;
            if (motion && Math.Abs(from - _onLayer.Opacity) > .01)
                _onLayer.BeginAnimation(UIElement.OpacityProperty, FluentMotion.Animation(from, _onLayer.Opacity, duration));
        }
    }

    private void StopAnimations()
    {
        _shift?.BeginAnimation(TranslateTransform.XProperty, null);
        _onLayer?.BeginAnimation(UIElement.OpacityProperty, null);
        _spinner?.BeginAnimation(RotateTransform.AngleProperty, null);
        _spinning = false;
        _visualOn = null;
    }
}

// The standard ToggleButton peer calls OnToggle directly, which would bypass the async request.
internal sealed class ToolSwitchAutomationPeer(ToolSwitch owner) : ToggleButtonAutomationPeer(owner), IToggleProvider
{
    protected override string GetClassNameCore() => nameof(ToolSwitch);
    public override object GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Toggle ? this : base.GetPattern(patternInterface);
    void IToggleProvider.Toggle()
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        ((ToolSwitch)Owner).RequestToggle();
    }
    ToggleState IToggleProvider.ToggleState => ((ToolSwitch)Owner).IsChecked == true ? ToggleState.On : ToggleState.Off;
}
