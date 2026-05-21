using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class SystemUiContext : IFloatingOverlayContext
{
    private readonly HudController _owner;

    public SystemUiContext(HudController owner)
    {
        _owner = owner;
    }

    public HBoxContainer? TopBar => _owner.SystemTopBar;
    public GridContainer? CommandButtons => _owner.SystemCommandButtons;
    public TurnManager? TurnManager => _owner.SystemTurnManager;
    public WorldRepository? WorldRepository => _owner.SystemWorldRepository;
    public LocalizationService? Localization => _owner.SystemLocalization;

    public bool BgmEnabled
    {
        get => _owner.SystemBgmEnabled;
        set => _owner.SystemBgmEnabled = value;
    }

    public bool SfxEnabled
    {
        get => _owner.SystemSfxEnabled;
        set => _owner.SystemSfxEnabled = value;
    }

    public float BgmVolume
    {
        get => _owner.SystemBgmVolume;
        set => _owner.SystemBgmVolume = value;
    }

    public float SfxVolume
    {
        get => _owner.SystemSfxVolume;
        set => _owner.SystemSfxVolume = value;
    }

    public Control CreateOverlay(string scenePath, System.Action closeAction)
    {
        var dialog = GD.Load<PackedScene>(scenePath).Instantiate<Control>();
        dialog.Visible = false;
        var parent = _owner.GetNodeOrNull<Control>("Root") as Node ?? _owner;
        parent.AddChild(dialog);
        return dialog;
    }

    public void PopupDialog(Control? dialog)
    {
        if (dialog == null)
        {
            return;
        }

        dialog.Show();
        dialog.MoveToFront();
    }

    public void CloseOverlay(System.Action closeAction)
    {
        PlayUiClickSfx();
        closeAction();
    }

    public void BringOverlayToFront(CanvasItem? item) => item?.MoveToFront();

    public void PlayUiClickSfx() => _owner.SystemPlayUiClickSfx();
    public void AddLog(string message, bool isPlayerRelated = false) => _owner.SystemAddLog(message, isPlayerRelated);
    public void ToggleLanguage() => _owner.SystemToggleLanguage();
    public void ToggleGodMode() => _owner.SystemToggleGodMode();
    public void ApplyAudioSettings() => _owner.SystemApplyAudioSettings();
    public void SaveOptionSettings() => _owner.SystemSaveOptionSettings();
    public void RestoreDefaultLayout() => _owner.SystemRestoreDefaultLayout();
    public void ApplyLoadedWorld(WorldState loadedWorld) => _owner.SystemApplyLoadedWorld(loadedWorld);
    public void ApplyButtonTheme(Button button) => _owner.SystemApplyButtonTheme(button);
    public void ApplyOptionEntryTheme(Button button) => _owner.SystemApplyOptionEntryTheme(button);

    public string GetOptionButtonText() => _owner.SystemGetOptionButtonText();
    public string GetOptionDialogTitle() => _owner.SystemGetOptionDialogTitle();
    public string GetOptionSaveLoadButtonText() => _owner.SystemGetOptionSaveLoadButtonText();
    public string GetOptionLanguageButtonText() => _owner.SystemGetOptionLanguageButtonText();
    public string GetOptionGodModeButtonText() => _owner.SystemGetOptionGodModeButtonText();
    public string GetAudioToggleButtonText(bool isBgm, bool enabled) => _owner.SystemGetAudioToggleButtonText(isBgm, enabled);
    public string GetSaveSettingsButtonText() => _owner.SystemGetSaveSettingsButtonText();
    public string GetRestoreLayoutButtonText() => _owner.SystemGetRestoreLayoutButtonText();
    public string GetBgmVolumeLabelText() => _owner.SystemGetBgmVolumeLabelText();
    public string GetSfxVolumeLabelText() => _owner.SystemGetSfxVolumeLabelText();
    public string GetOptionSettingsSavedMessage() => _owner.SystemGetOptionSettingsSavedMessage();
    public string GetRestoreLayoutSavedMessage() => _owner.SystemGetRestoreLayoutSavedMessage();

    public string GetSaveLoadDialogTitle() => _owner.SystemGetSaveLoadDialogTitle();
    public string GetSaveSlotListLabel() => _owner.SystemGetSaveSlotListLabel();
    public string GetSaveDescriptionLabel() => _owner.SystemGetSaveDescriptionLabel();
    public string GetSaveSummaryLabel() => _owner.SystemGetSaveSummaryLabel();
    public string GetSaveDescriptionPlaceholder() => _owner.SystemGetSaveDescriptionPlaceholder();
    public string GetSaveButtonText() => _owner.SystemGetSaveButtonText();
    public string GetLoadButtonText() => _owner.SystemGetLoadButtonText();
    public string GetCloseButtonText() => _owner.SystemGetCloseButtonText();
    public string GetSaveLoadConfirmTitle() => _owner.SystemGetSaveLoadConfirmTitle();
    public string GetConfirmYesText() => _owner.SystemGetConfirmYesText();
    public string GetConfirmNoText() => _owner.SystemGetConfirmNoText();
    public string GetSaveConfirmMessage(int slotNumber) => _owner.SystemGetSaveConfirmMessage(slotNumber);
    public string GetLoadConfirmMessage(int slotNumber) => _owner.SystemGetLoadConfirmMessage(slotNumber);
    public string GetSaveSlotSavedMessage(int slotNumber) => _owner.SystemGetSaveSlotSavedMessage(slotNumber);
    public string GetSaveSlotSaveFailedMessage(int slotNumber) => _owner.SystemGetSaveSlotSaveFailedMessage(slotNumber);
    public string GetSaveSlotMissingMessage(int slotNumber) => _owner.SystemGetSaveSlotMissingMessage(slotNumber);
    public string GetSaveSlotLoadedMessage(int slotNumber) => _owner.SystemGetSaveSlotLoadedMessage(slotNumber);
    public string BuildSaveSlotPath(int slotNumber) => _owner.SystemBuildSaveSlotPath(slotNumber);
    public string BuildSaveSlotListText(SaveSlotSummary summary) => _owner.SystemBuildSaveSlotListText(summary);
    public string BuildSaveSlotSummaryText(SaveSlotSummary summary) => _owner.SystemBuildSaveSlotSummaryText(summary);
}
