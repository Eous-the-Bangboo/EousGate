using System.IO;
using System.Windows;
using EousGate.Infrastructure;

namespace EousGate;

public partial class CustomProgramWindow : Window
{
    private readonly CustomMonitoredProgram _draft;
    private readonly Func<CustomMonitoredProgram, Task> _save;
    private readonly Func<Task>? _remove;
    private bool _busy;
    internal bool IsSaving => _busy;
    private string? _automaticName;
    internal CustomProgramWindow(CustomMonitoredProgram? program, Func<CustomMonitoredProgram, Task> save, Func<Task>? remove, string skin)
    {
        InitializeComponent(); _draft = program?.Clone() ?? new(); _save = save; _remove = remove;
        ProgramName.Text = _draft.Name; ProgramPath.Text = _draft.ExecutablePath; StartArguments.Text = _draft.StartArguments ?? "";
        if (program is not null) _automaticName = CustomProgramConfiguration.DisplayName(program.ExecutablePath);
        ForceStop.IsChecked = _draft.ForceTerminateOnTimeout;
        if (program is not null) { Title = EditorTitle.Text = "编辑监控程序"; SaveButton.Content = "保存"; RemoveButton.Visibility = Visibility.Visible; }
        Advanced.IsExpanded = _draft.StartArguments is not null || _draft.ForceTerminateOnTimeout;
        UpdateLauncherHint();
        ProgramPath.LostKeyboardFocus += (_, _) => UpdateLauncherHint();
        MicaWindowHelper.Enable(this, skin);
        Closing += (_, e) => { if (_busy) e.Cancel = true; };
    }
    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "选择监控程序", Filter = "程序文件 (*.exe)|*.exe", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        var updateName = string.IsNullOrWhiteSpace(ProgramName.Text) || ProgramName.Text == _automaticName;
        ProgramPath.Text = picker.FileName;
        if (updateName) ProgramName.Text = _automaticName = CustomProgramConfiguration.DisplayName(picker.FileName);
        UpdateLauncherHint();
    }
    private void UpdateLauncherHint()
    {
        try
        {
            var definition = CustomProgramConfiguration.Definition(new() { Id = _draft.Id, ExecutablePath = ProgramPath.Text });
            var launcher = !string.IsNullOrWhiteSpace(ProgramPath.Text) && System.IO.File.Exists(ProgramPath.Text)
                && LauncherProcessTargets.Resolve(definition, ProgramPath.Text).Count > 1;
            LauncherHint.Visibility = launcher ? Visibility.Visible : Visibility.Collapsed;
            if (launcher) Advanced.IsExpanded = true;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { LauncherHint.Visibility = Visibility.Collapsed; }
    }
    private async Task RunAsync(Func<Task> action, string progress = "正在保存…")
    {
        if (_busy) return;
        _busy = true; SaveButton.IsEnabled = RemoveButton.IsEnabled = BrowseButton.IsEnabled = false;
        EditorFields.IsEnabled = CancelButton.IsEnabled = false;
        EditorStatus.Text = progress; EditorStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "PanelMutedForeground");
        WorkbenchDialog.Refresh(this);
        try { await action(); _busy = false; Close(); }
        catch (Exception ex)
        {
            var actionName = progress.Contains("移除") ? "移除失败" : _remove is null ? "添加失败" : "程序设置未保存";
            EditorStatus.Text = $"{actionName}：{ex.Message}\n填写内容已保留，请重试。";
            EditorStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "PanelDanger");
            WorkbenchDialog.Refresh(this);
            _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => EditorStatus.BringIntoView()));
        }
        finally { _busy = false; SaveButton.IsEnabled = RemoveButton.IsEnabled = BrowseButton.IsEnabled = EditorFields.IsEnabled = CancelButton.IsEnabled = true; }
    }
    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            _draft.Name = string.IsNullOrWhiteSpace(ProgramName.Text) ? CustomProgramConfiguration.DisplayName(ProgramPath.Text) : ProgramName.Text;
            _draft.ExecutablePath = ProgramPath.Text; _draft.StartArguments = StartArguments.Text; _draft.ForceTerminateOnTimeout = ForceStop.IsChecked == true;
            await _save(_draft);
        });
    }
    private async void RemoveClick(object sender, RoutedEventArgs e) { if (_remove is not null) await RunAsync(_remove, "正在移除监控条目…"); }
    private void CancelClick(object sender, RoutedEventArgs e) { if (!_busy) Close(); }
}
