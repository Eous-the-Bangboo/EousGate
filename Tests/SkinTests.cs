using EousGate;
using EousGate.Infrastructure;
using System.Windows.Media;
using System.Text.Json;

internal static class SkinTests
{
    internal static void PersistenceAndContrast()
    {
        var folder = Path.Combine(Path.GetTempPath(), "EousAssistant-skin", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(Path.Combine(folder, "settings.json"));
            foreach (var skin in new[] { SkinCatalog.DefaultId, SkinCatalog.FocusDarkId })
            {
                store.Save(new UserSettings { SkinId = skin, Theme = "Light" });
                var loaded = store.Load();
                Check(loaded.SkinId == skin && loaded.Theme == "Light" && loaded.DesignSchemeId == PanelDesignCatalog.DefaultId, "skin changes must preserve the user's independent theme and default layout");
            }
            var focus = ThemePalette.Create("Light", false, false, SkinCatalog.FocusDarkId);
            Check(focus == ThemePalette.Create("Dark", true, false, SkinCatalog.FocusDarkId), "B must stay dark even if the previous theme is light or system");
            foreach (var skin in new[] { SkinCatalog.DefaultId, SkinCatalog.FocusDarkId })
            foreach (var theme in new[] { "Light", "Dark" })
            {
                var p = ThemePalette.Create(theme, false, false, skin);
                Check(Contrast(p.Foreground, p.ElevatedSurface) >= 4.5 && Contrast(p.MutedForeground, p.Background) >= 4.5, "A/B text must be readable");
                Check(Contrast(p.OnAccent, p.Accent) >= 4.5 && Contrast(p.OnAccent, p.AccentHover) >= 4.5, "primary action labels must be readable in normal and hover states");
                Check(Contrast(p.Accent, p.AccentSoft) >= 4.5, "selected candidate text must be readable");
                var high = ThemePalette.Create(theme, true, true, skin);
                Check(high.Background == "SystemWindow" && high.Accent == "SystemHighlight" && high.OnAccent == "SystemHighlightText", "high contrast must override both skins");
            }
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    internal static void RemovedAppearanceFallsBack()
    {
        var folder = Path.Combine(Path.GetTempPath(), "EousAssistant-skin", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "settings.json");
        try
        {
            Directory.CreateDirectory(folder);
            foreach (var skin in new[] { SkinCatalog.DefaultId, SkinCatalog.FocusDarkId, "acrylic-glass", "clear-surface" })
            foreach (var layout in new[] { PanelDesignCatalog.DefaultId, "compact-list", "icon-grid" })
            {
                var legacy = new UserSettings { SkinId = skin, DesignSchemeId = layout, Theme = "Dark", LargeText = true, CandidateCount = 7 };
                legacy.FileTypes[".txt"] = new FileTypePreference { CandidateOrder = ["my-editor"] };
                File.WriteAllText(path, JsonSerializer.Serialize(legacy));
                var store = new JsonSettingsStore(path);
                var loaded = store.Load();
                var expectedSkin = skin == SkinCatalog.FocusDarkId ? SkinCatalog.FocusDarkId : SkinCatalog.DefaultId;
                Check(store.LastError is null && loaded.SkinId == expectedSkin && loaded.DesignSchemeId == PanelDesignCatalog.DefaultId, "legacy appearance ids must load with supported defaults");
                Check(loaded.Theme == "Dark" && loaded.LargeText && loaded.CandidateCount == 7 && loaded.FileTypes[".txt"].CandidateOrder.SequenceEqual(new[] { "my-editor" }), "fallback must preserve independent preferences and software ordering");
                store.Save(legacy);
                var saved = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path))!;
                Check(store.LastError is null && saved.SkinId == expectedSkin && saved.DesignSchemeId == PanelDesignCatalog.DefaultId, "saving legacy settings must persist supported ids");
            }
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    private static double Contrast(string a, string b)
    {
        double L(string s)
        {
            var c = (Color)ColorConverter.ConvertFromString(s);
            double Linear(byte v) { var x = v / 255d; return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
            return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        }
        var x = L(a); var y = L(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
