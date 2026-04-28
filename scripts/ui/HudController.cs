using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using ThreeKingdom.Map;

namespace ThreeKingdom.UI;

public partial class HudController : CanvasLayer
{
    private enum OfficerListMode
    {
        View,
        CommandSelection
    }

    private enum OfficerListScope
    {
        City,
        Faction
    }

    private enum OfficerListContentMode
    {
        Officers,
        Cities
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
        Age,
        Strength,
        Intelligence,
        OfficerLoyalty,
        Owner,
        Gold,
        Food,
        Troops,
        OfficerCount,
        Farm,
        Commercial,
        Defense,
        Loyalty
    }

    private const string PortraitSheetPath = "res://assets/portrait/100.png";
    private const string PortraitMappingPath = "res://data/person/portraits_names.json";
    private const int HireOfficerGoldCost = 200;

    private Label? _monthLabel;
    private Label? _playerFactionLabel;
    private Label? _storyLabel;
    private Label? _cityNameLabel;
    private Label? _cityStatsLabel;
    private Label? _commandsTitle;
    private Label? _cityOfficerListTitle;
    private RichTextLabel? _cityOfficerListText;

    private Button? _languageButton;
    private Button? _endTurnButton;
    private Button? _developButton;
    private Button? _recruitButton;
    private Button? _moveButton;
    private Button? _searchButton;
    private Button? _merchantButton;
    private Button? _personnelButton;
    private Button? _civilButton;
    private Button? _attackButton;
    private Button? _viewButton;
    private PopupMenu? _targetCityMenu;
    private AcceptDialog? _merchantDialog;
    private OptionButton? _merchantModeOption;
    private SpinBox? _merchantFoodSpinBox;
    private Label? _merchantSummaryLabel;
    private AcceptDialog? _militaryDialog;
    private OptionButton? _militaryCommandOption;
    private AcceptDialog? _personnelDialog;
    private OptionButton? _personnelCommandOption;
    private AcceptDialog? _personnelBonusDialog;
    private ItemList? _personnelBonusOfficerList;
    private SpinBox? _personnelBonusGoldSpinBox;
    private SpinBox? _personnelBonusFoodSpinBox;
    private Label? _personnelBonusSummaryLabel;
    private AcceptDialog? _assignRoleDialog;
    private ItemList? _assignRoleOfficerList;
    private OptionButton? _assignRoleOption;
    private AcceptDialog? _hireOfficerDialog;
    private ItemList? _hireOfficerList;
    private Label? _hireOfficerSummaryLabel;
    private AcceptDialog? _civilDialog;
    private OptionButton? _civilCommandOption;
    private AcceptDialog? _civilReliefDialog;
    private SpinBox? _civilReliefGoldSpinBox;
    private SpinBox? _civilReliefFoodSpinBox;
    private Label? _civilReliefSummaryLabel;
    private AcceptDialog? _internalAffairsDialog;
    private OptionButton? _internalAffairsJobOption;
    private SpinBox? _internalAffairsDurationSpinBox;
    private ItemList? _internalAffairsOfficerList;
    private ItemList? _internalAffairsScheduleList;
    private Button? _internalAffairsTerminateButton;
    private Label? _internalAffairsWarningLabel;
    private AcceptDialog? _moveDialog;
    private OptionButton? _moveTargetCityOption;
    private SpinBox? _moveTroopsSpinBox;
    private SpinBox? _moveGoldSpinBox;
    private SpinBox? _moveFoodSpinBox;
    private ItemList? _moveOfficerList;
    private AcceptDialog? _attackDialog;
    private OptionButton? _attackTargetCityOption;
    private SpinBox? _attackTroopsSpinBox;
    private SpinBox? _attackGoldSpinBox;
    private SpinBox? _attackFoodSpinBox;
    private ItemList? _attackOfficerList;
    private Label? _attackWarningLabel;
    private AcceptDialog? _officerListDialog;
    private PanelContainer? _officerListTitlebarFill;
    private PanelContainer? _officerListHeaderPanel;
    private Label? _officerListHeaderLabel;
    private Button? _officerListCloseButton;
    private HBoxContainer? _officerListToolbar;
    private Button? _viewCityOfficersDialogButton;
    private Button? _viewFactionOfficersDialogButton;
    private Button? _viewCitiesDialogButton;
    private Button? _officerListConfirmButton;
    private OptionButton? _cityListFilterOption;
    private OptionButton? _officerSortOption;
    private Tree? _officerListTable;
    private ItemList? _officerListView;
    private AcceptDialog? _officerDetailDialog;
    private TextureRect? _officerPortraitRect;
    private Label? _officerPortraitPlaceholderLabel;
    private RichTextLabel? _officerDetailText;

    private RichTextLabel? _logText;

    private TurnManager? _turnManager;
    private CommandResolver? _commandResolver;
    private LocalizationService? _localization;
    private AiController? _aiController;
    private MapController? _mapController;
    private CityData? _selectedCity;

    private bool _isLanguageButtonConnected;
    private bool _isEndTurnButtonConnected;
    private bool _isDevelopButtonConnected;
    private bool _isRecruitButtonConnected;
    private bool _isMoveButtonConnected;
    private bool _isSearchButtonConnected;
    private bool _isMerchantButtonConnected;
    private bool _isPersonnelButtonConnected;
    private bool _isCivilButtonConnected;
    private bool _isAttackButtonConnected;
    private bool _isViewButtonConnected;
    private bool _merchantDialogSignalsConnected;
    private bool _gameEnded;
    private bool _isDraggingOfficerListDialog;
    private Vector2I _officerListDialogDragOffset;
    private readonly HashSet<int> _aliveFactionIds = new();
    private CommandType _pendingTargetCommand = CommandType.Pass;
    private Texture2D? _portraitSheetTexture;
    private readonly Dictionary<int, Rect2> _portraitRegions = new();
    private OfficerListMode _officerListMode = OfficerListMode.View;
    private OfficerListScope _officerListScope = OfficerListScope.City;
    private OfficerListContentMode _officerListContentMode = OfficerListContentMode.Officers;
    private CityListFilterMode _cityListFilterMode = CityListFilterMode.SelfFaction;
    private OfficerSortMode _officerSortMode = OfficerSortMode.Strength;
    private ViewTableSortField _viewTableSortField = ViewTableSortField.Name;
    private bool _viewTableSortAscending = true;
    private CommandType _pendingOfficerCommand = CommandType.Pass;

    public override void _Ready()
    {
        _monthLabel = GetNodeOrNull<Label>("Root/TopBar/MonthLabel");
        _playerFactionLabel = GetNodeOrNull<Label>("Root/TopBar/PlayerFactionLabel");
        _storyLabel = GetNodeOrNull<Label>("Root/TopBar/StoryLabel");
        _languageButton = GetNodeOrNull<Button>("Root/TopBar/LanguageButton");
        _endTurnButton = GetNodeOrNull<Button>("Root/TopBar/EndTurnButton");

        _cityNameLabel = GetNodeOrNull<Label>("Root/LeftPanel/CityNameLabel");
        _cityStatsLabel = GetNodeOrNull<Label>("Root/LeftPanel/CityStatsLabel");
        _commandsTitle = GetNodeOrNull<Label>("Root/LeftPanel/CommandsTitle");
        var leftPanel = GetNodeOrNull<VBoxContainer>("Root/LeftPanel");
        if (leftPanel != null)
        {
            EnsureOfficerListWidgets(leftPanel);
        }

        _developButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/DevelopButton");
        _recruitButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/RecruitButton");
        _moveButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/MoveButton");
        if (_moveButton != null)
        {
            _moveButton.Visible = false;
        }
        _searchButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/SearchButton");
        _merchantButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/MerchantButton");
        _personnelButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/PersonnelButton");
        _civilButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/CivilButton");
        _attackButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/AttackButton");
        if (_attackButton != null)
        {
            _attackButton.Visible = false;
        }
        _viewButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/ViewButton");

        _logText = GetNodeOrNull<RichTextLabel>("Root/LogText");
        if (_logText != null)
        {
            _logText.ScrollFollowing = true;
        }

        _targetCityMenu = new PopupMenu();
        AddChild(_targetCityMenu);
        _targetCityMenu.IdPressed += OnTargetCityMenuIdPressed;

        _merchantDialog = new AcceptDialog();
        _merchantDialog.Exclusive = false;
        _merchantDialog.Unfocusable = false;
        _merchantDialog.Confirmed += OnMerchantDialogConfirmed;
        AddChild(_merchantDialog);
        EnsureMerchantDialogWidgets();

        _militaryDialog = new AcceptDialog();
        _militaryDialog.Exclusive = false;
        _militaryDialog.Unfocusable = false;
        _militaryDialog.Confirmed += OnMilitaryDialogConfirmed;
        AddChild(_militaryDialog);
        EnsureMilitaryDialogWidgets();

        _personnelDialog = new AcceptDialog();
        _personnelDialog.Exclusive = false;
        _personnelDialog.Unfocusable = false;
        _personnelDialog.Confirmed += OnPersonnelDialogConfirmed;
        AddChild(_personnelDialog);
        EnsurePersonnelDialogWidgets();

        _personnelBonusDialog = new AcceptDialog();
        _personnelBonusDialog.Exclusive = false;
        _personnelBonusDialog.Unfocusable = false;
        _personnelBonusDialog.Confirmed += OnPersonnelBonusDialogConfirmed;
        AddChild(_personnelBonusDialog);
        EnsurePersonnelBonusDialogWidgets();

        _assignRoleDialog = new AcceptDialog();
        _assignRoleDialog.Exclusive = false;
        _assignRoleDialog.Unfocusable = false;
        _assignRoleDialog.Confirmed += OnAssignRoleDialogConfirmed;
        AddChild(_assignRoleDialog);
        EnsureAssignRoleDialogWidgets();

        _hireOfficerDialog = new AcceptDialog();
        _hireOfficerDialog.Exclusive = false;
        _hireOfficerDialog.Unfocusable = false;
        _hireOfficerDialog.Confirmed += OnHireOfficerDialogConfirmed;
        AddChild(_hireOfficerDialog);
        EnsureHireOfficerDialogWidgets();

        _civilDialog = new AcceptDialog();
        _civilDialog.Exclusive = false;
        _civilDialog.Unfocusable = false;
        _civilDialog.Confirmed += OnCivilDialogConfirmed;
        AddChild(_civilDialog);
        EnsureCivilDialogWidgets();

        _civilReliefDialog = new AcceptDialog();
        _civilReliefDialog.Exclusive = false;
        _civilReliefDialog.Unfocusable = false;
        _civilReliefDialog.Confirmed += OnCivilReliefDialogConfirmed;
        AddChild(_civilReliefDialog);
        EnsureCivilReliefDialogWidgets();

        _internalAffairsDialog = new AcceptDialog();
        _internalAffairsDialog.Exclusive = false;
        _internalAffairsDialog.Unfocusable = false;
        _internalAffairsDialog.Confirmed += OnInternalAffairsDialogConfirmed;
        AddChild(_internalAffairsDialog);
        EnsureInternalAffairsDialogWidgets();

        _moveDialog = new AcceptDialog();
        _moveDialog.Exclusive = false;
        _moveDialog.Unfocusable = false;
        _moveDialog.Confirmed += OnMoveDialogConfirmed;
        AddChild(_moveDialog);
        EnsureMoveDialogWidgets();

        _attackDialog = new AcceptDialog();
        _attackDialog.Exclusive = false;
        _attackDialog.Unfocusable = false;
        _attackDialog.Confirmed += OnAttackDialogConfirmed;
        AddChild(_attackDialog);
        EnsureAttackDialogWidgets();

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

        _officerListView = new ItemList
        {
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(320.0f, 220.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _officerListView.ItemSelected += OnOfficerListItemSelected;
        officerListContent.AddChild(_officerListView);

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
        _attackDialog?.Hide();
    }

    public void Initialize(
        TurnManager turnManager,
        CommandResolver commandResolver,
        AiController aiController,
        LocalizationService localization,
        MapController? mapController = null)
    {
        _turnManager = turnManager;
        _commandResolver = commandResolver;
        _localization = localization;
        _aiController = aiController;
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
        _selectedCity = city;
        RefreshSelectedCity();
        if (_localization != null)
        {
            AddLog(_localization.FormatCitySelected(_localization.GetCityName(city)));
        }
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

    public void AddLog(string message)
    {
        if (_logText == null)
        {
            return;
        }

        _logText.AppendText($"\n{message}");
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

        if (_personnelButton != null && !_isPersonnelButtonConnected)
        {
            _personnelButton.Pressed += OnPersonnelPressed;
            _isPersonnelButtonConnected = true;
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
    }

    private void DisconnectButtons()
    {
        if (_languageButton != null && _isLanguageButtonConnected)
        {
            _languageButton.Pressed -= OnLanguageButtonPressed;
            _isLanguageButtonConnected = false;
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

        if (_personnelButton != null && _isPersonnelButtonConnected)
        {
            _personnelButton.Pressed -= OnPersonnelPressed;
            _isPersonnelButtonConnected = false;
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
    }

    private void OnLanguageButtonPressed()
    {
        _localization?.ToggleLanguage();
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
        ShowOfficerCommandDialog(CommandType.Search);
    }

    private void OnMerchantPressed()
    {
        if (_selectedCity == null)
        {
            return;
        }

        ShowMerchantDialog();
    }

    private void OnPersonnelPressed()
    {
        if (_selectedCity == null)
        {
            return;
        }

        ShowPersonnelDialog();
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
        if (_selectedCity == null || _turnManager?.World == null || _officerListDialog == null || _officerListView == null)
        {
            return;
        }

        _officerListMode = OfficerListMode.View;
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

        var selectedOfficerIds = new List<int>();
        if (_moveOfficerList != null)
        {
            for (var index = 0; index < _moveOfficerList.ItemCount; index += 1)
            {
                if (!_moveOfficerList.IsSelected(index))
                {
                    continue;
                }

                var metadata = _moveOfficerList.GetItemMetadata(index);
                if (metadata.VariantType == Variant.Type.Int)
                {
                    selectedOfficerIds.Add(metadata.AsInt32());
                }
            }
        }

        ExecutePlayerCommand(
            CommandType.Move,
            targetMetadata.AsInt32(),
            _moveTroopsSpinBox != null ? (int)_moveTroopsSpinBox.Value : 0,
            _moveGoldSpinBox != null ? (int)_moveGoldSpinBox.Value : 0,
            _moveFoodSpinBox != null ? (int)_moveFoodSpinBox.Value : 0,
            selectedOfficerIds);
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

        ExecutePlayerCommand(
            CommandType.Merchant,
            null,
            0,
            0,
            _merchantFoodSpinBox != null ? (int)_merchantFoodSpinBox.Value : 0,
            null,
            selectedIndex == 1);
    }

    private void OnAttackDialogConfirmed()
    {
        if (_attackTargetCityOption == null)
        {
            return;
        }

        var selectedOfficerIds = GetSelectedItemMetadataIds(_attackOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            SetAttackDialogWarning(_localization?.T("ui.attack_officer_required_warning") ?? "Select at least one officer.");
            // Reopen after AcceptDialog confirmation so invalid input behaves like inline validation, not submit-and-close.
            ReopenAttackDialog();
            return;
        }

        var attackTroops = GetRequestedSpinBoxValue(_attackTroopsSpinBox);
        if (attackTroops <= 0)
        {
            SetAttackDialogWarning(_localization?.T("ui.attack_troops_required_warning") ?? "Enter the number of troops to deploy.");
            ReopenAttackDialog();
            return;
        }

        if (_selectedCity != null && attackTroops > _selectedCity.Troops)
        {
            SetAttackDialogWarning(_localization?.T("ui.attack_troops_exceed_warning") ?? "Troops to deploy cannot exceed the city's available troops.");
            ReopenAttackDialog();
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

        var result = ExecutePlayerCommand(
            CommandType.Attack,
            targetMetadata.AsInt32(),
            attackTroops,
            _attackGoldSpinBox != null ? (int)_attackGoldSpinBox.Value : 0,
            _attackFoodSpinBox != null ? (int)_attackFoodSpinBox.Value : 0,
            selectedOfficerIds);

        if (result.Success)
        {
            SetAttackDialogWarning(string.Empty);
            _attackDialog?.Hide();
            return;
        }

        SetAttackDialogWarning(GetLocalizedResultMessage(result));
        ReopenAttackDialog();
    }

    private void OnOfficerListItemSelected(long index)
    {
        if (_turnManager?.World == null || _officerListView == null)
        {
            return;
        }

        var metadata = _officerListView.GetItemMetadata((int)index);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return;
        }

        if (_officerListMode == OfficerListMode.View && _officerListContentMode == OfficerListContentMode.Cities)
        {
            var city = _turnManager.World.GetCity(metadata.AsInt32());
            if (city == null)
            {
                return;
            }

            _selectedCity = city;
            RefreshSelectedCity();
            _mapController?.SelectCityById(city.Id);
            _officerListDialog?.Hide();
            return;
        }

        var officer = _turnManager.World.GetOfficer(metadata.AsInt32());
        if (officer == null)
        {
            return;
        }

        if (_officerListMode == OfficerListMode.CommandSelection)
        {
            return;
        }

        if (_officerDetailDialog == null)
        {
            return;
        }

        _officerDetailDialog.Title = _localization?.T("ui.officer_detail") ?? "Officer Details";
        if (_officerDetailText != null)
        {
            _officerDetailText.Text = BuildOfficerDetailText(officer);
        }

        if (_officerPortraitRect != null)
        {
            _officerPortraitRect.Texture = BuildOfficerPortraitTexture(officer.Id);
        }

        if (_officerPortraitPlaceholderLabel != null)
        {
            var officerName = _localization?.GetOfficerName(officer) ?? officer.Name;
            var hasPortrait = _officerPortraitRect?.Texture != null;
            _officerPortraitPlaceholderLabel.Visible = !hasPortrait;
            _officerPortraitPlaceholderLabel.Text = $"{(_localization?.T("ui.portrait") ?? "Portrait")}\n{officerName}";
        }

        _officerDetailDialog.DialogText = string.Empty;
        if (_officerDetailDialog.Visible)
        {
            _officerDetailDialog.Show();
        }
        else
        {
            _officerDetailDialog.PopupCentered(new Vector2I(520, 340));
        }
    }

    private void OnOfficerListClosePressed()
    {
        _officerListDialog?.Hide();
    }

    private void OnOfficerListHeaderGuiInput(InputEvent inputEvent)
    {
        if (_officerListDialog == null)
        {
            return;
        }

        if (inputEvent is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            _isDraggingOfficerListDialog = mouseButton.Pressed;
            if (_isDraggingOfficerListDialog)
            {
                _officerListDialogDragOffset = DisplayServer.MouseGetPosition() - _officerListDialog.Position;
            }

            return;
        }

        if (_isDraggingOfficerListDialog && inputEvent is InputEventMouseMotion)
        {
            _officerListDialog.Position = DisplayServer.MouseGetPosition() - _officerListDialogDragOffset;
        }
    }

    private void OnOfficerListTableSelected()
    {
        if (_turnManager?.World == null || _officerListTable == null)
        {
            return;
        }

        var selectedItem = _officerListTable.GetSelected();
        if (selectedItem == null)
        {
            return;
        }

        ApplyViewTableSelectionHighlight(selectedItem);

        var metadata = selectedItem.GetMetadata(0);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return;
        }

        if (_officerListContentMode == OfficerListContentMode.Cities)
        {
            return;
        }

        var officer = _turnManager.World.GetOfficer(metadata.AsInt32());
        if (officer == null)
        {
            return;
        }

        if (_officerDetailDialog == null)
        {
            return;
        }

        _officerDetailDialog.Title = _localization?.T("ui.officer_detail") ?? "Officer Details";
        if (_officerDetailText != null)
        {
            _officerDetailText.Text = BuildOfficerDetailText(officer);
        }

        if (_officerPortraitRect != null)
        {
            _officerPortraitRect.Texture = BuildOfficerPortraitTexture(officer.Id);
        }

        if (_officerPortraitPlaceholderLabel != null)
        {
            var officerName = _localization?.GetOfficerName(officer) ?? officer.Name;
            var hasPortrait = _officerPortraitRect?.Texture != null;
            _officerPortraitPlaceholderLabel.Visible = !hasPortrait;
            _officerPortraitPlaceholderLabel.Text = $"{(_localization?.T("ui.portrait") ?? "Portrait")}\n{officerName}";
        }

        _officerDetailDialog.DialogText = string.Empty;
        if (_officerDetailDialog.Visible)
        {
            _officerDetailDialog.Show();
        }
        else
        {
            _officerDetailDialog.PopupCentered(new Vector2I(520, 340));
        }
    }

    private void OnOfficerListTableActivated()
    {
        if (_turnManager?.World == null || _officerListTable == null || _officerListContentMode != OfficerListContentMode.Cities)
        {
            return;
        }

        var selectedItem = _officerListTable.GetSelected();
        if (selectedItem == null)
        {
            return;
        }

        var metadata = selectedItem.GetMetadata(0);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return;
        }

        var city = _turnManager.World.GetCity(metadata.AsInt32());
        if (city == null)
        {
            return;
        }

        _selectedCity = city;
        RefreshSelectedCity();
        _mapController?.SelectCityById(city.Id);
        _officerListDialog?.Hide();
    }

    private void OnOfficerListTableColumnTitleClicked(long column, long mouseButtonIndex)
    {
        if (_officerListMode != OfficerListMode.View)
        {
            return;
        }

        var nextField = GetViewTableSortFieldForColumn((int)column);
        if (_viewTableSortField == nextField)
        {
            _viewTableSortAscending = !_viewTableSortAscending;
        }
        else
        {
            _viewTableSortField = nextField;
            _viewTableSortAscending = IsAscendingDefaultSortField(nextField);
        }

        PopulateOfficerListDialog();
    }

    private void OnOfficerListDialogConfirmed()
    {
        if (_officerListMode != OfficerListMode.CommandSelection || _officerListView == null)
        {
            _officerListDialog?.Hide();
            return;
        }

        var selectedItems = _officerListView.GetSelectedItems();
        if (selectedItems.Length == 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? "Select one officer first.");
            return;
        }

        var metadata = _officerListView.GetItemMetadata(selectedItems[0]);
        if (metadata.VariantType != Variant.Type.Int)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? "Select one officer first.");
            return;
        }

        var result = ExecutePlayerCommand(_pendingOfficerCommand, officerIds: new List<int> { metadata.AsInt32() });
        if (result.Success)
        {
            _officerListDialog?.Hide();
            return;
        }
    }

    private void PopulateOfficerListDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _officerListView == null || _officerListTable == null)
        {
            return;
        }

