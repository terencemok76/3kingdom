using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using ThreeKingdom.Map;

namespace ThreeKingdom.UI;

public partial class HudController : CanvasLayer
{
    private static void PopupDialogUsingSceneSize(Window? dialog)
    {
        if (dialog == null)
        {
            return;
        }

        var desiredSize = dialog.Size;
        if (desiredSize.X <= 0 || desiredSize.Y <= 0)
        {
            dialog.PopupCentered();
            return;
        }

        dialog.PopupCentered(desiredSize);
    }

    private sealed class PortraitMappingEntry
    {
        [JsonPropertyName("charId")]
        public int CharId { get; set; }

        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("width")]
        public float Width { get; set; }

        [JsonPropertyName("height")]
        public float Height { get; set; }
    }

    private enum OfficerListMode
    {
        View,
        CommandSelection,
        GenericSelection
    }

    private enum OfficerSelectorPrimaryStat
    {
        Strength,
        Politics,
        Charm,
        Intelligence
    }

    private enum AttackDialogMode
    {
        Attack,
        Defense
    }

    private enum OfficerListScope
    {
        City,
        Faction
    }

    private enum OfficerListContentMode
    {
        Officers,
        Cities,
        Items,
        DiplomacyRelations
    }

    private enum CityListFilterMode
    {
        SelfFaction,
        OtherFactions,
        AllCities
    }

    private enum OfficerSortMode
    {
        Strength,
        Intelligence,
        Status
    }

    private enum ViewTableSortField
    {
        Name,
        Role,
        Status,
        City,
        Holder,
        ItemType,
        Rarity,
        Age,
        Strength,
        Intelligence,
        Charm,
        Leadership,
        Politics,
        Combat,
        OfficerLoyalty,
        Owner,
        RelationStatus,
        RemainingMonths,
        RelationScore,
        SpyExperience,
        DiplomacyExperience,
        Gold,
        Food,
        Population,
        Troops,
        OfficerCount,
        Farm,
        Commercial,
        Defense,
        Loyalty
    }

    private enum HireOfficerSortField
    {
        Name,
        Role,
        City,
        Owner,
        Loyalty,
        Strength,
        Intelligence,
        Charm,
        Leadership,
        Combat
    }

    private static readonly (string SheetPath, string MappingPath)[] PortraitSources =
    {
        ("res://assets/portrait/team1.png", "res://data/person/person_image_1.json"),
        ("res://assets/portrait/team2.png", "res://data/person/person_image_2.json"),
        ("res://assets/portrait/team3.png", "res://data/person/person_image_3.json"),
        ("res://assets/portrait/team4.png", "res://data/person/person_image_4.json")
    };
    private const int HireOfficerGoldCost = 200;

    private Label? _monthLabel;
    private Label? _playerFactionLabel;
    private Label? _storyLabel;
    private Label? _cityNameLabel;
    private RichTextLabel? _cityStatsLabel;
    private Label? _commandsTitle;
    private GridContainer? _commandButtons;
    private Label? _cityOfficerListTitle;
    private RichTextLabel? _cityOfficerListText;

    private Button? _languageButton;
    private Button? _godModeButton;
    private Button? _endTurnButton;
    private Button? _developButton;
    private Button? _recruitButton;
    private Button? _moveButton;
    private Button? _searchButton;
    private Button? _merchantButton;
    private Button? _diplomacyButton;
    private Button? _spyButton;
    private Button? _personnelButton;
    private Button? _advisorButton;
    private Button? _civilButton;
    private Button? _attackButton;
    private Button? _viewButton;
    private Button? _optionButton;
    private PopupMenu? _targetCityMenu;
    private Window? _merchantDialog;
    private OptionButton? _merchantModeOption;
    private SpinBox? _merchantFoodSpinBox;
    private Label? _merchantSummaryLabel;
    private Button? _merchantConfirmButton;
    private Window? _militaryDialog;
    private OptionButton? _militaryCommandOption;
    private Button? _militaryConfirmButton;
    private Window? _recruitTroopDialog;
    private Label? _recruitTroopSelectedOfficerLabel;
    private Button? _recruitTroopSelectOfficerButton;
    private Button? _recruitTroopConfirmButton;
    private OptionButton? _recruitTroopTypeOption;
    private int _recruitTroopSelectedOfficerId = -1;
    private Window? _visitCitizenDialog;
    private Label? _visitCitizenSelectedOfficerLabel;
    private Button? _visitCitizenSelectOfficerButton;
    private Button? _visitCitizenConfirmButton;
    private int _visitCitizenSelectedOfficerId = -1;
    private AcceptDialog? _personnelDialog;
    private OptionButton? _personnelCommandOption;
    private AcceptDialog? _personnelBonusDialog;
    private Tree? _personnelBonusOfficerList;
    private Label? _personnelBonusSelectedOfficerLabel;
    private Button? _personnelBonusSelectOfficerButton;
    private SpinBox? _personnelBonusGoldSpinBox;
    private SpinBox? _personnelBonusFoodSpinBox;
    private OptionButton? _personnelBonusItemOption;
    private Label? _personnelBonusSummaryLabel;
    private int _personnelBonusSelectedOfficerId = -1;
    private AcceptDialog? _assignRoleDialog;
    private Tree? _assignRoleOfficerList;
    private Label? _assignRoleSelectedOfficerLabel;
    private Button? _assignRoleSelectOfficerButton;
    private OptionButton? _assignRoleOption;
    private int _assignRoleSelectedOfficerId = -1;
    private Window? _advisorDialog;
    private Tree? _advisorOfficerList;
    private Label? _advisorSelectedOfficerLabel;
    private Button? _advisorSelectOfficerButton;
    private Button? _advisorConfirmButton;
    private OptionButton? _advisorPositionOption;
    private Label? _advisorSummaryLabel;
    private int _advisorSelectedOfficerId = -1;
    private Window? _fireOfficerDialog;
    private Tree? _fireOfficerList;
    private Label? _fireOfficerSelectedOfficerLabel;
    private Button? _fireOfficerSelectOfficerButton;
    private Button? _fireOfficerConfirmButton;
    private int _fireOfficerSelectedOfficerId = -1;
    private Window? _requestItemDialog;
    private Tree? _requestItemOfficerList;
    private Label? _requestItemSelectedOfficerLabel;
    private Button? _requestItemSelectOfficerButton;
    private Button? _requestItemConfirmButton;
    private OptionButton? _requestItemOption;
    private int _requestItemSelectedOfficerId = -1;
    private Window? _hireOfficerDialog;
    private Tree? _hireOfficerList;
    private SpinBox? _hireOfficerGoldSpinBox;
    private SpinBox? _hireOfficerFoodSpinBox;
    private OptionButton? _hireOfficerItemOption;
    private Label? _hireOfficerSummaryLabel;
    private Button? _hireOfficerConfirmButton;
    private Window? _civilDialog;
    private OptionButton? _civilCommandOption;
    private Button? _civilConfirmButton;
    private AcceptDialog? _civilReliefDialog;
    private Tree? _civilReliefOfficerList;
    private Label? _civilReliefSelectedOfficerLabel;
    private Button? _civilReliefSelectOfficerButton;
    private SpinBox? _civilReliefGoldSpinBox;
    private SpinBox? _civilReliefFoodSpinBox;
    private Label? _civilReliefSummaryLabel;
    private int _civilReliefSelectedOfficerId = -1;
    private Window? _internalAffairsDialog;
    private OptionButton? _internalAffairsJobOption;
    private SpinBox? _internalAffairsDurationSpinBox;
    private Label? _internalAffairsSelectedOfficerLabel;
    private Button? _internalAffairsSelectOfficerButton;
    private Button? _internalAffairsConfirmButton;
    private ItemList? _internalAffairsScheduleList;
    private Button? _internalAffairsTerminateButton;
    private Label? _internalAffairsWarningLabel;
    private int _internalAffairsSelectedOfficerId = -1;
    private AcceptDialog? _moveDialog;
    private OptionButton? _moveTargetCityOption;
    private SpinBox? _moveTroopsSpinBox;
    private SpinBox? _moveGoldSpinBox;
    private SpinBox? _moveFoodSpinBox;
    private SpinBox? _moveHorseSpinBox;
    private Tree? _moveOfficerList;
    private Window? _attackDialog;
    private OptionButton? _attackTargetCityOption;
    private SpinBox? _attackTroopsSpinBox;
    private SpinBox? _attackGoldSpinBox;
    private SpinBox? _attackFoodSpinBox;
    private Tree? _attackOfficerList;
    private ScrollContainer? _attackDeploymentScroll;
    private VBoxContainer? _attackDeploymentList;
    private Label? _attackDeploymentSummaryLabel;
    private Label? _attackWarningLabel;
    private Button? _attackConfirmButton;
    private Window? _diplomacyDialog;
    private OptionButton? _diplomacyActionOption;
    private OptionButton? _diplomacyTargetFactionOption;
    private SpinBox? _diplomacyDurationSpinBox;
    private SpinBox? _diplomacyGoldSpinBox;
    private SpinBox? _diplomacyFoodSpinBox;
    private SpinBox? _diplomacyHorseSpinBox;
    private Label? _diplomacySelectedOfficerLabel;
    private Button? _diplomacySelectOfficerButton;
    private Label? _diplomacyRelationInfoLabel;
    private Label? _diplomacySummaryLabel;
    private Label? _diplomacyWarningLabel;
    private Button? _diplomacyConfirmButton;
    private int _diplomacySelectedOfficerId = -1;
    private Window? _diplomacyProposalDialog;
    private Label? _diplomacyProposalSummaryLabel;
    private Button? _diplomacyProposalAcceptButton;
    private Button? _diplomacyProposalRejectButton;
    private Window? _spyDialog;
    private OptionButton? _spyActionOption;
    private OptionButton? _spyTargetCityOption;
    private OptionButton? _spyTargetOfficerOption;
    private Tree? _spyOfficerList;
    private Label? _spySelectedOfficerLabel;
    private Button? _spySelectOfficerButton;
    private Label? _spySummaryLabel;
    private Label? _spyWarningLabel;
    private Button? _spyConfirmButton;
    private int _spySelectedOfficerId = -1;
    private Window? _optionDialog;
    private Button? _optionSaveLoadButton;
    private Button? _optionLanguageButton;
    private Button? _optionGodModeButton;
    private Button? _optionBgmToggleButton;
    private Button? _optionSfxToggleButton;
    private HSlider? _optionBgmVolumeSlider;
    private Label? _optionBgmVolumeValueLabel;
    private HSlider? _optionSfxVolumeSlider;
    private Label? _optionSfxVolumeValueLabel;
    private Button? _optionSaveSettingsButton;
    private Button? _optionRestoreLayoutButton;
    private Window? _saveLoadDialog;
    private ItemList? _saveSlotList;
    private LineEdit? _saveDescriptionLineEdit;
    private RichTextLabel? _saveSlotSummaryLabel;
    private Button? _saveSlotSaveButton;
    private Button? _saveSlotLoadButton;
    private Button? _saveSlotCloseButton;
    private Window? _saveLoadConfirmDialog;
    private Label? _saveLoadConfirmLabel;
    private Button? _saveLoadConfirmYesButton;
    private Button? _saveLoadConfirmNoButton;
    private AcceptDialog? _successionDialog;
    private Tree? _successionOfficerList;
    private Label? _successionSelectedOfficerLabel;
    private Button? _successionSelectOfficerButton;
    private Label? _successionSummaryLabel;
    private Label? _successionWarningLabel;
    private int _successionSelectedOfficerId = -1;
    private AcceptDialog? _officerListDialog;
    private PanelContainer? _officerListTitlebarFill;
    private PanelContainer? _officerListHeaderPanel;
    private Label? _officerListHeaderLabel;
    private Button? _officerListCloseButton;
    private HBoxContainer? _officerListToolbar;
    private HBoxContainer? _officerListAuxRow;
    private Label? _officerListAuxLabel;
    private OptionButton? _officerListAuxOption;
    private Button? _viewCityOfficersDialogButton;
    private Button? _viewFactionOfficersDialogButton;
    private Button? _viewFactionItemsDialogButton;
    private Button? _viewDiplomacyRelationsDialogButton;
    private Button? _viewCitiesDialogButton;
    private Button? _officerListConfirmButton;
    private OptionButton? _cityListFilterOption;
    private OptionButton? _officerSortOption;
    private Tree? _officerListTable;
    private AcceptDialog? _officerDetailDialog;
    private TextureRect? _officerPortraitRect;
    private Label? _officerPortraitPlaceholderLabel;
    private RichTextLabel? _officerDetailText;

    private RichTextLabel? _logText;

    private TurnManager? _turnManager;
    private CommandResolver? _commandResolver;
    private LocalizationService? _localization;
    private AiController? _aiController;
    private WorldRepository? _worldRepository;
    private MapController? _mapController;
    private CityData? _selectedCity;

    private bool _isLanguageButtonConnected;
    private bool _isGodModeButtonConnected;
    private bool _isEndTurnButtonConnected;
    private bool _isDevelopButtonConnected;
    private bool _isRecruitButtonConnected;
    private bool _isMoveButtonConnected;
    private bool _isSearchButtonConnected;
    private bool _isMerchantButtonConnected;
    private bool _isDiplomacyButtonConnected;
    private bool _isSpyButtonConnected;
    private bool _isPersonnelButtonConnected;
    private bool _isAdvisorButtonConnected;
    private bool _isCivilButtonConnected;
    private bool _isAttackButtonConnected;
    private bool _isViewButtonConnected;
    private bool _isOptionButtonConnected;
    private bool _merchantDialogSignalsConnected;
    private bool _militaryDialogSignalsConnected;
    private bool _attackOfficerListSignalsConnected;
    private bool _recruitTroopDialogSignalsConnected;
    private bool _visitCitizenDialogSignalsConnected;
    private bool _advisorDialogSignalsConnected;
    private bool _fireOfficerDialogSignalsConnected;
    private bool _requestItemDialogSignalsConnected;
    private bool _diplomacyDialogSignalsConnected;
    private bool _civilDialogSignalsConnected;
    private bool _internalAffairsDialogSignalsConnected;
    private bool _gameEnded;
    private bool _isDraggingOfficerListDialog;
    private Vector2I _officerListDialogDragOffset;
    private readonly HashSet<int> _aliveFactionIds = new();
    private CommandType _pendingTargetCommand = CommandType.Pass;
    private readonly Dictionary<int, Texture2D> _officerPortraitTextures = new();
    private OfficerListMode _officerListMode = OfficerListMode.View;
    private OfficerListScope _officerListScope = OfficerListScope.City;
    private OfficerListContentMode _officerListContentMode = OfficerListContentMode.Officers;
    private CityListFilterMode _cityListFilterMode = CityListFilterMode.SelfFaction;
    private OfficerSortMode _officerSortMode = OfficerSortMode.Strength;
    private ViewTableSortField _viewTableSortField = ViewTableSortField.Name;
    private bool _viewTableSortAscending = true;
    private HireOfficerSortField _hireOfficerSortField = HireOfficerSortField.Loyalty;
    private bool _hireOfficerSortAscending = true;
    private CommandType _pendingOfficerCommand = CommandType.Pass;
    private TroopType _pendingRecruitTroopType = TroopType.Infantry;
    private readonly Dictionary<int, AttackOfficerDeploymentData> _attackOfficerDeployments = new();
    private readonly List<int> _attackDeploymentOfficerOrder = new();
    private string _lastAttackDeploymentSelectionSignature = string.Empty;
    private int _attackDiplomacyWarningAcknowledgedTargetCityId = -1;
    private bool _hasLogEntries;
    private Action<int>? _genericOfficerSelectorConfirmedAction;
    private readonly List<int> _genericOfficerSelectorCandidateIds = new();
    private OfficerSelectorPrimaryStat _genericOfficerSelectorPrimaryStat = OfficerSelectorPrimaryStat.Politics;
    private AttackDialogMode _attackDialogMode = AttackDialogMode.Attack;
    private CityData? _attackDialogContextCity;
    private PendingCommandData? _pendingDefenseCommand;
    private PendingCommandData? _pendingDiplomacyProposalCommand;
    private int _pendingSuccessionFactionId = -1;
    private readonly List<PendingCommandData> _pendingNonAttackResolutionQueue = new();
    private readonly List<PendingCommandData> _pendingAttackResolutionQueue = new();
    private bool _isResolvingEndTurn;
    private bool _bgmEnabled = true;
    private bool _sfxEnabled = true;
    private float _bgmVolume = 1.0f;
    private float _sfxVolume = 1.0f;
    private int _selectedSaveSlotIndex;
    private SaveLoadConfirmAction _pendingSaveLoadConfirmAction = SaveLoadConfirmAction.None;

    public override void _Ready()
    {
        _monthLabel = GetNodeOrNull<Label>("Root/TopBar/MonthLabel");
        _playerFactionLabel = GetNodeOrNull<Label>("Root/TopBar/PlayerFactionLabel");
        _storyLabel = GetNodeOrNull<Label>("Root/TopBar/StoryLabel");
        _languageButton = GetNodeOrNull<Button>("Root/TopBar/LanguageButton");
        _godModeButton = GetNodeOrNull<Button>("Root/TopBar/GodModeButton");
        _endTurnButton = GetNodeOrNull<Button>("Root/TopBar/EndTurnButton");
        if (_languageButton != null)
        {
            _languageButton.Visible = false;
        }
        if (_godModeButton != null)
        {
            _godModeButton.Visible = false;
        }

        _cityNameLabel = GetNodeOrNull<Label>("Root/LeftPanel/CityNameLabel");
        _cityStatsLabel = GetNodeOrNull<RichTextLabel>("Root/LeftPanel/CityStatsLabel");
        _commandsTitle = GetNodeOrNull<Label>("Root/LeftPanel/CommandsTitle");
        _commandButtons = GetNodeOrNull<GridContainer>("Root/LeftPanel/CommandButtons");
        _cityOfficerListTitle = GetNodeOrNull<Label>("Root/LeftPanel/OfficerListTitle");
        if (_cityOfficerListTitle != null)
        {
            _cityOfficerListTitle.Visible = false;
        }

        _cityOfficerListText = GetNodeOrNull<RichTextLabel>("Root/LeftPanel/OfficerListText");
        if (_cityOfficerListText != null)
        {
            _cityOfficerListText.Visible = false;
        }

        _developButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/DevelopButton");
        _recruitButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/RecruitButton");
        _moveButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/MoveButton");
        if (_moveButton != null)
        {
            _moveButton.Visible = false;
        }
        _searchButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/SearchButton");
        if (_searchButton != null)
        {
            _searchButton.Visible = false;
        }
        _merchantButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/MerchantButton");
        _diplomacyButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/DiplomacyButton");
        _spyButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/SpyButton");
        _personnelButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/PersonnelButton");
        EnsureAdvisorButton();
        _civilButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/CivilButton");
        _attackButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/AttackButton");
        if (_attackButton != null)
        {
            _attackButton.Visible = false;
        }
        _viewButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/ViewButton");
        EnsureOptionButton();

        _logText = GetNodeOrNull<RichTextLabel>("Root/LogText");
        if (_logText != null)
        {
            _logText.ScrollFollowing = true;
        }
        InitializeFloatingPanels();

        _targetCityMenu = new PopupMenu();
        AddChild(_targetCityMenu);
        _targetCityMenu.IdPressed += OnTargetCityMenuIdPressed;

        _merchantDialog = GD.Load<PackedScene>("res://scenes/ui/MerchantDialog.tscn").Instantiate<Window>();
        _merchantDialog.Exclusive = false;
        _merchantDialog.Unresizable = true;
        _merchantDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _merchantDialog?.Hide();
        };
        AddChild(_merchantDialog);
        EnsureMerchantDialogWidgets();
        _merchantDialog.Hide();

        _militaryDialog = GD.Load<PackedScene>("res://scenes/ui/MilitaryDialog.tscn").Instantiate<Window>();
        _militaryDialog.Exclusive = false;
        _militaryDialog.Unresizable = true;
        _militaryDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _militaryDialog?.Hide();
        };
        AddChild(_militaryDialog);
        EnsureMilitaryDialogWidgets();
        _militaryDialog.Hide();

        _recruitTroopDialog = GD.Load<PackedScene>("res://scenes/ui/RecruitTroopDialog.tscn").Instantiate<Window>();
        _recruitTroopDialog.Exclusive = false;
        _recruitTroopDialog.Unresizable = true;
        _recruitTroopDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _recruitTroopDialog?.Hide();
        };
        AddChild(_recruitTroopDialog);
        EnsureRecruitTroopDialogWidgets();
        _recruitTroopDialog.Hide();

        _personnelDialog = new AcceptDialog();
        _personnelDialog.Exclusive = false;
        _personnelDialog.Unfocusable = false;
        _personnelDialog.Confirmed += OnPersonnelDialogConfirmed;
        _personnelDialog.CloseRequested += PlayUiClickSfx;
        AddChild(_personnelDialog);
        EnsurePersonnelDialogWidgets();

        _personnelBonusDialog = new AcceptDialog();
        _personnelBonusDialog.Exclusive = false;
        _personnelBonusDialog.Unfocusable = false;
        _personnelBonusDialog.Confirmed += OnPersonnelBonusDialogConfirmed;
        _personnelBonusDialog.CloseRequested += PlayUiClickSfx;
        AddChild(_personnelBonusDialog);
        EnsurePersonnelBonusDialogWidgets();

        _assignRoleDialog = new AcceptDialog();
        _assignRoleDialog.Exclusive = false;
        _assignRoleDialog.Unfocusable = false;
        _assignRoleDialog.Confirmed += OnAssignRoleDialogConfirmed;
        _assignRoleDialog.CloseRequested += PlayUiClickSfx;
        AddChild(_assignRoleDialog);
        EnsureAssignRoleDialogWidgets();

        _advisorDialog = GD.Load<PackedScene>("res://scenes/ui/AdvisorDialog.tscn").Instantiate<Window>();
        _advisorDialog.Exclusive = false;
        _advisorDialog.Unresizable = true;
        _advisorDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _advisorDialog?.Hide();
        };
        AddChild(_advisorDialog);
        EnsureAdvisorDialogWidgets();
        _advisorDialog.Hide();

        _fireOfficerDialog = GD.Load<PackedScene>("res://scenes/ui/FireOfficerDialog.tscn").Instantiate<Window>();
        _fireOfficerDialog.Exclusive = false;
        _fireOfficerDialog.Unresizable = true;
        _fireOfficerDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _fireOfficerDialog?.Hide();
        };
        AddChild(_fireOfficerDialog);
        EnsureFireOfficerDialogWidgets();
        _fireOfficerDialog.Hide();

        _requestItemDialog = GD.Load<PackedScene>("res://scenes/ui/RequestItemDialog.tscn").Instantiate<Window>();
        _requestItemDialog.Exclusive = false;
        _requestItemDialog.Unresizable = true;
        _requestItemDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _requestItemDialog?.Hide();
        };
        AddChild(_requestItemDialog);
        EnsureRequestItemDialogWidgets();
        _requestItemDialog.Hide();

        _hireOfficerDialog = new Window();
        _hireOfficerDialog.Exclusive = false;
        _hireOfficerDialog.Unresizable = true;
        _hireOfficerDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _hireOfficerDialog?.Hide();
        };
        AddChild(_hireOfficerDialog);
        EnsureHireOfficerDialogWidgets();
        _hireOfficerDialog.Hide();

        _civilDialog = GD.Load<PackedScene>("res://scenes/ui/CivilDialog.tscn").Instantiate<Window>();
        _civilDialog.Exclusive = false;
        _civilDialog.Unresizable = true;
        _civilDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _civilDialog?.Hide();
        };
        AddChild(_civilDialog);
        EnsureCivilDialogWidgets();
        _civilDialog.Hide();

        _visitCitizenDialog = GD.Load<PackedScene>("res://scenes/ui/VisitCitizenDialog.tscn").Instantiate<Window>();
        _visitCitizenDialog.Exclusive = false;
        _visitCitizenDialog.Unresizable = true;
        _visitCitizenDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _visitCitizenDialog?.Hide();
        };
        AddChild(_visitCitizenDialog);
        EnsureVisitCitizenDialogWidgets();
        _visitCitizenDialog.Hide();

        _civilReliefDialog = new AcceptDialog();
        _civilReliefDialog.Exclusive = false;
        _civilReliefDialog.Unfocusable = false;
        _civilReliefDialog.Confirmed += OnCivilReliefDialogConfirmed;
        _civilReliefDialog.CloseRequested += PlayUiClickSfx;
        AddChild(_civilReliefDialog);
        EnsureCivilReliefDialogWidgets();

        _diplomacyDialog = GD.Load<PackedScene>("res://scenes/ui/DiplomacyDialog.tscn").Instantiate<Window>();
        _diplomacyDialog.Exclusive = false;
        _diplomacyDialog.Unresizable = true;
        _diplomacyDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _diplomacyDialog?.Hide();
        };
        AddChild(_diplomacyDialog);
        EnsureDiplomacyDialogWidgets();
        _diplomacyDialog.Hide();

        _diplomacyProposalDialog = new Window();
        _diplomacyProposalDialog.Exclusive = false;
        _diplomacyProposalDialog.Unresizable = true;
        _diplomacyProposalDialog.CloseRequested += OnDiplomacyProposalCloseRequested;
        AddChild(_diplomacyProposalDialog);
        EnsureDiplomacyProposalDialogWidgets();
        _diplomacyProposalDialog.Hide();

        _spyDialog = new Window();
        _spyDialog.Exclusive = false;
        _spyDialog.Unresizable = true;
        _spyDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _spyDialog?.Hide();
        };
        AddChild(_spyDialog);
        EnsureSpyDialogWidgets();
        _spyDialog.Hide();

        _optionDialog = new Window();
        _optionDialog.Exclusive = false;
        _optionDialog.Unresizable = true;
        _optionDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _optionDialog?.Hide();
        };
        AddChild(_optionDialog);
        EnsureOptionDialogWidgets();
        _optionDialog.Hide();

        _saveLoadDialog = new Window();
        _saveLoadDialog.Exclusive = false;
        _saveLoadDialog.Unresizable = true;
        _saveLoadDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _saveLoadDialog?.Hide();
        };
        AddChild(_saveLoadDialog);
        EnsureSaveLoadDialogWidgets();
        _saveLoadDialog.Hide();

        _saveLoadConfirmDialog = new Window();
        _saveLoadConfirmDialog.Exclusive = false;
        _saveLoadConfirmDialog.Unresizable = true;
        _saveLoadConfirmDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            OnSaveLoadConfirmNoPressed();
        };
        AddChild(_saveLoadConfirmDialog);
        EnsureSaveLoadConfirmDialogWidgets();
        _saveLoadConfirmDialog.Hide();

        _successionDialog = new AcceptDialog();
        _successionDialog.Exclusive = false;
        _successionDialog.Unfocusable = false;
        _successionDialog.Confirmed += OnSuccessionDialogConfirmed;
        _successionDialog.CloseRequested += OnSuccessionDialogCloseRequested;
        AddChild(_successionDialog);
        EnsureSuccessionDialogWidgets();

        _internalAffairsDialog = GD.Load<PackedScene>("res://scenes/ui/InternalAffairsDialog.tscn").Instantiate<Window>();
        _internalAffairsDialog.Exclusive = false;
        _internalAffairsDialog.Unresizable = true;
        _internalAffairsDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _internalAffairsDialog?.Hide();
        };
        AddChild(_internalAffairsDialog);
        EnsureInternalAffairsDialogWidgets();
        _internalAffairsDialog.Hide();

        _moveDialog = new AcceptDialog();
        _moveDialog.Exclusive = false;
        _moveDialog.Unfocusable = false;
        _moveDialog.Confirmed += OnMoveDialogConfirmed;
        _moveDialog.CloseRequested += PlayUiClickSfx;
        AddChild(_moveDialog);
        EnsureMoveDialogWidgets();

        _attackDialog = new Window();
        _attackDialog.Exclusive = false;
        _attackDialog.Unresizable = true;
        _attackDialog.CloseRequested += OnAttackDialogCloseRequested;
        AddChild(_attackDialog);
        EnsureAttackDialogWidgets();
        _attackDialog.Hide();

        _officerListDialog = new AcceptDialog();
        _officerListDialog.Title = " ";
        _officerListDialog.Borderless = true;
        _officerListDialog.Exclusive = false;
        _officerListDialog.Unfocusable = false;
        _officerListDialog.Confirmed += OnOfficerListDialogConfirmed;
        AddChild(_officerListDialog);

        _officerListTitlebarFill = new PanelContainer
        {
            Name = "OfficerListTitlebarFill",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            OffsetLeft = 0.0f,
            OffsetTop = 0.0f,
            OffsetRight = 0.0f,
            OffsetBottom = 34.0f,
            AnchorLeft = 0.0f,
            AnchorTop = 0.0f,
            AnchorRight = 1.0f,
            AnchorBottom = 0.0f,
            Visible = false
        };
        _officerListDialog.AddChild(_officerListTitlebarFill);

        var officerListRoot = new VBoxContainer
        {
            Name = "OfficerListDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 280.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        officerListRoot.AddThemeConstantOverride("separation", 8);
        _officerListDialog.AddChild(officerListRoot);

        var officerListContentMargin = new MarginContainer
        {
            Name = "OfficerListContentMargin",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        officerListContentMargin.AddThemeConstantOverride("margin_left", 12);
        officerListContentMargin.AddThemeConstantOverride("margin_top", 8);
        officerListContentMargin.AddThemeConstantOverride("margin_right", 12);
        officerListContentMargin.AddThemeConstantOverride("margin_bottom", 8);
        officerListRoot.AddChild(officerListContentMargin);

        var officerListContent = new VBoxContainer
        {
            Name = "OfficerListContent",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        officerListContent.AddThemeConstantOverride("separation", 8);
        officerListContentMargin.AddChild(officerListContent);

        _officerListHeaderPanel = new PanelContainer
        {
            Name = "OfficerListHeaderPanel",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _officerListHeaderPanel.GuiInput += OnOfficerListHeaderGuiInput;
        officerListContent.AddChild(_officerListHeaderPanel);

        var officerListHeaderRow = new HBoxContainer
        {
            Name = "OfficerListHeaderRow",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        officerListHeaderRow.AddThemeConstantOverride("separation", 8);
        _officerListHeaderPanel.AddChild(officerListHeaderRow);

        _officerListHeaderLabel = new Label
        {
            Name = "OfficerListHeaderLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        officerListHeaderRow.AddChild(_officerListHeaderLabel);

        _officerListCloseButton = new Button
        {
            Name = "OfficerListCloseButton",
            Text = "X",
            CustomMinimumSize = new Vector2(28.0f, 24.0f),
            FocusMode = Control.FocusModeEnum.None
        };
        _officerListCloseButton.Pressed += OnOfficerListClosePressed;
        officerListHeaderRow.AddChild(_officerListCloseButton);

        _officerListToolbar = new HBoxContainer
        {
            Name = "OfficerListToolbar",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _officerListToolbar.AddThemeConstantOverride("separation", 8);
        officerListContent.AddChild(_officerListToolbar);

        _officerListAuxRow = new HBoxContainer
        {
            Name = "OfficerListAuxRow",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Visible = false
        };
        _officerListAuxRow.AddThemeConstantOverride("separation", 8);
        officerListContent.AddChild(_officerListAuxRow);

        _officerListAuxLabel = new Label
        {
            Name = "OfficerListAuxLabel",
            CustomMinimumSize = new Vector2(84.0f, 0.0f),
            VerticalAlignment = VerticalAlignment.Center
        };
        _officerListAuxRow.AddChild(_officerListAuxLabel);

        _officerListAuxOption = new OptionButton
        {
            Name = "OfficerListAuxOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _officerListAuxOption.ItemSelected += OnOfficerListAuxOptionSelected;
        _officerListAuxRow.AddChild(_officerListAuxOption);

        _viewCityOfficersDialogButton = new Button
        {
            Name = "ViewCityOfficersButton",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _viewCityOfficersDialogButton.Pressed += OnViewCityOfficersDialogPressed;
        _officerListToolbar.AddChild(_viewCityOfficersDialogButton);

        _viewFactionOfficersDialogButton = new Button
        {
            Name = "ViewFactionOfficersButton",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _viewFactionOfficersDialogButton.Pressed += OnViewFactionOfficersDialogPressed;
        _officerListToolbar.AddChild(_viewFactionOfficersDialogButton);

        _viewFactionItemsDialogButton = new Button
        {
            Name = "ViewFactionItemsButton",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _viewFactionItemsDialogButton.Pressed += OnViewFactionItemsDialogPressed;
        _officerListToolbar.AddChild(_viewFactionItemsDialogButton);

        _viewDiplomacyRelationsDialogButton = new Button
        {
            Name = "ViewDiplomacyRelationsButton",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _viewDiplomacyRelationsDialogButton.Pressed += OnViewDiplomacyRelationsDialogPressed;
        _officerListToolbar.AddChild(_viewDiplomacyRelationsDialogButton);

        _viewCitiesDialogButton = new Button
        {
            Name = "ViewCitiesButton",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _viewCitiesDialogButton.Pressed += OnViewCitiesDialogPressed;
        _officerListToolbar.AddChild(_viewCitiesDialogButton);

        _cityListFilterOption = new OptionButton
        {
            Name = "CityListFilterOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _cityListFilterOption.ItemSelected += OnCityListFilterOptionSelected;
        _officerListToolbar.AddChild(_cityListFilterOption);

        _officerSortOption = new OptionButton
        {
            Name = "OfficerSortOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _officerSortOption.ItemSelected += OnOfficerSortOptionSelected;
        _officerListToolbar.AddChild(_officerSortOption);

        _officerListTable = new Tree
        {
            Name = "OfficerListTable",
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(920.0f, 260.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _officerListTable.ItemSelected += OnOfficerListTableSelected;
        _officerListTable.ItemActivated += OnOfficerListTableActivated;
        _officerListTable.ColumnTitleClicked += OnOfficerListTableColumnTitleClicked;
        officerListContent.AddChild(_officerListTable);

        var officerListConfirmRow = new CenterContainer
        {
            Name = "OfficerListConfirmRow",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0.0f, 34.0f)
        };
        officerListContent.AddChild(officerListConfirmRow);

        _officerListConfirmButton = new Button
        {
            Name = "OfficerListConfirmButton",
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(92.0f, 28.0f)
        };
        _officerListConfirmButton.Pressed += OnOfficerListDialogConfirmed;
        officerListConfirmRow.AddChild(_officerListConfirmButton);

        ApplyOfficerListDialogTheme();

        _officerDetailDialog = new AcceptDialog();
        _officerDetailDialog.Exclusive = false;
        _officerDetailDialog.Unfocusable = false;
        AddChild(_officerDetailDialog);
        EnsureOfficerDetailWidgets();
        LoadPortraitData();
        AttachClickSfxToButtons(this);
        LoadOptionSettings();
        ApplyAudioSettings();
    }

    public override void _ExitTree()
    {
        if (_localization != null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }

        DisconnectButtons();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationWMCloseRequest)
        {
            return;
        }

        _officerDetailDialog?.Hide();
        _officerListDialog?.Hide();
        _militaryDialog?.Hide();
        _personnelDialog?.Hide();
        _personnelBonusDialog?.Hide();
        _assignRoleDialog?.Hide();
        _hireOfficerDialog?.Hide();
        _civilDialog?.Hide();
        _civilReliefDialog?.Hide();
        _internalAffairsDialog?.Hide();
        _merchantDialog?.Hide();
        _moveDialog?.Hide();
        _spyDialog?.Hide();
        _attackDialog?.Hide();
        _optionDialog?.Hide();
        _saveLoadDialog?.Hide();
    }

    public override void _Process(double delta)
    {
        SyncAttackDeploymentEditorSelection();
        UpdateFloatingPanelDragging();
        ProcessFloatingPanelDeferredRefresh();
    }

    private void AttachClickSfxToButtons(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Button button)
            {
                RegisterButtonClickSfx(button);
            }

            AttachClickSfxToButtons(child);
        }
    }

    private static void RegisterButtonClickSfx(Button button)
    {
        const string clickSfxConnectedKey = "_click_sfx_connected";
        if (button.HasMeta(clickSfxConnectedKey))
        {
            return;
        }

        button.SetMeta(clickSfxConnectedKey, true);
        button.Pressed += () => GameAudioController.Instance?.PlayClickSfx();
    }

    private static void PlayUiClickSfx()
    {
        GameAudioController.Instance?.PlayClickSfx();
    }

    public void Initialize(
        TurnManager turnManager,
        CommandResolver commandResolver,
        AiController aiController,
        LocalizationService localization,
        WorldRepository worldRepository,
        MapController? mapController = null)
    {
        _turnManager = turnManager;
        _commandResolver = commandResolver;
        _localization = localization;
        _aiController = aiController;
        _worldRepository = worldRepository;
        _mapController = mapController;

        _localization.LanguageChanged -= OnLanguageChanged;
        _localization.LanguageChanged += OnLanguageChanged;

        ConnectButtons();
        ResetAliveFactionSnapshot();
        RefreshAllText();
        AddLog(_localization.T("log.boot"));
    }

    public void OnCitySelected(CityData city)
    {
        GameAudioController.Instance?.PlayCityClickSfx();
        _selectedCity = city;
        RefreshSelectedCity();
    }

    public void RefreshMonth()
    {
        var world = _turnManager?.World;
        if (_monthLabel == null || _localization == null || world == null)
        {
            return;
        }

        _monthLabel.Text = _localization.FormatYearMonth(world.Year, world.Month);
    }

    public void AddLog(string message, bool isPlayerRelated = false)
    {
        if (_logText == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (_hasLogEntries)
        {
            _logText.Newline();
        }

        if (isPlayerRelated)
        {
            _logText.PushColor(new Color(0.24f, 0.43f, 0.82f, 1.0f));
        }

        _logText.AddText(message);

        if (isPlayerRelated)
        {
            _logText.Pop();
        }

        _hasLogEntries = true;
        CallDeferred(nameof(ScrollLogToBottom));
    }

    private void ScrollLogToBottom()
    {
        if (_logText == null)
        {
            return;
        }

        var lastLine = Mathf.Max(_logText.GetLineCount() - 1, 0);
        _logText.ScrollToLine(lastLine);
    }

    private void ConnectButtons()
    {
        if (_languageButton != null && !_isLanguageButtonConnected)
        {
            _languageButton.Pressed += OnLanguageButtonPressed;
            _isLanguageButtonConnected = true;
        }

        if (_godModeButton != null && !_isGodModeButtonConnected)
        {
            _godModeButton.Pressed += OnGodModePressed;
            _isGodModeButtonConnected = true;
        }

        if (_endTurnButton != null && !_isEndTurnButtonConnected)
        {
            _endTurnButton.Pressed += OnEndTurnPressed;
            _isEndTurnButtonConnected = true;
        }

        if (_developButton != null && !_isDevelopButtonConnected)
        {
            _developButton.Pressed += OnDevelopPressed;
            _isDevelopButtonConnected = true;
        }

        if (_recruitButton != null && !_isRecruitButtonConnected)
        {
            _recruitButton.Pressed += OnRecruitPressed;
            _isRecruitButtonConnected = true;
        }

        if (_moveButton != null && !_isMoveButtonConnected)
        {
            _moveButton.Pressed += OnMovePressed;
            _isMoveButtonConnected = true;
        }

        if (_searchButton != null && !_isSearchButtonConnected)
        {
            _searchButton.Pressed += OnSearchPressed;
            _isSearchButtonConnected = true;
        }

        if (_merchantButton != null && !_isMerchantButtonConnected)
        {
            _merchantButton.Pressed += OnMerchantPressed;
            _isMerchantButtonConnected = true;
        }

        if (_diplomacyButton != null && !_isDiplomacyButtonConnected)
        {
            _diplomacyButton.Pressed += OnDiplomacyPressed;
            _isDiplomacyButtonConnected = true;
        }

        if (_spyButton != null && !_isSpyButtonConnected)
        {
            _spyButton.Pressed += OnSpyPressed;
            _isSpyButtonConnected = true;
        }

        if (_personnelButton != null && !_isPersonnelButtonConnected)
        {
            _personnelButton.Pressed += OnPersonnelPressed;
            _isPersonnelButtonConnected = true;
        }

        if (_advisorButton != null && !_isAdvisorButtonConnected)
        {
            _advisorButton.Pressed += OnAdvisorPressed;
            _isAdvisorButtonConnected = true;
        }

        if (_civilButton != null && !_isCivilButtonConnected)
        {
            _civilButton.Pressed += OnCivilPressed;
            _isCivilButtonConnected = true;
        }

        if (_attackButton != null && !_isAttackButtonConnected)
        {
            _attackButton.Pressed += OnAttackPressed;
            _isAttackButtonConnected = true;
        }

        if (_viewButton != null && !_isViewButtonConnected)
        {
            _viewButton.Pressed += OnViewPressed;
            _isViewButtonConnected = true;
        }

        if (_optionButton != null && !_isOptionButtonConnected)
        {
            _optionButton.Pressed += OnOptionPressed;
            _isOptionButtonConnected = true;
        }
    }

    private void DisconnectButtons()
    {
        if (_languageButton != null && _isLanguageButtonConnected)
        {
            _languageButton.Pressed -= OnLanguageButtonPressed;
            _isLanguageButtonConnected = false;
        }

        if (_godModeButton != null && _isGodModeButtonConnected)
        {
            _godModeButton.Pressed -= OnGodModePressed;
            _isGodModeButtonConnected = false;
        }

        if (_endTurnButton != null && _isEndTurnButtonConnected)
        {
            _endTurnButton.Pressed -= OnEndTurnPressed;
            _isEndTurnButtonConnected = false;
        }

        if (_developButton != null && _isDevelopButtonConnected)
        {
            _developButton.Pressed -= OnDevelopPressed;
            _isDevelopButtonConnected = false;
        }

        if (_recruitButton != null && _isRecruitButtonConnected)
        {
            _recruitButton.Pressed -= OnRecruitPressed;
            _isRecruitButtonConnected = false;
        }

        if (_moveButton != null && _isMoveButtonConnected)
        {
            _moveButton.Pressed -= OnMovePressed;
            _isMoveButtonConnected = false;
        }

        if (_searchButton != null && _isSearchButtonConnected)
        {
            _searchButton.Pressed -= OnSearchPressed;
            _isSearchButtonConnected = false;
        }

        if (_merchantButton != null && _isMerchantButtonConnected)
        {
            _merchantButton.Pressed -= OnMerchantPressed;
            _isMerchantButtonConnected = false;
        }

        if (_diplomacyButton != null && _isDiplomacyButtonConnected)
        {
            _diplomacyButton.Pressed -= OnDiplomacyPressed;
            _isDiplomacyButtonConnected = false;
        }

        if (_spyButton != null && _isSpyButtonConnected)
        {
            _spyButton.Pressed -= OnSpyPressed;
            _isSpyButtonConnected = false;
        }

        if (_personnelButton != null && _isPersonnelButtonConnected)
        {
            _personnelButton.Pressed -= OnPersonnelPressed;
            _isPersonnelButtonConnected = false;
        }

        if (_advisorButton != null && _isAdvisorButtonConnected)
        {
            _advisorButton.Pressed -= OnAdvisorPressed;
            _isAdvisorButtonConnected = false;
        }

        if (_civilButton != null && _isCivilButtonConnected)
        {
            _civilButton.Pressed -= OnCivilPressed;
            _isCivilButtonConnected = false;
        }

        if (_attackButton != null && _isAttackButtonConnected)
        {
            _attackButton.Pressed -= OnAttackPressed;
            _isAttackButtonConnected = false;
        }

        if (_viewButton != null && _isViewButtonConnected)
        {
            _viewButton.Pressed -= OnViewPressed;
            _isViewButtonConnected = false;
        }

        if (_optionButton != null && _isOptionButtonConnected)
        {
            _optionButton.Pressed -= OnOptionPressed;
            _isOptionButtonConnected = false;
        }
    }

    private void OnLanguageButtonPressed()
    {
        _localization?.ToggleLanguage();
    }

    private void OnGodModePressed()
    {
        if (_turnManager?.World == null)
        {
            return;
        }

        _turnManager.World.ViewAllInformationEnabled = !_turnManager.World.ViewAllInformationEnabled;
        RefreshAllText();
        AddLog(_turnManager.World.ViewAllInformationEnabled
            ? "God Mode enabled."
            : "God Mode disabled.");
    }

    private void OnDevelopPressed()
    {
        ShowInternalAffairsDialog();
    }

    private void OnRecruitPressed()
    {
        ShowMilitaryDialog();
    }

    private void OnMovePressed()
    {
        OpenMoveFlow();
    }

    private void OpenMoveFlow()
    {
        if (_turnManager?.World == null || _selectedCity == null)
        {
            return;
        }

        var candidateIds = new List<int>();
        foreach (var targetId in _selectedCity.ConnectedCityIds)
        {
            var target = _turnManager.World.GetCity(targetId);
            if (target == null || target.OwnerFactionId != _selectedCity.OwnerFactionId)
            {
                continue;
            }

            candidateIds.Add(target.Id);
        }

        if (candidateIds.Count == 0)
        {
            AddLog(_localization?.T("ui.no_connected_friendly_city") ?? "No connected friendly city to move troops, resources, or officers.");
            return;
        }

        ShowMoveDialog(candidateIds);
    }

    private void OnSearchPressed()
    {
        ShowVisitCitizenDialog();
    }

    private void OnMerchantPressed()
    {
        if (_selectedCity == null)
        {
            return;
        }

        ShowMerchantDialog();
    }

    private void OnDiplomacyPressed()
    {
        if (_selectedCity == null)
        {
            return;
        }

        ShowDiplomacyDialog();
    }

    private void OnSpyPressed()
    {
        if (_selectedCity == null)
        {
            return;
        }

        ShowSpyDialog();
    }

    private void OnPersonnelPressed()
    {
        if (_selectedCity == null)
        {
            return;
        }

        ShowPersonnelDialog();
    }

    private void OnAdvisorPressed()
    {
        if (_selectedCity == null)
        {
            return;
        }

        ShowAdvisorDialog();
    }

    private void OnCivilPressed()
    {
        if (_selectedCity == null)
        {
            return;
        }

        ShowCivilDialog();
    }

    private void OnAttackPressed()
    {
        OpenAttackFlow();
    }

    private void OpenAttackFlow()
    {
        if (_turnManager?.World == null || _selectedCity == null)
        {
            return;
        }

        var candidateIds = new List<int>();
        foreach (var targetId in _selectedCity.ConnectedCityIds)
        {
            var target = _turnManager.World.GetCity(targetId);
            if (target == null || target.OwnerFactionId == _selectedCity.OwnerFactionId)
            {
                continue;
            }

            candidateIds.Add(target.Id);
        }

        if (candidateIds.Count == 0)
        {
            AddLog(_localization?.T("ui.no_connected_enemy_city") ?? "No connected enemy city to attack.");
            return;
        }

        ShowAttackDialog(candidateIds);
    }

    private void OnViewPressed()
    {
        if (_selectedCity == null || _turnManager?.World == null || _officerListDialog == null || _officerListTable == null)
        {
            return;
        }

        _officerListMode = OfficerListMode.View;
        ConfigureOfficerListDialogLayout(isCommandSelection: false);
        if (_officerListAuxRow != null)
        {
            _officerListAuxRow.Visible = false;
        }
        _officerListContentMode = OfficerListContentMode.Officers;
        _officerListScope = OfficerListScope.City;
        _officerListDialog.OkButtonText = _localization?.T("ui.confirm_officer_selection") ?? "Confirm Selection";
        UpdateOfficerListToolbar();
        PopulateOfficerListDialog();
        _officerListDialog.PopupCentered(new Vector2I(420, 320));
    }

    private void OnViewCityOfficersDialogPressed()
    {
        if (_officerListMode != OfficerListMode.View)
        {
            return;
        }

        _officerListScope = OfficerListScope.City;
        _officerListContentMode = OfficerListContentMode.Officers;
        UpdateOfficerListToolbar();
        PopulateOfficerListDialog();
    }

    private void OnViewFactionOfficersDialogPressed()
    {
        if (_officerListMode != OfficerListMode.View)
        {
            return;
        }

        _officerListScope = OfficerListScope.Faction;
        _officerListContentMode = OfficerListContentMode.Officers;
        UpdateOfficerListToolbar();
        PopulateOfficerListDialog();
    }

    private void OnViewCitiesDialogPressed()
    {
        if (_officerListMode != OfficerListMode.View)
        {
            return;
        }

        _officerListContentMode = OfficerListContentMode.Cities;
        UpdateOfficerListToolbar();
        PopulateOfficerListDialog();
    }

    private void OnOfficerSortOptionSelected(long index)
    {
        _officerSortMode = index switch
        {
            1 => OfficerSortMode.Intelligence,
            2 => OfficerSortMode.Status,
            _ => OfficerSortMode.Strength
        };

        if (_officerListMode == OfficerListMode.View)
        {
            PopulateOfficerListDialog();
        }
    }

    private void OnCityListFilterOptionSelected(long index)
    {
        _cityListFilterMode = index switch
        {
            1 => CityListFilterMode.OtherFactions,
            2 => CityListFilterMode.AllCities,
            _ => CityListFilterMode.SelfFaction
        };

        if (_officerListMode == OfficerListMode.View && _officerListContentMode == OfficerListContentMode.Cities)
        {
            PopulateOfficerListDialog();
        }
    }

    private void ExecuteTargetSelectionOrCommand(
        CommandType commandType,
        List<int> candidateIds,
        string noTargetMessage)
    {
        if (_turnManager?.World == null || _selectedCity == null)
        {
            return;
        }

        if (candidateIds.Count == 0)
        {
            AddLog(noTargetMessage);
            return;
        }

        if (candidateIds.Count == 1)
        {
            ExecutePlayerCommand(commandType, candidateIds[0], _selectedCity.Troops / 2);
            return;
        }

        ShowTargetCityMenu(commandType, candidateIds);
    }

    private void ShowTargetCityMenu(CommandType commandType, List<int> candidateIds)
    {
        if (_targetCityMenu == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        _pendingTargetCommand = commandType;
        _targetCityMenu.Clear();

        foreach (var cityId in candidateIds)
        {
            var city = _turnManager.World.GetCity(cityId);
            if (city == null)
            {
                continue;
            }

            _targetCityMenu.AddItem(_localization.GetCityName(city), cityId);
        }

        if (_targetCityMenu.ItemCount == 0)
        {
            return;
        }

        var mousePos = GetViewport().GetMousePosition();
        _targetCityMenu.Position = new Vector2I((int)mousePos.X, (int)mousePos.Y);
        _targetCityMenu.ResetSize();
        _targetCityMenu.Popup();
    }

    private void OnTargetCityMenuIdPressed(long id)
    {
        if (_selectedCity == null)
        {
            return;
        }

        ExecutePlayerCommand(_pendingTargetCommand, (int)id, _selectedCity.Troops / 2);
    }

    private void OnMoveDialogConfirmed()
    {
        if (_selectedCity == null || _moveTargetCityOption == null)
        {
            return;
        }

        var selectedIndex = _moveTargetCityOption.Selected;
        if (selectedIndex < 0)
        {
            return;
        }

        var targetMetadata = _moveTargetCityOption.GetItemMetadata(selectedIndex);
        if (targetMetadata.VariantType != Variant.Type.Int)
        {
            return;
        }

        var selectedOfficerIds = GetCheckedTreeMetadataIds(_moveOfficerList);

        ExecutePlayerCommand(
            CommandType.Move,
            targetCityId: targetMetadata.AsInt32(),
            troopsToSend: _moveTroopsSpinBox != null ? (int)_moveTroopsSpinBox.Value : 0,
            goldToSend: _moveGoldSpinBox != null ? (int)_moveGoldSpinBox.Value : 0,
            foodToSend: _moveFoodSpinBox != null ? (int)_moveFoodSpinBox.Value : 0,
            horsesToSend: _moveHorseSpinBox != null ? (int)_moveHorseSpinBox.Value : 0,
            officerIds: selectedOfficerIds);
    }

    private void OnMerchantDialogConfirmed()
    {
        if (_merchantModeOption == null)
        {
            return;
        }

        var selectedIndex = _merchantModeOption.Selected;
        if (selectedIndex < 0)
        {
            return;
        }

        var tradeMode = GetSelectedMerchantTradeMode();
        ExecutePlayerCommand(
            CommandType.Merchant,
            foodToSend: _merchantFoodSpinBox != null ? (int)_merchantFoodSpinBox.Value : 0,
            sellFood: tradeMode == MerchantTradeMode.SellFood,
            merchantTradeMode: tradeMode);
    }

    private void OnAttackDialogConfirmed()
    {
        var dialogCity = GetAttackDialogCityContext();
        if (_attackTargetCityOption == null || dialogCity == null)
        {
            return;
        }

        var attackDeployments = _attackOfficerDeployments.Values
            .Where(item => item.TroopCount > 0)
            .Select(item => new AttackOfficerDeploymentData
            {
                OfficerId = item.OfficerId,
                TroopType = item.TroopType,
                TroopCount = item.TroopCount
            })
            .ToList();

        if (attackDeployments.Count == 0)
        {
            SetAttackDialogWarning(_localization?.T("ui.attack_deployment_required_warning") ?? "Configure troop type and count for each deployed officer.");
            ReopenAttackDialog();
            return;
        }

        var attackAllocation = BuildTroopAllocationFromAttackDeployments(attackDeployments);
        var attackTroops = attackAllocation.Total;
        if (attackTroops <= 0)
        {
            SetAttackDialogWarning(_localization?.T("ui.attack_troops_required_warning") ?? "Enter the number of troops to deploy.");
            ReopenAttackDialog();
            return;
        }

        if (attackAllocation.Infantry > dialogCity.InfantryTroops ||
            attackAllocation.Spearman > dialogCity.SpearmanTroops ||
            attackAllocation.Cavalry > dialogCity.CavalryTroops ||
            attackAllocation.Archer > dialogCity.ArcherTroops ||
            attackAllocation.Crossbow > dialogCity.CrossbowTroops ||
            attackAllocation.Siege > dialogCity.SiegeTroops)
        {
            SetAttackDialogWarning(_localization?.T("ui.attack_deployment_exceed_warning") ?? "Troop deployment exceeds the city's available troop types.");
            ReopenAttackDialog();
            return;
        }

        if (_attackDialogMode == AttackDialogMode.Defense)
        {
            if (_pendingDefenseCommand == null)
            {
                return;
            }

            _pendingDefenseCommand.DefenderOfficerDeployments = attackDeployments;
            SetAttackDialogWarning(string.Empty);
            _attackDialog?.Hide();
            ContinuePendingAttackResolution();
            return;
        }

        var selectedIndex = _attackTargetCityOption.Selected;
        if (selectedIndex < 0)
        {
            return;
        }

        var targetMetadata = _attackTargetCityOption.GetItemMetadata(selectedIndex);
        if (targetMetadata.VariantType != Variant.Type.Int)
        {
            return;
        }

        var targetCityId = targetMetadata.AsInt32();
        if (ShouldWarnAttackBreakPact(targetCityId) &&
            _attackDiplomacyWarningAcknowledgedTargetCityId != targetCityId)
        {
            _attackDiplomacyWarningAcknowledgedTargetCityId = targetCityId;
            SetAttackDialogWarning(_localization?.T("ui.attack_break_pact_warning") ?? "This attack will automatically break the current alliance or truce. Confirm again to proceed.");
            ReopenAttackDialog();
            return;
        }

        var result = ExecutePlayerCommand(
            CommandType.Attack,
            targetCityId: targetCityId,
            troopsToSend: attackTroops,
            goldToSend: _attackGoldSpinBox != null ? (int)_attackGoldSpinBox.Value : 0,
            foodToSend: _attackFoodSpinBox != null ? (int)_attackFoodSpinBox.Value : 0,
            attackOfficerDeployments: attackDeployments,
            officerIds: attackDeployments.Select(item => item.OfficerId).Distinct().ToList());

        if (result.Success)
        {
            SetAttackDialogWarning(string.Empty);
            _attackDialog?.Hide();
            _attackDiplomacyWarningAcknowledgedTargetCityId = -1;
            _attackDialogContextCity = null;
            _pendingDefenseCommand = null;
            _attackDialogMode = AttackDialogMode.Attack;
            return;
        }

        SetAttackDialogWarning(GetLocalizedResultMessage(result));
        ReopenAttackDialog();
    }

    private void OnAttackDialogCloseRequested()
    {
        PlayUiClickSfx();

        if (_attackDialogMode == AttackDialogMode.Defense)
        {
            ReopenAttackDialog();
            return;
        }

        _attackDialog?.Hide();
        _attackDiplomacyWarningAcknowledgedTargetCityId = -1;
        _attackDialogContextCity = null;
        _pendingDefenseCommand = null;
        _attackDialogMode = AttackDialogMode.Attack;
    }


}
