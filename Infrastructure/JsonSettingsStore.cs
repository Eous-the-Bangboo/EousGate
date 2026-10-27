using System.Text.Json;
using System.IO;

namespace EousGate.Infrastructure;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly DiagnosticsLogger _logger;
    private readonly string? _legacyPath;
    public string? LastError { get; private set; }
    private bool _readBlocked;

    public JsonSettingsStore(string? path = null, DiagnosticsLogger? logger = null, string? legacyPath = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EousAssistant", "settings.json");
        _legacyPath = legacyPath ?? (path is null ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EousGate", "settings.json") : null);
        _logger = logger ?? new DiagnosticsLogger();
    }
    public UserSettings Load()
    {
        try
        {
            var migrating = !File.Exists(_path) && _legacyPath is not null && File.Exists(_legacyPath);
            var raw = File.Exists(_path) ? File.ReadAllText(_path) : migrating ? File.ReadAllText(_legacyPath!) : null;
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
            NormalizeClipboardSettings(settings);
            settings.LeftEdgeBand = NormalizeEdgeBandLayout(settings.LeftEdgeBand);
            settings.RightEdgeBand = NormalizeEdgeBandLayout(settings.RightEdgeBand);
            settings.TopEdgeBand = NormalizeEdgeBandLayout(settings.TopEdgeBand);
            NormalizeCustomApps(settings);
            if (!CustomProgramConfiguration.Normalize(settings)) LastError = "部分自定义程序配置无效，已忽略这些条目；请检查原配置。";
            settings.ToolSelections ??= new();
            settings.ExternalPrograms ??= new();
            foreach (var definition in ExternalProgramCatalog.All)
            {
                settings.ToolSelections.TryAdd(definition.Descriptor.Id, true);
                if (!settings.ExternalPrograms.TryGetValue(definition.Descriptor.Id, out var option) || option is null)
                    settings.ExternalPrograms[definition.Descriptor.Id] = new();
            }
            settings.ToolSelections.TryAdd("file-hop", !settings.Paused);
            settings.ToolSelections.TryAdd("web-hop", settings.ClipboardLinksEnabled && !settings.Paused);
            settings.ToolSelections.TryAdd("folder-hop", true);
            settings.SkinId = SkinCatalog.Normalize(settings.SkinId);
            if (migrating) Save(settings);
            return settings;
        }
        catch
        {
            _readBlocked = true;
            LastError = "无法读取设置，已使用默认值；请备份原配置后再保存。";
            _logger.Log("settingsloadfailed");
            return new UserSettings();
        }
    }
    public void Save(UserSettings settings)
    {
        if (_readBlocked) { LastError = "原设置无法读取；为保留原数据，暂不写入，请先备份并修复配置。"; return; }
        try
        {
            LastError = null;
            settings.DesignSchemeId = PanelDesignCatalog.Normalize(settings.DesignSchemeId);
            settings.SkinId = SkinCatalog.Normalize(settings.SkinId);
            NormalizeCustomApps(settings);
            if (!CustomProgramConfiguration.Normalize(settings)) throw new ArgumentException("自定义程序配置无效。");
            settings.EdgeBandPosition = NormalizeEdgeBandPosition(settings.EdgeBandPosition);
            settings.AutoDismissDelayMs = Math.Clamp(settings.AutoDismissDelayMs, 0, 10_000);
            NormalizeClipboardSettings(settings);
            settings.LeftEdgeBand = NormalizeEdgeBandLayout(settings.LeftEdgeBand);
            settings.RightEdgeBand = NormalizeEdgeBandLayout(settings.RightEdgeBand);
            settings.TopEdgeBand = NormalizeEdgeBandLayout(settings.TopEdgeBand);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(_path)) File.Replace(temp, _path, _path + ".bak");
            else File.Move(temp, _path);
        }
        catch (Exception ex) { LastError = "无法保存设置（" + ex.GetType().Name + "），请检查数据目录是否可写。"; _logger.Log("settingssavefailed"); }
    }

    private static void NormalizeClipboardSettings(UserSettings settings)
    {
        settings.ClipboardPopupDurationSeconds = NotificationLifetime.NormalizeDuration(settings.ClipboardPopupDurationSeconds);
        settings.ClipboardAutoOpenCopyCount = RepeatedCopyGesture.NormalizeCount(settings.ClipboardAutoOpenCopyCount);
        settings.ClipboardPopupSide = string.Equals(settings.ClipboardPopupSide, "Left", StringComparison.OrdinalIgnoreCase) ? "Left" : "Right";
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
