using System.Windows.Forms;

namespace EousGate.FolderHop;

// WPF common dialogs run a modal loop on the host thread. Keep the modeless companion off that loop.
internal sealed class DialogCompanion : IDisposable
{
    private readonly DialogButton _button;
    private readonly Thread _thread;
    private volatile bool _disposed;
    internal IntPtr Handle { get; }
    internal bool Visible => !_disposed && Native.IsWindowVisible(Handle);
    internal bool IsDisposed => _disposed;

    internal DialogCompanion(Action<IntPtr> open)
    {
        var ready = new TaskCompletionSource<DialogButton>(TaskCreationOptions.RunContinuationsAsynchronously);
        _thread = new Thread(() =>
        {
            try
            {
                using var context = new ApplicationContext();
                using var button = new DialogButton(open);
                _ = button.Handle;
                ready.SetResult(button);
                System.Windows.Forms.Application.Run(context);
            }
            catch (Exception ex) { ready.TrySetException(ex); }
        }) { IsBackground = true, Name = "EousFolderHop.Companion" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _button = ready.Task.GetAwaiter().GetResult();
        Handle = _button.Handle;
    }
    private void Post(Action action)
    {
        if (_disposed) return;
        try { _button.BeginInvoke(() => { if (!_disposed) action(); }); }
        catch (InvalidOperationException) when (_disposed) { }
    }
    internal void Follow(IntPtr dialog) => Post(() => _button.Follow(dialog));
    internal void Hide() => Post(_button.Hide);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _button.Invoke(() => { _button.Close(); System.Windows.Forms.Application.ExitThread(); });
        if (!_thread.Join(TimeSpan.FromSeconds(3))) throw new InvalidOperationException("文件夹快跳按钮尚未释放，请重试停止。");
    }
}
