using System.Diagnostics;
using EousGate;
using EousGate.Infrastructure;

internal static class ClipboardLinkTests
{
    internal static void ExtractsLinks()
    {
        var links = ClipboardLinks.Extract("请看：https://example.com/a?q=hello%20world&b=2。还有 [文档](http://example.org/wiki/A_(B))，以及 www.example.net/path！");
        Assert(links.Count == 3, "URLs must be extracted from prose and Markdown without surrounding punctuation");
        Assert(links[0].Query == "?q=hello%20world&b=2" && links[1].AbsolutePath == "/wiki/A_(B)" && links[2].Scheme == "https", "queries, balanced parentheses and www links must be preserved");
        Assert(ClipboardLinks.Extract("https://例子.中国/路径").Count == 1, "international domain names and paths must work");
    }

    internal static void RejectsInvalidInput()
    {
        foreach (var value in new[] { "plain text", "file:///C:/demo.txt", "javascript:alert(1)", "mailto:a@example.com", "ftp://example.com", "https://", "https://user:pass@example.com", "user@www.example.com" })
            Assert(ClipboardLinks.Extract(value).Count == 0, "invalid or non-web input must not create a link");
        Assert(ClipboardLinks.Extract(new string('a', ClipboardLinks.MaxTextLength + 1)).Count == 0, "oversized clipboard text must be bounded");
        var many = string.Join(' ', Enumerable.Range(0, 100).Select(i => $"https://example.com/{i}"));
        Assert(ClipboardLinks.Extract(many).Count == ClipboardLinks.MaxLinks, "large link lists must be bounded");
    }

    internal static void DeduplicatesWithinCopy()
    {
        var links = ClipboardLinks.Extract("https://EXAMPLE.com/A https://example.com/A https://example.com/a");
        Assert(links.Count == 2, "host case is equivalent, path case is significant");
        Assert(ClipboardLinks.Extract("https://example.com/A").Count == 1 && ClipboardLinks.Extract("https://example.com/A").Count == 1, "separate copies must never be suppressed by remembered content");
    }

    internal static void RepeatedCopiesResetLifetime()
    {
        var timer = new NotificationLifetime();
        timer.Restart(5);
        Assert(!timer.Advance(TimeSpan.FromSeconds(4), false), "first copy must stay for its configured duration");
        timer.Restart(5);
        Assert(!timer.Advance(TimeSpan.FromSeconds(4), false), "copying again must provide a fresh full duration");
        Assert(timer.Advance(TimeSpan.FromSeconds(1), false), "popup must expire at duration");
        timer.Restart(10);
        Assert(timer.RemainingSeconds == 10, "copying after expiry must show again with updated settings");
    }

    internal static void InteractionPausesLifetime()
    {
        var timer = new NotificationLifetime();
        timer.Restart(5);
        timer.Advance(TimeSpan.FromSeconds(2), false);
        Assert(!timer.Advance(TimeSpan.FromSeconds(50), true) && timer.RemainingSeconds == 3, "hover or keyboard interaction must pause the countdown");
        Assert(timer.Advance(TimeSpan.FromSeconds(3), false), "countdown resumes after interaction");
    }

    internal static void SettingsRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), "EousGate-clipboard-tests", Guid.NewGuid() + ".json");
        try
        {
            var settings = new UserSettings { ClipboardLinksEnabled = false, ClipboardPopupDurationSeconds = 12, ClipboardPopupSide = "Left" };
            var clone = settings.Clone();
            Assert(!clone.ClipboardLinksEnabled && clone.ClipboardPopupDurationSeconds == 12 && clone.ClipboardPopupSide == "Left", "settings draft must preserve clipboard fields");
            var store = new JsonSettingsStore(path);
            store.Save(clone);
            var loaded = store.Load();
            Assert(!loaded.ClipboardLinksEnabled && loaded.ClipboardPopupDurationSeconds == 12 && loaded.ClipboardPopupSide == "Left", "saved clipboard preferences must survive restart");
            clone.ClipboardPopupDurationSeconds = 999;
            clone.ClipboardPopupSide = "bad-side";
            store.Save(clone);
            Assert(store.Load().ClipboardPopupDurationSeconds == 60 && store.Load().ClipboardPopupSide == "Right", "invalid stored preferences must normalize");
            Assert(NotificationLifetime.NormalizeDuration(-1) == 2, "minimum lifetime must be bounded");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    internal static void LegacySettings()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"CandidateCount\":4}");
            var loaded = new JsonSettingsStore(path).Load();
            Assert(loaded.ClipboardLinksEnabled && loaded.ClipboardPopupDurationSeconds == 5 && loaded.ClipboardPopupSide == "Right", "old settings must enable the requested feature with usable defaults");
        }
        finally { File.Delete(path); }
    }

    internal static void OpensOnlyWebLinks()
    {
        var starts = new List<ProcessStartInfo>();
        var opener = new WebLinkOpener(starts.Add);
        var uri = ClipboardLinks.Extract("https://example.com/a?q=x&b=2")[0];
        Assert(starts.Count == 0, "detection must never launch a browser");
        Assert(opener.Open(uri).Success && starts.Count == 1 && starts[0].UseShellExecute && starts[0].FileName == uri.AbsoluteUri && starts[0].Arguments == "", "a click must hand the exact URL to the default handler");
        Assert(!opener.Open(new Uri("file:///C:/example.txt")).Success && starts.Count == 1, "opener must independently reject non-web protocols");
        var failing = new WebLinkOpener(_ => throw new System.ComponentModel.Win32Exception());
        Assert(!failing.Open(uri).Success, "browser startup errors must become recoverable failures");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
