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

    private static void ShowOverlay(Control? overlay)
    {
        if (overlay == null)
        {
            return;
        }

        overlay.Show();
        overlay.MoveToFront();
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

    internal sealed class OfficerSelectorScopeOption
    {
        public required string Key { get; init; }
        public required string Label { get; init; }
        public required List<int> CandidateOfficerIds { get; init; }
    }

    internal sealed class OfficerSelectorColumnDefinition
    {
        public required string Title { get; init; }
        public int MinWidth { get; init; } = 90;
    }

    internal sealed class OfficerSelectorDisplayConfig
    {
        public required IReadOnlyList<OfficerSelectorColumnDefinition> Columns { get; init; }
        public required Func<OfficerData, IReadOnlyList<string>> BuildRowTexts { get; init; }
        public Vector2 PanelSize { get; init; } = new(620.0f, 320.0f);
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
        Appointment,
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
        BowWorkshop,
        SiegeWorkshop,
        HorsePasture,
        Ram,
        Catapult,
        Ladder,
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

    private GridContainer? _commandButtons;
    private Label? _cityOfficerListTitle;
    private RichTextLabel? _cityOfficerListText;

    private Button? _advisorButton;
    private MainHudUiController? _mainHudUiController;
    private PopupMenu? _targetCityMenu;
    private MerchantUiController? _merchantUiController;
    private MilitaryUiController? _militaryUiController;
    private CivilUiController? _civilUiController;
    private PersonnelUiController? _personnelUiController;
    private AdvisorUiController? _advisorUiController;
    private DiplomacyUiController? _diplomacyUiController;
    private InternalAffairsUiController? _internalAffairsUiController;
    private SpyUiController? _spyUiController;
    private SystemUiController? _systemUiController;
    private ViewUiController? _viewUiController;
    private readonly UiEventHub _uiEventHub = new();
    private Window? _optionDialog;
    private Window? _saveLoadDialog;
    private Window? _saveLoadConfirmDialog;
    private Control? _officerListDialog;
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
    private Control? _officerDetailDialog;
    private TextureRect? _officerPortraitRect;
    private Label? _officerPortraitPlaceholderLabel;
    private RichTextLabel? _officerDetailText;

    private TurnManager? _turnManager;
    private CommandResolver? _commandResolver;
    private LocalizationService? _localization;
    private AiController? _aiController;
    private WorldRepository? _worldRepository;
    private MapController? _mapController;
    private CityData? _selectedCity;

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
    private Action<int>? _genericOfficerSelectorConfirmedAction;
    private readonly List<int> _genericOfficerSelectorCandidateIds = new();
    private OfficerSelectorPrimaryStat _genericOfficerSelectorPrimaryStat = OfficerSelectorPrimaryStat.Politics;
    private Func<string>? _genericOfficerSelectorTitleFactory;
    private Func<IEnumerable<OfficerSelectorScopeOption>?>? _genericOfficerSelectorScopeOptionsFactory;
    private string? _genericOfficerSelectorInitialScopeKey;
    private OfficerSelectorDisplayConfig? _genericOfficerSelectorDisplayConfig;
    private Func<OfficerSelectorDisplayConfig?>? _genericOfficerSelectorDisplayConfigFactory;
    private readonly List<PendingCommandData> _pendingNonAttackResolutionQueue = new();
    private readonly List<PendingCommandData> _pendingAttackResolutionQueue = new();
    private bool _isResolvingEndTurn;
    private bool _bgmEnabled = true;
    private bool _sfxEnabled = true;
    private float _bgmVolume = 1.0f;
    private float _sfxVolume = 1.0f;
    internal UiEventHub UiEventHub => _uiEventHub;
    public override void _Ready()
    {
        var languageButton = MainHudLanguageButton;
        var godModeButton = MainHudGodModeButton;
        if (languageButton != null)
        {
            languageButton.Visible = false;
        }
        if (godModeButton != null)
        {
            godModeButton.Visible = false;
        }

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

        var moveButton = MainHudMoveButton;
        if (moveButton != null)
        {
            moveButton.Visible = false;
        }
        var searchButton = MainHudSearchButton;
        if (searchButton != null)
        {
            searchButton.Visible = false;
        }
        EnsureAdvisorButton();
        var attackButton = MainHudAttackButton;
        if (attackButton != null)
        {
            attackButton.Visible = false;
        }
        _mainHudUiController = new MainHudUiController(this);
        InitializeFloatingPanels();
        _mainHudUiController.Initialize();

        _targetCityMenu = new PopupMenu();
        AddChild(_targetCityMenu);
        _targetCityMenu.IdPressed += OnTargetCityMenuIdPressed;

        _merchantUiController = new MerchantUiController(this);
        _merchantUiController.Initialize();

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

        _systemUiController = new SystemUiController(this);
        _systemUiController.Initialize();

        _internalAffairsUiController = new InternalAffairsUiController(this);
        _internalAffairsUiController.Initialize();

        _viewUiController = new ViewUiController(this);
        _viewUiController.Initialize();

        _selectOfficerDialog = GD.Load<PackedScene>("res://scenes/ui/view/SelectOfficerDialog.tscn").Instantiate<SelectOfficerDialog>();
        if (GetNodeOrNull<Control>("Root") is Control overlayRoot)
        {
            overlayRoot.AddChild(_selectOfficerDialog);
        }
        else
        {
            AddChild(_selectOfficerDialog);
        }
        _selectOfficerDialog.Hide();
        LoadPortraitData();
        AttachClickSfxToButtons(this);
        InitializeEventPresentationUi();
        LoadOptionSettings();
        ApplyAudioSettings();
    }

    public override void _ExitTree()
    {
        if (_localization != null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }

        _mainHudUiController?.Shutdown();
        _systemUiController?.Shutdown();
        _viewUiController?.Shutdown();
        _advisorUiController?.Shutdown();
        _internalAffairsUiController?.Shutdown();
        _personnelUiController?.Shutdown();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationWMCloseRequest)
        {
            return;
        }

        _viewUiController?.HideDialogs();
        _militaryUiController?.HideDialogs();
        _personnelUiController?.HideDialogs();
        _civilUiController?.HideDialogs();
        _internalAffairsUiController?.HideDialogs();
        _merchantUiController?.HideDialogs();
        _spyUiController?.HideDialogs();
        _systemUiController?.HideDialogs();
    }

    public override void _Process(double delta)
    {
        _militaryUiController?.ProcessDialogs();
        UpdateFloatingPanelDragging();
        ProcessFloatingPanelDeferredRefresh();
        ProcessEventPresentation();
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

        ResetAliveFactionSnapshot();
        RefreshAllText();
    }

    public void OnCitySelected(CityData city)
    {
        GameAudioController.Instance?.PlayCityClickSfx();
        _selectedCity = city;
        RefreshSelectedCity();
    }

    public void RefreshMonth()
    {
        _mainHudUiController?.RefreshMonth();
    }

    public void AddLog(string message, bool isPlayerRelated = false)
    {
        _mainHudUiController?.AddLog(message, isPlayerRelated);
    }

    private void OnLanguageButtonPressed()
    {
        _localization?.ToggleLanguage();
        SaveOptionSettings();
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
        _militaryUiController?.OpenMoveFlow();
    }

    private void OpenMoveFlow()
    {
        _militaryUiController?.OpenMoveFlow();
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

        _merchantUiController?.ShowMerchantDialog();
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
        _militaryUiController?.OpenAttackFlow();
    }

    private void OpenAttackFlow()
    {
        _militaryUiController?.OpenAttackFlow();
    }

    private void OnViewPressed()
    {
        _viewUiController?.ShowViewDialog();
    }

    private void CloseOfficerListDialog()
    {
        _officerListDialog?.Hide();
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
            _viewUiController?.RefreshOfficerListContent();
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
            _viewUiController?.RefreshOfficerListContent();
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

}
