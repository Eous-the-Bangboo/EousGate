using System.Text.RegularExpressions;

namespace EousGate;

public static class ClipboardLinks
{
    public const int MaxTextLength = 262_144;
    public const int MaxLinks = 20;
    private static readonly Regex LinkPattern = new(
        "https?://[^\\s<>\"'`，。；！？、（）【】《》“”‘’]+|(?<![\\w@./])www\\.[^\\s<>\"'`，。；！？、（）【】《》“”‘’]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static IReadOnlyList<Uri> Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxTextLength) return [];
        var links = new List<Uri>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (Match match in LinkPattern.Matches(text))
            {
                var value = match.Value.TrimEnd('.', ',', ';', '!', '?', ':');
                foreach (var (open, close) in new[] { ('(', ')'), ('[', ']'), ('{', '}') })
                    while (value.EndsWith(close) && value.Count(c => c == close) > value.Count(c => c == open))
                        value = value[..^1];
                if (value.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) value = "https://" + value;
                if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && IsWebUri(uri) && seen.Add(uri.AbsoluteUri))
                    links.Add(uri);
                if (links.Count == MaxLinks) break;
            }
        }
        catch (RegexMatchTimeoutException) { return []; }
        return links;
    }

    public static bool IsWebUri(Uri? uri)
        => uri is { IsAbsoluteUri: true } && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo)
            && uri.HostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6;
}

public sealed class NotificationLifetime
{
    public double RemainingSeconds { get; private set; }
    public static int NormalizeDuration(int seconds) => Math.Clamp(seconds, 2, 60);
    public void Restart(int seconds) => RemainingSeconds = NormalizeDuration(seconds);
    public bool Advance(TimeSpan elapsed, bool interacting)
    {
        if (!interacting) RemainingSeconds = Math.Max(0, RemainingSeconds - Math.Max(0, elapsed.TotalSeconds));
        return RemainingSeconds <= 0;
    }
}
