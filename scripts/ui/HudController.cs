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

    private enum OfficerSortMode
    {
        Strength,
        Intelligence,
        Status
    }

    private const string PortraitSheetPath = "res://assets/portrait/100.png";
    private const string PortraitMappingPath = "res://data/person/portraits_names.json";

    private Label? _monthLabel;
    private Label? _playerFactionLabel;
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
    private Button? _attackButton;
    private Button? _viewButton;
    private PopupMenu? _targetCityMenu;
    private AcceptDialog? _merchantDialog;
    private OptionButton? _merchantModeOption;
    private SpinBox? _merchantFoodSpinBox;
    private Label? _merchantSummaryLabel;
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
    private HBoxContainer? _officerListToolbar;
    private Button? _viewCityOfficersDialogButton;
    private Button? _viewFactionOfficersDialogButton;
    private OptionButton? _officerSortOption;
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
    private bool _isAttackButtonConnected;
    private bool _isViewButtonConnected;
    private bool _merchantDialogSignalsConnected;
    private bool _gameEnded;
    private readonly HashSet<int> _aliveFactionIds = new();
    private CommandType _pendingTargetCommand = CommandType.Pass;
    private Texture2D? _portraitSheetTexture;
    private readonly Dictionary<int, Rect2> _portraitRegions = new();
    private OfficerListMode _officerListMode = OfficerListMode.View;
    private OfficerListScope _officerListScope = OfficerListScope.City;
    private OfficerSortMode _officerSortMode = OfficerSortMode.Strength;
    private CommandType _pendingOfficerCommand = CommandType.Pass;

    public override void _Ready()
    {
        _monthLabel = GetNodeOrNull<Label>("Root/TopBar/MonthLabel");
        _playerFactionLabel = GetNodeOrNull<Label>("Root/TopBar/PlayerFactionLabel");
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
        _searchButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/SearchButton");
        _merchantButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/MerchantButton");
        _attackButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/AttackButton");
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
        _officerListDialog.Title = "Select Officer";
        _officerListDialog.Exclusive = false;
        _officerListDialog.Unfocusable = false;
        _officerListDialog.Confirmed += OnOfficerListDialogConfirmed;
        AddChild(_officerListDialog);

        var officerListRoot = new VBoxContainer
        {
            Name = "OfficerListDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 280.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        officerListRoot.AddThemeConstantOverride("separation", 8);
        _officerListDialog.AddChild(officerListRoot);

        _officerListToolbar = new HBoxContainer
        {
            Name = "OfficerListToolbar",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _officerListToolbar.AddThemeConstantOverride("separation", 8);
        officerListRoot.AddChild(_officerListToolbar);

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

        _officerSortOption = new OptionButton
        {
            Name = "OfficerSortOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _officerSortOption.ItemSelected += OnOfficerSortOptionSelected;
        _officerListToolbar.AddChild(_officerSortOption);

        _officerListView = new ItemList
        {
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(320.0f, 220.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _officerListView.ItemSelected += OnOfficerListItemSelected;
        officerListRoot.AddChild(_officerListView);

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
        ShowOfficerCommandDialog(CommandType.Develop);
    }

    private void OnRecruitPressed()
    {
        ShowOfficerCommandDialog(CommandType.Recruit);
    }

    private void OnMovePressed()
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

    private void OnAttackPressed()
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

        if (_selectedCity.OfficerIds.Count == 0)
        {
            AddLog(_localization?.T("ui.no_officer_in_city") ?? "No officers available in this city.");
            return;
        }

        _officerListMode = OfficerListMode.View;
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

    private void OnOfficerListDialogConfirmed()
    {
        if (_officerListMode != OfficerListMode.CommandSelection || _officerListView == null)
        {
            return;
        }

        var selectedItems = _officerListView.GetSelectedItems();
        if (selectedItems.Length == 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? "Select one officer first.");
            ReopenOfficerListDialog();
            return;
        }

        var metadata = _officerListView.GetItemMetadata(selectedItems[0]);
        if (metadata.VariantType != Variant.Type.Int)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? "Select one officer first.");
            ReopenOfficerListDialog();
            return;
        }

        var result = ExecutePlayerCommand(_pendingOfficerCommand, officerIds: new List<int> { metadata.AsInt32() });
        if (result.Success)
        {
            _officerListDialog?.Hide();
            return;
        }

        ReopenOfficerListDialog();
    }

    private void PopulateOfficerListDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _officerListView == null)
        {
            return;
        }

        _officerListView.Clear();

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

        foreach (var officer in GetSortedOfficers(officers))
        {
            var itemIndex = _officerListView.AddItem(BuildOfficerListRowText(officer, includeCityName));
            _officerListView.SetItemMetadata(itemIndex, officer.Id);
        }

        if (_officerListView.ItemCount == 0)
        {
            AddLog(emptyMessage);
        }

        UpdateOfficerListDialogTitle();
    }

    private void UpdateOfficerListToolbar()
    {
        if (_officerListToolbar == null || _viewCityOfficersDialogButton == null || _viewFactionOfficersDialogButton == null || _officerSortOption == null || _selectedCity == null || _turnManager?.World == null || _localization == null)
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
        _viewCityOfficersDialogButton.Disabled = _officerListScope == OfficerListScope.City;
        _viewFactionOfficersDialogButton.Disabled = !hasFaction || _officerListScope == OfficerListScope.Faction;
    }

    private void UpdateOfficerListDialogTitle()
    {
        if (_officerListDialog == null || _localization == null)
        {
            return;
        }

        if (_officerListMode != OfficerListMode.View)
        {
            return;
        }

        _officerListDialog.Title = _officerListScope == OfficerListScope.Faction
            ? _localization.T("ui.view_dialog_title_faction")
            : _localization.T("ui.view_dialog_title_city");
    }

    private IEnumerable<OfficerData> GetSortedOfficers(List<OfficerData> officers)
    {
        return _officerSortMode switch
        {
            OfficerSortMode.Intelligence => officers
                .OrderByDescending(officer => officer.Intelligence)
                .ThenByDescending(officer => officer.Strength)
                .ThenBy(officer => _localization?.GetOfficerName(officer) ?? officer.Name),
            OfficerSortMode.Status => officers
                .OrderBy(officer => GetOfficerStatusSortKey(officer))
                .ThenByDescending(officer => officer.Strength)
                .ThenBy(officer => _localization?.GetOfficerName(officer) ?? officer.Name),
            _ => officers
                .OrderByDescending(officer => officer.Strength)
                .ThenByDescending(officer => officer.Intelligence)
                .ThenBy(officer => _localization?.GetOfficerName(officer) ?? officer.Name)
        };
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

            result.Add(officerId);
        }

        return result;
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
        _officerListDialog.Title = _localization.Format("fmt.select_officer_for_command", GetCommandName(commandType));
        _officerListDialog.OkButtonText = _localization.T("ui.confirm_officer_selection");
        UpdateOfficerListToolbar();
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
        var hasUsedDevelop = false;
        var hasUsedRecruit = false;
        var hasUsedSearch = false;

        if (world != null && _selectedCity != null)
        {
            hasUsedDevelop =
                _selectedCity.LastDevelopYear == world.Year &&
                _selectedCity.LastDevelopMonth == world.Month;
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
            _developButton.Disabled = !baseEnabled || !isPlayerCity || hasUsedDevelop;
        }

        if (_recruitButton != null)
        {
            _recruitButton.Disabled = !baseEnabled || !isPlayerCity || hasUsedRecruit;
        }

        if (_searchButton != null)
        {
            _searchButton.Disabled = !baseEnabled || !isPlayerCity || hasUsedSearch;
        }

        if (_merchantButton != null)
        {
            _merchantButton.Disabled = !baseEnabled || !isPlayerCity;
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

    private void RefreshAllText()
    {
        if (_localization == null)
        {
            return;
        }

        RefreshMonth();
        RefreshPlayerFaction();

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
            _developButton.Text = _localization.T("ui.develop");
        }

        if (_recruitButton != null)
        {
            _recruitButton.Text = _localization.T("ui.recruit");
        }

        if (_moveButton != null)
        {
            _moveButton.Text = _localization.T("ui.move");
        }

        if (_searchButton != null)
        {
            _searchButton.Text = _localization.T("ui.search");
        }

        if (_merchantButton != null)
        {
            _merchantButton.Text = _localization.T("ui.merchant");
        }

        if (_attackButton != null)
        {
            _attackButton.Text = _localization.T("ui.attack");
        }

        if (_viewButton != null)
        {
            _viewButton.Text = _localization.T("ui.view");
        }

        if (_officerListDialog != null)
        {
            _officerListDialog.Title = _localization.T("ui.select_officer");
            _officerListDialog.OkButtonText = _localization.T("ui.confirm_officer_selection");
        }

        UpdateOfficerListToolbar();
        UpdateOfficerListDialogTitle();

        UpdateMerchantDialogText();
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
        return
            $"{officerName}\n" +
            $"{_localization?.T("ui.role") ?? "Role"}: {roleName}\n" +
            $"{BuildOfficerStatusText(officer)}\n" +
            $"{_localization?.T("ui.age") ?? "Age"}: {officer.Age}\n" +
            $"{_localization?.T("ui.strength") ?? "STR"}: {officer.Strength}\n" +
            $"{_localization?.T("ui.intelligence") ?? "INT"}: {officer.Intelligence}\n" +
            $"{_localization?.T("ui.charm") ?? "CHA"}: {officer.Charm}\n" +
            $"{_localization?.T("ui.leadership") ?? "LEA"}: {officer.Leadership}\n" +
            $"{_localization?.T("ui.politics") ?? "POL"}: {officer.Politics}\n" +
            $"{_localization?.T("ui.combat") ?? "COM"}: {officer.Combat}\n" +
            $"{_localization?.T("ui.loyalty_short") ?? "LOY"}: {officer.Loyalty}\n" +
            $"{_localization?.T("ui.ambition") ?? "AMB"}: {officer.Ambition}";
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

