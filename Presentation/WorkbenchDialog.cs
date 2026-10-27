using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Runtime.CompilerServices;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace EousGate;

// An owned window spans the workbench, so outside clicks hit its backdrop.
// Existing editors keep their validation, save handlers and native file pickers.
internal sealed class WorkbenchDialog
{
    private static readonly ConditionalWeakTable<Window, WorkbenchDialog> Instances = new();
    private readonly Window _window;
    private readonly AssistantWindow _owner;
    private readonly Border _card;
    private readonly Grid _backdrop;
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _shift = new();
    private readonly TextBlock _heading;
    private readonly int _duration;
    private readonly double _preferredWidth;
    private readonly IInputElement? _returnFocus;
    private bool _allowClose, _closing, _ownerClosing;
    private bool _discardConfirmed;
    private Grid? _confirmation;
    private Action? _cancelConfirmation;

    internal static void Attach(Window window, AssistantWindow owner, int duration)
    {
        if (!Instances.TryGetValue(window, out _)) Instances.Add(window, new(window, owner, duration));
    }

    internal static void Refresh(Window window)
    {
        if (Instances.TryGetValue(window, out var dialog)) dialog.Position();
    }

    private WorkbenchDialog(Window window, AssistantWindow owner, int duration)
    {
        _window = window; _owner = owner; _duration = Math.Clamp(duration, 80, 400);
        _preferredWidth = 720;
        _returnFocus = Keyboard.FocusedElement;
        var content = (FrameworkElement)window.Content;
        if (window is not SettingsWindow) content.Margin = new Thickness(24);
        window.Content = null;
        WorkbenchTheme.Apply(window.Resources, owner.CurrentTheme);
        window.Resources["WorkbenchDialogMode"] = true;
        window.Resources[typeof(CheckBox)] = window.FindResource("WorkbenchCheckBox");
        window.Resources[typeof(Expander)] = window.FindResource("WorkbenchExpander");
        window.Resources[typeof(System.Windows.Controls.TextBox)] = window.FindResource("WorkbenchTextBox");
        window.Owner = owner;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.AllowsTransparency = true;
        window.ShowInTaskbar = false;
        window.Background = Brushes.Transparent;
        window.MinWidth = window.MinHeight = 0;
        window.SizeToContent = SizeToContent.Manual;
        _backdrop = new Grid { Background = new SolidColorBrush(Color.FromArgb(38, 34, 37, 41)) };
        KeyboardNavigation.SetTabNavigation(_backdrop, KeyboardNavigationMode.Cycle);
        _card = new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Stretch,
            RenderTransformOrigin = new Point(.5, .5), RenderTransform = new TransformGroup { Children = { _scale, _shift } }, ClipToBounds = true };
        _card.SetResourceReference(Border.BackgroundProperty, "PanelElevatedSurface");
        _card.SetResourceReference(Border.BorderBrushProperty, "PanelBorder");
        var layout = new Grid();
        layout.RowDefinitions.Add(new() { Height = new GridLength(68) });
        layout.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var header = new DockPanel { Margin = new Thickness(24, 10, 16, 10), LastChildFill = true };
        var close = new Button { Content = "\uE8BB", Style = (Style)window.FindResource("WorkbenchIconButton"), ToolTip = "关闭设置" };
        System.Windows.Automation.AutomationProperties.SetName(close, "关闭设置");
        close.Click += (_, _) => window.Close();
        DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        _heading = new TextBlock { Text = window is SettingsWindow editor ? editor.DrawerTitle : window is FolderSettingsWindow ? "文件夹快跳" : window.Title,
            FontSize = 22, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(_heading);
        layout.Children.Add(header);
        Grid.SetRow(content, 1); layout.Children.Add(content);
        _card.Child = layout; _backdrop.Children.Add(_card); window.Content = _backdrop;
        if (window is SettingsWindow settings) { settings.UseWorkbenchLayout(); settings.SectionChanged += SectionChanged; }
        UpdateSelection();
        if (window.FindName("BrandHeader") is FrameworkElement oldHeader) oldHeader.Visibility = Visibility.Collapsed;
        if (window.FindName("BrandHeaderRow") is RowDefinition oldRow) oldRow.Height = new GridLength(0);
        foreach (var name in new[] { "EditorTitle", "ProgramTitle", "EditorHeading" })
            if (window.FindName(name) is FrameworkElement title) title.Visibility = Visibility.Collapsed;
        if (window is FolderSettingsWindow && content is StackPanel { Children.Count: > 0 } folderContent)
            ((UIElement)folderContent.Children[0]).Visibility = Visibility.Collapsed;
        if (window.FindName("Advanced") is Expander advanced)
        {
            advanced.Expanded += (_, _) => Position();
            advanced.Collapsed += (_, _) => Position();
        }
        _backdrop.MouseDown += (_, e) => { if (ReferenceEquals(e.OriginalSource, _backdrop)) window.Close(); };
        window.KeyDown += (_, e) => { if (e.Key == Key.Escape) { if (_cancelConfirmation is { } cancel) cancel(); else window.Close(); e.Handled = true; } };
        owner.LocationChanged += OwnerMoved; owner.SizeChanged += OwnerSized; owner.Closing += OwnerClosing;
        window.Loaded += Loaded; window.Closing += Closing; window.Closed += Closed;
        Position();
    }

