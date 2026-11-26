using Godot;
using System;
using ThreeKingdom.Core;

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

    private void OnBattleOptionButtonPressed()
    {
        ShowBattleOptionDialog();
    }

    private void OnBattleOptionDebugButtonPressed()
    {
        HideBattleOptionDialog();
        OnBattleDebugButtonPressed();
    }

    private void ShowBattleOptionDialog()
    {
        HideCommandMenu();
        var status = GetNodeOrNull<Label>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SaveSettingsStatus");
        if (status != null) status.Visible = false;
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

    private void OnBattleOptionSaveLoadButtonPressed()
    {
        ShowCampaignSaveLoadDialog();
        RefreshBattleOptionDialogText();
    }

    private void OnBattleOptionLanguageButtonPressed()
    {
        ClearBattleOptionSaveStatus();
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
        ClearBattleOptionSaveStatus();
        _battleBgmEnabled = !_battleBgmEnabled;
        ApplyBattleAudioSettings();
        RefreshBattleOptionDialogText();
    }

    private void OnBattleSfxToggleButtonPressed()
    {
        ClearBattleOptionSaveStatus();
        _battleSfxEnabled = !_battleSfxEnabled;
        ApplyBattleAudioSettings();
        RefreshBattleOptionDialogText();
    }

    private void OnBattleBgmVolumeChanged(double value)
    {
        ClearBattleOptionSaveStatus();
        _battleBgmVolume = Mathf.Clamp((float)value / 100.0f, 0.0f, 1.0f);
        ApplyBattleAudioSettings();
        RefreshBattleOptionDialogText(updateSliderValues: false);
    }

    private void OnBattleSfxVolumeChanged(double value)
    {
        ClearBattleOptionSaveStatus();
        _battleSfxVolume = Mathf.Clamp((float)value / 100.0f, 0.0f, 1.0f);
        ApplyBattleAudioSettings();
        RefreshBattleOptionDialogText(updateSliderValues: false);
    }

    private void OnBattleSaveSettingsButtonPressed()
    {
        var status = GetNodeOrNull<Label>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SaveSettingsStatus");
        try
        {
            SaveBattleOptionSettings();
            var message = BattleText("log.option_settings_saved", "Option settings saved.");
            AppendBattleLog(GetCurrentTurnSideName(), "Option", message);
            if (status != null)
            {
                status.Text = message;
                status.AddThemeColorOverride("font_color", new Color(0.84f, 0.92f, 0.68f));
                status.Visible = true;
            }
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Battle option settings could not be saved: {exception.Message}");
            if (status != null)
            {
                status.Text = BattleText("ui.battle.option_settings_save_failed", "Could not save option settings.");
                status.AddThemeColorOverride("font_color", new Color(1.0f, 0.48f, 0.42f));
                status.Visible = true;
            }
        }
        RefreshBattleOptionDialogText();
    }

    private void ClearBattleOptionSaveStatus()
    {
        var status = GetNodeOrNull<Label>("UiLayer/BattleOptionOverlay/Center/Panel/Margin/OptionRoot/SaveSettingsStatus");
        if (status != null) status.Visible = false;
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

        if (_battleOptionSaveLoadButton != null)
        {
            _battleOptionSaveLoadButton.Text = BattleText("ui.save_load", "Save / Load");
            _battleOptionSaveLoadButton.Disabled = _activeCampaign == null || CampaignRuntimeContext.World == null;
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

    private void ApplyBattleOptionDialogStyle()
    {
        var debugPanel = GetNodeOrNull<PanelContainer>("UiLayer/BattleDebugOverlay/Center/Panel");
        if (debugPanel != null)
        {
            debugPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.05f, 0.05f, 0.06f, 0.88f),
                BorderColor = new Color(0.58f, 0.48f, 0.28f, 1.0f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.34f),
                ShadowSize = 8
            });
        }

        foreach (var label in new[]
        {
            _battleOptionTitleLabel,
            _battleDebugTitleLabel,
            _battleBgmVolumeValueLabel,
            _battleSfxVolumeValueLabel
        })
        {
            label?.AddThemeColorOverride("font_color", new Color(0.92f, 0.86f, 0.72f, 1.0f));
        }

        foreach (var button in new[]
        {
            _battleOptionCloseButton,
            _battleDebugCloseButton,
            _enableAiButton,
            _disableAiButton,
            _startRoundButton,
            _nextAiButton,
            _attackerOneDayFoodButton,
            _defenderOneDayFoodButton,
            _monthlyBattleLimitOneDayButton,
            _battleOptionSaveLoadButton,
            _battleOptionLanguageButton,
            _battleOptionDebugButton,
            _battleBgmToggleButton,
            _battleSfxToggleButton,
            _battleSaveSettingsButton
        })
        {
            ApplyBattleOptionButtonStyle(button);
        }

        ApplyBattleOptionSliderStyle(_battleBgmVolumeSlider);
        ApplyBattleOptionSliderStyle(_battleSfxVolumeSlider);
    }

    private static void ApplyBattleOptionButtonStyle(Button? button)
    {
        if (button == null)
        {
            return;
        }

        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.76f, 0.68f, 0.48f, 1.0f),
            BorderColor = new Color(0.42f, 0.33f, 0.18f, 1.0f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5
        };
        var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
        hoverStyle.BgColor = new Color(0.86f, 0.76f, 0.54f, 1.0f);
        var pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
        pressedStyle.BgColor = new Color(0.58f, 0.49f, 0.32f, 1.0f);

        button.AddThemeStyleboxOverride("normal", normalStyle);
        button.AddThemeStyleboxOverride("hover", hoverStyle);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);
        button.AddThemeStyleboxOverride("disabled", pressedStyle);
        button.AddThemeColorOverride("font_color", new Color(0.12f, 0.09f, 0.05f, 1.0f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.10f, 0.07f, 0.04f, 1.0f));
        button.AddThemeColorOverride("font_pressed_color", new Color(0.08f, 0.06f, 0.03f, 1.0f));
    }

    private static void ApplyBattleOptionSliderStyle(HSlider? slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.AddThemeColorOverride("font_color", new Color(0.92f, 0.86f, 0.72f, 1.0f));
    }
}
