using Godot;
using System;
using ThreeKingdom.Core;
using static ThreeKingdom.Battle.BattleResourcePaths;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private void InitializeBattleLocalization()
    {
        _localization.Load();
        var settings = LoadBattleOptionSettings();
        _localization.SetLanguage(settings.Language);
        RefreshBattleOptionDialogText();
    }


    private OptionSettingsData LoadBattleOptionSettings()
    {
        var settings = OptionSettingsStore.LoadOrDefault();
        _battleBgmEnabled = settings.BgmEnabled;
        _battleSfxEnabled = settings.SfxEnabled;
        _battleBgmVolume = Mathf.Clamp(settings.BgmVolume, 0.0f, 1.0f);
        _battleSfxVolume = Mathf.Clamp(settings.SfxVolume, 0.0f, 1.0f);
        return settings;
    }

    private void SaveBattleOptionSettings()
    {
        var settings = OptionSettingsStore.LoadOrDefault();
        settings.Language = _localization.CurrentLanguage;
        settings.BgmEnabled = _battleBgmEnabled;
        settings.SfxEnabled = _battleSfxEnabled;
        settings.BgmVolume = _battleBgmVolume;
        settings.SfxVolume = _battleSfxVolume;
        OptionSettingsStore.Save(settings);
    }

    private void ApplyBattleAudioSettings()
    {
        GameAudioController.Instance?.SetBgmEnabled(_battleBgmEnabled);
        GameAudioController.Instance?.SetSfxEnabled(_battleSfxEnabled);
        GameAudioController.Instance?.SetBgmVolume(_battleBgmVolume);
        GameAudioController.Instance?.SetSfxVolume(_battleSfxVolume);
        SetAudioBusMute(_battleSfxEnabled, "Sfx", "SFX");
    }

    private static void SetAudioBusMute(bool enabled, params string[] busNames)
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

    private string BattleText(string key, string fallback)
    {
        var text = _localization.T(key);
        return string.Equals(text, key, StringComparison.OrdinalIgnoreCase) ? fallback : text;
    }

    private string BattleFormat(string key, string fallback, params object[] args)
    {
        var template = BattleText(key, fallback);
        return string.Format(template, args);
    }

    private void ApplyPendingLaunchOptions()
    {
        if (PendingLaunchOptions == null)
        {
            return;
        }

        ScenarioType = PendingLaunchOptions.ScenarioType;
        UseEditorAuthoredLayout = PendingLaunchOptions.UseEditorAuthoredLayout;
        PendingLaunchOptions = null;
    }

    private void StartBattleBgm()
    {
        var audioController = GameAudioController.Instance;
        if (audioController == null)
        {
            _battleAudioController = new GameAudioController
            {
                Name = "BattleAudioController"
            };
            AddChild(_battleAudioController);
            audioController = GameAudioController.Instance;
        }

        if (audioController == null)
        {
            return;
        }

        ApplyBattleAudioSettings();
        audioController.PlayBattleBgm();
    }

    private void OnBattleSaveButtonPressed()
    {
        if (TrySaveBattleQuickSave(out var errorMessage))
        {
            AppendBattleLog(GetCurrentTurnSideName(), "Save", $"Battle quick save completed: {BattleQuickSavePath}");
            return;
        }

        AppendBattleLog(GetCurrentTurnSideName(), "Save", $"Battle quick save failed: {errorMessage}");
    }

    private void OnBattleLoadButtonPressed()
    {
        if (TryLoadBattleQuickSave(out var errorMessage))
        {
            AppendBattleLog(GetCurrentTurnSideName(), "Load", $"Battle quick load completed: {BattleQuickSavePath}");
            return;
        }

        AppendBattleLog(GetCurrentTurnSideName(), "Load", $"Battle quick load failed: {errorMessage}");
    }

    private void OnBattleOptionButtonPressed()
    {
        ShowBattleOptionDialog();
    }

    private void ShowBattleOptionDialog()
    {
        RefreshBattleOptionDialogText();
        if (_battleOptionOverlay != null)
        {
            _battleOptionOverlay.Visible = true;
        }
    }

    private void HideBattleOptionDialog()
    {
        if (_battleOptionOverlay != null)
        {
            _battleOptionOverlay.Visible = false;
        }
    }

    private void OnBattleOptionSaveButtonPressed()
    {
        OnBattleSaveButtonPressed();
        RefreshBattleOptionDialogText();
    }

    private void OnBattleOptionLoadButtonPressed()
    {
        OnBattleLoadButtonPressed();
        RefreshBattleOptionDialogText();
    }

    private void OnBattleOptionLanguageButtonPressed()
    {
        _localization.ToggleLanguage();
        ConfigureHud();
        RefreshMarkerNamePlates();
        RefreshBattleLogPanel();
        RefreshBattleOptionDialogText();
        if (_commandMenu?.Visible == true)
        {
            var commandMenuPosition = _commandMenu.Position;
            ShowCommandMenu(commandMenuPosition - new Vector2(12.0f, 12.0f));
            _commandMenu.Position = commandMenuPosition;
        }
    }

    private void OnBattleBgmToggleButtonPressed()
    {
        _battleBgmEnabled = !_battleBgmEnabled;
        ApplyBattleAudioSettings();
        RefreshBattleOptionDialogText();
    }

    private void OnBattleSfxToggleButtonPressed()
    {
        _battleSfxEnabled = !_battleSfxEnabled;
        ApplyBattleAudioSettings();
        RefreshBattleOptionDialogText();
    }

    private void OnBattleBgmVolumeChanged(double value)
    {
        _battleBgmVolume = Mathf.Clamp((float)value / 100.0f, 0.0f, 1.0f);
        ApplyBattleAudioSettings();
        RefreshBattleOptionDialogText(updateSliderValues: false);
    }

    private void OnBattleSfxVolumeChanged(double value)
    {
        _battleSfxVolume = Mathf.Clamp((float)value / 100.0f, 0.0f, 1.0f);
        ApplyBattleAudioSettings();
        RefreshBattleOptionDialogText(updateSliderValues: false);
    }

    private void OnBattleSaveSettingsButtonPressed()
    {
        SaveBattleOptionSettings();
        AppendBattleLog(GetCurrentTurnSideName(), "Option", BattleText("log.option_settings_saved", "Option settings saved."));
        RefreshBattleOptionDialogText();
    }

    private void RefreshBattleOptionDialogText(bool updateSliderValues = true)
    {
        if (_battleOptionButton != null)
        {
            _battleOptionButton.Text = BattleText("ui.option", "Option");
        }

        if (_battleOptionTitleLabel != null)
        {
            _battleOptionTitleLabel.Text = BattleText("ui.options", "Options");
        }

        if (_battleOptionSaveButton != null)
        {
            _battleOptionSaveButton.Text = BattleText("ui.battle.save", "Save");
        }

        if (_battleOptionLoadButton != null)
        {
            _battleOptionLoadButton.Text = BattleText("ui.battle.load", "Load");
        }

        if (_battleOptionLanguageButton != null)
        {
            _battleOptionLanguageButton.Text = BattleText("ui.option_language_toggle", "Language: English / 繁體中文");
        }

        if (_battleBgmToggleButton != null)
        {
            _battleBgmToggleButton.Text = FormatBattleAudioToggleText(isBgm: true, _battleBgmEnabled);
        }

        if (_battleSfxToggleButton != null)
        {
            _battleSfxToggleButton.Text = FormatBattleAudioToggleText(isBgm: false, _battleSfxEnabled);
        }

        if (_battleBgmVolumeValueLabel != null)
        {
            _battleBgmVolumeValueLabel.Text = FormatBattleVolumePercent(_battleBgmVolume);
        }

        if (_battleSfxVolumeValueLabel != null)
        {
            _battleSfxVolumeValueLabel.Text = FormatBattleVolumePercent(_battleSfxVolume);
        }

        if (updateSliderValues)
        {
            if (_battleBgmVolumeSlider != null)
            {
                _battleBgmVolumeSlider.SetValueNoSignal(Mathf.RoundToInt(_battleBgmVolume * 100.0f));
            }

            if (_battleSfxVolumeSlider != null)
            {
                _battleSfxVolumeSlider.SetValueNoSignal(Mathf.RoundToInt(_battleSfxVolume * 100.0f));
            }
        }

        if (_battleSaveSettingsButton != null)
        {
            _battleSaveSettingsButton.Text = BattleText("ui.save_settings", "Save Settings");
        }
    }

    private string FormatBattleAudioToggleText(bool isBgm, bool enabled)
    {
        var label = isBgm ? "BGM" : "SFX";
        var state = enabled ? BattleText("ui.on", "On") : BattleText("ui.off", "Off");
        return BattleFormat("fmt.audio_toggle_button", "{0}: {1}", label, state);
    }

    private static string FormatBattleVolumePercent(float volume)
    {
        return $"{Mathf.RoundToInt(Mathf.Clamp(volume, 0.0f, 1.0f) * 100.0f)}%";
    }



}
