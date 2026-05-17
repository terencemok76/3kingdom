using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    internal Window? SystemOptionDialog => _optionDialog;
    internal Window? SystemSaveLoadDialog => _saveLoadDialog;
    internal Window? SystemSaveLoadConfirmDialog => _saveLoadConfirmDialog;
    internal HBoxContainer? SystemTopBar => GetNodeOrNull<HBoxContainer>("Root/TopBar");
    internal GridContainer? SystemCommandButtons => _commandButtons;
    internal TurnManager? SystemTurnManager => _turnManager;
    internal WorldRepository? SystemWorldRepository => _worldRepository;
    internal LocalizationService? SystemLocalization => _localization;

    internal bool SystemBgmEnabled
    {
        get => _bgmEnabled;
        set => _bgmEnabled = value;
    }

    internal bool SystemSfxEnabled
    {
        get => _sfxEnabled;
        set => _sfxEnabled = value;
    }

    internal float SystemBgmVolume
    {
        get => _bgmVolume;
        set => _bgmVolume = value;
    }

    internal float SystemSfxVolume
    {
        get => _sfxVolume;
        set => _sfxVolume = value;
    }

    internal void SystemPopupDialog(Window? dialog) => PopupDialogUsingSceneSize(dialog);
    internal void SystemPlayUiClickSfx() => PlayUiClickSfx();
    internal void SystemAddLog(string message, bool isPlayerRelated = false) => AddLog(message, isPlayerRelated);
    internal void SystemToggleLanguage() => OnLanguageButtonPressed();
    internal void SystemToggleGodMode() => OnGodModePressed();
    internal void SystemApplyAudioSettings() => ApplyAudioSettings();
    internal void SystemSaveOptionSettings() => SaveOptionSettings();
    internal void SystemRestoreDefaultLayout() => RestoreDefaultFloatingPanelLayout();
    internal void SystemApplyLoadedWorld(WorldState loadedWorld) => ApplyLoadedWorld(loadedWorld);
    internal void SystemApplyButtonTheme(Button button)
    {
        if (MainHudViewButton != null)
        {
            CopyButtonTheme(MainHudViewButton, button);
        }
    }

    internal void SystemApplyOptionEntryTheme(Button button)
    {
        if (MainHudEndTurnButton != null)
        {
            CopyButtonTheme(MainHudEndTurnButton, button);
            button.CustomMinimumSize = MainHudEndTurnButton.CustomMinimumSize;
        }
        else if (MainHudViewButton != null)
        {
            CopyButtonTheme(MainHudViewButton, button);
        }
    }

    internal string SystemGetOptionButtonText() => GetOptionButtonText();
    internal string SystemGetOptionDialogTitle() => GetOptionDialogTitle();
    internal string SystemGetOptionSaveLoadButtonText() => GetOptionSaveLoadButtonText();
    internal string SystemGetOptionLanguageButtonText() => GetOptionLanguageButtonText();
    internal string SystemGetOptionGodModeButtonText() => GetOptionGodModeButtonText();
    internal string SystemGetAudioToggleButtonText(bool isBgm, bool enabled) => GetAudioToggleButtonText(isBgm, enabled);
    internal string SystemGetSaveSettingsButtonText() => GetSaveSettingsButtonText();
    internal string SystemGetRestoreLayoutButtonText() => GetRestoreLayoutButtonText();
    internal string SystemGetBgmVolumeLabelText() => GetBgmVolumeLabelText();
    internal string SystemGetSfxVolumeLabelText() => GetSfxVolumeLabelText();
    internal string SystemGetOptionSettingsSavedMessage() => GetOptionSettingsSavedMessage();
    internal string SystemGetRestoreLayoutSavedMessage() => GetRestoreLayoutSavedMessage();

    internal string SystemGetSaveLoadDialogTitle() => GetSaveLoadDialogTitle();
    internal string SystemGetSaveSlotListLabel() => GetSaveSlotListLabel();
    internal string SystemGetSaveDescriptionLabel() => GetSaveDescriptionLabel();
    internal string SystemGetSaveSummaryLabel() => GetSaveSummaryLabel();
    internal string SystemGetSaveDescriptionPlaceholder() => GetSaveDescriptionPlaceholder();
    internal string SystemGetSaveButtonText() => GetSaveButtonText();
    internal string SystemGetLoadButtonText() => GetLoadButtonText();
    internal string SystemGetCloseButtonText() => GetCloseButtonText();
    internal string SystemGetSaveLoadConfirmTitle() => GetSaveLoadConfirmTitle();
    internal string SystemGetConfirmYesText() => GetConfirmYesText();
    internal string SystemGetConfirmNoText() => GetConfirmNoText();
    internal string SystemGetSaveConfirmMessage(int slotNumber) => GetSaveConfirmMessage(slotNumber);
    internal string SystemGetLoadConfirmMessage(int slotNumber) => GetLoadConfirmMessage(slotNumber);
    internal string SystemGetSaveSlotSavedMessage(int slotNumber) => GetSaveSlotSavedMessage(slotNumber);
    internal string SystemGetSaveSlotSaveFailedMessage(int slotNumber) => GetSaveSlotSaveFailedMessage(slotNumber);
    internal string SystemGetSaveSlotMissingMessage(int slotNumber) => GetSaveSlotMissingMessage(slotNumber);
    internal string SystemGetSaveSlotLoadedMessage(int slotNumber) => GetSaveSlotLoadedMessage(slotNumber);
    internal string SystemBuildSaveSlotPath(int slotNumber) => BuildSaveSlotPath(slotNumber);
    internal string SystemBuildSaveSlotListText(SaveSlotSummary summary) => BuildSaveSlotListText(summary);
    internal string SystemBuildSaveSlotSummaryText(SaveSlotSummary summary) => BuildSaveSlotSummaryText(summary);
}
