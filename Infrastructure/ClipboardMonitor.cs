using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace EousGate.Infrastructure;

/// <summary>Listens to copy events, never compares clipboard text or stores history.</summary>
public sealed class ClipboardMonitor : IDisposable
{
    private const int ClipboardUpdate = 0x031D;
    private readonly HwndSource _source;
    private readonly DispatcherTimer _retryTimer;
    private readonly Action<string?> _textCopied;
    private readonly DiagnosticsLogger _logger;
    private readonly Func<(bool Success, string? Text)> _readText;
    internal IntPtr WindowHandle => _source.Handle;
    private int _attempts;
    private bool _enabled;
    private bool _disposed;

    public ClipboardMonitor(Action<string?> textCopied, DiagnosticsLogger? logger = null)
        : this(textCopied, logger, null) { }

    internal ClipboardMonitor(Action<string?> textCopied, DiagnosticsLogger? logger, Func<(bool Success, string? Text)>? readText)
    {
        _textCopied = textCopied;
        _logger = logger ?? new DiagnosticsLogger();
        _source = new HwndSource(new HwndSourceParameters("EousGate Clipboard Listener")
        {
            ParentWindow = new IntPtr(-3), Width = 0, Height = 0, WindowStyle = 0
        });
        _source.AddHook(WindowProc);
        _readText = readText ?? (() => { var success = TryReadText(_source.Handle, out var text); return (success, text); });
        _retryTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(80) };
        _retryTimer.Tick += ReadClipboard;
    }

    public bool SetEnabled(bool enabled)
    {
        if (_disposed) return false;
        if (enabled == _enabled) return true;
        _retryTimer.Stop();
        if (enabled)
        {
            if (!AddClipboardFormatListener(_source.Handle))
            {
                _logger.Log("clipboardlistenfailed");
                return false;
            }
        }
        else RemoveClipboardFormatListener(_source.Handle);
        _enabled = enabled;
        return true;
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == ClipboardUpdate && _enabled)
        {
            // Repeated copies of identical text are separate events. Restart
            // pending reads as well so a newer copy supersedes an older retry.
            _attempts = 0;
            _retryTimer.Stop();
            _retryTimer.Start();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ReadClipboard(object? sender, EventArgs e)
    {
        if (!_enabled || _disposed) { _retryTimer.Stop(); return; }
        var (success, text) = _readText();
        if (success)
        {
            _retryTimer.Stop();
            _textCopied(text);
        }
        else if (++_attempts >= 6)
        {
            _retryTimer.Stop();
            _logger.Log("clipboardreadfailed");
        }
    }

    private static bool TryReadText(IntPtr owner, out string? text)
    {
        text = null;
        if (!OpenClipboard(owner)) return false;
        try
        {
            if (!IsClipboardFormatAvailable(13)) return true; // CF_UNICODETEXT
            var data = GetClipboardData(13);
            if (data == IntPtr.Zero) return false;
            var bytes = GlobalSize(data).ToUInt64();
            if (bytes < 2 || bytes > (ClipboardLinks.MaxTextLength + 1UL) * 2) return true;
            var pointer = GlobalLock(data);
            if (pointer == IntPtr.Zero) return false;
            try
            {
                var value = Marshal.PtrToStringUni(pointer, (int)(bytes / 2)) ?? "";
                var terminator = value.IndexOf('\0');
                text = terminator >= 0 ? value[..terminator] : value;
                return true;
            }
            finally { GlobalUnlock(data); }
        }
        finally { CloseClipboard(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        SetEnabled(false);
        _disposed = true;
        _retryTimer.Tick -= ReadClipboard;
        _source.RemoveHook(WindowProc);
        _source.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("kernel32.dll")] private static extern UIntPtr GlobalSize(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr memory);
}
