using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace EousGate.Infrastructure;

/// <summary>Forwards separate clipboard updates without keeping clipboard history.</summary>
public sealed class ClipboardMonitor : IDisposable
{
    private const int ClipboardUpdate = 0x031D;
    private readonly HwndSource _source;
    private readonly DispatcherTimer _retryTimer;
    private readonly Action<string?> _textCopied;
    private readonly DiagnosticsLogger _logger;
    private readonly Func<(bool Success, string? Text, uint Sequence)> _readText;
    private uint? _lastDeliveredSequence;
    internal IntPtr WindowHandle => _source.Handle;
    private int _attempts;
    private bool _enabled;
    private bool _disposed;

    public ClipboardMonitor(Action<string?> textCopied, DiagnosticsLogger? logger = null)
        : this(textCopied, logger, null) { }

    internal ClipboardMonitor(Action<string?> textCopied, DiagnosticsLogger? logger, Func<(bool Success, string? Text)>? readText,
        Func<uint>? sequenceNumber = null)
    {
        _textCopied = textCopied;
        _logger = logger ?? new DiagnosticsLogger();
        _source = new HwndSource(new HwndSourceParameters("EousWebHop Clipboard Listener")
        {
            ParentWindow = new IntPtr(-3), Width = 0, Height = 0, WindowStyle = 0
        });
        _source.AddHook(WindowProc);
        _readText = readText is null
            ? () => { var success = TryReadText(_source.Handle, out var text, out var sequence); return (success, text, sequence); }
            : () => { var (success, text) = readText(); return (success, text, sequenceNumber?.Invoke() ?? 0); };
        _retryTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(80) };
        _retryTimer.Tick += ReadClipboard;
    }

    public bool SetEnabled(bool enabled)
    {
        if (_disposed) return false;
        if (enabled == _enabled) return true;
        _retryTimer.Stop();
        _lastDeliveredSequence = null;
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
            // Read each notification immediately when possible: an 80ms debounce would
            // coalesce the quick repeated copies used by the deliberate open gesture.
            ReadClipboard(this, EventArgs.Empty);
            if (_enabled && !_disposed && _attempts > 0 && _attempts < 6) _retryTimer.Start();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ReadClipboard(object? sender, EventArgs e)
    {
        if (!_enabled || _disposed) { _retryTimer.Stop(); return; }
        var (success, text, sequence) = _readText();
        if (success)
        {
            _retryTimer.Stop();
            // Reading delayed clipboard formats can itself enqueue another update.
            // Count the completed clipboard version once, while allowing a new
            // version containing exactly the same text to count as another copy.
            if (sequence != 0 && _lastDeliveredSequence == sequence) return;
            _lastDeliveredSequence = sequence == 0 ? null : sequence;
            _textCopied(text);
        }
        else if (++_attempts >= 6)
        {
            _retryTimer.Stop();
            _logger.Log("clipboardreadfailed");
        }
    }

    private static bool TryReadText(IntPtr owner, out string? text, out uint sequence)
    {
        text = null;
        sequence = 0;
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
        finally
        {
            // Snapshot after delayed rendering, while this clipboard is still locked.
            sequence = GetClipboardSequenceNumber();
            CloseClipboard();
        }
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
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("kernel32.dll")] private static extern UIntPtr GlobalSize(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr memory);
}
