using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    internal LocalizationService? MainHudLocalization => _localization;
    internal TurnManager? MainHudTurnManager => _turnManager;
    internal WorldState? MainHudWorld => _turnManager?.World;
    internal int MainHudPlayerFactionId => _turnManager?.GetPlayerFactionId() ?? -1;
    internal CityData? MainHudSelectedCity => _selectedCity;

    internal Label? MainHudMonthLabel => GetNodeOrNull<Label>("Root/TopBar/MonthLabel");
    internal Label? MainHudPlayerFactionLabel => GetNodeOrNull<Label>("Root/TopBar/PlayerFactionLabel");
    internal Label? MainHudStoryLabel => GetNodeOrNull<Label>("Root/TopBar/StoryLabel");
    internal Button? MainHudLanguageButton => GetNodeOrNull<Button>("Root/TopBar/LanguageButton");
    internal Button? MainHudGodModeButton => GetNodeOrNull<Button>("Root/TopBar/GodModeButton");
    internal Button? MainHudTopBarTestButton => GetNodeOrNull<Button>("Root/TopBar/TestButton");
    internal Button? MainHudEndTurnButton => GetNodeOrNull<Button>("Root/TopBar/EndTurnButton");
    internal Control? MainHudOverlayParent => GetNodeOrNull<Control>("Root");

    internal Label? MainHudCityNameLabel => GetNodeOrNull<Label>("Root/LeftPanel/CityNameLabel");
    internal VBoxContainer? MainHudCityStatsPanel => GetNodeOrNull<VBoxContainer>("Root/LeftPanel/CityStatsPanel");
    internal Label? MainHudCommandsTitle => GetNodeOrNull<Label>("Root/LeftPanel/CommandsTitle");
    internal Button? MainHudDevelopButton => GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/DevelopButton");
    internal Button? MainHudRecruitButton => GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/RecruitButton");
    internal Button? MainHudMoveButton => GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/MoveButton");
    internal Button? MainHudSearchButton => GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/SearchButton");
    internal Button? MainHudMerchantButton => GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/MerchantButton");
    internal Button? MainHudDiplomacyButton => GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/DiplomacyButton");
    internal Button? MainHudSpyButton => GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/SpyButton");
    internal Button? MainHudPersonnelButton => GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/PersonnelButton");
    internal Button? MainHudAdvisorButton => _advisorButton;
    internal Button? MainHudCivilButton => GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/CivilButton");
    internal Button? MainHudAttackButton => GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/AttackButton");
    internal Button? MainHudViewButton => GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/ViewButton");
    internal Button? MainHudTestCaptureButton => GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/TestCaptureButton");
    internal RichTextLabel? MainHudLogText => GetNodeOrNull<RichTextLabel>("Root/LogText");
    internal Label? MainHudCityPanelHeaderLabel => _leftPanelHeaderLabel;
    internal Label? MainHudLogPanelHeaderLabel => _logPanelHeaderLabel;

    internal string MainHudBuildGodModeButtonText() => BuildGodModeButtonText();
    internal string MainHudBuildCityHeaderText(CityData? city) => BuildCityHeaderText(city);
    internal void MainHudPopulateCityStats(VBoxContainer panel, string ownerName, CityData? city, int freeOfficerCount) => PopulateCityStatsPanel(panel, ownerName, city, freeOfficerCount);
    internal void MainHudUpdateGameplayButtonStates() => UpdateGameplayButtonStates();
    internal void MainHudRequestFloatingPanelLayoutRefresh() => RequestFloatingPanelLayoutRefresh();
    internal void MainHudMoveToFront(CanvasItem? item) => item?.MoveToFront();
    internal void MainHudToggleLanguage() => OnLanguageButtonPressed();
    internal void MainHudToggleGodMode() => OnGodModePressed();
    internal bool MainHudIsGodModeEnabled() => _turnManager?.World?.ViewAllInformationEnabled ?? false;
    internal void MainHudEndTurn() => OnEndTurnPressed();
    internal void MainHudOpenTestDialog() => _mainHudUiController?.ShowTestToolsDialog();
    internal void MainHudOpenInternalAffairs() => OnDevelopPressed();
    internal void MainHudOpenMilitary() => OnRecruitPressed();
    internal void MainHudOpenMove() => OnMovePressed();
    internal void MainHudOpenSearch() => OnSearchPressed();
    internal void MainHudOpenMerchant() => OnMerchantPressed();
    internal void MainHudOpenDiplomacy() => OnDiplomacyPressed();
    internal void MainHudOpenSpy() => OnSpyPressed();
    internal void MainHudOpenPersonnel() => OnPersonnelPressed();
    internal void MainHudOpenAdvisor() => OnAdvisorPressed();
    internal void MainHudOpenCivil() => OnCivilPressed();
    internal void MainHudOpenAttack() => OnAttackPressed();
    internal void MainHudOpenView() => OnViewPressed();
    internal void MainHudOpenTestCapture() => OnTestCapturePressed();
    internal void MainHudPopupDialog(Control? dialog) => ShowOverlay(dialog);
    internal void MainHudPlayUiClickSfx() => PlayUiClickSfx();
}
