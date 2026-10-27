using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using EousGate.Infrastructure;
using System.Windows.Media;

namespace EousGate;

public partial class AssistantWindow : Window
{
    private readonly ToolManager _manager;
    private readonly Action _generalSettings;
    private readonly Action? _addProgram;
    private readonly Action? _about;
    internal string CurrentTheme { get; private set; } = "Light";
    private readonly HashSet<IAssistantTool> _observed = [];
    private bool _busy;
    private readonly FluentMotion _motion = new();
    private bool _fluent;
    private int _duration;
    internal AssistantWindow(ToolManager manager, Action generalSettings, string skin, int animationDuration = 320, Action? addProgram = null, bool largeText = false, string theme = "Light", Action? about = null)
    {
        InitializeComponent(); _manager = manager; _generalSettings = generalSettings; _addProgram = addProgram; _about = about;
        AddProgramButton.Visibility = addProgram is null ? Visibility.Collapsed : Visibility.Visible;
        manager.CollectionChanged += RefreshTools;
        RefreshTools(this, EventArgs.Empty);
        Closed += (_, _) => { _motion.Dispose(); manager.CollectionChanged -= RefreshTools; foreach (var tool in _observed) tool.PropertyChanged -= ToolChanged; _observed.Clear(); };
        ApplyAppearance(skin, animationDuration, largeText, theme);
        SetActiveSection("overview");
        Loaded += (_, _) => AnimateEntrance();
        IsVisibleChanged += (_, _) => { if (!IsVisible) _motion.StopAll(); };
        UpdateSummary();
    }
    private void RefreshTools(object? sender, EventArgs e)
    {
        var reveal = _observed.Count == 0 ? null : _manager.Tools.LastOrDefault(t => t.Descriptor.Kind == ToolKind.External && !_observed.Contains(t));
        foreach (var old in _observed.Where(t => !_manager.Tools.Contains(t)).ToArray()) { old.PropertyChanged -= ToolChanged; _observed.Remove(old); }
        foreach (var tool in _manager.Tools) if (_observed.Add(tool)) tool.PropertyChanged += ToolChanged;
        ToolList.ItemsSource = _manager.Tools.Where(t => t.Descriptor.Kind == ToolKind.BuiltIn).ToArray();
        ExternalToolList.ItemsSource = _manager.Tools.Where(t => t.Descriptor.Kind == ToolKind.External).ToArray();
        UpdateSummary();
        if (reveal is not null) Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (IsVisible && ExternalToolList.ItemContainerGenerator.ContainerFromItem(reveal) is FrameworkElement item) item.BringIntoView();
        }));
    }
    internal void ApplyAppearance(string skin, int animationDuration, bool largeText = false, string theme = "Light")
    {
        _motion.StopAll();
        _fluent = true;
        _duration = animationDuration;
        CurrentTheme = theme;
        WorkbenchTheme.Apply(Resources, theme);
        Resources["ToolSwitchAnimationDuration"] = Math.Clamp(animationDuration, 80, 180);
        Resources["WorkbenchBodySize"] = largeText ? 15d : 13d;
    }
    private void FluentRowLoaded(object sender, RoutedEventArgs e)
    {
        if (!_fluent || sender is not FrameworkElement { DataContext: IAssistantTool tool } row) return;
        var index = _manager.Tools.ToList().IndexOf(tool);
        var delay = index < 3 ? 60 + index * 30 : 120 + (index - 3) * 30;
        _motion.Enter(row, _duration, delay, y: 6);
    }
    private void AnimateEntrance()
    {
        if (!_fluent) return;
        _motion.Enter(HomeHeading, _duration, y: 8);
        _motion.Enter(NativeHeading, _duration, 40, 140, y: 4);
        _motion.Enter(ExternalHeading, _duration, 100, 140, y: 4);
        _motion.Enter(FluentFooter, _duration, 140, y: 4);
    }
    private void ToolChanged(object? sender, PropertyChangedEventArgs e) => UpdateSummary();
    private void UpdateSummary()
    {
        NativeCount.Text = $"{_manager.Tools.Count(t => t.Descriptor.Kind == ToolKind.BuiltIn)}";
        ExternalCount.Text = $"{_manager.Tools.Count(t => t.Descriptor.Kind == ToolKind.External)}";
    }
    internal void Report(string text)
    {
        Feedback.Text = text;
        Feedback.Visibility = Visibility.Visible;
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true; ToolArea.IsEnabled = BatchActions.IsEnabled = false; Report("正在处理…");
        try
        {
            await action();
            var failed = _manager.Tools.Count(t => t.State == ToolState.Failed);
            Report(_manager.BatchErrors.Count > 0 ? string.Join("；", _manager.BatchErrors) : failed > 0 ? $"操作完成，{failed} 项失败，请查看对应工具的原因。" : "操作完成");
        }
        catch (Exception ex) { Report(ex.Message); }
        finally { _busy = false; ToolArea.IsEnabled = BatchActions.IsEnabled = true; UpdateSummary(); }
    }
    private async void StartClick(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is IAssistantTool tool) await RunAsync(tool.StartAsync); }
    private async void StopClick(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is IAssistantTool tool) await RunAsync(tool.StopAsync); }
    private async void ToolSwitchClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToolSwitch control || control.Tag is not IAssistantTool tool || tool.State is ToolState.Starting or ToolState.Stopping) return;
        var start = control.RequestsStart;
        if (start ? tool.CanStart : tool.CanStop) await RunAsync(start ? tool.StartAsync : tool.StopAsync);
    }
    private void SettingsClick(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is IAssistantTool tool) tool.OpenSettings(); }
    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void ClosePanelClick(object sender, RoutedEventArgs e) => Close();
    private void GeneralSettingsClick(object sender, RoutedEventArgs e) => _generalSettings();
    private void AboutClick(object sender, RoutedEventArgs e) => (_about ?? _generalSettings)();
    private void OverviewClick(object sender, RoutedEventArgs e) { SetActiveSection("overview"); ToolArea.ScrollToHome(); }
    private void NativeNavigationClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id }) _manager.Tools.FirstOrDefault(t => t.Descriptor.Id == id)?.OpenSettings();
    }
    internal void SetActiveSection(string id)
    {
        foreach (var button in new[] { NavOverview, NavFile, NavWeb, NavFolder, NavGeneral, NavAbout })
        {
            var active = Equals(button.Tag, id);
            button.SetResourceReference(BackgroundProperty, active ? "PanelSelection" : "PanelBackground");
            button.SetResourceReference(ForegroundProperty, active ? "PanelForeground" : "PanelMutedForeground");
            button.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }
    private void AddProgramClick(object sender, RoutedEventArgs e) { if (!_busy) _addProgram?.Invoke(); }
    private async void BatchClick(object sender, RoutedEventArgs e)
    {
        var tag = ((Button)sender).Tag.ToString()!;
        await RunAsync(() => _manager.RunAsync(tag.StartsWith("start"), tag.EndsWith("selected")));
    }
}
