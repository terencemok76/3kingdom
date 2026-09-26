using System;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using ThreeKingdom.Map;

namespace ThreeKingdom.UI;

internal sealed class MainHudUiContext : IFloatingOverlayContext
{
    private readonly HudController _owner;

    public MainHudUiContext(HudController owner)
    {
        _owner = owner;
    }

    public LocalizationService? Localization => _owner.MainHudLocalization;
    public TurnManager? TurnManager => _owner.MainHudTurnManager;
    public WorldState? World => _owner.MainHudWorld;
    public CityData? SelectedCity => _owner.MainHudSelectedCity;
    public int PlayerFactionId => _owner.MainHudPlayerFactionId;
    public UiEventHub UiEventHub => _owner.UiEventHub;
    public MapController? MapController => _owner.MainHudMapController;

    public Label? MonthLabel => _owner.MainHudMonthLabel;
    public Label? PlayerFactionLabel => _owner.MainHudPlayerFactionLabel;
    public Label? StoryLabel => _owner.MainHudStoryLabel;
    public Button? LanguageButton => _owner.MainHudLanguageButton;
    public Button? GodModeButton => _owner.MainHudGodModeButton;
    public Button? TestButton => _owner.MainHudTopBarTestButton;
    public Button? EndTurnButton => _owner.MainHudEndTurnButton;

    public Label? CityNameLabel => _owner.MainHudCityNameLabel;
    public VBoxContainer? CityStatsPanel => _owner.MainHudCityStatsPanel;
    public Label? CommandsTitle => _owner.MainHudCommandsTitle;
    public Button? DevelopButton => _owner.MainHudDevelopButton;
    public Button? RecruitButton => _owner.MainHudRecruitButton;
    public Button? MoveButton => _owner.MainHudMoveButton;
    public Button? SearchButton => _owner.MainHudSearchButton;
    public Button? MerchantButton => _owner.MainHudMerchantButton;
    public Button? DiplomacyButton => _owner.MainHudDiplomacyButton;
    public Button? SpyButton => _owner.MainHudSpyButton;
    public Button? PersonnelButton => _owner.MainHudPersonnelButton;
    public Button? AdvisorButton => _owner.MainHudAdvisorButton;
    public Button? CivilButton => _owner.MainHudCivilButton;
    public Button? AttackButton => _owner.MainHudAttackButton;
    public Button? ViewButton => _owner.MainHudViewButton;
    public Button? StrategicMapButton => _owner.MainHudStrategicMapButton;
    public Button? TestCaptureButton => _owner.MainHudTestCaptureButton;

    public RichTextLabel? LogText => _owner.MainHudLogText;
    public Label? CityPanelHeaderLabel => _owner.MainHudCityPanelHeaderLabel;
    public Label? LogPanelHeaderLabel => _owner.MainHudLogPanelHeaderLabel;

    public string BuildGodModeButtonText() => _owner.MainHudBuildGodModeButtonText();
    public bool IsGodModeEnabled() => _owner.MainHudIsGodModeEnabled();
    public string BuildCityHeaderText(CityData? city) => _owner.MainHudBuildCityHeaderText(city);
    public void PopulateCityStats(VBoxContainer panel, string ownerName, CityData? city, int freeOfficerCount) => _owner.MainHudPopulateCityStats(panel, ownerName, city, freeOfficerCount);
    public void UpdateGameplayButtonStates() => _owner.MainHudUpdateGameplayButtonStates();
    public void ApplyCommandButtonTheme(Button button) => _owner.MainHudApplyCommandButtonTheme(button);
    public void RequestFloatingPanelLayoutRefresh() => _owner.MainHudRequestFloatingPanelLayoutRefresh();
    public void MoveToFront(CanvasItem? item) => _owner.MainHudMoveToFront(item);
    public void ToggleLanguage() => _owner.MainHudToggleLanguage();
    public void ToggleGodMode() => _owner.MainHudToggleGodMode();
    public void OpenTestDialog() => _owner.MainHudOpenTestDialog();
    public void EndTurn() => _owner.MainHudEndTurn();
    public void OpenInternalAffairs() => _owner.MainHudOpenInternalAffairs();
    public void OpenMilitary() => _owner.MainHudOpenMilitary();
    public void OpenMove() => _owner.MainHudOpenMove();
    public void OpenSearch() => _owner.MainHudOpenSearch();
    public void OpenMerchant() => _owner.MainHudOpenMerchant();
    public void OpenDiplomacy() => _owner.MainHudOpenDiplomacy();
    public void OpenSpy() => _owner.MainHudOpenSpy();
    public void OpenPersonnel() => _owner.MainHudOpenPersonnel();
    public void OpenAdvisor() => _owner.MainHudOpenAdvisor();
    public void OpenCivil() => _owner.MainHudOpenCivil();
    public void OpenAttack() => _owner.MainHudOpenAttack();
    public void OpenView() => _owner.MainHudOpenView();
    public void OpenStrategicMap() => _owner.MainHudOpenStrategicMap();
    public bool SelectCityById(int cityId) => _owner.MainHudSelectCityById(cityId);
    public void OpenTestCapture() => _owner.MainHudOpenTestCapture();
    public void AddTestBattleEquipment() => _owner.MainHudAddTestBattleEquipment();
    public void AddTestEngineers() => _owner.MainHudAddTestEngineers();
    public void UpgradeTestBowWorkshop() => _owner.MainHudUpgradeTestBowWorkshop();
    public void UpgradeTestHorsePasture() => _owner.MainHudUpgradeTestHorsePasture();

    public Control CreateOverlay(string scenePath, Action closeAction)
    {
        var dialog = GD.Load<PackedScene>(scenePath).Instantiate<Control>();
        dialog.Visible = false;
        Node parent = _owner;
        if (_owner.MainHudOverlayParent != null)
        {
            parent = _owner.MainHudOverlayParent;
        }

        parent.AddChild(dialog);
        return dialog;
    }

    public void PopupDialog(Control? dialog) => _owner.MainHudPopupDialog(dialog);

    public void CloseOverlay(Action closeAction)
    {
        _owner.MainHudPlayUiClickSfx();
        closeAction();
    }

    public void BringOverlayToFront(CanvasItem? item) => _owner.MainHudMoveToFront(item);
}