        var isViewTable = _officerListMode == OfficerListMode.View;
        _officerListTable.Visible = isViewTable;
        _officerListView.Visible = !isViewTable;

        if (!isViewTable)
        {
            _officerListView.Clear();
            foreach (var officerId in _selectedCity.OfficerIds)
            {
                var officer = _turnManager.World.GetOfficer(officerId);
                if (officer == null)
                {
                    continue;
                }

                var itemIndex = _officerListView.AddItem(BuildOfficerListRowText(officer));
                _officerListView.SetItemMetadata(itemIndex, officer.Id);
            }

            return;
        }

        _officerListTable.Clear();
        ConfigureViewTableColumns();
        var root = _officerListTable.CreateItem();

        if (_officerListMode == OfficerListMode.View && _officerListContentMode == OfficerListContentMode.Cities)
        {
            var cities = GetFilteredCities();
            for (var index = 0; index < cities.Count; index += 1)
            {
                var row = _officerListTable.CreateItem(root);
                PopulateCityTableRow(row, cities[index]);
                ApplyViewTableRowStriping(row, index, 10);
            }

            if (cities.Count == 0)
            {
                AddLog(_localization?.T("ui.no_city_in_scope") ?? "No cities available in this view.");
            }
        }
        else
        {
            var officers = new List<OfficerData>();
            string emptyMessage;
            var includeCityName = false;
            if (_officerListMode == OfficerListMode.View && _officerListScope == OfficerListScope.Faction)
            {
                var faction = _turnManager.World.GetFaction(_selectedCity.OwnerFactionId);
                if (faction != null)
                {
                    foreach (var officerId in faction.OfficerIds)
                    {
                        var officer = _turnManager.World.GetOfficer(officerId);
                        if (officer != null)
                        {
                            officers.Add(officer);
                        }
                    }
                }

                emptyMessage = _localization?.T("ui.no_officer_in_faction") ?? "No officers available in this faction.";
                includeCityName = true;
            }
            else
            {
                foreach (var officerId in _selectedCity.OfficerIds)
                {
                    var officer = _turnManager.World.GetOfficer(officerId);
                    if (officer != null)
                    {
                        officers.Add(officer);
                    }
                }

                emptyMessage = _localization?.T("ui.no_officer_in_city") ?? "No officers available in this city.";
            }

            var sortedOfficers = GetSortedOfficers(officers).ToList();
            var columnCount = includeCityName ? 8 : 7;
            for (var index = 0; index < sortedOfficers.Count; index += 1)
            {
                var row = _officerListTable.CreateItem(root);
                PopulateOfficerTableRow(row, sortedOfficers[index], includeCityName);
                ApplyViewTableRowStriping(row, index, columnCount);
            }

            if (officers.Count == 0)
            {
                AddLog(emptyMessage);
            }
        }