    private void Position()
    {
        if (_owner.WindowState == WindowState.Minimized) return;
        var position = new Point(_owner.Left, _owner.Top);
        if (_owner.WindowState == WindowState.Maximized && PresentationSource.FromVisual(_owner)?.CompositionTarget is { } target)
            position = target.TransformFromDevice.Transform(_owner.PointToScreen(new Point()));
        _window.Left = position.X; _window.Top = position.Y + 40;
        _window.Width = _owner.ActualWidth > 0 ? _owner.ActualWidth : _owner.Width;
        _window.Height = Math.Max(1, (_owner.ActualHeight > 0 ? _owner.ActualHeight : _owner.Height) - 40);
        _card.Width = Math.Min(_preferredWidth, Math.Max(1, _window.Width - 184));
        _card.Height = _window.Height;
    }
    private void OwnerMoved(object? sender, EventArgs e) => Position();
    private void OwnerSized(object sender, SizeChangedEventArgs e) => Position();
    private void OwnerClosing(object? sender, CancelEventArgs e)
    {
        if (_window is CustomProgramWindow { IsSaving: true } or ExternalProgramSettingsWindow { IsSaving: true }) { e.Cancel = true; return; }
        if (!_discardConfirmed && _window is SettingsWindow { HasUnsavedEdits: true } settings) { e.Cancel = true; ConfirmUnsaved(settings); return; }
        _ownerClosing = true;
        _owner.Dispatcher.BeginInvoke(new Action(() => { if (_owner.IsVisible) _ownerClosing = false; }));
    }
    private void SectionChanged(object? sender, EventArgs e) => UpdateSelection();
    private void UpdateSelection()
    {
        if (_window is SettingsWindow settings)
        {
            _heading.Text = settings.DrawerTitle;
            _owner.SetActiveSection(settings.CurrentSection switch { 1 or 3 => "file-hop", 2 => "web-hop", 4 => "about", _ => "general" });
        }
        else _owner.SetActiveSection(_window is FolderSettingsWindow ? "folder-hop" : "overview");
    }
    private void Loaded(object sender, RoutedEventArgs e)
    {
        if (_closing || !FluentMotion.IsEnabled) return;
        var scaleIn = FluentMotion.Animation(36, 0, _duration);
        scaleIn.Completed += (_, _) =>
        {
            if (_closing) return;
            _shift.BeginAnimation(TranslateTransform.XProperty, null); _shift.X = 0;
            _card.BeginAnimation(UIElement.OpacityProperty, null); _backdrop.BeginAnimation(UIElement.OpacityProperty, null);
        };
        _card.BeginAnimation(UIElement.OpacityProperty, FluentMotion.Animation(0, 1, _duration * 1.35));
        _shift.BeginAnimation(TranslateTransform.XProperty, scaleIn);
        _backdrop.BeginAnimation(UIElement.OpacityProperty, FluentMotion.Animation(0, 1, _duration));
    }
    private void Closing(object? sender, CancelEventArgs e)
    {
        if (e.Cancel || _ownerClosing) return;
        if (!_discardConfirmed && _window is SettingsWindow { HasUnsavedEdits: true } settings)
        {
            e.Cancel = true; ConfirmUnsaved(settings); return;
        }
        if (_allowClose || !FluentMotion.IsEnabled) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true; _backdrop.IsHitTestVisible = false;
        var animation = FluentMotion.Animation(_card.Opacity, 0, _duration);
        animation.Completed += (_, _) => { _allowClose = true; _window.Close(); };
        _card.BeginAnimation(UIElement.OpacityProperty, animation);
        _backdrop.BeginAnimation(UIElement.OpacityProperty, FluentMotion.Animation(_backdrop.Opacity, 0, _duration));
        _shift.BeginAnimation(TranslateTransform.XProperty, FluentMotion.Animation(_shift.X, 36, _duration));
    }
    private void ConfirmUnsaved(SettingsWindow settings)
    {
        if (_confirmation is not null || _card.Child is not Grid layout) return;
        foreach (UIElement child in layout.Children) child.IsEnabled = false;
        _confirmation = new Grid { Background = new SolidColorBrush(Color.FromArgb(74, 34, 37, 41)) };
        Grid.SetRowSpan(_confirmation, 2);
        var card = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(26), Margin = new Thickness(24), MaxWidth = 460,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        card.SetResourceReference(Border.BackgroundProperty, "PanelElevatedSurface");
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = "保存本次修改？", FontSize = 20, FontWeight = FontWeights.SemiBold });
        content.Children.Add(new TextBlock { Text = "有尚未应用的修改，选择保存或放弃后再关闭。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 22) });
        var actions = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Button Action(string text, Action handler, bool primary = false)
        {
            var button = new Button { Content = text, Margin = new Thickness(8, 0, 0, 0), Style = (Style)_window.FindResource(primary ? "WorkbenchPrimary" : "WorkbenchButton") };
            button.Click += (_, _) => handler(); actions.Children.Add(button); return button;
        }
        void Restore()
        {
            layout.Children.Remove(_confirmation!); _confirmation = null; _cancelConfirmation = null;
            foreach (UIElement child in layout.Children) child.IsEnabled = true;
        }
        var cancel = Action("继续编辑", Restore);
        _cancelConfirmation = Restore;
        Action("放弃修改", () => { Restore(); _discardConfirmed = true; _window.Close(); });
        Action("保存修改", () => { var saved = settings.ApplyBeforeClose(); Restore(); if (saved) _window.Close(); }, true);
        content.Children.Add(actions); card.Child = content; _confirmation.Children.Add(card); layout.Children.Add(_confirmation);
        KeyboardNavigation.SetTabNavigation(_confirmation, KeyboardNavigationMode.Cycle);
        cancel.Focus();
    }
    private void Closed(object? sender, EventArgs e)
    {
        _owner.LocationChanged -= OwnerMoved; _owner.SizeChanged -= OwnerSized; _owner.Closing -= OwnerClosing;
        _card.BeginAnimation(UIElement.OpacityProperty, null); _backdrop.BeginAnimation(UIElement.OpacityProperty, null);
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _shift.BeginAnimation(TranslateTransform.XProperty, null);
        if (_window is SettingsWindow settings) settings.SectionChanged -= SectionChanged;
        _owner.SetActiveSection("overview");
        if (_returnFocus is UIElement { IsVisible: true, IsEnabled: true } element) element.Focus();
    }
}
