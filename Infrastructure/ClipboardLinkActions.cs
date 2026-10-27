using System.Diagnostics;

namespace EousGate.Infrastructure;

/// <summary>Coordinates prompts and the explicitly enabled repeated-copy open gesture.</summary>
public sealed class ClipboardLinkActions
{
    private readonly Func<UserSettings> _settings;
    private readonly WebLinkOpener _opener;
    private readonly Action<IReadOnlyList<Uri>, string?> _show;
    private readonly Action _dismiss;
    private readonly Func<TimeSpan> _now;
    private readonly RepeatedCopyGesture _gesture = new();

    public ClipboardLinkActions(Func<UserSettings> settings, WebLinkOpener opener,
        Action<IReadOnlyList<Uri>, string?> show, Action dismiss, Func<TimeSpan>? now = null)
    {
        _settings = settings;
        _opener = opener;
        _show = show;
        _dismiss = dismiss;
        var started = Stopwatch.GetTimestamp();
        _now = now ?? (() => Stopwatch.GetElapsedTime(started));
    }

    public void Reset() => _gesture.Reset();

    public void HandleCopy(string? text)
    {
        var settings = _settings();
        var links = ClipboardLinks.Extract(text);
        if (!settings.ClipboardLinksEnabled || links.Count == 0)
        {
            Reset();
            _dismiss();
            return;
        }
        if (!settings.ClipboardAutoOpenEnabled)
        {
            Reset();
            _show(links, null);
            return;
        }
        var shouldOpen = _gesture.Observe(text!, settings.ClipboardAutoOpenCopyCount, _now());
        if (!shouldOpen)
        {
            if (!_gesture.HasTriggered) _show(links, null);
            return;
        }

        var failed = new List<Uri>();
        foreach (var link in links)
            if (!_opener.Open(link).Success) failed.Add(link);
        if (failed.Count == 0) _dismiss();
        else _show(failed, "部分网页未能打开，请检查默认浏览器，或点击下方链接重试。");
    }
}
