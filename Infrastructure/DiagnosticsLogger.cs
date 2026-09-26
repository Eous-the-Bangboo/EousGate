using System.Globalization;
using System.IO;
using System.Text;

namespace EousGate.Infrastructure;

/// <summary>
/// Writes opt-in, privacy-preserving local diagnostics. Entries contain only a
/// timestamp and a fixed event name; callers must not pass file or exception data.
/// </summary>
public sealed class DiagnosticsLogger
{
    private static readonly HashSet<string> AllowedEvents = new(StringComparer.Ordinal)
    {
        "settingsloadfailed",
        "settingssavefailed",
        "processstartfailed",
        "packagedfileactivationfailed",
        "packagedprocessstartfailed",
        "appdiscoveryfailed",
        "shellhandlerdiscoveryfailed"
        ,"clipboardlistenfailed"
        ,"clipboardreadfailed"
        ,"webopenfailed"
    };
    private readonly Func<bool> _isEnabled;
    private readonly string _path;
    private readonly object _sync = new();

    public DiagnosticsLogger(Func<bool>? isEnabled = null, string? path = null)
    {
        _isEnabled = isEnabled ?? (() => false);
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EousGate",
            "diagnostics.log");
    }

    public void Log(string eventName)
    {
        if (!_isEnabled()) return;
        if (string.IsNullOrWhiteSpace(eventName)) return;

        try
        {
            var safeEventName = new string(eventName.Where(char.IsLetterOrDigit).ToArray());
            if (!AllowedEvents.Contains(safeEventName)) return;
            var line = $"{DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)} {safeEventName}{Environment.NewLine}";
            lock (_sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.AppendAllText(_path, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never affect the main workflow.
        }
    }
}
