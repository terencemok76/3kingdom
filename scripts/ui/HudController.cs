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

    internal enum OfficerSelectorPrimaryStat
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

    private static readonly (string SheetPath, string MappingPath)[] PortraitSources =
    {
        ("res://assets/portrait/team1.png", "res://data/person/person_image_1.json"),
        ("res://assets/portrait/team2.png", "res://data/person/person_image_2.json"),
        ("res://assets/portrait/team3.png", "res://data/person/person_image_3.json"),
        ("res://assets/portrait/team4.png", "res://data/person/person_image_4.json")
    };
    internal const int HireOfficerGoldCost = 200;

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
    private MilitaryUiController? _militaryUiController;
    private CivilUiController? _civilUiController;
    private PersonnelUiController? _personnelUiController;
    private AdvisorUiController? _advisorUiController;
    private DiplomacyUiController? _diplomacyUiController;
    private InternalAffairsUiController? _internalAffairsUiController;
    private Window? _moveDialog;
    private OptionButton? _moveTargetCityOption;
    private Button? _moveConfirmButton;
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
    private SpyUiController? _spyUiController;
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
    private Window? _officerListDialog;
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
    private SelectOfficerDialog? _selectOfficerDialog;
    private Window? _officerDetailDialog;
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
    private bool _moveDialogSignalsConnected;
    private bool _attackOfficerListSignalsConnected;
    private bool _gameEnded;
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

        _merchantDialog = GD.Load<PackedScene>("res://scenes/ui/merchant/MerchantDialog.tscn").Instantiate<Window>();
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

        _militaryUiController = new MilitaryUiController(this);
        _militaryUiController.Initialize();

        _civilUiController = new CivilUiController(this);
        _civilUiController.Initialize();

        _personnelUiController = new PersonnelUiController(this);
        _personnelUiController.Initialize();

        _advisorUiController = new AdvisorUiController(this);
        _advisorUiController.Initialize();
        _diplomacyUiController = new DiplomacyUiController(
            this,
            () => _personnelUiController?.HasPendingPlayerSuccession() == true,
            () => _personnelUiController?.ShowSuccessionDialog());
        _diplomacyUiController.Initialize();

        _spyUiController = new SpyUiController(this);
        _spyUiController.Initialize();

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

        _internalAffairsUiController = new InternalAffairsUiController(this);
        _internalAffairsUiController.Initialize();

        _moveDialog = GD.Load<PackedScene>("res://scenes/ui/military/MoveDialog.tscn").Instantiate<Window>();
        _moveDialog.Exclusive = false;
        _moveDialog.Unresizable = true;
        _moveDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _moveDialog?.Hide();
        };
        AddChild(_moveDialog);
        EnsureMoveDialogWidgets();
        _moveDialog.Hide();

        _attackDialog = GD.Load<PackedScene>("res://scenes/ui/military/AttackDialog.tscn").Instantiate<Window>();
        _attackDialog.Exclusive = false;
        _attackDialog.Unresizable = true;
        _attackDialog.CloseRequested += OnAttackDialogCloseRequested;
        AddChild(_attackDialog);
        EnsureAttackDialogWidgets();
        _attackDialog.Hide();

        _officerListDialog = GD.Load<PackedScene>("res://scenes/ui/view/OfficerListDialog.tscn").Instantiate<Window>();
        _officerListDialog.Exclusive = false;
        _officerListDialog.Unresizable = true;
        _officerListDialog.CloseRequested += OnOfficerListClosePressed;
        AddChild(_officerListDialog);
        _officerListToolbar = _officerListDialog.GetNodeOrNull<HBoxContainer>("OfficerListDialogRoot/OfficerListContentMargin/OfficerListContent/OfficerListToolbar");
        _officerListAuxRow = _officerListDialog.GetNodeOrNull<HBoxContainer>("OfficerListDialogRoot/OfficerListContentMargin/OfficerListContent/OfficerListAuxRow");
        _officerListAuxLabel = _officerListDialog.GetNodeOrNull<Label>("OfficerListDialogRoot/OfficerListContentMargin/OfficerListContent/OfficerListAuxRow/OfficerListAuxLabel");
        _officerListAuxOption = _officerListDialog.GetNodeOrNull<OptionButton>("OfficerListDialogRoot/OfficerListContentMargin/OfficerListContent/OfficerListAuxRow/OfficerListAuxOption");
        _viewCityOfficersDialogButton = _officerListDialog.GetNodeOrNull<Button>("OfficerListDialogRoot/OfficerListContentMargin/OfficerListContent/OfficerListToolbar/ViewCityOfficersButton");
        _viewFactionOfficersDialogButton = _officerListDialog.GetNodeOrNull<Button>("OfficerListDialogRoot/OfficerListContentMargin/OfficerListContent/OfficerListToolbar/ViewFactionOfficersButton");
        _viewFactionItemsDialogButton = _officerListDialog.GetNodeOrNull<Button>("OfficerListDialogRoot/OfficerListContentMargin/OfficerListContent/OfficerListToolbar/ViewFactionItemsButton");
        _viewDiplomacyRelationsDialogButton = _officerListDialog.GetNodeOrNull<Button>("OfficerListDialogRoot/OfficerListContentMargin/OfficerListContent/OfficerListToolbar/ViewDiplomacyRelationsButton");
        _viewCitiesDialogButton = _officerListDialog.GetNodeOrNull<Button>("OfficerListDialogRoot/OfficerListContentMargin/OfficerListContent/OfficerListToolbar/ViewCitiesButton");
        _cityListFilterOption = _officerListDialog.GetNodeOrNull<OptionButton>("OfficerListDialogRoot/OfficerListContentMargin/OfficerListContent/OfficerListToolbar/CityListFilterOption");
        _officerSortOption = _officerListDialog.GetNodeOrNull<OptionButton>("OfficerListDialogRoot/OfficerListContentMargin/OfficerListContent/OfficerListToolbar/OfficerSortOption");
        _officerListTable = _officerListDialog.GetNodeOrNull<Tree>("OfficerListDialogRoot/OfficerListContentMargin/OfficerListContent/OfficerListTable");
        _officerListConfirmButton = _officerListDialog.GetNodeOrNull<Button>("OfficerListDialogRoot/OfficerListContentMargin/OfficerListContent/OfficerListConfirmRow/OfficerListConfirmButton");
        if (_officerListAuxOption != null) _officerListAuxOption.ItemSelected += OnOfficerListAuxOptionSelected;
        if (_viewCityOfficersDialogButton != null) _viewCityOfficersDialogButton.Pressed += OnViewCityOfficersDialogPressed;
        if (_viewFactionOfficersDialogButton != null) _viewFactionOfficersDialogButton.Pressed += OnViewFactionOfficersDialogPressed;
        if (_viewFactionItemsDialogButton != null) _viewFactionItemsDialogButton.Pressed += OnViewFactionItemsDialogPressed;
        if (_viewDiplomacyRelationsDialogButton != null) _viewDiplomacyRelationsDialogButton.Pressed += OnViewDiplomacyRelationsDialogPressed;
        if (_viewCitiesDialogButton != null) _viewCitiesDialogButton.Pressed += OnViewCitiesDialogPressed;
        if (_cityListFilterOption != null) _cityListFilterOption.ItemSelected += OnCityListFilterOptionSelected;
        if (_officerSortOption != null) _officerSortOption.ItemSelected += OnOfficerSortOptionSelected;
        if (_officerListTable != null)
        {
            _officerListTable.ItemSelected += OnOfficerListTableSelected;
            _officerListTable.ItemActivated += OnOfficerListTableActivated;
            _officerListTable.ColumnTitleClicked += OnOfficerListTableColumnTitleClicked;
        }
        if (_officerListConfirmButton != null) _officerListConfirmButton.Pressed += OnOfficerListDialogConfirmed;
        _officerListDialog.Hide();

        ApplyOfficerListDialogTheme();

        _selectOfficerDialog = GD.Load<PackedScene>("res://scenes/ui/view/SelectOfficerDialog.tscn").Instantiate<SelectOfficerDialog>();
        _selectOfficerDialog.Exclusive = false;
        _selectOfficerDialog.Unresizable = true;
        AddChild(_selectOfficerDialog);
        _selectOfficerDialog.Hide();

        _officerDetailDialog = GD.Load<PackedScene>("res://scenes/ui/view/OfficerDetailDialog.tscn").Instantiate<Window>();
        _officerDetailDialog.Exclusive = false;
        _officerDetailDialog.Unresizable = true;
        _officerDetailDialog.CloseRequested += () =>
        {
            PlayUiClickSfx();
            _officerDetailDialog?.Hide();
        };
        AddChild(_officerDetailDialog);
        EnsureOfficerDetailWidgets();
        _officerDetailDialog.Hide();
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
        _militaryUiController?.HideDialogs();
        _personnelUiController?.HideDialogs();
        _civilUiController?.HideDialogs();
        _internalAffairsUiController?.HideDialogs();
        _merchantDialog?.Hide();
        _moveDialog?.Hide();
        _spyUiController?.HideDialogs();
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
        _internalAffairsUiController?.ShowInternalAffairsDialog();
    }

    private void OnRecruitPressed()
    {
        _militaryUiController?.ShowMilitaryDialog();
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
        _civilUiController?.ShowVisitCitizenDialog();
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

        _diplomacyUiController?.ShowDiplomacyDialog();
    }

    private void OnSpyPressed()
    {
        if (_selectedCity == null)
        {
            return;
        }

        _spyUiController?.ShowSpyDialog();
    }

    private void OnPersonnelPressed()
    {
        if (_selectedCity == null)
        {
            return;
        }

        _personnelUiController?.ShowPersonnelDialog();
    }

    private void OnAdvisorPressed()
    {
        if (_selectedCity == null)
        {
            return;
        }

        _advisorUiController?.ShowAdvisorDialog();
    }

    private void OnCivilPressed()
    {
        if (_selectedCity == null)
        {
            return;
        }

        _civilUiController?.ShowCivilDialog();
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
        ResetOfficerListDialogLayoutToSceneDefaults();
        if (_officerListAuxRow != null)
        {
            _officerListAuxRow.Visible = false;
        }
        _officerListContentMode = OfficerListContentMode.Officers;
        _officerListScope = OfficerListScope.City;
        if (_officerListConfirmButton != null)
        {
            _officerListConfirmButton.Text = _localization?.T("ui.confirm_officer_selection") ?? "Confirm Selection";
        }
        UpdateOfficerListToolbar();
        PopulateOfficerListDialog();
        PopupDialogUsingSceneSize(_officerListDialog);
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
