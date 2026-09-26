using System.Diagnostics;

namespace EousGate.Infrastructure;

public sealed class WebLinkOpener
{
    private readonly Action<ProcessStartInfo> _start;
    private readonly DiagnosticsLogger _logger;
    public WebLinkOpener(Action<ProcessStartInfo>? start = null, DiagnosticsLogger? logger = null)
    {
        _start = start ?? (info => Process.Start(info));
        _logger = logger ?? new DiagnosticsLogger();
    }

    public OpenResult Open(Uri uri)
    {
        if (!ClipboardLinks.IsWebUri(uri)) return new(false, "这个地址不是可打开的网页链接。");
        try
        {
            _start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true, Verb = "open" });
            return new(true);
        }
        catch
        {
            _logger.Log("webopenfailed");
            return new(false, "无法打开网页，请检查默认浏览器后重试。");
        }
    }
}
