using System.IO;
using System.Text.Json;
using Godot;

namespace ThreeKingdom.Core;

public sealed class OptionSettingsData
{
    public GameLanguage Language { get; set; } = GameLanguage.TraditionalChinese;
    public bool BgmEnabled { get; set; } = true;
    public bool SfxEnabled { get; set; } = true;
    public float BgmVolume { get; set; } = 1.0f;
    public float SfxVolume { get; set; } = 1.0f;
    public bool LeftPanelMinimized { get; set; }
    public float LeftPanelX { get; set; } = 10.0f;
    public float LeftPanelY { get; set; } = 70.0f;
    public float LeftPanelWidth { get; set; } = 320.0f;
    public float LeftPanelHeight { get; set; } = 790.0f;
    public float TopBarX { get; set; } = 10.0f;
    public float TopBarY { get; set; } = 10.0f;
    public bool LogPanelMinimized { get; set; }
    public float LogPanelX { get; set; } = 370.0f;
    public float LogPanelY { get; set; } = 700.0f;
    public float LogPanelWidth { get; set; } = 1210.0f;
    public float LogPanelHeight { get; set; } = 180.0f;
}

public static class OptionSettingsStore
{
    public const string OptionSettingsPath = "user://settings/options.json";

    public static OptionSettingsData LoadOrDefault()
    {
        var resolvedPath = ProjectSettings.GlobalizePath(OptionSettingsPath);
        if (!File.Exists(resolvedPath))
        {
            return new OptionSettingsData();
        }

        var json = File.ReadAllText(resolvedPath);
        var settings = JsonSerializer.Deserialize<OptionSettingsData>(json);
        return settings ?? new OptionSettingsData();
    }

    public static void Save(OptionSettingsData settings)
    {
        var resolvedPath = ProjectSettings.GlobalizePath(OptionSettingsPath);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(resolvedPath, JsonSerializer.Serialize(settings));
    }
}
