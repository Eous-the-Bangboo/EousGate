using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace EousGate.FolderHop;

// Hosted by WPF's existing STA message loop. No second Application.Run or tray icon.
internal sealed class FolderSession
{
    internal HistoryStore Store { get; }
    internal PickerForm Picker { get; } = null!;
    internal DialogCompanion Button { get; } = null!;
    private readonly HotkeyWindow _hotkey = null!;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 400 };
    private readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 3000 };
    private readonly CancellationTokenSource _stop = new();
    private readonly List<Task> _workers = [];
    private volatile bool _stopping;
    private bool _disposed;
    private readonly Action<string> _warning;

    internal FolderSession(HistoryStore store, Action<string> warning, Func<IntPtr>? foregroundProbe = null)
    {
        Store = store; _warning = warning;
        try
        {
            Picker = new PickerForm(store);
            _ = Picker.Handle;
            Button = new DialogCompanion(target => Post(() => ShowPicker(target)));
            _hotkey = new HotkeyWindow();
            _hotkey.Pressed = () => ShowPicker(Native.GetForegroundWindow());
            if (!_hotkey.Registered) warning("Ctrl+Alt+J 已被占用；仍可通过对话框按钮或面板打开文件夹快跳。");
            Picker.Choose += Choose;
            Picker.Changed += Save;
            _timer.Tick += (_, _) => RefreshCompanion((foregroundProbe ?? Native.GetForegroundWindow)());
            _saveTimer.Tick += (_, _) => Save();
            _timer.Start(); _saveTimer.Start();
            RunWorker(ReadLoop);
        }
        catch
        {
            _stopping = true; _stop.Cancel(); _timer.Dispose(); _saveTimer.Dispose();
            _hotkey?.Dispose(); Button?.Dispose(); Picker?.Dispose(); _stop.Dispose();
            throw;
        }
    }
    internal bool IsStopped => _disposed;
    internal string Warning => !_hotkey.Registered ? "Ctrl+Alt+J 已被占用；可通过面板或对话框按钮打开。" : Store.Warning;
    private void Save() { if (Store.Dirty && !Store.Save()) { Picker.Status(Store.Warning); _warning(Store.Warning); } }
    private void RunWorker(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _workers.RemoveAll(t => t.IsCompleted);
        _workers.Add(completion.Task);
        var worker = new Thread(() => { try { action(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } }) { IsBackground = true, Name = "EousFolderHop.Shell" };
        worker.SetApartmentState(ApartmentState.STA); worker.Start();
    }
    private void Post(Action action)
    {
        if (_stopping) return;
        try { Picker.BeginInvoke(() => { if (!_stopping) action(); }); }
        catch (InvalidOperationException) { /* Host is shutting down. */ }
    }
    private void ReadLoop()
    {
        string previous = "";
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                var front = Native.GetForegroundWindow();
                if (!Store.Data.Paused && Native.IsExplorer(front))
                {
                    var paths = ExplorerReader.Paths(front);
                    var signature = front + "|" + string.Join("|", paths);
                    if (paths.Count > 0 && signature != previous)
                    {
                        previous = signature;
                        Post(() => { if (!Store.Data.Paused) { foreach (var path in paths) Store.Touch(path); if (Picker.Visible) Picker.RefreshRows(); } });
                    }
                }
                else previous = "";
            }
            catch { /* Explorer can close or restart during a COM query. */ }
            _stop.Token.WaitHandle.WaitOne(750);
        }
    }
    internal void RefreshCompanion(IntPtr foreground)
    {
        if (_stopping || foreground == Picker.Handle || foreground == Button.Handle) { Button.Hide(); return; }
        if (Store.Data.ShowDialogButton && Native.IsFileDialog(foreground)) Button.Follow(foreground);
        else Button.Hide();
    }
    internal void ShowPicker(IntPtr target)
    {
        if (_stopping) return;
        if (Picker.Visible && target == Picker.Handle && Picker.WindowState != FormWindowState.Minimized)
        { Picker.Activate(); Picker.Search.Focus(); return; }
        Button.Hide(); Picker.ShowFor(target, []);
        if (!string.IsNullOrEmpty(Warning)) Picker.Status(Warning);
        RunWorker(() => { var paths = ExplorerReader.Paths(IntPtr.Zero); Post(() => { Picker.Current = paths; Picker.RefreshRows(); }); });
    }
    private void Choose(FolderEntry entry)
    {
        if (_stopping) return;
        if (Picker.Target != IntPtr.Zero)
        {
            var target = Picker.Target; Picker.Hide();
            Native.Navigate(target, entry.Path, (ok, message) =>
            {
                if (_stopping) return;
                if (ok) { Store.Touch(entry.Path); Save(); }
                else { Picker.ShowFor(target, Picker.Current); Picker.Status(message); }
            });
        }
        else
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = Quote(entry.Path) });
                Store.Touch(entry.Path); Save(); Picker.Hide();
            }
            catch (Exception ex) { Picker.Status("无法打开：" + ex.Message); }
        }
    }
    internal static string Quote(string value) => "\"" + (value.EndsWith('\\') ? value + "." : value) + "\"";
    internal async Task StopAsync()
    {
        if (_disposed) return;
        _stopping = true; _stop.Cancel();
        _timer.Stop(); _saveTimer.Stop(); Native.CancelNavigation();
        _hotkey.Dispose(); Button.Hide(); Picker.Hide();
        // Cancellation does not abort a COM call. Keep a failed session owned until it can be stopped.
        try { await Task.WhenAll(_workers).WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) when (ex is not TimeoutException) { _warning("目录读取任务已结束：" + ex.GetType().Name); }
        var saved = Store.Save();
        Button.Dispose(); Picker.Dispose(); _timer.Dispose(); _saveTimer.Dispose(); _stop.Dispose();
        _disposed = true;
        if (!saved) throw new IOException(Store.Warning);
    }
}
