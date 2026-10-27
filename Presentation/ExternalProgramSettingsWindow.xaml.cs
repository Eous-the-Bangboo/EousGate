using System.IO;
using System.Windows;
using EousGate.Infrastructure;

namespace EousGate;

public partial class ExternalProgramSettingsWindow : Window
{
    private readonly ExternalProgramTool _tool;
    private readonly Action<string?> _save;
    private readonly IExternalProgramController _controller;
    private bool _busy;
    internal bool IsSaving => _busy;
    internal ExternalProgramSettingsWindow(ExternalProgramTool tool, IExternalProgramController controller, Action<string?> save, string skin)
    {
        InitializeComponent(); _tool = tool; _controller = controller; _save = save;
        Title = tool.Descriptor.ChineseName + " · 程序设置"; ProgramTitle.Text = tool.Descriptor.ChineseName;
        ExecutablePath.Text = tool.ExecutablePath ?? "";
        Status.Text = "正常退出无响应时，停止会结束此路径的应用进程；退出 Eous 助手会保留外部程序。";
        MicaWindowHelper.Enable(this, skin);
        Closing += (_, e) => { if (_busy) e.Cancel = true; };
    }
    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "选择 " + _tool.Definition.ExecutableName, Filter = "程序文件|*.exe", FileName = _tool.Definition.ExecutableName };
        if (picker.ShowDialog(this) == true) ExecutablePath.Text = picker.FileName;
    }
    private async void DetectClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true; DetectButton.IsEnabled = SaveButton.IsEnabled = BrowseButton.IsEnabled = ExecutablePath.IsEnabled = false;
        Status.Text = "正在定位程序…";
        try
        {
            var result = await Task.Run(() => _controller.Inspect(_tool.Definition, null));
            if (result.ExecutablePath is { } path) ExecutablePath.Text = path;
            Status.Text = result.Error ?? "已定位，点击保存应用。";
        }
        catch { Status.Text = "定位失败，请手动选择程序文件。"; }
        finally { _busy = false; DetectButton.IsEnabled = SaveButton.IsEnabled = BrowseButton.IsEnabled = ExecutablePath.IsEnabled = true; }
    }
    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true; SaveButton.IsEnabled = DetectButton.IsEnabled = BrowseButton.IsEnabled = ExecutablePath.IsEnabled = false;
        Status.Text = "正在保存程序路径…";
        try { await _tool.ConfigurePathAsync(string.IsNullOrWhiteSpace(ExecutablePath.Text) ? null : Path.GetFullPath(ExecutablePath.Text.Trim().Trim('"')), _save); _busy = false; Close(); }
        catch (Exception ex) { Status.Text = ex.Message; }
        finally { _busy = false; SaveButton.IsEnabled = DetectButton.IsEnabled = BrowseButton.IsEnabled = ExecutablePath.IsEnabled = true; }
    }
    private void CancelClick(object sender, RoutedEventArgs e) => Close();
}
