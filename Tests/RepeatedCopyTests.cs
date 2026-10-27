using EousGate;
using EousGate.Infrastructure;

internal static class RepeatedCopyTests
{
    private const string Sample = "请看 https://example.com/a 和 https://EXAMPLE.com/a，再看 www.example.org/docs。";

    internal static void ThresholdAndDistinctLinks()
    {
        var settings = new UserSettings { ClipboardAutoOpenEnabled = true, ClipboardAutoOpenCopyCount = 3 };
        var opened = new List<string>();
        var shown = 0; var dismissed = 0; var now = TimeSpan.Zero;
        var actions = new ClipboardLinkActions(() => settings, new WebLinkOpener(p => opened.Add(p.FileName)),
            (_, _) => shown++, () => dismissed++, () => now);
        void Copy() { now += TimeSpan.FromMilliseconds(50); actions.HandleCopy(Sample); }
        Copy(); Copy();
        Check(opened.Count == 0 && shown == 2, "copies below the threshold must only refresh the prompt");
        Copy();
        Check(opened.SequenceEqual(new[] { "https://example.com/a", "https://www.example.org/docs" }) && dismissed == 1,
            "the third copy must open all unique URLs once without a click and dismiss the prompt");
        Copy(); Copy(); Copy();
        Check(opened.Count == 2 && shown == 2, "continued copies in the same streak must not open another round or re-show the prompt");
        now += TimeSpan.FromSeconds(4);
        Copy(); Copy(); Copy();
        Check(opened.Count == 4, "a new deliberate streak after the interval must work again");
    }

    internal static void ResetAndSettings()
    {
        var settings = new UserSettings { ClipboardAutoOpenCopyCount = 2 };
        var opened = 0; var now = TimeSpan.Zero;
        var actions = new ClipboardLinkActions(() => settings, new WebLinkOpener(_ => opened++), (_, _) => { }, () => { }, () => now);
        void Copy(string? text = Sample) { now += TimeSpan.FromMilliseconds(100); actions.HandleCopy(text); }
        Copy(); Copy(); Copy();
        Check(opened == 0, "the optional gesture must be off by default");
        settings.ClipboardAutoOpenEnabled = true;
        Copy(); Copy("different paragraph " + Sample); Copy();
        Check(opened == 0, "changing the paragraph must restart even when its URLs are unchanged");
        Copy("ordinary copied text"); Copy(); Copy(null); Copy();
        Check(opened == 0, "non-link and non-text copies must break the streak");
        actions.Reset(); Copy();
        Check(opened == 0, "module stop or applied settings must clear prior copies");
        now += TimeSpan.FromSeconds(4); Copy();
        Check(opened == 0, "widely spaced copies must not accumulate");
        settings.ClipboardAutoOpenCopyCount = 3;
        Copy(); Copy();
        Check(opened == 0, "changing the configured threshold must restart the gesture");
        Copy(); Check(opened == 2, "the new threshold must apply to both extracted links");
        actions.Reset(); settings.ClipboardLinksEnabled = false;
        Copy(); Copy(); Copy();
        Check(opened == 2, "disabled detection must never open a browser");
    }

    internal static void PartialFailure()
    {
        var settings = new UserSettings { ClipboardAutoOpenEnabled = true, ClipboardAutoOpenCopyCount = 2 };
        var attempts = new List<string>();
        IReadOnlyList<Uri> shown = []; string? error = null;
        var actions = new ClipboardLinkActions(() => settings, new WebLinkOpener(p =>
        {
            attempts.Add(p.FileName);
            if (p.FileName.Contains("example.org")) throw new InvalidOperationException("synthetic browser failure");
        }), (links, message) => { shown = links; error = message; }, () => { });
        actions.HandleCopy(Sample); actions.HandleCopy(Sample);
        Check(attempts.Count == 2 && shown.Count == 1 && shown[0].Host == "www.example.org" && error is not null,
            "a partial browser failure must retain only failed links for manual retry");
        actions.HandleCopy(Sample);
        Check(attempts.Count == 2 && error is not null, "continued copies after a failure must not reopen successful URLs or erase the error");
        actions.HandleCopy("javascript:alert(1)");
        Check(attempts.Count == 2, "only validated HTTP/HTTPS URLs may reach the browser");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
