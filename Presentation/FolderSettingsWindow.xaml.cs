using System.Windows;
using EousGate.FolderHop;
using EousGate.Infrastructure;

namespace EousGate;

public partial class FolderSettingsWindow : Window
{
    private readonly HistoryStore _store;
    private readonly Func<FolderSession?> _session;
    internal FolderSettingsWindow(HistoryStore store, Func<FolderSession?> session, string skin)
    {
        InitializeComponent(); _store = store; _session = session;
        Record.IsChecked = !store.Data.Paused; Companion.IsChecked = store.Data.ShowDialogButton;
        MicaWindowHelper.Enable(this, skin);
        Status.Text = session()?.Warning ?? store.Warning;
        OpenPickerButton.IsEnabled = session() is not null;
        Activated += (_, _) => OpenPickerButton.IsEnabled = session() is { IsStopped: false };
    }
    private void OptionsChanged(object sender, RoutedEventArgs e)
    {
        _store.Data.Paused = Record.IsChecked != true; _store.Data.ShowDialogButton = Companion.IsChecked == true;
        _store.Dirty = true; Status.Text = _store.Save() ? "已保存" : _store.Warning;
        _session()?.RefreshCompanion(Native.GetForegroundWindow());
    }
    private void OpenPickerClick(object sender, RoutedEventArgs e)
    {
        if (_session() is { } session) session.ShowPicker(IntPtr.Zero);
        else Status.Text = "请先在控制面板启动文件夹快跳。";
    }
    private void ImportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择旧 FolderHop 的 Data/history.json", Filter = "历史数据 JSON|*.json|所有文件|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        try { var count = _store.Import(dialog.FileName); Status.Text = $"已合并 {count} 条记录，保留收藏及最新访问时间。"; _session()?.Picker.RefreshRows(); }
        catch (Exception ex) { Status.Text = "导入失败：" + ex.Message; }
    }
    private void ClearClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show(this, "清空最近访问记录？收藏会保留。", "文件夹快跳", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        _store.ClearRecent(); Status.Text = _store.Save() ? "最近记录已清空，收藏已保留。" : _store.Warning;
        _session()?.Picker.RefreshRows();
    }
}
