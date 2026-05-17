using System;
using System.Text.Json;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private const string OptionSettingsPath = "user://settings/options.json";

    internal sealed class OptionSettingsData
    {
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

    private static void CopyButtonTheme(Button source, Button target)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
        {
            var style = source.GetThemeStylebox(state);
            if (style != null)
            {
                target.AddThemeStyleboxOverride(state, style);
            }
        }

        foreach (var colorName in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_disabled_color" })
        {
            target.AddThemeColorOverride(colorName, source.GetThemeColor(colorName));
        }
    }

    private string GetRestoreLayoutButtonText() => Localize("ui.restore_layout", "Restore Layout");

    private string GetRestoreLayoutSavedMessage() => Localize("log.layout_restored", "Layout restored to default.");

    private void ApplyAudioSettings()
    {
        GameAudioController.Instance?.SetBgmEnabled(_bgmEnabled);
        GameAudioController.Instance?.SetSfxEnabled(_sfxEnabled);
        GameAudioController.Instance?.SetBgmVolume(_bgmVolume);
        GameAudioController.Instance?.SetSfxVolume(_sfxVolume);
        SetBusMute(_sfxEnabled, "Sfx", "SFX");
    }

    public void ReapplyOptionSettings()
    {
        ApplyAudioSettings();
        _systemUiController?.RefreshText();
    }

    private static void SetBusMute(bool enabled, params string[] busNames)
    {
        foreach (var busName in busNames)
        {
            var busIndex = AudioServer.GetBusIndex(busName);
            if (busIndex >= 0)
            {
                AudioServer.SetBusMute(busIndex, !enabled);
            }
        }
    }

    private void LoadOptionSettings()
    {
        var resolvedPath = ProjectSettings.GlobalizePath(OptionSettingsPath);
        if (!File.Exists(resolvedPath))
        {
            return;
        }

        var json = File.ReadAllText(resolvedPath);
        var settings = JsonSerializer.Deserialize<OptionSettingsData>(json);
        if (settings == null)
        {
            return;
        }

        _bgmEnabled = settings.BgmEnabled;
        _sfxEnabled = settings.SfxEnabled;
        _bgmVolume = Mathf.Clamp(settings.BgmVolume, 0.0f, 1.0f);
        _sfxVolume = Mathf.Clamp(settings.SfxVolume, 0.0f, 1.0f);
        ApplyLoadedFloatingPanelSettings(
            settings.LeftPanelMinimized,
            settings.LeftPanelX,
            settings.LeftPanelY,
            settings.LeftPanelWidth,
            settings.LeftPanelHeight,
            settings.TopBarX,
            settings.TopBarY,
            settings.LogPanelMinimized,
            settings.LogPanelX,
            settings.LogPanelY,
            settings.LogPanelWidth,
            settings.LogPanelHeight);
    }

    private void SaveOptionSettings()
    {
        var resolvedPath = ProjectSettings.GlobalizePath(OptionSettingsPath);
        var directory = System.IO.Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var settings = new OptionSettingsData
        {
            BgmEnabled = _bgmEnabled,
            SfxEnabled = _sfxEnabled,
            BgmVolume = _bgmVolume,
            SfxVolume = _sfxVolume
        };

        PopulateFloatingPanelSettings(settings);
        File.WriteAllText(resolvedPath, JsonSerializer.Serialize(settings));
    }

    private string BuildSaveSlotPath(int slotNumber)
    {
        return $"user://saves/slot{slotNumber:00}.json";
    }

    private string BuildSaveSlotListText(SaveSlotSummary summary)
    {
        var description = !summary.Exists
            ? GetEmptySlotText()
            : (string.IsNullOrWhiteSpace(summary.Description) ? GetNoDescriptionText() : summary.Description);

        return LocalizeFormat("fmt.save_slot_list_item", "{0} {1}", GetSlotPrefix(summary.SlotIndex), description);
    }

    private string BuildSaveSlotSummaryText(SaveSlotSummary summary)
    {
        if (!summary.Exists)
        {
            return Localize("fmt.save_slot_empty_summary", "This save slot is empty.\nEnter a description and save the game here.");
        }

        var savedTime = FormatSavedTime(summary.SavedAtUtc);
        var useChinese = _localization?.IsTraditionalChinese != false;
        var storyName = useChinese
            ? (!string.IsNullOrWhiteSpace(summary.StoryNameZhHant) ? summary.StoryNameZhHant : summary.StoryNameEn)
            : (!string.IsNullOrWhiteSpace(summary.StoryNameEn) ? summary.StoryNameEn : summary.StoryNameZhHant);
        var description = string.IsNullOrWhiteSpace(summary.Description) ? GetNoDescriptionText() : summary.Description;

        return LocalizeFormat(
            "fmt.save_slot_summary",
            "Slot: {0}\nDescription: {1}\nStory: {2}\nSaved: {3}\nProgress: Year {4}, Month {5}",
            summary.SlotIndex,
            description,
            storyName,
            savedTime,
            summary.Year,
            summary.Month);
    }

    private string FormatSavedTime(string savedAtUtc)
    {
        if (DateTime.TryParse(savedAtUtc, out var dateTime))
        {
            return dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        return Localize("ui.unknown", "Unknown");
    }

    private string GetOptionButtonText() => Localize("ui.option", "Option");
    private string GetOptionDialogTitle() => Localize("ui.options", "Options");
    private string GetOptionSaveLoadButtonText() => Localize("ui.save_load", "Save / Load");
    private string GetSaveSettingsButtonText() => Localize("ui.save_settings", "Save Settings");
    private string GetBgmVolumeLabelText() => Localize("ui.bgm_volume", "BGM Volume");
    private string GetSfxVolumeLabelText() => Localize("ui.sfx_volume", "SFX Volume");
    private string GetSaveLoadDialogTitle() => Localize("ui.save_load", "Save / Load");
    private string GetSaveSlotListLabel() => Localize("ui.save_slots", "Save Slots");
    private string GetSaveDescriptionLabel() => Localize("ui.description", "Description");
    private string GetSaveSummaryLabel() => Localize("ui.save_details", "Save Details");
    private string GetSaveDescriptionPlaceholder() => Localize("ui.save_description_placeholder", "Enter a description for this slot");
    private string GetSaveButtonText() => Localize("ui.save", "Save");
    private string GetLoadButtonText() => Localize("ui.load", "Load");
    private string GetCloseButtonText() => Localize("ui.close", "Close");
    private string GetEmptySlotText() => Localize("ui.empty", "Empty");
    private string GetNoDescriptionText() => Localize("ui.no_description", "No Description");
    private string GetSlotPrefix(int slotIndex) => LocalizeFormat("fmt.save_slot_prefix", "Slot {0}", slotIndex);

    private string GetAudioToggleButtonText(bool isBgm, bool enabled)
    {
        var label = isBgm ? "BGM" : "SFX";
        var state = enabled ? Localize("ui.on", "On") : Localize("ui.off", "Off");
        return LocalizeFormat("fmt.audio_toggle_button", "{0}: {1}", label, state);
    }

    private string GetSaveSlotSavedMessage(int slotNumber) => LocalizeFormat("log.save_slot_saved", "Saved to slot {0}.", slotNumber);
    private string GetSaveSlotSaveFailedMessage(int slotNumber) => LocalizeFormat("log.save_slot_save_failed", "Save to slot {0} failed.", slotNumber);
    private string GetSaveSlotMissingMessage(int slotNumber) => LocalizeFormat("log.save_slot_missing", "Save slot {0} is empty.", slotNumber);
    private string GetSaveSlotLoadedMessage(int slotNumber) => LocalizeFormat("log.save_slot_loaded", "Loaded slot {0}.", slotNumber);
    private string GetSaveLoadConfirmTitle() => Localize("ui.confirm", "Confirm");
    private string GetConfirmYesText() => Localize("ui.confirm", "Confirm");
    private string GetConfirmNoText() => Localize("ui.cancel", "Cancel");
    private string GetSaveConfirmMessage(int slotNumber) => LocalizeFormat("fmt.save_confirm_message", "Save to slot {0}?", slotNumber);
    private string GetLoadConfirmMessage(int slotNumber) => LocalizeFormat("fmt.load_confirm_message", "Load slot {0}?", slotNumber);
    private string GetOptionSettingsSavedMessage() => Localize("log.option_settings_saved", "Option settings saved.");
    private string GetOptionLanguageButtonText() => Localize("ui.option_language_toggle", "Language: English / 繁體中文");
    private string GetOptionGodModeButtonText() => LocalizeFormat(
        "fmt.option_god_mode",
        "{0}: {1}",
        Localize("ui.option_god_mode", "God Mode"),
        (_turnManager?.World?.ViewAllInformationEnabled ?? false) ? Localize("ui.on", "On") : Localize("ui.off", "Off"));

    private string Localize(string key, string fallback)
    {
        if (_localization == null)
        {
            return fallback;
        }

        var localized = _localization.T(key);
        return string.Equals(localized, key, StringComparison.Ordinal) ? fallback : localized;
    }

    private string LocalizeFormat(string key, string fallback, params object[] args)
    {
        if (_localization == null)
        {
            return string.Format(fallback, args);
        }

        var template = _localization.T(key);
        if (string.Equals(template, key, StringComparison.Ordinal))
        {
            template = fallback;
        }

        return string.Format(template, args);
    }
}
