using System.Text.Json;
using System.IO;

namespace EousGate.Infrastructure;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly DiagnosticsLogger _logger;

    public JsonSettingsStore(string? path = null, DiagnosticsLogger? logger = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EousGate", "settings.json");
        _logger = logger ?? new DiagnosticsLogger();
    }
    public UserSettings Load()
    {
        try
        {
            var raw = File.Exists(_path) ? File.ReadAllText(_path) : null;
            var settings = raw is not null
                ? JsonSerializer.Deserialize<UserSettings>(raw) ?? new UserSettings()
                : new UserSettings();
            if (raw is not null && !HasPerSideLayouts(raw))
            {
                // Keep legacy EdgeBandWidth active until the user saves the new per-side settings.
                settings.LeftEdgeBand = null;
                settings.RightEdgeBand = null;
                settings.TopEdgeBand = null;
            }
            settings.DesignSchemeId = PanelDesignCatalog.Normalize(settings.DesignSchemeId);
            settings.SkinId = SkinCatalog.Normalize(settings.SkinId);
            settings.EdgeBandPosition = NormalizeEdgeBandPosition(settings.EdgeBandPosition);
            settings.AutoDismissDelayMs = Math.Clamp(settings.AutoDismissDelayMs, 0, 10_000);
            settings.LeftEdgeBand = NormalizeEdgeBandLayout(settings.LeftEdgeBand);
            settings.RightEdgeBand = NormalizeEdgeBandLayout(settings.RightEdgeBand);
            settings.TopEdgeBand = NormalizeEdgeBandLayout(settings.TopEdgeBand);
            NormalizeCustomApps(settings);
            settings.SkinId = SkinCatalog.Normalize(settings.SkinId);
            return settings;
        }
        catch
        {
            _logger.Log("settingsloadfailed");
            return new UserSettings();
        }
    }
    public void Save(UserSettings settings)
    {
        try
        {
            NormalizeCustomApps(settings);
            settings.EdgeBandPosition = NormalizeEdgeBandPosition(settings.EdgeBandPosition);
            settings.AutoDismissDelayMs = Math.Clamp(settings.AutoDismissDelayMs, 0, 10_000);
            settings.LeftEdgeBand = NormalizeEdgeBandLayout(settings.LeftEdgeBand);
            settings.RightEdgeBand = NormalizeEdgeBandLayout(settings.RightEdgeBand);
            settings.TopEdgeBand = NormalizeEdgeBandLayout(settings.TopEdgeBand);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { _logger.Log("settingssavefailed"); }
    }

    private static void NormalizeCustomApps(UserSettings settings)
    {
        foreach (var preference in settings.FileTypes.Values)
        {
            preference.CustomApps ??= [];
            preference.CustomApps = preference.CustomApps
                .Where(app => app is not null && !string.IsNullOrWhiteSpace(app.ExecutablePath))
                .Select(app =>
                {
                    app.ExecutablePath = AppDiscovery.NormalizeExecutablePath(app.ExecutablePath);
                    app.Id = app.ExecutablePath;
                    app.DisplayName = string.IsNullOrWhiteSpace(app.DisplayName) ? AppDiscovery.GetDisplayName(app.ExecutablePath) : app.DisplayName.Trim();
                    app.Arguments = string.IsNullOrWhiteSpace(app.Arguments) ? null : app.Arguments.Trim();
                    return app;
                })
                .GroupBy(app => app.ExecutablePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }
    }

    private static EdgeBandLayout? NormalizeEdgeBandLayout(EdgeBandLayout? layout)
        => layout is null ? null : EdgeBandLayoutMath.Normalize(layout);

    private static bool HasPerSideLayouts(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        return root.TryGetProperty(nameof(UserSettings.LeftEdgeBand), out _)
            || root.TryGetProperty(nameof(UserSettings.RightEdgeBand), out _)
            || root.TryGetProperty(nameof(UserSettings.TopEdgeBand), out _);
    }

    private static string NormalizeEdgeBandPosition(string? position)
    {
        var values = (position ?? "")
            .Split([',', ';', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.ToLowerInvariant())
            .SelectMany(value => value switch
            {
                "both" => new[] { "Left", "Right" },
                "left" => new[] { "Left" },
                "right" => new[] { "Right" },
                "top" => new[] { "Top" },
                _ => Array.Empty<string>()
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return values.Count == 0 ? "Left,Right" : string.Join(',', new[] { "Left", "Right", "Top" }.Where(side => values.Contains(side, StringComparer.OrdinalIgnoreCase)));
    }
}
