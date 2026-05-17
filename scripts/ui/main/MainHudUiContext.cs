using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class MainHudUiContext
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

    public Label? MonthLabel => _owner.MainHudMonthLabel;
    public Label? PlayerFactionLabel => _owner.MainHudPlayerFactionLabel;
    public Label? StoryLabel => _owner.MainHudStoryLabel;
    public Button? LanguageButton => _owner.MainHudLanguageButton;
    public Button? GodModeButton => _owner.MainHudGodModeButton;
    public Button? EndTurnButton => _owner.MainHudEndTurnButton;

    public Label? CityNameLabel => _owner.MainHudCityNameLabel;
    public RichTextLabel? CityStatsLabel => _owner.MainHudCityStatsLabel;
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

    public RichTextLabel? LogText => _owner.MainHudLogText;
    public Label? CityPanelHeaderLabel => _owner.MainHudCityPanelHeaderLabel;
    public Label? LogPanelHeaderLabel => _owner.MainHudLogPanelHeaderLabel;

    public string BuildGodModeButtonText() => _owner.MainHudBuildGodModeButtonText();
    public string BuildCityStatsText(string ownerName, CityData? city, int freeOfficerCount) => _owner.MainHudBuildCityStatsText(ownerName, city, freeOfficerCount);
    public void UpdateGameplayButtonStates() => _owner.MainHudUpdateGameplayButtonStates();
    public void RequestFloatingPanelLayoutRefresh() => _owner.MainHudRequestFloatingPanelLayoutRefresh();
    public void MoveToFront(CanvasItem? item) => _owner.MainHudMoveToFront(item);
    public void ToggleLanguage() => _owner.MainHudToggleLanguage();
    public void ToggleGodMode() => _owner.MainHudToggleGodMode();
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
}
