using System.Collections.Generic;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using ThreeKingdom.Map;

namespace ThreeKingdom.UI;

public partial class HudController : CanvasLayer
{
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
    private Button? _attackButton;
    private PopupMenu? _targetCityMenu;

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
    private bool _isAttackButtonConnected;
    private bool _gameEnded;
    private readonly HashSet<int> _aliveFactionIds = new();
    private CommandType _pendingTargetCommand = CommandType.Pass;

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
        _attackButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/AttackButton");

        _logText = GetNodeOrNull<RichTextLabel>("Root/LogText");
        if (_logText != null)
        {
            _logText.ScrollFollowing = true;
        }

        _targetCityMenu = new PopupMenu();
        AddChild(_targetCityMenu);
        _targetCityMenu.IdPressed += OnTargetCityMenuIdPressed;
    }

    public override void _ExitTree()
    {
        if (_localization != null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }

        DisconnectButtons();
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

        if (_attackButton != null && !_isAttackButtonConnected)
        {
            _attackButton.Pressed += OnAttackPressed;
            _isAttackButtonConnected = true;
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

        if (_attackButton != null && _isAttackButtonConnected)
        {
            _attackButton.Pressed -= OnAttackPressed;
            _isAttackButtonConnected = false;
        }
    }

    private void OnLanguageButtonPressed()
    {
        _localization?.ToggleLanguage();
    }

    private void OnDevelopPressed()
    {
        ExecutePlayerCommand(CommandType.Develop);
    }

    private void OnRecruitPressed()
    {
        ExecutePlayerCommand(CommandType.Recruit);
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

        ExecuteTargetSelectionOrCommand(
            CommandType.Move,
            candidateIds,
            "No connected friendly city to move troops, resources, or officers.");
    }

    private void OnSearchPressed()
    {
        ExecutePlayerCommand(CommandType.Search);
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

        ExecuteTargetSelectionOrCommand(
            CommandType.Attack,
            candidateIds,
            "No connected enemy city to attack.");
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

    private void ExecutePlayerCommand(CommandType type, int? targetCityId = null, int troopsToSend = 0)
    {
        if (_gameEnded || _turnManager?.World == null || _commandResolver == null || _selectedCity == null)
        {
            return;
        }

        var request = new CommandRequest
        {
            Type = type,
            ActorFactionId = _turnManager.GetPlayerFactionId(),
            SourceCityId = _selectedCity.Id,
            TargetCityId = targetCityId,
            TroopsToSend = troopsToSend,
            GoldToSend = type == CommandType.Move ? _selectedCity.Gold / 2 : 0,
            FoodToSend = type == CommandType.Move ? _selectedCity.Food / 2 : 0,
            OfficerIds = type == CommandType.Move ? new List<int>(_selectedCity.OfficerIds) : new List<int>()
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

        _turnManager.ApplyMonthlyEconomy();
        AddLog(_localization.T("log.monthly_economy"));

        _turnManager.AdvanceMonth();
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
            AddLog("Defeat: You have lost all cities.");
            SetGameplayButtonsEnabled(false);
            return;
        }

        if (playerCityCount == world.Cities.Count)
        {
            _gameEnded = true;
            AddLog("Victory: You control all cities.");
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

        if (_attackButton != null)
        {
            _attackButton.Disabled = !enabled;
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

        if (_attackButton != null)
        {
            _attackButton.Text = _localization.T("ui.attack");
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

            officerLines.Add(
                $"{_localization.GetOfficerName(officer)} | {officer.Role} | STR {officer.Strength} | INT {officer.Intelligence} | CHA {officer.Charm}");
        }

        return officerLines.Count == 0 ? _localization.T("ui.none") : string.Join("\n", officerLines);
    }
}