        UpdateOfficerListDialogTitle();
    }

    private List<CityData> GetFilteredCities()
    {
        if (_turnManager?.World == null || _selectedCity == null)
        {
            return new List<CityData>();
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        var result = new List<CityData>();
        foreach (var city in _turnManager.World.Cities)
        {
            var include = _cityListFilterMode switch
            {
                CityListFilterMode.OtherFactions => city.OwnerFactionId > 0 && city.OwnerFactionId != playerFactionId,
                CityListFilterMode.AllCities => true,
                _ => city.OwnerFactionId == playerFactionId
            };

            if (include)
            {
                result.Add(city);
            }
        }

        IOrderedEnumerable<CityData> ordered = _viewTableSortField switch
        {
            ViewTableSortField.Owner => _viewTableSortAscending
                ? result.OrderBy(city => _localization?.GetFactionName(_turnManager.World, city.OwnerFactionId) ?? city.OwnerFactionId.ToString())
                : result.OrderByDescending(city => _localization?.GetFactionName(_turnManager.World, city.OwnerFactionId) ?? city.OwnerFactionId.ToString()),
            ViewTableSortField.Gold => _viewTableSortAscending
                ? result.OrderBy(city => city.Gold)
                : result.OrderByDescending(city => city.Gold),
            ViewTableSortField.Food => _viewTableSortAscending
                ? result.OrderBy(city => city.Food)
                : result.OrderByDescending(city => city.Food),
            ViewTableSortField.Troops => _viewTableSortAscending
                ? result.OrderBy(city => city.Troops)
                : result.OrderByDescending(city => city.Troops),
            ViewTableSortField.OfficerCount => _viewTableSortAscending
                ? result.OrderBy(city => city.OfficerIds.Count)
                : result.OrderByDescending(city => city.OfficerIds.Count),
            ViewTableSortField.Farm => _viewTableSortAscending
                ? result.OrderBy(city => city.Farm)
                : result.OrderByDescending(city => city.Farm),
            ViewTableSortField.Commercial => _viewTableSortAscending
                ? result.OrderBy(city => city.Commercial)
                : result.OrderByDescending(city => city.Commercial),
            ViewTableSortField.Defense => _viewTableSortAscending
                ? result.OrderBy(city => city.Defense)
                : result.OrderByDescending(city => city.Defense),
            ViewTableSortField.Loyalty => _viewTableSortAscending
                ? result.OrderBy(city => city.Loyalty)
                : result.OrderByDescending(city => city.Loyalty),
            _ => _viewTableSortAscending
                ? result.OrderBy(city => _localization?.GetCityName(city) ?? city.NameEn)
                : result.OrderByDescending(city => _localization?.GetCityName(city) ?? city.NameEn)
        };

        return ordered
            .ThenBy(city => city.Id)
            .ToList();
    }

    private void ConfigureViewTableColumns()
    {
        if (_officerListTable == null || _localization == null)
        {
            return;
        }

        if (_officerListContentMode == OfficerListContentMode.Cities)
        {
            _officerListTable.Columns = 10;
            SetViewTableColumn(0, _localization.T("ui.city"), 130, ViewTableSortField.Name);
            SetViewTableColumn(1, _localization.T("ui.owner"), 140, ViewTableSortField.Owner);
            SetViewTableColumn(2, _localization.T("ui.gold"), 90, ViewTableSortField.Gold);
            SetViewTableColumn(3, _localization.T("ui.food"), 90, ViewTableSortField.Food);
            SetViewTableColumn(4, _localization.T("ui.troops"), 90, ViewTableSortField.Troops);
            SetViewTableColumn(5, _localization.T("ui.officers"), 90, ViewTableSortField.OfficerCount);
            SetViewTableColumn(6, _localization.T("ui.farm"), 90, ViewTableSortField.Farm);
            SetViewTableColumn(7, _localization.T("ui.commercial"), 110, ViewTableSortField.Commercial);
            SetViewTableColumn(8, _localization.T("ui.defense"), 90, ViewTableSortField.Defense);
            SetViewTableColumn(9, _localization.T("ui.loyalty"), 90, ViewTableSortField.Loyalty);
            return;
        }

        var includeCityName = _officerListScope == OfficerListScope.Faction;
        _officerListTable.Columns = includeCityName ? 8 : 7;
        SetViewTableColumn(0, _localization.T("ui.officers"), 170, ViewTableSortField.Name);
        SetViewTableColumn(1, _localization.T("ui.role"), 120, ViewTableSortField.Role);
        SetViewTableColumn(2, _localization.T("ui.status"), 100, ViewTableSortField.Status);
        if (includeCityName)
        {
            SetViewTableColumn(3, _localization.T("ui.city"), 140, ViewTableSortField.City);
            SetViewTableColumn(4, _localization.T("ui.age"), 70, ViewTableSortField.Age);
            SetViewTableColumn(5, _localization.T("ui.loyalty"), 90, ViewTableSortField.OfficerLoyalty);
            SetViewTableColumn(6, _localization.T("ui.strength"), 90, ViewTableSortField.Strength);
            SetViewTableColumn(7, _localization.T("ui.intelligence"), 90, ViewTableSortField.Intelligence);
        }
        else
        {
            SetViewTableColumn(3, _localization.T("ui.age"), 70, ViewTableSortField.Age);
            SetViewTableColumn(4, _localization.T("ui.loyalty"), 90, ViewTableSortField.OfficerLoyalty);
            SetViewTableColumn(5, _localization.T("ui.strength"), 90, ViewTableSortField.Strength);
            SetViewTableColumn(6, _localization.T("ui.intelligence"), 90, ViewTableSortField.Intelligence);
        }
    }

    private void SetViewTableColumn(int column, string title, int minWidth, ViewTableSortField field)
    {
        _officerListTable?.SetColumnTitle(column, BuildSortableColumnTitle(title, field));
        _officerListTable?.SetColumnCustomMinimumWidth(column, minWidth);
        _officerListTable?.SetColumnTitleAlignment(column, HorizontalAlignment.Left);
    }

    private string BuildSortableColumnTitle(string title, ViewTableSortField field)
    {
        if (_viewTableSortField != field)
        {
            return title;
        }

        return _viewTableSortAscending ? $"{title} ▲" : $"{title} ▼";
    }

    private static void ApplyViewTableRowStriping(TreeItem row, int rowIndex, int columnCount)
    {
        var background = rowIndex % 2 == 0
            ? new Color(0.98f, 0.95f, 0.89f, 0.92f)
            : new Color(0.93f, 0.88f, 0.78f, 0.9f);
        var textColor = new Color(0.13f, 0.09f, 0.05f, 1.0f);

        for (var column = 0; column < columnCount; column += 1)
        {
            row.SetCustomBgColor(column, background, false);
            row.SetCustomColor(column, textColor);
        }
    }

    private void ApplyViewTableSelectionHighlight(TreeItem selectedRow)
    {
        if (_officerListTable == null)
        {
            return;
        }

        var root = _officerListTable.GetRoot();
        if (root == null)
        {
            return;
        }

        var columnCount = _officerListTable.Columns;
        var row = root.GetFirstChild();
        var rowIndex = 0;
        while (row != null)
        {
            if (row == selectedRow)
            {
                ApplyViewTableSelectedRowStyle(row, columnCount);
            }
            else
            {
                ApplyViewTableRowStriping(row, rowIndex, columnCount);
            }

            row = row.GetNext();
            rowIndex += 1;
        }
    }

    private static void ApplyViewTableSelectedRowStyle(TreeItem row, int columnCount)
    {
        var background = new Color(0.82f, 0.68f, 0.38f, 1.0f);
        var textColor = new Color(0.22f, 0.05f, 0.02f, 1.0f);

        for (var column = 0; column < columnCount; column += 1)
        {
            row.SetCustomBgColor(column, background, false);
            row.SetCustomColor(column, textColor);
        }
    }

    private void PopulateOfficerTableRow(TreeItem row, OfficerData officer, bool includeCityName)
    {
        if (_localization == null)
        {
            return;
        }

        row.SetMetadata(0, officer.Id);
        row.SetText(0, _localization.GetOfficerName(officer));
        row.SetText(1, _localization.GetOfficerRole(officer));
        var world = _turnManager!.World!;
        row.SetText(2, _localization.GetOfficerStatus(world, officer));
        var officerAge = CalculateOfficerAge(officer, world.Year);
        var loyaltyText = BuildOfficerLoyaltyTableText(world, officer);
        if (includeCityName)
        {
            var city = _turnManager?.World?.GetCity(officer.CityId);
            row.SetText(3, city != null ? _localization.GetCityName(city) : "-");
            row.SetText(4, officerAge.ToString());
            row.SetText(5, loyaltyText);
            row.SetText(6, officer.Strength.ToString());
            row.SetText(7, officer.Intelligence.ToString());
        }
        else
        {
            row.SetText(3, officerAge.ToString());
            row.SetText(4, loyaltyText);
            row.SetText(5, officer.Strength.ToString());
            row.SetText(6, officer.Intelligence.ToString());
        }
    }

    private static string BuildOfficerLoyaltyTableText(WorldState world, OfficerData officer)
    {
        return IsFactionRuler(world, officer) ? "-" : officer.Loyalty.ToString();
    }

    private static bool IsFactionRuler(WorldState world, OfficerData officer)
    {
        return world.Factions.Any(faction => faction.RulerOfficerId == officer.Id);
    }

    private void PopulateCityTableRow(TreeItem row, CityData city)
    {
        if (_localization == null || _turnManager?.World == null)
        {
            return;
        }

        row.SetMetadata(0, city.Id);
        row.SetText(0, _localization.GetCityName(city));
        row.SetText(1, _localization.GetFactionName(_turnManager.World, city.OwnerFactionId));
        row.SetText(2, city.Gold.ToString());
        row.SetText(3, city.Food.ToString());
        row.SetText(4, city.Troops.ToString());
        row.SetText(5, city.OfficerIds.Count.ToString());
        row.SetText(6, city.Farm.ToString());
        row.SetText(7, city.Commercial.ToString());
        row.SetText(8, city.Defense.ToString());
        row.SetText(9, city.Loyalty.ToString());
    }

    private ViewTableSortField GetViewTableSortFieldForColumn(int column)
    {
        if (_officerListContentMode == OfficerListContentMode.Cities)
        {
            return column switch
            {
                1 => ViewTableSortField.Owner,
                2 => ViewTableSortField.Gold,
                3 => ViewTableSortField.Food,
                4 => ViewTableSortField.Troops,
                5 => ViewTableSortField.OfficerCount,
                6 => ViewTableSortField.Farm,
                7 => ViewTableSortField.Commercial,
                8 => ViewTableSortField.Defense,
                9 => ViewTableSortField.Loyalty,
                _ => ViewTableSortField.Name
            };
        }

        if (_officerListScope == OfficerListScope.Faction)
        {
            return column switch
            {
                1 => ViewTableSortField.Role,
                2 => ViewTableSortField.Status,
                3 => ViewTableSortField.City,
                4 => ViewTableSortField.Age,
                5 => ViewTableSortField.OfficerLoyalty,
                6 => ViewTableSortField.Strength,
                7 => ViewTableSortField.Intelligence,
                _ => ViewTableSortField.Name
            };
        }

        return column switch
        {
            1 => ViewTableSortField.Role,
            2 => ViewTableSortField.Status,
            3 => ViewTableSortField.Age,
            4 => ViewTableSortField.OfficerLoyalty,
            5 => ViewTableSortField.Strength,
            6 => ViewTableSortField.Intelligence,
            _ => ViewTableSortField.Name
        };
    }

    private static bool IsAscendingDefaultSortField(ViewTableSortField field)
    {
        return field is ViewTableSortField.Name or ViewTableSortField.Role or ViewTableSortField.Status or ViewTableSortField.City or ViewTableSortField.Owner;
    }

    private void UpdateOfficerListToolbar()
    {
        if (_officerListToolbar == null || _viewCityOfficersDialogButton == null || _viewFactionOfficersDialogButton == null || _viewCitiesDialogButton == null || _cityListFilterOption == null || _officerSortOption == null || _selectedCity == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        var isViewMode = _officerListMode == OfficerListMode.View;
        _officerListToolbar.Visible = isViewMode;
        if (!isViewMode)
        {
            return;
        }

        _viewCityOfficersDialogButton.Text = _localization.T("ui.view_city_officers");
        _viewFactionOfficersDialogButton.Text = _localization.T("ui.view_faction_officers");
        _viewCitiesDialogButton.Text = _localization.T("ui.view_cities");
        if (_cityListFilterOption.ItemCount == 0)
        {
            _cityListFilterOption.AddItem(_localization.T("ui.city_filter_self"));
            _cityListFilterOption.AddItem(_localization.T("ui.city_filter_other"));
            _cityListFilterOption.AddItem(_localization.T("ui.city_filter_all"));
        }
        else
        {
            _cityListFilterOption.SetItemText(0, _localization.T("ui.city_filter_self"));
            _cityListFilterOption.SetItemText(1, _localization.T("ui.city_filter_other"));
            _cityListFilterOption.SetItemText(2, _localization.T("ui.city_filter_all"));
        }

        _cityListFilterOption.Select(_cityListFilterMode switch
        {
            CityListFilterMode.OtherFactions => 1,
            CityListFilterMode.AllCities => 2,
            _ => 0
        });

        if (_officerSortOption.ItemCount == 0)
        {
            _officerSortOption.AddItem(_localization.T("ui.sort_strength"));
            _officerSortOption.AddItem(_localization.T("ui.sort_intelligence"));
            _officerSortOption.AddItem(_localization.T("ui.sort_status"));
        }
        else
        {
            _officerSortOption.SetItemText(0, _localization.T("ui.sort_strength"));
            _officerSortOption.SetItemText(1, _localization.T("ui.sort_intelligence"));
            _officerSortOption.SetItemText(2, _localization.T("ui.sort_status"));
        }

        _officerSortOption.Select(_officerSortMode switch
        {
            OfficerSortMode.Intelligence => 1,
            OfficerSortMode.Status => 2,
            _ => 0
        });

        var hasFaction = _selectedCity.OwnerFactionId > 0 && _turnManager.World.GetFaction(_selectedCity.OwnerFactionId) != null;
        _viewFactionOfficersDialogButton.Visible = hasFaction;
        _viewCityOfficersDialogButton.Disabled = _officerListContentMode == OfficerListContentMode.Officers && _officerListScope == OfficerListScope.City;
        _viewFactionOfficersDialogButton.Disabled = !hasFaction || (_officerListContentMode == OfficerListContentMode.Officers && _officerListScope == OfficerListScope.Faction);
        _viewCitiesDialogButton.Disabled = _officerListContentMode == OfficerListContentMode.Cities;
        _cityListFilterOption.Visible = _officerListContentMode == OfficerListContentMode.Cities;
        _officerSortOption.Visible = false;
    }

    private void UpdateOfficerListDialogTitle()
    {
        if (_localization == null)
        {
            return;
        }

        if (_officerListMode != OfficerListMode.View)
        {
            return;
        }

        if (_officerListContentMode == OfficerListContentMode.Cities)
        {
            SetOfficerListDialogTitle(_cityListFilterMode switch
            {
                CityListFilterMode.OtherFactions => _localization.T("ui.view_dialog_title_other_cities"),
                CityListFilterMode.AllCities => _localization.T("ui.view_dialog_title_all_cities"),
                _ => _localization.T("ui.view_dialog_title_self_cities")
            });
            return;
        }

        SetOfficerListDialogTitle(_officerListScope == OfficerListScope.Faction
            ? _localization.T("ui.view_dialog_title_faction")
            : _localization.Format("fmt.view_dialog_title_city_name", _selectedCity != null ? _localization.GetCityName(_selectedCity) : _localization.T("ui.view_dialog_title_city")));
    }

    private void SetOfficerListDialogTitle(string title)
    {
        if (_officerListDialog != null)
        {
            // Keep the built-in titlebar visually quiet and show the real title in our themed header row.
            _officerListDialog.Title = " ";
        }

        if (_officerListHeaderLabel != null)
        {
            _officerListHeaderLabel.Text = title;
        }
    }

    private IEnumerable<OfficerData> GetSortedOfficers(List<OfficerData> officers)
    {
        return _viewTableSortField switch
        {
            ViewTableSortField.Role => _viewTableSortAscending
                ? officers.OrderBy(officer => _localization?.GetOfficerRole(officer) ?? officer.Role)
                : officers.OrderByDescending(officer => _localization?.GetOfficerRole(officer) ?? officer.Role),
            ViewTableSortField.Status => _viewTableSortAscending
                ? officers.OrderBy(officer => GetOfficerStatusSortKey(officer))
                : officers.OrderByDescending(officer => GetOfficerStatusSortKey(officer)),
            ViewTableSortField.City => _viewTableSortAscending
                ? officers.OrderBy(officer => GetOfficerCityNameForSort(officer))
                : officers.OrderByDescending(officer => GetOfficerCityNameForSort(officer)),
            ViewTableSortField.Age => _viewTableSortAscending
                ? officers.OrderBy(officer => CalculateOfficerAge(officer, _turnManager?.World?.Year ?? 0))
                : officers.OrderByDescending(officer => CalculateOfficerAge(officer, _turnManager?.World?.Year ?? 0)),
            ViewTableSortField.OfficerLoyalty => _viewTableSortAscending
                ? officers.OrderBy(officer => officer.Loyalty)
                : officers.OrderByDescending(officer => officer.Loyalty),
            ViewTableSortField.Strength => _viewTableSortAscending
                ? officers.OrderBy(officer => officer.Strength)
                : officers.OrderByDescending(officer => officer.Strength),
            ViewTableSortField.Intelligence => _viewTableSortAscending
                ? officers.OrderBy(officer => officer.Intelligence)
                : officers.OrderByDescending(officer => officer.Intelligence),
            _ => _viewTableSortAscending
                ? officers.OrderBy(officer => _localization?.GetOfficerName(officer) ?? officer.Name)
                : officers.OrderByDescending(officer => _localization?.GetOfficerName(officer) ?? officer.Name)
        };
    }

    private string GetOfficerCityNameForSort(OfficerData officer)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return string.Empty;
        }

        var city = _turnManager.World.GetCity(officer.CityId);
        return city != null ? _localization.GetCityName(city) : string.Empty;
    }

    private int GetOfficerStatusSortKey(OfficerData officer)
    {
        if (_turnManager?.World == null)
        {
            return 0;
        }

        if (officer.LastAssignedYear != _turnManager.World.Year || officer.LastAssignedMonth != _turnManager.World.Month)
        {
            return 0;
        }

        return officer.LastAssignedCommand switch
        {
            CommandType.InternalAffairs => 1,
            CommandType.Develop => 1,
            CommandType.Recruit => 2,
            CommandType.Search => 3,
            CommandType.Move => 4,
            CommandType.Attack => 5,
            _ => 0
        };
    }

    private void ReopenOfficerListDialog()
    {
        if (_officerListDialog == null)
        {
            return;
        }

        CallDeferred(nameof(ReopenOfficerListDialogDeferred));
    }

    private void ReopenOfficerListDialogDeferred()
    {
        _officerListDialog?.PopupCentered(new Vector2I(420, 320));
    }

    private CommandResult ExecutePlayerCommand(
        CommandType type,
        int? targetCityId = null,
        int troopsToSend = 0,
        int goldToSend = 0,
        int foodToSend = 0,
        List<int>? officerIds = null,
        bool sellFood = false)
    {
        if (_gameEnded || _turnManager?.World == null || _commandResolver == null || _selectedCity == null)
        {
            return new CommandResult
            {
                Success = false,
                Message = string.Empty,
                MessageZhHant = string.Empty,
                MessageEn = string.Empty
            };
        }

        var request = new CommandRequest
        {
            Type = type,
            ActorFactionId = _turnManager.GetPlayerFactionId(),
            SourceCityId = _selectedCity.Id,
            TargetCityId = targetCityId,
            TroopsToSend = troopsToSend,
            GoldToSend = type is CommandType.Move or CommandType.Attack ? goldToSend : 0,
            FoodToSend = type is CommandType.Move or CommandType.Attack or CommandType.Merchant ? foodToSend : 0,
            SellFood = type == CommandType.Merchant && sellFood,
            OfficerIds = type is CommandType.Merchant or CommandType.Pass ? new List<int>() : (officerIds ?? new List<int>())
        };

        var result = _commandResolver.Execute(request);
        AddLog(GetLocalizedResultMessage(result));

        var refreshed = _turnManager.World.GetCity(_selectedCity.Id);
        if (refreshed != null)
        {
            _selectedCity = refreshed;
        }

        RefreshSelectedCity();
        CheckFactionEliminations();
        EvaluateWinLose();
        _mapController?.RefreshVisuals();
        return result;
    }

    private void EnsureMoveDialogWidgets()
    {
        if (_moveDialog == null)
        {
            return;
        }

        var existingRoot = _moveDialog.GetNodeOrNull<VBoxContainer>("MoveDialogRoot");
        if (existingRoot != null)
        {
            _moveTargetCityOption = existingRoot.GetNodeOrNull<OptionButton>("TargetCityOption");
            _moveTroopsSpinBox = existingRoot.GetNodeOrNull<SpinBox>("TroopsSpinBox");
            _moveGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldSpinBox");
            _moveFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodSpinBox");
            _moveOfficerList = existingRoot.GetNodeOrNull<ItemList>("OfficerList");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "MoveDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 420.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 10);
        _moveDialog.AddChild(root);

        root.AddChild(CreateMoveFieldLabel("TargetCityLabel"));
        _moveTargetCityOption = new OptionButton
        {
            Name = "TargetCityOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_moveTargetCityOption);

        root.AddChild(CreateMoveFieldLabel("TroopsLabel"));
        _moveTroopsSpinBox = CreateMoveSpinBox("TroopsSpinBox");
        root.AddChild(_moveTroopsSpinBox);

        root.AddChild(CreateMoveFieldLabel("GoldLabel"));
        _moveGoldSpinBox = CreateMoveSpinBox("GoldSpinBox");
        root.AddChild(_moveGoldSpinBox);

        root.AddChild(CreateMoveFieldLabel("FoodLabel"));
        _moveFoodSpinBox = CreateMoveSpinBox("FoodSpinBox");
        root.AddChild(_moveFoodSpinBox);

        root.AddChild(CreateMoveFieldLabel("OfficerListLabel"));
        _moveOfficerList = new ItemList
        {
            Name = "OfficerList",
            SelectMode = ItemList.SelectModeEnum.Multi,
            AllowReselect = true,
            CustomMinimumSize = new Vector2(0.0f, 180.0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_moveOfficerList);
    }

    private void EnsureMerchantDialogWidgets()
    {
        if (_merchantDialog == null)
        {
            return;
        }

        var existingRoot = _merchantDialog.GetNodeOrNull<VBoxContainer>("MerchantDialogRoot");
        if (existingRoot != null)
        {
            _merchantModeOption = existingRoot.GetNodeOrNull<OptionButton>("TradeModeOption");
            _merchantFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodSpinBox");
            _merchantSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            ConnectMerchantDialogSignals();
            return;
        }

        var root = new VBoxContainer
        {
            Name = "MerchantDialogRoot",
            CustomMinimumSize = new Vector2(380.0f, 180.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 10);
        _merchantDialog.AddChild(root);

        root.AddChild(CreateMoveFieldLabel("TradeModeLabel"));
        _merchantModeOption = new OptionButton
        {
            Name = "TradeModeOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_merchantModeOption);

        root.AddChild(CreateMoveFieldLabel("FoodLabel"));
        _merchantFoodSpinBox = CreateMoveSpinBox("FoodSpinBox");
        _merchantFoodSpinBox.Step = 100;
        root.AddChild(_merchantFoodSpinBox);

        _merchantSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_merchantSummaryLabel);

        ConnectMerchantDialogSignals();
    }

    private Label CreateMoveFieldLabel(string name)
    {
        return new Label
        {
            Name = name
        };
    }

    private SpinBox CreateMoveSpinBox(string name)
    {
        return new SpinBox
        {
            Name = name,
            MinValue = 0,
            Step = 1,
            Rounded = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
    }

    private void ShowMoveDialog(List<int> candidateIds)
    {
        if (_turnManager?.World == null || _selectedCity == null || _moveDialog == null || _moveTargetCityOption == null)
        {
            return;
        }

        EnsureMoveDialogWidgets();
        UpdateMoveDialogText();

        _moveTargetCityOption.Clear();
        foreach (var cityId in candidateIds)
        {
            var city = _turnManager.World.GetCity(cityId);
            if (city == null)
            {
                continue;
            }

            var label = _localization?.GetCityName(city) ?? city.NameEn;
            _moveTargetCityOption.AddItem(label);
            _moveTargetCityOption.SetItemMetadata(_moveTargetCityOption.ItemCount - 1, city.Id);
        }

        if (_moveTargetCityOption.ItemCount > 0)
        {
            _moveTargetCityOption.Select(0);
        }

        ConfigureMoveSpinBox(_moveTroopsSpinBox, _selectedCity.Troops, _selectedCity.Troops / 2);
        ConfigureMoveSpinBox(_moveGoldSpinBox, _selectedCity.Gold, _selectedCity.Gold / 2);
        ConfigureMoveSpinBox(_moveFoodSpinBox, _selectedCity.Food, _selectedCity.Food / 2);

        var availableOfficerIds = GetAvailableOfficerIdsForOrder();
        if (_moveOfficerList != null)
        {
            _moveOfficerList.Clear();
            foreach (var officerId in _selectedCity.OfficerIds)
            {
                if (!availableOfficerIds.Contains(officerId))
                {
                    continue;
                }

                var officer = _turnManager.World.GetOfficer(officerId);
                if (officer == null)
                {
                    continue;
                }

                var itemIndex = _moveOfficerList.AddItem(BuildOfficerListRowText(officer));
                _moveOfficerList.SetItemMetadata(itemIndex, officer.Id);
            }
        }

        _moveDialog.PopupCentered(new Vector2I(460, 520));
    }

    private void EnsureAttackDialogWidgets()
    {
        if (_attackDialog == null)
        {
            return;
        }

        var existingRoot = _attackDialog.GetNodeOrNull<VBoxContainer>("AttackDialogRoot");
        if (existingRoot != null)
        {
            _attackTargetCityOption = existingRoot.GetNodeOrNull<OptionButton>("TargetCityOption");
            _attackTroopsSpinBox = existingRoot.GetNodeOrNull<SpinBox>("TroopsSpinBox");
            _attackGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldSpinBox");
            _attackFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodSpinBox");
            _attackOfficerList = existingRoot.GetNodeOrNull<ItemList>("OfficerList");
            _attackWarningLabel = existingRoot.GetNodeOrNull<Label>("WarningLabel");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "AttackDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 460.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 10);
        _attackDialog.AddChild(root);

        root.AddChild(CreateMoveFieldLabel("TargetCityLabel"));
        _attackTargetCityOption = new OptionButton
        {
            Name = "TargetCityOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_attackTargetCityOption);

        root.AddChild(CreateMoveFieldLabel("TroopsLabel"));
        _attackTroopsSpinBox = CreateMoveSpinBox("TroopsSpinBox");
        root.AddChild(_attackTroopsSpinBox);

        root.AddChild(CreateMoveFieldLabel("GoldLabel"));
        _attackGoldSpinBox = CreateMoveSpinBox("GoldSpinBox");
        root.AddChild(_attackGoldSpinBox);

        root.AddChild(CreateMoveFieldLabel("FoodLabel"));
        _attackFoodSpinBox = CreateMoveSpinBox("FoodSpinBox");
        root.AddChild(_attackFoodSpinBox);

        root.AddChild(CreateMoveFieldLabel("OfficerListLabel"));
        _attackOfficerList = new ItemList
        {
            Name = "OfficerList",
            SelectMode = ItemList.SelectModeEnum.Multi,
            AllowReselect = true,
            CustomMinimumSize = new Vector2(0.0f, 180.0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_attackOfficerList);

        _attackWarningLabel = new Label
        {
            Name = "WarningLabel",
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.Off,
            ClipText = true,
            CustomMinimumSize = new Vector2(0.0f, 24.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _attackWarningLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.52f, 0.45f, 1.0f));
        root.AddChild(_attackWarningLabel);
    }

    private void ConfigureMoveSpinBox(SpinBox? spinBox, int maxValue, int defaultValue)
    {
        if (spinBox == null)
        {
            return;
        }

        spinBox.MaxValue = maxValue;
        spinBox.Value = maxValue <= 0 ? 0 : Mathf.Clamp(defaultValue, 0, maxValue);
    }

    private static void ConfigureAttackTroopsSpinBox(SpinBox? spinBox, int availableTroops)
    {
        if (spinBox == null)
        {
            return;
        }

        // Allow over-typing here so confirm-time validation can show a real warning instead of silent clamping.
        spinBox.MinValue = 0;
        spinBox.MaxValue = Mathf.Max(availableTroops * 10, 99999);
        spinBox.Value = availableTroops <= 0 ? 0 : availableTroops / 2;
    }

    private static int GetRequestedSpinBoxValue(SpinBox? spinBox)
    {
        if (spinBox == null)
        {
            return 0;
        }

        // Read the raw text first because SpinBox.Value may already be clamped to MaxValue.
        var lineEdit = spinBox.GetLineEdit();
        if (lineEdit != null)
        {
            var rawText = lineEdit.Text?.Trim();
            if (!string.IsNullOrEmpty(rawText) && int.TryParse(rawText, out var parsedValue))
            {
                return parsedValue;
            }
        }

        return (int)spinBox.Value;
    }

    private void ShowAttackDialog(List<int> candidateIds)
    {
        if (_turnManager?.World == null || _selectedCity == null || _attackDialog == null || _attackTargetCityOption == null)
        {
            return;
        }

        EnsureAttackDialogWidgets();
        UpdateAttackDialogText();
        SetAttackDialogWarning(string.Empty);

        _attackTargetCityOption.Clear();
        foreach (var cityId in candidateIds)
        {
            var city = _turnManager.World.GetCity(cityId);
            if (city == null)
            {
                continue;
            }

            var label = _localization?.GetCityName(city) ?? city.NameEn;
            _attackTargetCityOption.AddItem(label);
            _attackTargetCityOption.SetItemMetadata(_attackTargetCityOption.ItemCount - 1, city.Id);
        }

        if (_attackTargetCityOption.ItemCount > 0)
        {
            _attackTargetCityOption.Select(0);
        }

        ConfigureAttackTroopsSpinBox(_attackTroopsSpinBox, _selectedCity.Troops);
        ConfigureMoveSpinBox(_attackGoldSpinBox, _selectedCity.Gold, 0);
        ConfigureMoveSpinBox(_attackFoodSpinBox, _selectedCity.Food, 0);

        var availableOfficerIds = GetAvailableOfficerIdsForOrder();
        if (_attackOfficerList != null)
        {
            _attackOfficerList.Clear();
            foreach (var officerId in _selectedCity.OfficerIds)
            {
                if (!availableOfficerIds.Contains(officerId))
                {
                    continue;
                }

                var officer = _turnManager.World.GetOfficer(officerId);
                if (officer == null)
                {
                    continue;
                }

                var itemIndex = _attackOfficerList.AddItem(BuildOfficerListRowText(officer));
                _attackOfficerList.SetItemMetadata(itemIndex, officer.Id);
            }
        }

        _attackDialog.PopupCentered(new Vector2I(460, 560));
    }

    private void ShowMerchantDialog()
    {
        if (_merchantDialog == null || _merchantModeOption == null)
        {
            return;
        }

        EnsureMerchantDialogWidgets();
        UpdateMerchantDialogText();

        _merchantModeOption.Clear();
        _merchantModeOption.AddItem(_localization?.T("ui.buy_food") ?? "Buy Food");
        _merchantModeOption.AddItem(_localization?.T("ui.sell_food") ?? "Sell Food");
        _merchantModeOption.Select(0);

        UpdateMerchantFoodSpinBoxRange();
        UpdateMerchantTradeSummary();
        _merchantDialog.PopupCentered(new Vector2I(400, 220));
    }

    private void UpdateMoveDialogText()
    {
        if (_moveDialog == null || _localization == null)
        {
            return;
        }

        _moveDialog.Title = _localization.T("ui.move");
        _moveDialog.OkButtonText = _localization.T("ui.confirm_move");

        SetMoveDialogLabelText("TargetCityLabel", _localization.T("ui.target_city"));
        SetMoveDialogLabelText("TroopsLabel", _localization.T("ui.transfer_troops"));
        SetMoveDialogLabelText("GoldLabel", _localization.T("ui.transfer_gold"));
        SetMoveDialogLabelText("FoodLabel", _localization.T("ui.transfer_food"));
        SetMoveDialogLabelText("OfficerListLabel", _localization.T("ui.transfer_officers"));
    }

    private void UpdateMerchantDialogText()
    {
        if (_merchantDialog == null || _localization == null)
        {
            return;
        }

        _merchantDialog.Title = _localization.T("ui.merchant");
        _merchantDialog.OkButtonText = _localization.T("ui.confirm_merchant");
        SetMerchantDialogLabelText("TradeModeLabel", _localization.T("ui.trade_mode"));
        SetMerchantDialogLabelText("FoodLabel", _localization.T("ui.food_amount"));
        UpdateMerchantTradeSummary();
    }

    private void UpdateAttackDialogText()
    {
        if (_attackDialog == null || _localization == null)
        {
            return;
        }

        _attackDialog.Title = _localization.T("ui.attack");
        _attackDialog.OkButtonText = _localization.T("ui.confirm_attack");

        SetAttackDialogLabelText("TargetCityLabel", _localization.T("ui.target_city"));
        SetAttackDialogLabelText("TroopsLabel", _localization.T("ui.attack_troops"));
        SetAttackDialogLabelText("GoldLabel", _localization.T("ui.attack_gold"));
        SetAttackDialogLabelText("FoodLabel", _localization.T("ui.attack_food"));
        SetAttackDialogLabelText("OfficerListLabel", _localization.T("ui.attack_officers"));
    }

    private void SetMoveDialogLabelText(string nodeName, string text)
    {
        var label = _moveDialog?.GetNodeOrNull<Label>($"MoveDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void SetAttackDialogLabelText(string nodeName, string text)
    {
        var label = _attackDialog?.GetNodeOrNull<Label>($"AttackDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void SetAttackDialogWarning(string text)
    {
        if (_attackWarningLabel == null)
        {
            return;
        }

        _attackWarningLabel.Text = text;
        _attackWarningLabel.Visible = !string.IsNullOrWhiteSpace(text);
    }

    private void ReopenAttackDialog()
    {
        if (_attackDialog == null)
        {
            return;
        }

        CallDeferred(nameof(ReopenAttackDialogDeferred));
    }

    private void ReopenAttackDialogDeferred()
    {
        if (_attackDialog == null)
        {
            return;
        }

        var size = _attackDialog.Size;
        if (size == Vector2I.Zero)
        {
            size = new Vector2I(460, 560);
        }

        _attackDialog.PopupCentered(size);
    }

    private void SetMerchantDialogLabelText(string nodeName, string text)
    {
        var label = _merchantDialog?.GetNodeOrNull<Label>($"MerchantDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void ConnectMerchantDialogSignals()
    {
        if (_merchantDialogSignalsConnected)
        {
            return;
        }

        if (_merchantModeOption != null)
        {
            _merchantModeOption.ItemSelected += OnMerchantModeSelected;
        }

        if (_merchantFoodSpinBox != null)
        {
            _merchantFoodSpinBox.ValueChanged += OnMerchantFoodValueChanged;
        }

        _merchantDialogSignalsConnected = true;
    }

    private void OnMerchantModeSelected(long index)
    {
        UpdateMerchantFoodSpinBoxRange();
        UpdateMerchantTradeSummary();
    }

    private void OnMerchantFoodValueChanged(double value)
    {
        UpdateMerchantTradeSummary();
    }

    private void UpdateMerchantFoodSpinBoxRange()
    {
        if (_merchantFoodSpinBox == null || _merchantModeOption == null || _selectedCity == null)
        {
            return;
        }

        var isSell = _merchantModeOption.Selected == 1;
        var maxFood = isSell ? _selectedCity.Food : (_selectedCity.Gold / 10) * 100;
        ConfigureMoveSpinBox(_merchantFoodSpinBox, maxFood, maxFood > 0 ? 100 : 0);
        _merchantFoodSpinBox.Step = 100;
    }

    private void UpdateMerchantTradeSummary()
    {
        if (_merchantSummaryLabel == null || _merchantFoodSpinBox == null || _merchantModeOption == null || _localization == null)
        {
            return;
        }

        var foodAmount = (int)_merchantFoodSpinBox.Value;
        var goldAmount = foodAmount / 100 * 10;
        if (_merchantModeOption.Selected == 1)
        {
            _merchantSummaryLabel.Text = _localization.Format("fmt.merchant_sell_preview", foodAmount, goldAmount);
            return;
        }

        _merchantSummaryLabel.Text = _localization.Format("fmt.merchant_buy_preview", goldAmount, foodAmount);
    }

    private void EnsureMilitaryDialogWidgets()
    {
        if (_militaryDialog == null)
        {
            return;
        }

        var existingRoot = _militaryDialog.GetNodeOrNull<VBoxContainer>("MilitaryDialogRoot");
        if (existingRoot != null)
        {
            _militaryCommandOption = existingRoot.GetNodeOrNull<OptionButton>("CommandOption");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "MilitaryDialogRoot",
            CustomMinimumSize = new Vector2(320.0f, 110.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _militaryDialog.AddChild(root);

        root.AddChild(new Label { Name = "CommandLabel" });
        _militaryCommandOption = new OptionButton
        {
            Name = "CommandOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_militaryCommandOption);
    }

    private void ShowMilitaryDialog()
    {
        if (_selectedCity == null || _militaryDialog == null || _localization == null)
        {
            return;
        }

        EnsureMilitaryDialogWidgets();
        UpdateMilitaryDialogText();
        PopulateMilitaryDialog();
        _militaryDialog.PopupCentered(new Vector2I(340, 150));
    }

    private void PopulateMilitaryDialog()
    {
        if (_militaryCommandOption == null || _localization == null)
        {
            return;
        }

        _militaryCommandOption.Clear();
        _militaryCommandOption.AddItem(_localization.T("ui.military_recruit"));
        _militaryCommandOption.SetItemMetadata(_militaryCommandOption.ItemCount - 1, (int)CommandType.Recruit);
        _militaryCommandOption.AddItem(_localization.T("ui.military_move"));
        _militaryCommandOption.SetItemMetadata(_militaryCommandOption.ItemCount - 1, (int)CommandType.Move);
        _militaryCommandOption.AddItem(_localization.T("ui.military_attack"));
        _militaryCommandOption.SetItemMetadata(_militaryCommandOption.ItemCount - 1, (int)CommandType.Attack);
    }

    private void UpdateMilitaryDialogText()
    {
        if (_militaryDialog == null || _localization == null)
        {
            return;
        }

        _militaryDialog.Title = _localization.T("ui.military");
        _militaryDialog.OkButtonText = _localization.T("ui.confirm_military");
        var label = _militaryDialog.GetNodeOrNull<Label>("MilitaryDialogRoot/CommandLabel");
        if (label != null)
        {
            label.Text = _localization.T("ui.military_command");
        }
    }

    private void OnMilitaryDialogConfirmed()
    {
        var commandType = GetSelectedMilitaryCommandType();
        if (commandType == CommandType.Attack)
        {
            OpenAttackFlow();
            return;
        }

        if (commandType == CommandType.Move)
        {
            OpenMoveFlow();
            return;
        }

        ShowOfficerCommandDialog(CommandType.Recruit);
    }

    private CommandType GetSelectedMilitaryCommandType()
    {
        if (_militaryCommandOption == null)
        {
            return CommandType.Recruit;
        }

        var metadata = _militaryCommandOption.GetItemMetadata(_militaryCommandOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (CommandType)metadata.AsInt32()
            : CommandType.Recruit;
    }

    private void EnsurePersonnelDialogWidgets()
    {
        if (_personnelDialog == null)
        {
            return;
        }

        var existingRoot = _personnelDialog.GetNodeOrNull<VBoxContainer>("PersonnelDialogRoot");
        if (existingRoot != null)
        {
            _personnelCommandOption = existingRoot.GetNodeOrNull<OptionButton>("CommandOption");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "PersonnelDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 130.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _personnelDialog.AddChild(root);
        root.AddChild(new Label { Name = "CommandLabel" });
        _personnelCommandOption = new OptionButton
        {
            Name = "CommandOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_personnelCommandOption);
    }

    private void ShowPersonnelDialog()
    {
        if (_personnelDialog == null || _localization == null)
        {
            return;
        }

        EnsurePersonnelDialogWidgets();
        UpdatePersonnelDialogText();
        PopulatePersonnelDialog();
        _personnelDialog.PopupCentered(new Vector2I(440, 170));
    }

    private void PopulatePersonnelDialog()
    {
        if (_personnelCommandOption == null || _localization == null)
        {
            return;
        }

        _personnelCommandOption.Clear();
        AddPersonnelCommandOption("personnel.command.give_bonus");
        AddPersonnelCommandOption("personnel.command.assign_title");
        AddPersonnelCommandOption("personnel.command.hire_officer");
    }

    private void AddPersonnelCommandOption(string localeKey)
    {
        if (_personnelCommandOption == null || _localization == null)
        {
            return;
        }

        _personnelCommandOption.AddItem(_localization.T(localeKey));
        _personnelCommandOption.SetItemMetadata(_personnelCommandOption.ItemCount - 1, localeKey);
    }

    private void UpdatePersonnelDialogText()
    {
        if (_personnelDialog == null || _localization == null)
        {
            return;
        }

        _personnelDialog.Title = _localization.T("ui.personnel");
        _personnelDialog.OkButtonText = _localization.T("ui.confirm_personnel");
        var label = _personnelDialog.GetNodeOrNull<Label>("PersonnelDialogRoot/CommandLabel");
        if (label != null)
        {
            label.Text = _localization.T("ui.personnel_command");
        }
    }

    private void OnPersonnelDialogConfirmed()
    {
        if (_localization == null || _personnelCommandOption == null)
        {
            return;
        }

        var metadata = _personnelCommandOption.GetItemMetadata(_personnelCommandOption.Selected);
        var commandKey = metadata.VariantType == Variant.Type.String ? metadata.AsString() : string.Empty;
        if (commandKey == "personnel.command.give_bonus")
        {
            ShowPersonnelBonusDialog();
            return;
        }

        if (commandKey == "personnel.command.assign_title")
        {
            ShowAssignRoleDialog();
            return;
        }

        if (commandKey == "personnel.command.hire_officer")
        {
            ShowHireOfficerDialog();
            return;
        }

        AddLog(_localization.Format("log.personnel_command_selected", _personnelCommandOption.GetItemText(_personnelCommandOption.Selected)));
    }

    private void EnsurePersonnelBonusDialogWidgets()
    {
        if (_personnelBonusDialog == null)
        {
            return;
        }

        var existingRoot = _personnelBonusDialog.GetNodeOrNull<VBoxContainer>("PersonnelBonusDialogRoot");
        if (existingRoot != null)
        {
            _personnelBonusOfficerList = existingRoot.GetNodeOrNull<ItemList>("OfficerList");
            _personnelBonusGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldSpinBox");
            _personnelBonusFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodSpinBox");
            _personnelBonusSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "PersonnelBonusDialogRoot",
            CustomMinimumSize = new Vector2(460.0f, 360.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _personnelBonusDialog.AddChild(root);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _personnelBonusOfficerList = new ItemList
        {
            Name = "OfficerList",
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0.0f, 150.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_personnelBonusOfficerList);

        root.AddChild(new Label { Name = "GoldLabel" });
        _personnelBonusGoldSpinBox = CreateMoveSpinBox("GoldSpinBox");
        _personnelBonusGoldSpinBox.Step = 100;
        _personnelBonusGoldSpinBox.ValueChanged += _ => UpdatePersonnelBonusSummary();
        root.AddChild(_personnelBonusGoldSpinBox);

        root.AddChild(new Label { Name = "FoodLabel" });
        _personnelBonusFoodSpinBox = CreateMoveSpinBox("FoodSpinBox");
        _personnelBonusFoodSpinBox.Step = 500;
        _personnelBonusFoodSpinBox.ValueChanged += _ => UpdatePersonnelBonusSummary();
        root.AddChild(_personnelBonusFoodSpinBox);

        _personnelBonusSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_personnelBonusSummaryLabel);
    }

    private void ShowPersonnelBonusDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _personnelBonusDialog == null || _localization == null)
        {
            return;
        }

        EnsurePersonnelBonusDialogWidgets();
        UpdatePersonnelBonusDialogText();
        PopulatePersonnelBonusDialog();
        _personnelBonusDialog.PopupCentered(new Vector2I(480, 400));
    }

    private void PopulatePersonnelBonusDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _personnelBonusOfficerList == null)
        {
            return;
        }

        _personnelBonusOfficerList.Clear();
        foreach (var officerId in _selectedCity.OfficerIds)
        {
            var officer = _turnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (IsFactionRuler(_turnManager.World, officer))
            {
                continue;
            }

            var itemIndex = _personnelBonusOfficerList.AddItem(BuildPersonnelBonusOfficerRowText(officer));
            _personnelBonusOfficerList.SetItemMetadata(itemIndex, officer.Id);
        }

        ConfigureMoveSpinBox(_personnelBonusGoldSpinBox, _selectedCity.Gold, 0);
        ConfigureMoveSpinBox(_personnelBonusFoodSpinBox, _selectedCity.Food, 0);
        if (_personnelBonusGoldSpinBox != null)
        {
            _personnelBonusGoldSpinBox.Step = 100;
        }
        if (_personnelBonusFoodSpinBox != null)
        {
            _personnelBonusFoodSpinBox.Step = 500;
        }

        UpdatePersonnelBonusSummary();
    }

    private void UpdatePersonnelBonusDialogText()
    {
        if (_personnelBonusDialog == null || _localization == null)
        {
            return;
        }

        _personnelBonusDialog.Title = _localization.T("personnel.command.give_bonus");
        _personnelBonusDialog.OkButtonText = _localization.T("ui.confirm_personnel_bonus");
        SetPersonnelBonusDialogLabelText("OfficerListLabel", _localization.T("ui.personnel_bonus_officer"));
        SetPersonnelBonusDialogLabelText("GoldLabel", _localization.T("ui.personnel_bonus_gold"));
        SetPersonnelBonusDialogLabelText("FoodLabel", _localization.T("ui.personnel_bonus_food"));
    }

    private string BuildPersonnelBonusOfficerRowText(OfficerData officer)
    {
        var officerName = _localization?.GetOfficerName(officer) ?? officer.Name;
        var roleName = _localization?.GetOfficerRole(officer) ?? officer.Role;
        var loyaltyLabel = _localization?.T("ui.loyalty") ?? "Loyalty";
        return $"{officerName} | {roleName} | {loyaltyLabel} {officer.Loyalty}";
    }

    private void SetPersonnelBonusDialogLabelText(string nodeName, string text)
    {
        var label = _personnelBonusDialog?.GetNodeOrNull<Label>($"PersonnelBonusDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void UpdatePersonnelBonusSummary()
    {
        if (_personnelBonusSummaryLabel == null || _personnelBonusGoldSpinBox == null || _personnelBonusFoodSpinBox == null || _localization == null)
        {
            return;
        }

        var gold = (int)_personnelBonusGoldSpinBox.Value;
        var food = (int)_personnelBonusFoodSpinBox.Value;
        var gain = gold / 100 + food / 500;
        _personnelBonusSummaryLabel.Text = _localization.Format("fmt.personnel_bonus_preview", gain);
    }

    private void OnPersonnelBonusDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var selectedOfficerIds = GetSelectedItemMetadataIds(_personnelBonusOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenPersonnelBonusDialog();
            return;
        }

        var result = _commandResolver.ExecutePersonnelBonus(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            selectedOfficerIds[0],
            (int)(_personnelBonusGoldSpinBox?.Value ?? 0),
            (int)(_personnelBonusFoodSpinBox?.Value ?? 0));
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
    }

    private void ReopenPersonnelBonusDialog()
    {
        CallDeferred(nameof(ReopenPersonnelBonusDialogDeferred));
    }

    private void ReopenPersonnelBonusDialogDeferred()
    {
        _personnelBonusDialog?.PopupCentered(new Vector2I(480, 400));
    }

    private void EnsureAssignRoleDialogWidgets()
    {
        if (_assignRoleDialog == null)
        {
            return;
        }

        var existingRoot = _assignRoleDialog.GetNodeOrNull<VBoxContainer>("AssignRoleDialogRoot");
        if (existingRoot != null)
        {
            _assignRoleOfficerList = existingRoot.GetNodeOrNull<ItemList>("OfficerList");
            _assignRoleOption = existingRoot.GetNodeOrNull<OptionButton>("RoleOption");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "AssignRoleDialogRoot",
            CustomMinimumSize = new Vector2(460.0f, 330.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _assignRoleDialog.AddChild(root);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _assignRoleOfficerList = new ItemList
        {
            Name = "OfficerList",
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0.0f, 160.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_assignRoleOfficerList);

        root.AddChild(new Label { Name = "RoleLabel" });
        _assignRoleOption = new OptionButton
        {
            Name = "RoleOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_assignRoleOption);
    }

    private void ShowAssignRoleDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _assignRoleDialog == null || _localization == null)
        {
            return;
        }

        EnsureAssignRoleDialogWidgets();
        UpdateAssignRoleDialogText();
        PopulateAssignRoleDialog();
        _assignRoleDialog.PopupCentered(new Vector2I(480, 370));
    }

    private void PopulateAssignRoleDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return;
        }

        if (_assignRoleOfficerList != null)
        {
            _assignRoleOfficerList.Clear();
            foreach (var officerId in _selectedCity.OfficerIds)
            {
                var officer = _turnManager.World.GetOfficer(officerId);
                if (officer == null || IsFactionRuler(_turnManager.World, officer))
                {
                    continue;
                }

                var itemIndex = _assignRoleOfficerList.AddItem(BuildAssignRoleOfficerRowText(officer));
                _assignRoleOfficerList.SetItemMetadata(itemIndex, officer.Id);
            }
        }

        if (_assignRoleOption != null)
        {
            _assignRoleOption.Clear();
            AddAssignRoleOption("General");
            AddAssignRoleOption("Strategist");
            AddAssignRoleOption("Advisor");
            AddAssignRoleOption("Governor");
        }
    }

    private void AddAssignRoleOption(string role)
    {
        if (_assignRoleOption == null || _localization == null)
        {
            return;
        }

        _assignRoleOption.AddItem(GetRoleDisplayName(role));
        _assignRoleOption.SetItemMetadata(_assignRoleOption.ItemCount - 1, role);
    }

    private void UpdateAssignRoleDialogText()
    {
        if (_assignRoleDialog == null || _localization == null)
        {
            return;
        }

        _assignRoleDialog.Title = _localization.T("personnel.command.assign_title");
        _assignRoleDialog.OkButtonText = _localization.T("ui.confirm_assign_role");
        SetAssignRoleDialogLabelText("OfficerListLabel", _localization.T("ui.assign_role_officer"));
        SetAssignRoleDialogLabelText("RoleLabel", _localization.T("ui.assign_role_title"));
    }

    private void SetAssignRoleDialogLabelText(string nodeName, string text)
    {
        var label = _assignRoleDialog?.GetNodeOrNull<Label>($"AssignRoleDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private string BuildAssignRoleOfficerRowText(OfficerData officer)
    {
        var officerName = _localization?.GetOfficerName(officer) ?? officer.Name;
        var roleName = _localization?.GetOfficerRole(officer) ?? officer.Role;
        return $"{officerName} | {roleName}";
    }

    private string GetRoleDisplayName(string role)
    {
        if (_localization == null)
        {
            return role;
        }

        return role.ToLowerInvariant() switch
        {
            "general" => _localization.T("role.general"),
            "strategist" => _localization.T("role.strategist"),
            "advisor" => _localization.T("role.advisor"),
            "governor" => _localization.T("role.governor"),
            _ => role
        };
    }

    private void OnAssignRoleDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var selectedOfficerIds = GetSelectedItemMetadataIds(_assignRoleOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenAssignRoleDialog();
            return;
        }

        var roleMetadata = _assignRoleOption?.GetItemMetadata(_assignRoleOption.Selected);
        var role = roleMetadata?.VariantType == Variant.Type.String ? roleMetadata.Value.AsString() : "General";
        var result = _commandResolver.ExecuteAssignOfficerRole(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            selectedOfficerIds[0],
            role);
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
    }

    private void ReopenAssignRoleDialog()
    {
        CallDeferred(nameof(ReopenAssignRoleDialogDeferred));
    }

    private void ReopenAssignRoleDialogDeferred()
    {
        _assignRoleDialog?.PopupCentered(new Vector2I(480, 370));
    }

    private void EnsureHireOfficerDialogWidgets()
    {
        if (_hireOfficerDialog == null)
        {
            return;
        }

        var existingRoot = _hireOfficerDialog.GetNodeOrNull<VBoxContainer>("HireOfficerDialogRoot");
        if (existingRoot != null)
        {
            _hireOfficerList = existingRoot.GetNodeOrNull<ItemList>("OfficerList");
            _hireOfficerSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "HireOfficerDialogRoot",
            CustomMinimumSize = new Vector2(560.0f, 360.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _hireOfficerDialog.AddChild(root);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _hireOfficerList = new ItemList
        {
            Name = "OfficerList",
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0.0f, 220.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_hireOfficerList);

        _hireOfficerSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_hireOfficerSummaryLabel);
    }

    private void ShowHireOfficerDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _hireOfficerDialog == null || _localization == null)
        {
            return;
        }

        EnsureHireOfficerDialogWidgets();
        UpdateHireOfficerDialogText();
        PopulateHireOfficerDialog();
        _hireOfficerDialog.PopupCentered(new Vector2I(580, 400));
    }

    private void PopulateHireOfficerDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _hireOfficerList == null || _localization == null)
        {
            return;
        }

        _hireOfficerList.Clear();
        var playerFactionId = _turnManager.GetPlayerFactionId();
        foreach (var officer in _turnManager.World.Officers.OrderBy(officer => _localization.GetOfficerName(officer)))
        {
            if (!IsHireOfficerCandidate(_turnManager.World, playerFactionId, officer))
            {
                continue;
            }

            var itemIndex = _hireOfficerList.AddItem(BuildHireOfficerRowText(officer));
            _hireOfficerList.SetItemMetadata(itemIndex, officer.Id);
        }

        if (_hireOfficerList.ItemCount == 0)
        {
            _hireOfficerList.AddItem(_localization.T("ui.no_hireable_officer"));
            _hireOfficerList.SetItemDisabled(0, true);
        }

        if (_hireOfficerSummaryLabel != null)
        {
            _hireOfficerSummaryLabel.Text = _localization.Format("fmt.hire_officer_preview", HireOfficerGoldCost);
        }
    }

    private void UpdateHireOfficerDialogText()
    {
        if (_hireOfficerDialog == null || _localization == null)
        {
            return;
        }

        _hireOfficerDialog.Title = _localization.T("personnel.command.hire_officer");
        _hireOfficerDialog.OkButtonText = _localization.T("ui.confirm_hire_officer");
        var label = _hireOfficerDialog.GetNodeOrNull<Label>("HireOfficerDialogRoot/OfficerListLabel");
        if (label != null)
        {
            label.Text = _localization.T("ui.hire_officer_target");
        }
    }

    private string BuildHireOfficerRowText(OfficerData officer)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return officer.Name;
        }

        var sourceCity = officer.CityId > 0 ? _turnManager.World.GetCity(officer.CityId) : null;
        var sourceCityName = sourceCity != null ? _localization.GetCityName(sourceCity) : _localization.T("ui.none");
        var sourceFactionName = sourceCity != null ? _localization.GetFactionName(_turnManager.World, sourceCity.OwnerFactionId) : _localization.T("ui.none");
        return _localization.Format(
            "fmt.hire_officer_row",
            _localization.GetOfficerName(officer),
            _localization.GetOfficerRole(officer),
            officer.Loyalty,
            sourceCityName,
            sourceFactionName);
    }

    private static bool IsHireOfficerCandidate(WorldState world, int playerFactionId, OfficerData officer)
    {
        if (IsFactionRuler(world, officer))
        {
            return false;
        }

        if (!IsOfficerOldEnoughToJoin(world, officer))
        {
            return false;
        }

        var sourceCity = officer.CityId > 0 ? world.GetCity(officer.CityId) : null;
        return sourceCity == null || sourceCity.OwnerFactionId != playerFactionId;
    }

    private void OnHireOfficerDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var selectedOfficerIds = GetSelectedItemMetadataIds(_hireOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenHireOfficerDialog();
            return;
        }

        var result = _commandResolver.ExecuteHireOfficer(_turnManager.GetPlayerFactionId(), _selectedCity.Id, selectedOfficerIds[0]);
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
    }

    private void ReopenHireOfficerDialog()
    {
        CallDeferred(nameof(ReopenHireOfficerDialogDeferred));
    }

    private void ReopenHireOfficerDialogDeferred()
    {
        _hireOfficerDialog?.PopupCentered(new Vector2I(580, 400));
    }

    private void EnsureCivilDialogWidgets()
    {
        if (_civilDialog == null)
        {
            return;
        }

        var existingRoot = _civilDialog.GetNodeOrNull<VBoxContainer>("CivilDialogRoot");
        if (existingRoot != null)
        {
            _civilCommandOption = existingRoot.GetNodeOrNull<OptionButton>("CommandOption");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "CivilDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 120.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _civilDialog.AddChild(root);
        root.AddChild(new Label { Name = "CommandLabel" });
        _civilCommandOption = new OptionButton
        {
            Name = "CommandOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_civilCommandOption);
    }

    private void ShowCivilDialog()
    {
        if (_civilDialog == null || _localization == null)
        {
            return;
        }

        EnsureCivilDialogWidgets();
        UpdateCivilDialogText();
        PopulateCivilDialog();
        _civilDialog.PopupCentered(new Vector2I(440, 160));
    }

    private void PopulateCivilDialog()
    {
        if (_civilCommandOption == null || _localization == null)
        {
            return;
        }

        _civilCommandOption.Clear();
        AddCivilCommandOption("civil.command.relief");
        AddCivilCommandOption("civil.command.investigate_people");
    }

    private void AddCivilCommandOption(string localeKey)
    {
        if (_civilCommandOption == null || _localization == null)
        {
            return;
        }

        _civilCommandOption.AddItem(_localization.T(localeKey));
        _civilCommandOption.SetItemMetadata(_civilCommandOption.ItemCount - 1, localeKey);
    }

    private void UpdateCivilDialogText()
    {
        if (_civilDialog == null || _localization == null)
        {
            return;
        }

        _civilDialog.Title = _localization.T("ui.civil");
        _civilDialog.OkButtonText = _localization.T("ui.confirm_civil");
        var label = _civilDialog.GetNodeOrNull<Label>("CivilDialogRoot/CommandLabel");
        if (label != null)
        {
            label.Text = _localization.T("ui.civil_command");
        }
    }

    private void OnCivilDialogConfirmed()
    {
        if (_localization == null || _civilCommandOption == null)
        {
            return;
        }

        var metadata = _civilCommandOption.GetItemMetadata(_civilCommandOption.Selected);
        var commandKey = metadata.VariantType == Variant.Type.String ? metadata.AsString() : string.Empty;
        if (commandKey == "civil.command.relief")
        {
            ShowCivilReliefDialog();
            return;
        }

        if (commandKey == "civil.command.investigate_people")
        {
            ExecuteCivilInvestigation();
            return;
        }

        AddLog(_localization.Format("log.civil_command_selected", _civilCommandOption.GetItemText(_civilCommandOption.Selected)));
    }

    private void ExecuteCivilInvestigation()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var result = _commandResolver.ExecuteCivilInvestigation(_turnManager.GetPlayerFactionId(), _selectedCity.Id);
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
    }

    private void EnsureCivilReliefDialogWidgets()
    {
        if (_civilReliefDialog == null)
        {
            return;
        }

        var existingRoot = _civilReliefDialog.GetNodeOrNull<VBoxContainer>("CivilReliefDialogRoot");
        if (existingRoot != null)
        {
            _civilReliefGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldSpinBox");
            _civilReliefFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodSpinBox");
            _civilReliefSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "CivilReliefDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 230.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _civilReliefDialog.AddChild(root);

        root.AddChild(new Label { Name = "GoldLabel" });
        _civilReliefGoldSpinBox = CreateMoveSpinBox("GoldSpinBox");
        _civilReliefGoldSpinBox.Step = 100;
        _civilReliefGoldSpinBox.ValueChanged += _ => UpdateCivilReliefSummary();
        root.AddChild(_civilReliefGoldSpinBox);

        root.AddChild(new Label { Name = "FoodLabel" });
        _civilReliefFoodSpinBox = CreateMoveSpinBox("FoodSpinBox");
        _civilReliefFoodSpinBox.Step = 1000;
        _civilReliefFoodSpinBox.ValueChanged += _ => UpdateCivilReliefSummary();
        root.AddChild(_civilReliefFoodSpinBox);

        _civilReliefSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_civilReliefSummaryLabel);
    }

    private void ShowCivilReliefDialog()
    {
        if (_selectedCity == null || _civilReliefDialog == null || _localization == null)
        {
            return;
        }

        EnsureCivilReliefDialogWidgets();
        UpdateCivilReliefDialogText();
        PopulateCivilReliefDialog();
        _civilReliefDialog.PopupCentered(new Vector2I(440, 260));
    }

    private void PopulateCivilReliefDialog()
    {
        if (_selectedCity == null)
        {
            return;
        }

        ConfigureMoveSpinBox(_civilReliefGoldSpinBox, _selectedCity.Gold, 0);
        ConfigureMoveSpinBox(_civilReliefFoodSpinBox, _selectedCity.Food, 0);
        if (_civilReliefGoldSpinBox != null)
        {
            _civilReliefGoldSpinBox.Step = 100;
        }
        if (_civilReliefFoodSpinBox != null)
        {
            _civilReliefFoodSpinBox.Step = 1000;
        }

        UpdateCivilReliefSummary();
    }

    private void UpdateCivilReliefDialogText()
    {
        if (_civilReliefDialog == null || _localization == null)
        {
            return;
        }

        _civilReliefDialog.Title = _localization.T("civil.command.relief");
        _civilReliefDialog.OkButtonText = _localization.T("ui.confirm_civil_relief");
        SetCivilReliefDialogLabelText("GoldLabel", _localization.T("ui.civil_relief_gold"));
        SetCivilReliefDialogLabelText("FoodLabel", _localization.T("ui.civil_relief_food"));
    }

    private void SetCivilReliefDialogLabelText(string nodeName, string text)
    {
        var label = _civilReliefDialog?.GetNodeOrNull<Label>($"CivilReliefDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void UpdateCivilReliefSummary()
    {
        if (_civilReliefSummaryLabel == null || _civilReliefGoldSpinBox == null || _civilReliefFoodSpinBox == null || _localization == null)
        {
            return;
        }

        var gold = (int)_civilReliefGoldSpinBox.Value;
        var food = (int)_civilReliefFoodSpinBox.Value;
        var gain = gold / 100 * 10 + food / 1000 * 10;
        _civilReliefSummaryLabel.Text = _localization.Format("fmt.civil_relief_preview", gain);
    }

    private void OnCivilReliefDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var result = _commandResolver.ExecuteCivilRelief(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            (int)(_civilReliefGoldSpinBox?.Value ?? 0),
            (int)(_civilReliefFoodSpinBox?.Value ?? 0));
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
    }

    private void EnsureInternalAffairsDialogWidgets()
    {
        if (_internalAffairsDialog == null)
        {
            return;
        }

        var existingRoot = _internalAffairsDialog.GetNodeOrNull<VBoxContainer>("InternalAffairsDialogRoot");
        if (existingRoot != null)
        {
            _internalAffairsJobOption = existingRoot.GetNodeOrNull<OptionButton>("JobOption");
            _internalAffairsDurationSpinBox = existingRoot.GetNodeOrNull<SpinBox>("DurationSpinBox");
            _internalAffairsOfficerList = existingRoot.GetNodeOrNull<ItemList>("OfficerList");
            _internalAffairsScheduleList = existingRoot.GetNodeOrNull<ItemList>("ScheduleList");
            _internalAffairsTerminateButton = existingRoot.GetNodeOrNull<Button>("TerminateButton");
            _internalAffairsWarningLabel = existingRoot.GetNodeOrNull<Label>("WarningLabel");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "InternalAffairsDialogRoot",
            CustomMinimumSize = new Vector2(460.0f, 520.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _internalAffairsDialog.AddChild(root);

        root.AddChild(new Label { Name = "JobLabel" });
        _internalAffairsJobOption = new OptionButton
        {
            Name = "JobOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_internalAffairsJobOption);

        root.AddChild(new Label { Name = "DurationLabel" });
        _internalAffairsDurationSpinBox = new SpinBox
        {
            Name = "DurationSpinBox",
            MinValue = 1,
            MaxValue = 24,
            Step = 1,
            Value = 3,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_internalAffairsDurationSpinBox);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _internalAffairsOfficerList = new ItemList
        {
            Name = "OfficerList",
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0.0f, 130.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_internalAffairsOfficerList);

        root.AddChild(new Label { Name = "ScheduleListLabel" });
        _internalAffairsScheduleList = new ItemList
        {
            Name = "ScheduleList",
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0.0f, 130.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_internalAffairsScheduleList);

        _internalAffairsTerminateButton = new Button
        {
            Name = "TerminateButton",
            FocusMode = Control.FocusModeEnum.None
        };
        _internalAffairsTerminateButton.Pressed += OnInternalAffairsTerminatePressed;
        root.AddChild(_internalAffairsTerminateButton);

        _internalAffairsWarningLabel = new Label
        {
            Name = "WarningLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _internalAffairsWarningLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.2f, 0.15f, 1.0f));
        root.AddChild(_internalAffairsWarningLabel);
    }

    private void ShowInternalAffairsDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _internalAffairsDialog == null || _localization == null)
        {
            return;
        }

        EnsureInternalAffairsDialogWidgets();
        UpdateInternalAffairsDialogText();
        PopulateInternalAffairsDialog();
        SetInternalAffairsWarning(string.Empty);
        _internalAffairsDialog.PopupCentered(new Vector2I(500, 560));
    }

    private void PopulateInternalAffairsDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        if (_internalAffairsJobOption != null)
        {
            _internalAffairsJobOption.Clear();
            AddInternalAffairsJobOption(InternalAffairsJobType.Farm);
            AddInternalAffairsJobOption(InternalAffairsJobType.Commercial);
            AddInternalAffairsJobOption(InternalAffairsJobType.Defend);
            AddInternalAffairsJobOption(InternalAffairsJobType.WaterControl);
            AddInternalAffairsJobOption(InternalAffairsJobType.Construction);
        }

        if (_internalAffairsOfficerList != null)
        {
            _internalAffairsOfficerList.Clear();
            var availableOfficerIds = GetAvailableOfficerIdsForOrder();
            foreach (var officerId in _selectedCity.OfficerIds)
            {
                if (!availableOfficerIds.Contains(officerId))
                {
                    continue;
                }

                var officer = _turnManager.World.GetOfficer(officerId);
                if (officer == null)
                {
                    continue;
                }

                var itemIndex = _internalAffairsOfficerList.AddItem(BuildOfficerListRowText(officer));
                _internalAffairsOfficerList.SetItemMetadata(itemIndex, officer.Id);
            }
        }

        RefreshInternalAffairsScheduleList();
    }

    private void AddInternalAffairsJobOption(InternalAffairsJobType jobType)
    {
        if (_internalAffairsJobOption == null)
        {
            return;
        }

        _internalAffairsJobOption.AddItem(GetInternalAffairsJobName(jobType));
        _internalAffairsJobOption.SetItemMetadata(_internalAffairsJobOption.ItemCount - 1, (int)jobType);
    }

    private void RefreshInternalAffairsScheduleList()
    {
        if (_selectedCity == null || _turnManager?.World == null || _internalAffairsScheduleList == null || _localization == null)
        {
            return;
        }

        _internalAffairsScheduleList.Clear();
        foreach (var schedule in _turnManager.World.InternalAffairsSchedules)
        {
            if (schedule.State != InternalAffairsScheduleState.Active || schedule.CityId != _selectedCity.Id)
            {
                continue;
            }

            var officer = _turnManager.World.GetOfficer(schedule.OfficerId);
            var officerName = officer != null ? _localization.GetOfficerName(officer) : "-";
            var itemIndex = _internalAffairsScheduleList.AddItem(
                _localization.Format("fmt.internal_affairs_schedule_row", GetInternalAffairsJobName(schedule.JobType), officerName, schedule.RemainingMonths));
            _internalAffairsScheduleList.SetItemMetadata(itemIndex, schedule.Id);
        }
    }

    private void UpdateInternalAffairsDialogText()
    {
        if (_internalAffairsDialog == null || _localization == null)
        {
            return;
        }

        _internalAffairsDialog.Title = _localization.T("ui.internal_affairs");
        _internalAffairsDialog.OkButtonText = _localization.T("ui.confirm_internal_affairs");
        SetInternalAffairsDialogLabelText("JobLabel", _localization.T("ui.internal_affairs_job"));
        SetInternalAffairsDialogLabelText("DurationLabel", _localization.T("ui.internal_affairs_duration"));
        SetInternalAffairsDialogLabelText("OfficerListLabel", _localization.T("ui.internal_affairs_officer"));
        SetInternalAffairsDialogLabelText("ScheduleListLabel", _localization.T("ui.internal_affairs_active_schedules"));
        if (_internalAffairsTerminateButton != null)
        {
            _internalAffairsTerminateButton.Text = _localization.T("ui.terminate_internal_affairs");
        }
    }

    private void SetInternalAffairsDialogLabelText(string nodeName, string text)
    {
        var label = _internalAffairsDialog?.GetNodeOrNull<Label>($"InternalAffairsDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void OnInternalAffairsDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager?.World == null || _commandResolver == null || _localization == null)
        {
            return;
        }

        var selectedOfficerIds = GetSelectedItemMetadataIds(_internalAffairsOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            SetInternalAffairsWarning(_localization.T("ui.select_officer_warning"));
            ReopenInternalAffairsDialog();
            return;
        }

        var jobType = GetSelectedInternalAffairsJobType();
        var months = Math.Max(1, (int)(_internalAffairsDurationSpinBox?.Value ?? 1));
        var result = _commandResolver.ScheduleInternalAffairs(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            selectedOfficerIds[0],
            jobType,
            months);
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        RefreshInternalAffairsScheduleList();
        _mapController?.RefreshVisuals();
    }

    private void OnInternalAffairsTerminatePressed()
    {
        if (_turnManager?.World == null || _commandResolver == null || _localization == null)
        {
            return;
        }

        var selectedIds = GetSelectedItemMetadataIds(_internalAffairsScheduleList);
        if (selectedIds.Count == 0)
        {
            SetInternalAffairsWarning(_localization.T("ui.select_internal_affairs_schedule_warning"));
            return;
        }

        var result = _commandResolver.TerminateInternalAffairsSchedule(_turnManager.GetPlayerFactionId(), selectedIds[0]);
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        PopulateInternalAffairsDialog();
    }

    private InternalAffairsJobType GetSelectedInternalAffairsJobType()
    {
        if (_internalAffairsJobOption == null)
        {
            return InternalAffairsJobType.Farm;
        }

        var metadata = _internalAffairsJobOption.GetItemMetadata(_internalAffairsJobOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (InternalAffairsJobType)metadata.AsInt32()
            : InternalAffairsJobType.Farm;
    }

    private string GetInternalAffairsJobName(InternalAffairsJobType jobType)
    {
        if (_localization == null)
        {
            return jobType.ToString();
        }

        return jobType switch
        {
            InternalAffairsJobType.Farm => _localization.T("internal_affairs.job.farm"),
            InternalAffairsJobType.Commercial => _localization.T("internal_affairs.job.commercial"),
            InternalAffairsJobType.Defend => _localization.T("internal_affairs.job.defend"),
            InternalAffairsJobType.WaterControl => _localization.T("internal_affairs.job.water_control"),
            InternalAffairsJobType.Construction => _localization.T("internal_affairs.job.construction"),
            _ => jobType.ToString()
        };
    }

    private void SetInternalAffairsWarning(string text)
    {
        if (_internalAffairsWarningLabel != null)
        {
            _internalAffairsWarningLabel.Text = text;
        }
    }

    private void ReopenInternalAffairsDialog()
    {
        CallDeferred(nameof(ReopenInternalAffairsDialogDeferred));
    }

    private void ReopenInternalAffairsDialogDeferred()
    {
        _internalAffairsDialog?.PopupCentered(new Vector2I(500, 560));
    }

    private static List<int> GetSelectedItemMetadataIds(ItemList? itemList)
    {
        var result = new List<int>();
        if (itemList == null)
        {
            return result;
        }

        for (var index = 0; index < itemList.ItemCount; index += 1)
        {
            if (!itemList.IsSelected(index))
            {
                continue;
            }

            var metadata = itemList.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int)
            {
                result.Add(metadata.AsInt32());
            }
        }

        return result;
    }

    private HashSet<int> GetAvailableOfficerIdsForOrder()
    {
        if (_turnManager?.World == null || _selectedCity == null)
        {
            return new HashSet<int>();
        }

        var result = new HashSet<int>();
        foreach (var officerId in _selectedCity.OfficerIds)
        {
            var officer = _turnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (officer.LastAssignedYear == _turnManager.World.Year &&
                officer.LastAssignedMonth == _turnManager.World.Month)
            {
                continue;
            }

            if (HasActiveInternalAffairsSchedule(officer.Id))
            {
                continue;
            }

            result.Add(officerId);
        }

        return result;
    }

    private bool HasActiveInternalAffairsSchedule(int officerId)
    {
        var world = _turnManager?.World;
        if (world == null)
        {
            return false;
        }

        return world.InternalAffairsSchedules.Any(schedule =>
            schedule.State == InternalAffairsScheduleState.Active &&
            schedule.OfficerId == officerId);
    }

    private void ShowOfficerCommandDialog(CommandType commandType)
    {
        if (_selectedCity == null || _turnManager?.World == null || _officerListDialog == null || _officerListView == null || _localization == null)
        {
            return;
        }

        var availableOfficerIds = GetAvailableOfficerIdsForOrder();
        if (availableOfficerIds.Count == 0)
        {
            AddLog(_localization.Format("ui.no_available_officer_for_command", GetCommandName(commandType)));
            return;
        }

        _pendingOfficerCommand = commandType;
        _officerListMode = OfficerListMode.CommandSelection;
        SetOfficerListDialogTitle(_localization.Format("fmt.select_officer_for_command", GetCommandName(commandType)));
        _officerListDialog.OkButtonText = _localization.T("ui.confirm_officer_selection");
        if (_officerListConfirmButton != null)
        {
            _officerListConfirmButton.Text = _localization.T("ui.confirm_officer_selection");
        }
        UpdateOfficerListToolbar();
        if (_officerListTable != null)
        {
            _officerListTable.Visible = false;
        }
        _officerListView.Visible = true;
        _officerListView.Clear();

        foreach (var officerId in _selectedCity.OfficerIds)
        {
            if (!availableOfficerIds.Contains(officerId))
            {
                continue;
            }

            var officer = _turnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            var itemIndex = _officerListView.AddItem(BuildOfficerListRowText(officer));
            _officerListView.SetItemMetadata(itemIndex, officer.Id);
        }

        if (_officerListView.ItemCount == 0)
        {
            AddLog(_localization.Format("ui.no_available_officer_for_command", GetCommandName(commandType)));
            return;
        }

        _officerListDialog.PopupCentered(new Vector2I(420, 320));
    }

    private string GetCommandName(CommandType commandType)
    {
        if (_localization == null)
        {
            return commandType.ToString();
        }

        return commandType switch
        {
            CommandType.Develop => _localization.T("ui.develop"),
            CommandType.Recruit => _localization.T("ui.recruit"),
            CommandType.Search => _localization.T("ui.search"),
            _ => commandType.ToString()
        };
    }

    private void OnEndTurnPressed()
    {
        if (_gameEnded || _turnManager?.World == null || _localization == null || _aiController == null)
        {
            return;
        }

        var world = _turnManager.World;
        AddLog(_localization.T("log.player_end_turn"));

        foreach (var faction in world.Factions)
        {
            if (faction.IsPlayer)
            {
                continue;
            }

            var cityIds = new List<int>();
            foreach (var city in world.Cities)
            {
                if (city.OwnerFactionId == faction.Id)
                {
                    cityIds.Add(city.Id);
                }
            }

            foreach (var cityId in cityIds)
            {
                var city = world.GetCity(cityId);
                if (city == null)
                {
                    continue;
                }

                var result = _aiController.RunSingleCityDecision(faction.Id, cityId);
                var cityName = _localization.GetCityName(city);
                var factionName = _localization.GetFactionName(world, faction.Id);
                AddLog(_localization.FormatAiCityAction(factionName, cityName, GetLocalizedResultMessage(result)));
                CheckFactionEliminations();
            }
        }

        if (_commandResolver != null)
        {
            foreach (var result in _turnManager.ResolvePendingCommands(_commandResolver))
            {
                AddLog(GetLocalizedResultMessage(result));
                CheckFactionEliminations();
            }
        }

        _turnManager.AdvanceMonth();
        var economyMonth = world.Month;
        var economyResult = _turnManager.ApplyMonthlyEconomy();
        AddLog(_localization.T("log.monthly_economy"));
        if (economyMonth == 4)
        {
            AddLog(_localization.T("log.player_city_gold_income_header"));
            foreach (var entry in economyResult.PlayerCityGoldIncome)
            {
                var city = world.GetCity(entry.CityId);
                if (city == null)
                {
                    continue;
                }

                AddLog(_localization.Format("log.player_city_income_line", _localization.GetCityName(city), entry.Amount));
            }
        }

        if (economyMonth == 8)
        {
            AddLog(_localization.T("log.player_city_food_income_header"));
            foreach (var entry in economyResult.PlayerCityFoodIncome)
            {
                var city = world.GetCity(entry.CityId);
                if (city == null)
                {
                    continue;
                }

                AddLog(_localization.Format("log.player_city_income_line", _localization.GetCityName(city), entry.Amount));
            }
        }

        AddLog(_localization.FormatMonthAdvanced(world.Year, world.Month));
        RefreshMonth();

        if (_selectedCity != null)
        {
            var refreshed = world.GetCity(_selectedCity.Id);
            if (refreshed != null)
            {
                _selectedCity = refreshed;
            }
        }

        RefreshSelectedCity();
        EvaluateWinLose();
        _mapController?.RefreshVisuals();
    }

    private void EvaluateWinLose()
    {
        if (_turnManager?.World == null || _gameEnded)
        {
            return;
        }

        var world = _turnManager.World;
        var playerFactionId = _turnManager.GetPlayerFactionId();
        var playerCityCount = 0;

        foreach (var city in world.Cities)
        {
            if (city.OwnerFactionId == playerFactionId)
            {
                playerCityCount += 1;
            }
        }

        if (playerCityCount == 0)
        {
            _gameEnded = true;
            AddLog(_localization?.T("log.defeat_all_cities") ?? "Defeat: You have lost all cities.");
            SetGameplayButtonsEnabled(false);
            return;
        }

        if (playerCityCount == world.Cities.Count)
        {
            _gameEnded = true;
            AddLog(_localization?.T("log.victory_all_cities") ?? "Victory: You control all cities.");
            SetGameplayButtonsEnabled(false);
        }
    }

    private void ResetAliveFactionSnapshot()
    {
        _aliveFactionIds.Clear();
        if (_turnManager?.World == null)
        {
            return;
        }

        foreach (var city in _turnManager.World.Cities)
        {
            if (city.OwnerFactionId > 0)
            {
                _aliveFactionIds.Add(city.OwnerFactionId);
            }
        }
    }

    private void CheckFactionEliminations()
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return;
        }

        var world = _turnManager.World;
        var aliveNow = new HashSet<int>();
        foreach (var city in world.Cities)
        {
            if (city.OwnerFactionId > 0)
            {
                aliveNow.Add(city.OwnerFactionId);
            }
        }

        foreach (var factionId in _aliveFactionIds)
        {
            if (aliveNow.Contains(factionId))
            {
                continue;
            }

            var factionName = _localization.GetFactionName(world, factionId);
            AddLog(_localization.FormatFactionDestroyed(factionName));
        }

        _aliveFactionIds.Clear();
        foreach (var factionId in aliveNow)
        {
            _aliveFactionIds.Add(factionId);
        }
    }

    private void SetGameplayButtonsEnabled(bool enabled)
    {
        if (_endTurnButton != null)
        {
            _endTurnButton.Disabled = !enabled;
        }

        if (_developButton != null)
        {
            _developButton.Disabled = !enabled;
        }

        if (_recruitButton != null)
        {
            _recruitButton.Disabled = !enabled;
        }

        if (_moveButton != null)
        {
            _moveButton.Disabled = !enabled;
        }

        if (_searchButton != null)
        {
            _searchButton.Disabled = !enabled;
        }

        if (_merchantButton != null)
        {
            _merchantButton.Disabled = !enabled;
        }

        if (_personnelButton != null)
        {
            _personnelButton.Disabled = !enabled;
        }

        if (_civilButton != null)
        {
            _civilButton.Disabled = !enabled;
        }

        if (_attackButton != null)
        {
            _attackButton.Disabled = !enabled;
        }

        if (_viewButton != null)
        {
            _viewButton.Disabled = !enabled;
        }
    }

    private void UpdateGameplayButtonStates()
    {
        var baseEnabled = !_gameEnded;
        var world = _turnManager?.World;
        var playerFactionId = _turnManager?.GetPlayerFactionId() ?? -1;
        var hasSelectedCity = _selectedCity != null;
        var isPlayerCity = hasSelectedCity && _selectedCity!.OwnerFactionId == playerFactionId;
        var hasUsedRecruit = false;
        var hasUsedSearch = false;

        if (world != null && _selectedCity != null)
        {
            hasUsedRecruit =
                _selectedCity.LastRecruitYear == world.Year &&
                _selectedCity.LastRecruitMonth == world.Month;
            hasUsedSearch =
                _selectedCity.LastSearchYear == world.Year &&
                _selectedCity.LastSearchMonth == world.Month;
        }

        if (_endTurnButton != null)
        {
            _endTurnButton.Disabled = !baseEnabled;
        }

        if (_developButton != null)
        {
            _developButton.Disabled = !baseEnabled || !isPlayerCity;
        }

        if (_recruitButton != null)
        {
            _recruitButton.Disabled = !baseEnabled || !isPlayerCity;
        }

        if (_searchButton != null)
        {
            _searchButton.Disabled = !baseEnabled || !isPlayerCity || hasUsedSearch;
        }

        if (_merchantButton != null)
        {
            _merchantButton.Disabled = !baseEnabled || !isPlayerCity;
        }

        if (_personnelButton != null)
        {
            _personnelButton.Disabled = !baseEnabled || !isPlayerCity;
        }

        if (_civilButton != null)
        {
            _civilButton.Disabled = !baseEnabled || !isPlayerCity;
        }

        if (_moveButton != null)
        {
            _moveButton.Disabled = !baseEnabled || !isPlayerCity;
        }

        if (_attackButton != null)
        {
            _attackButton.Disabled = !baseEnabled || !isPlayerCity;
        }

        if (_viewButton != null)
        {
            _viewButton.Disabled = !baseEnabled || !hasSelectedCity;
        }
    }

    private string GetLocalizedResultMessage(CommandResult result)
    {
        if (_localization == null)
        {
            return result.Message;
        }

        if (_localization.IsTraditionalChinese && !string.IsNullOrWhiteSpace(result.MessageZhHant))
        {
            return result.MessageZhHant;
        }

        if (!_localization.IsTraditionalChinese && !string.IsNullOrWhiteSpace(result.MessageEn))
        {
            return result.MessageEn;
        }

        return result.Message;
    }

    private void OnLanguageChanged()
    {
        RefreshAllText();
    }

    private void ApplyOfficerListDialogTheme()
    {
        if (_officerListDialog == null)
        {
            return;
        }

        var dialogPanel = new StyleBoxFlat
        {
            BgColor = new Color(0.96f, 0.94f, 0.88f, 1.0f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.56f, 0.45f, 0.29f, 1.0f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusBottomLeft = 10
        };
        _officerListDialog.AddThemeStyleboxOverride("panel", dialogPanel);
        var embeddedBorder = new StyleBoxFlat
        {
            BgColor = new Color(0.92f, 0.88f, 0.8f, 1.0f),
            BorderWidthLeft = 2,
            BorderWidthTop = 26,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.56f, 0.45f, 0.29f, 1.0f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusBottomLeft = 10
        };
        var embeddedBorderUnfocused = (StyleBoxFlat)embeddedBorder.Duplicate();
        embeddedBorderUnfocused.BgColor = new Color(0.88f, 0.84f, 0.77f, 1.0f);
        embeddedBorderUnfocused.BorderColor = new Color(0.54f, 0.45f, 0.33f, 1.0f);
        _officerListDialog.AddThemeStyleboxOverride("embedded_border", embeddedBorder);
        _officerListDialog.AddThemeStyleboxOverride("embedded_unfocused_border", embeddedBorderUnfocused);
        _officerListDialog.AddThemeColorOverride("title_color", new Color(0.27f, 0.2f, 0.12f, 1.0f));
        var titleButtonNormal = new StyleBoxFlat
        {
            BgColor = new Color(0.92f, 0.88f, 0.8f, 1.0f),
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0
        };
        var titleButtonHover = (StyleBoxFlat)titleButtonNormal.Duplicate();
        titleButtonHover.BgColor = new Color(0.95f, 0.91f, 0.83f, 1.0f);
        var titleButtonPressed = (StyleBoxFlat)titleButtonNormal.Duplicate();
        titleButtonPressed.BgColor = new Color(0.84f, 0.77f, 0.64f, 1.0f);
        _officerListDialog.AddThemeStyleboxOverride("close", titleButtonNormal);
        _officerListDialog.AddThemeStyleboxOverride("close_pressed", titleButtonPressed);
        _officerListDialog.AddThemeStyleboxOverride("title_button_normal", titleButtonNormal);
        _officerListDialog.AddThemeStyleboxOverride("title_button_hover", titleButtonHover);
        _officerListDialog.AddThemeStyleboxOverride("title_button_pressed", titleButtonPressed);
        _officerListDialog.AddThemeColorOverride("close_color", new Color(0.34f, 0.24f, 0.14f, 1.0f));
        _officerListDialog.AddThemeColorOverride("close_hover_color", new Color(0.22f, 0.16f, 0.09f, 1.0f));

        if (_officerListHeaderPanel != null)
        {
            var headerPanel = new StyleBoxFlat
            {
                BgColor = new Color(0.9f, 0.84f, 0.71f, 0.98f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.62f, 0.49f, 0.29f, 1.0f),
                CornerRadiusTopLeft = 7,
                CornerRadiusTopRight = 7,
                CornerRadiusBottomRight = 7,
                CornerRadiusBottomLeft = 7,
                ContentMarginLeft = 10,
                ContentMarginTop = 6,
                ContentMarginRight = 10,
                ContentMarginBottom = 6
            };
            _officerListHeaderPanel.AddThemeStyleboxOverride("panel", headerPanel);
        }

        if (_officerListTitlebarFill != null)
        {
            var titlebarFillPanel = new StyleBoxFlat
            {
                BgColor = new Color(0.92f, 0.88f, 0.8f, 1.0f),
                BorderWidthLeft = 0,
                BorderWidthTop = 0,
                BorderWidthRight = 0,
                BorderWidthBottom = 0,
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10
            };
            _officerListTitlebarFill.AddThemeStyleboxOverride("panel", titlebarFillPanel);
        }

        if (_officerListHeaderLabel != null)
        {
            _officerListHeaderLabel.AddThemeColorOverride("font_color", new Color(0.25f, 0.18f, 0.1f, 1.0f));
        }

        if (_officerListCloseButton != null)
        {
            var closeNormal = new StyleBoxFlat
            {
                BgColor = new Color(0.86f, 0.78f, 0.62f, 1.0f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.54f, 0.42f, 0.25f, 1.0f),
                CornerRadiusTopLeft = 5,
                CornerRadiusTopRight = 5,
                CornerRadiusBottomRight = 5,
                CornerRadiusBottomLeft = 5
            };
            var closeHover = (StyleBoxFlat)closeNormal.Duplicate();
            closeHover.BgColor = new Color(0.94f, 0.84f, 0.66f, 1.0f);
            var closePressed = (StyleBoxFlat)closeNormal.Duplicate();
            closePressed.BgColor = new Color(0.73f, 0.61f, 0.42f, 1.0f);

            _officerListCloseButton.AddThemeStyleboxOverride("normal", closeNormal);
            _officerListCloseButton.AddThemeStyleboxOverride("hover", closeHover);
            _officerListCloseButton.AddThemeStyleboxOverride("pressed", closePressed);
            _officerListCloseButton.AddThemeColorOverride("font_color", new Color(0.22f, 0.15f, 0.08f, 1.0f));
        }

        var okNormal = new StyleBoxFlat
        {
            BgColor = new Color(0.86f, 0.78f, 0.6f, 1.0f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.48f, 0.36f, 0.2f, 1.0f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusBottomLeft = 6
        };
        var okHover = (StyleBoxFlat)okNormal.Duplicate();
        okHover.BgColor = new Color(0.91f, 0.84f, 0.67f, 1.0f);
        var okPressed = (StyleBoxFlat)okNormal.Duplicate();
        okPressed.BgColor = new Color(0.76f, 0.65f, 0.46f, 1.0f);

        _officerListDialog.GetOkButton().Visible = false;
        if (_officerListConfirmButton != null)
        {
            _officerListConfirmButton.AddThemeStyleboxOverride("normal", okNormal);
            _officerListConfirmButton.AddThemeStyleboxOverride("hover", okHover);
            _officerListConfirmButton.AddThemeStyleboxOverride("pressed", okPressed);
            _officerListConfirmButton.AddThemeColorOverride("font_color", new Color(0.14f, 0.1f, 0.06f, 1.0f));
        }

        foreach (var button in new[] { _viewCityOfficersDialogButton, _viewFactionOfficersDialogButton, _viewCitiesDialogButton })
        {
            if (button == null)
            {
                continue;
            }

            var normal = new StyleBoxFlat
            {
                BgColor = new Color(0.88f, 0.81f, 0.65f, 0.97f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.54f, 0.42f, 0.24f, 1.0f),
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomRight = 6,
                CornerRadiusBottomLeft = 6
            };
            var hover = (StyleBoxFlat)normal.Duplicate();
            hover.BgColor = new Color(0.93f, 0.87f, 0.72f, 1.0f);
            var disabled = (StyleBoxFlat)normal.Duplicate();
            disabled.BgColor = new Color(0.76f, 0.72f, 0.65f, 0.92f);
            disabled.BorderColor = new Color(0.58f, 0.54f, 0.47f, 1.0f);

            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("disabled", disabled);
            button.AddThemeColorOverride("font_color", new Color(0.16f, 0.12f, 0.08f, 1.0f));
            button.AddThemeColorOverride("font_disabled_color", new Color(0.32f, 0.29f, 0.24f, 1.0f));
        }

        if (_officerListTable != null)
        {
            var tablePanel = new StyleBoxFlat
            {
                BgColor = new Color(0.96f, 0.93f, 0.86f, 0.98f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.47f, 0.39f, 0.27f, 0.95f)
            };
            var focusPanel = (StyleBoxFlat)tablePanel.Duplicate();
            focusPanel.BorderColor = new Color(0.65f, 0.49f, 0.25f, 1.0f);
            var selectedPanel = new StyleBoxFlat
            {
                BgColor = new Color(0.82f, 0.72f, 0.52f, 1.0f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.55f, 0.4f, 0.2f, 1.0f)
            };
            var selectedFocusPanel = (StyleBoxFlat)selectedPanel.Duplicate();
            selectedFocusPanel.BgColor = new Color(0.86f, 0.76f, 0.56f, 1.0f);

            var titleNormal = new StyleBoxFlat
            {
                BgColor = new Color(0.67f, 0.53f, 0.31f, 1.0f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.33f, 0.24f, 0.13f, 1.0f)
            };
            var titleHover = (StyleBoxFlat)titleNormal.Duplicate();
            titleHover.BgColor = new Color(0.75f, 0.6f, 0.37f, 1.0f);
            var titlePressed = (StyleBoxFlat)titleNormal.Duplicate();
            titlePressed.BgColor = new Color(0.56f, 0.43f, 0.24f, 1.0f);

            _officerListTable.AddThemeStyleboxOverride("panel", tablePanel);
            _officerListTable.AddThemeStyleboxOverride("focus", focusPanel);
            _officerListTable.AddThemeStyleboxOverride("selected", selectedPanel);
            _officerListTable.AddThemeStyleboxOverride("selected_focus", selectedFocusPanel);
            _officerListTable.AddThemeStyleboxOverride("title_button_normal", titleNormal);
            _officerListTable.AddThemeStyleboxOverride("title_button_hover", titleHover);
            _officerListTable.AddThemeStyleboxOverride("title_button_pressed", titlePressed);
            _officerListTable.AddThemeColorOverride("font_color", new Color(0.17f, 0.13f, 0.09f, 1.0f));
            _officerListTable.AddThemeColorOverride("font_hovered_color", new Color(0.12f, 0.09f, 0.06f, 1.0f));
            _officerListTable.AddThemeColorOverride("font_selected_color", new Color(0.12f, 0.09f, 0.06f, 1.0f));
            _officerListTable.AddThemeColorOverride("font_hovered_selected_color", new Color(0.1f, 0.07f, 0.04f, 1.0f));
            _officerListTable.AddThemeColorOverride("font_outline_color", new Color(0.96f, 0.93f, 0.86f, 0.0f));
            _officerListTable.AddThemeColorOverride("custom_button_font_highlight", new Color(0.12f, 0.09f, 0.06f, 1.0f));
            _officerListTable.AddThemeColorOverride("custom_button_font_highlight_pressed", new Color(0.1f, 0.07f, 0.04f, 1.0f));
            _officerListTable.AddThemeColorOverride("title_button_color", new Color(0.98f, 0.95f, 0.9f, 1.0f));
            _officerListTable.AddThemeColorOverride("title_button_hover_color", Colors.White);
            _officerListTable.AddThemeColorOverride("title_button_pressed_color", Colors.White);
            _officerListTable.AddThemeColorOverride("guide_color", new Color(0.58f, 0.5f, 0.38f, 0.65f));
            _officerListTable.AddThemeColorOverride("drop_position_color", new Color(0.75f, 0.55f, 0.22f, 1.0f));
        }
    }

    private void RefreshAllText()
    {
        if (_localization == null)
        {
            return;
        }

        RefreshMonth();
        RefreshPlayerFaction();
        RefreshStoryName();

        if (_commandsTitle != null)
        {
            _commandsTitle.Text = _localization.T("ui.commands");
        }

        if (_cityOfficerListTitle != null)
        {
            _cityOfficerListTitle.Text = _localization.T("ui.officer_list");
        }

        if (_endTurnButton != null)
        {
            _endTurnButton.Text = _localization.T("ui.end_turn");
        }

        if (_developButton != null)
        {
            _developButton.Text = _localization.T("ui.internal_affairs");
        }

        if (_recruitButton != null)
        {
            _recruitButton.Text = _localization.T("ui.military");
        }

        if (_moveButton != null)
        {
            _moveButton.Text = _localization.T("ui.move");
            _moveButton.Visible = false;
        }

        if (_searchButton != null)
        {
            _searchButton.Text = _localization.T("ui.search");
        }

        if (_merchantButton != null)
        {
            _merchantButton.Text = _localization.T("ui.merchant");
        }

        if (_personnelButton != null)
        {
            _personnelButton.Text = _localization.T("ui.personnel");
        }

        if (_civilButton != null)
        {
            _civilButton.Text = _localization.T("ui.civil");
        }

        if (_attackButton != null)
        {
            _attackButton.Text = _localization.T("ui.attack");
            _attackButton.Visible = false;
        }

        if (_viewButton != null)
        {
            _viewButton.Text = _localization.T("ui.view");
        }

        if (_officerListDialog != null)
        {
            _officerListDialog.OkButtonText = _localization.T("ui.confirm_officer_selection");
        }

        if (_officerListConfirmButton != null)
        {
            _officerListConfirmButton.Text = _localization.T("ui.confirm_officer_selection");
        }

        UpdateOfficerListToolbar();
        UpdateOfficerListDialogTitle();

        UpdateMerchantDialogText();
        UpdateMilitaryDialogText();
        UpdatePersonnelDialogText();
        UpdatePersonnelBonusDialogText();
        UpdateAssignRoleDialogText();
        UpdateHireOfficerDialogText();
        UpdateCivilDialogText();
        UpdateCivilReliefDialogText();
        UpdateInternalAffairsDialogText();
        UpdateMoveDialogText();
        UpdateAttackDialogText();

        if (_officerDetailDialog != null)
        {
            _officerDetailDialog.Title = _localization.T("ui.officer_detail");
        }

        if (_officerPortraitPlaceholderLabel != null && (_officerDetailDialog == null || !_officerDetailDialog.Visible))
        {
            _officerPortraitPlaceholderLabel.Visible = true;
            _officerPortraitPlaceholderLabel.Text = _localization.T("ui.portrait_pending_asset");
        }

        if (_languageButton != null)
        {
            _languageButton.Text = _localization.IsTraditionalChinese
                ? _localization.T("ui.lang_btn_en")
                : _localization.T("ui.lang_btn_zh");
        }

        RefreshSelectedCity();
    }

    private void RefreshPlayerFaction()
    {
        if (_playerFactionLabel == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        var factionName = _localization.GetFactionName(_turnManager.World, playerFactionId);
        _playerFactionLabel.Text = _localization.FormatPlayerFaction(factionName);
    }

    private void RefreshStoryName()
    {
        if (_storyLabel == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        var world = _turnManager.World;
        _storyLabel.Text = _localization.IsTraditionalChinese
            ? (!string.IsNullOrWhiteSpace(world.StoryNameZhHant) ? world.StoryNameZhHant : world.StoryNameEn)
            : (!string.IsNullOrWhiteSpace(world.StoryNameEn) ? world.StoryNameEn : world.StoryNameZhHant);
    }

    private void RefreshSelectedCity()
    {
        if (_localization == null || _turnManager?.World == null)
        {
            return;
        }

        if (_selectedCity == null)
        {
            if (_cityNameLabel != null)
            {
                _cityNameLabel.Text = _localization.FormatCityHeader("-");
            }

            if (_cityStatsLabel != null)
            {
                _cityStatsLabel.Text =
                    _localization.FormatOwnerLine("-") +
                    "\n" +
                    _localization.FormatEmptyCityStats();
            }

            if (_cityOfficerListText != null)
            {
                _cityOfficerListText.Text = _localization.T("ui.none");
            }

            UpdateGameplayButtonStates();
            return;
        }

        if (_cityNameLabel != null)
        {
            _cityNameLabel.Text = _localization.FormatCityHeader(_localization.GetCityName(_selectedCity));
        }

        if (_cityStatsLabel != null)
        {
            var ownerName = _localization.GetFactionName(_turnManager.World, _selectedCity.OwnerFactionId);
            _cityStatsLabel.Text =
                _localization.FormatOwnerLine(ownerName) +
                "\n" +
                _localization.FormatCityStats(_selectedCity);
        }

        if (_cityOfficerListText != null)
        {
            _cityOfficerListText.Text = BuildOfficerListText(_selectedCity);
        }

        UpdateGameplayButtonStates();
    }

    private void EnsureOfficerListWidgets(VBoxContainer leftPanel)
    {
        _cityOfficerListTitle = GetNodeOrNull<Label>("Root/LeftPanel/OfficerListTitle");
        if (_cityOfficerListTitle == null)
        {
            _cityOfficerListTitle = new Label
            {
                Name = "OfficerListTitle"
            };
            leftPanel.AddChild(_cityOfficerListTitle);
        }

        _cityOfficerListText = GetNodeOrNull<RichTextLabel>("Root/LeftPanel/OfficerListText");
        if (_cityOfficerListText == null)
        {
            _cityOfficerListText = new RichTextLabel
            {
                Name = "OfficerListText",
                FitContent = true,
                ScrollActive = true,
                CustomMinimumSize = new Vector2(0.0f, 180.0f)
            };
            leftPanel.AddChild(_cityOfficerListText);
        }
    }

    private string BuildOfficerListText(CityData city)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return string.Empty;
        }

        if (city.OfficerIds.Count == 0)
        {
            return _localization.T("ui.none");
        }

        var officerLines = new List<string>();
        foreach (var officerId in city.OfficerIds)
        {
            var officer = _turnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            var roleName = _localization.GetOfficerRole(officer);
            officerLines.Add(
                $"{_localization.GetOfficerName(officer)} | {roleName} | {BuildOfficerStatusText(officer)} | {_localization.T("ui.strength")} {officer.Strength} | {_localization.T("ui.intelligence")} {officer.Intelligence} | {_localization.T("ui.charm")} {officer.Charm}");
        }

        return officerLines.Count == 0 ? _localization.T("ui.none") : string.Join("\n", officerLines);
    }

    private string BuildOfficerDetailText(OfficerData officer)
    {
        var officerName = _localization?.GetOfficerName(officer) ?? officer.Name;
        var roleName = _localization?.GetOfficerRole(officer) ?? officer.Role;
        var currentYear = _turnManager?.World?.Year ?? 0;
        var officerAge = CalculateOfficerAge(officer, currentYear);
        return
            $"{officerName}\n" +
            $"{_localization?.T("ui.role") ?? "Role"}: {roleName}\n" +
            $"{BuildOfficerStatusText(officer)}\n" +
            $"{_localization?.T("ui.age") ?? "Age"}: {officerAge}\n" +
            $"{_localization?.T("ui.strength") ?? "STR"}: {officer.Strength}\n" +
            $"{_localization?.T("ui.intelligence") ?? "INT"}: {officer.Intelligence}\n" +
            $"{_localization?.T("ui.charm") ?? "CHA"}: {officer.Charm}\n" +
            $"{_localization?.T("ui.leadership") ?? "LEA"}: {officer.Leadership}\n" +
            $"{_localization?.T("ui.politics") ?? "POL"}: {officer.Politics}\n" +
            $"{_localization?.T("ui.combat") ?? "COM"}: {officer.Combat}\n" +
            $"{_localization?.T("ui.loyalty_short") ?? "LOY"}: {officer.Loyalty}\n" +
            $"{_localization?.T("ui.ambition") ?? "AMB"}: {officer.Ambition}";
    }

    private static int CalculateOfficerAge(OfficerData officer, int currentYear)
    {
        if (officer.BirthYear <= 0 || currentYear <= 0)
        {
            return 0;
        }

        return Math.Max(0, currentYear - officer.BirthYear);
    }

    private static bool IsOfficerOldEnoughToJoin(WorldState world, OfficerData officer)
    {
        return officer.BirthYear <= 0 || world.Year - officer.BirthYear >= 18;
    }

    private void EnsureOfficerDetailWidgets()
    {
        if (_officerDetailDialog == null)
        {
            return;
        }

        var existingRoot = _officerDetailDialog.GetNodeOrNull<HBoxContainer>("OfficerDetailRoot");
        if (existingRoot != null)
        {
            _officerPortraitRect = existingRoot.GetNodeOrNull<TextureRect>("PortraitPanel/PortraitRect");
            _officerPortraitPlaceholderLabel = existingRoot.GetNodeOrNull<Label>("PortraitPanel/PortraitPlaceholder");
            _officerDetailText = existingRoot.GetNodeOrNull<RichTextLabel>("DetailText");
            return;
        }

        var root = new HBoxContainer
        {
            Name = "OfficerDetailRoot",
            CustomMinimumSize = new Vector2(460.0f, 240.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 16);
        _officerDetailDialog.AddChild(root);

        var portraitPanel = new PanelContainer
        {
            Name = "PortraitPanel",
            CustomMinimumSize = new Vector2(160.0f, 220.0f)
        };
        root.AddChild(portraitPanel);

        var portraitCenter = new CenterContainer();
        portraitPanel.AddChild(portraitCenter);

        var portraitStack = new VBoxContainer();
        portraitStack.Alignment = BoxContainer.AlignmentMode.Center;
        portraitStack.AddThemeConstantOverride("separation", 8);
        portraitCenter.AddChild(portraitStack);

        _officerPortraitRect = new TextureRect
        {
            Name = "PortraitRect",
            CustomMinimumSize = new Vector2(128.0f, 160.0f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Modulate = new Color(0.7f, 0.7f, 0.75f, 1.0f)
        };
        portraitStack.AddChild(_officerPortraitRect);

        _officerPortraitPlaceholderLabel = new Label
        {
            Name = "PortraitPlaceholder",
            Text = _localization?.T("ui.portrait_pending_asset") ?? "Portrait\nPending Asset",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        portraitStack.AddChild(_officerPortraitPlaceholderLabel);

        _officerDetailText = new RichTextLabel
        {
            Name = "DetailText",
            FitContent = true,
            ScrollActive = true,
            CustomMinimumSize = new Vector2(260.0f, 220.0f),
            BbcodeEnabled = false
        };
        root.AddChild(_officerDetailText);
    }

    private string BuildOfficerListRowText(OfficerData officer, bool includeCityName = false)
    {
        var officerName = _localization?.GetOfficerName(officer) ?? officer.Name;
        var roleName = _localization?.GetOfficerRole(officer) ?? officer.Role;
        var cityText = string.Empty;
        if (includeCityName && _turnManager?.World != null && _localization != null)
        {
            var city = _turnManager.World.GetCity(officer.CityId);
            if (city != null)
            {
                cityText = $" | {_localization.T("ui.city")} {_localization.GetCityName(city)}";
            }
        }

        return $"{officerName} | {roleName} | {BuildOfficerStatusText(officer)}{cityText} | {_localization?.T("ui.strength") ?? "STR"} {officer.Strength} | {_localization?.T("ui.intelligence") ?? "INT"} {officer.Intelligence}";
    }

    private string BuildCityListRowText(CityData city)
    {
        var cityName = _localization?.GetCityName(city) ?? city.NameEn;
        var ownerName = _turnManager?.World != null && _localization != null
            ? _localization.GetFactionName(_turnManager.World, city.OwnerFactionId)
            : city.OwnerFactionId.ToString();
        return $"{cityName} | {_localization?.T("ui.owner") ?? "Owner"} {ownerName} | {_localization?.T("ui.gold") ?? "Gold"} {city.Gold} | {_localization?.T("ui.food") ?? "Food"} {city.Food} | {_localization?.T("ui.troops") ?? "Troops"} {city.Troops} | {_localization?.T("ui.officers") ?? "Officers"} {city.OfficerIds.Count}";
    }

    private string BuildOfficerStatusText(OfficerData officer)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return "Status: Idle";
        }

        return $"{_localization.T("ui.status")}: {_localization.GetOfficerStatus(_turnManager.World, officer)}";
    }

    private void LoadPortraitData()
    {
        _portraitRegions.Clear();
        _portraitSheetTexture = ResourceLoader.Load<Texture2D>(PortraitSheetPath);
        if (_portraitSheetTexture == null || !FileAccess.FileExists(PortraitMappingPath))
        {
            return;
        }

        using var file = FileAccess.Open(PortraitMappingPath, FileAccess.ModeFlags.Read);
        var rawText = file.GetAsText();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return;
        }

        var matches = Regex.Matches(
            rawText,
            "\\{\\s*\"id\"\\s*:\\s*(\\d+)\\s*,.*?\"filename\"\\s*:\\s*\"([^\"]+)\"\\s*,.*?\"row\"\\s*:\\s*(\\d+)\\s*,.*?\"col\"\\s*:\\s*(\\d+)",
            RegexOptions.Singleline);

        if (matches.Count == 0)
        {
            return;
        }

        var entries = new List<(int Id, int Row, int Col)>();
        var maxRow = 0;
        var maxCol = 0;
        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            var id = int.Parse(match.Groups[1].Value);
            var row = int.Parse(match.Groups[3].Value);
            var col = int.Parse(match.Groups[4].Value);
            entries.Add((id, row, col));
            if (row > maxRow)
            {
                maxRow = row;
            }

            if (col > maxCol)
            {
                maxCol = col;
            }
        }

        if (entries.Count == 0 || maxRow <= 0 || maxCol <= 0)
        {
            return;
        }

        var tileWidth = _portraitSheetTexture.GetWidth() / (float)maxCol;
        var tileHeight = _portraitSheetTexture.GetHeight() / (float)maxRow;
        foreach (var entry in entries)
        {
            var region = new Rect2(
                (entry.Col - 1) * tileWidth,
                (entry.Row - 1) * tileHeight,
                tileWidth,
                tileHeight);
            _portraitRegions[entry.Id] = region;
        }
    }

    private Texture2D? BuildOfficerPortraitTexture(int officerId)
    {
        if (_portraitSheetTexture == null || !_portraitRegions.TryGetValue(officerId, out var region))
        {
            return null;
        }

        var atlasTexture = new AtlasTexture
        {
            Atlas = _portraitSheetTexture,
            Region = region
        };
        return atlasTexture;
    }

}

