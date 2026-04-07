using System.Collections.Generic;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController : CanvasLayer
{
    private Label? _monthLabel;
    private Label? _playerFactionLabel;
    private Label? _cityNameLabel;
    private Label? _cityStatsLabel;
    private Label? _commandsTitle;

    private Button? _languageButton;
    private Button? _endTurnButton;
    private Button? _developButton;
    private Button? _recruitButton;
    private Button? _moveButton;
    private Button? _searchButton;
    private Button? _attackButton;

    private RichTextLabel? _logText;

    private TurnManager? _turnManager;
    private LocalizationService? _localization;
    private AiController? _aiController;
    private CityData? _selectedCity;

    private bool _isLanguageButtonConnected;
    private bool _isEndTurnButtonConnected;

    public override void _Ready()
    {
        _monthLabel = GetNodeOrNull<Label>("Root/TopBar/MonthLabel");
        _playerFactionLabel = GetNodeOrNull<Label>("Root/TopBar/PlayerFactionLabel");
        _languageButton = GetNodeOrNull<Button>("Root/TopBar/LanguageButton");
        _endTurnButton = GetNodeOrNull<Button>("Root/TopBar/EndTurnButton");

        _cityNameLabel = GetNodeOrNull<Label>("Root/LeftPanel/CityNameLabel");
        _cityStatsLabel = GetNodeOrNull<Label>("Root/LeftPanel/CityStatsLabel");
        _commandsTitle = GetNodeOrNull<Label>("Root/LeftPanel/CommandsTitle");

        _developButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/DevelopButton");
        _recruitButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/RecruitButton");
        _moveButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/MoveButton");
        _searchButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/SearchButton");
        _attackButton = GetNodeOrNull<Button>("Root/LeftPanel/CommandButtons/AttackButton");

        _logText = GetNodeOrNull<RichTextLabel>("Root/LogText");
    }

    public override void _ExitTree()
    {
        if (_localization != null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }

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
    }

    public void Initialize(
        TurnManager turnManager,
        CommandResolver commandResolver,
        AiController aiController,
        LocalizationService localization)
    {
        _turnManager = turnManager;
        _localization = localization;
        _aiController = aiController;

        _localization.LanguageChanged -= OnLanguageChanged;
        _localization.LanguageChanged += OnLanguageChanged;

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
    }

    private void OnLanguageButtonPressed()
    {
        _localization?.ToggleLanguage();
    }

    private void OnEndTurnPressed()
    {
        if (_turnManager?.World == null || _localization == null || _aiController == null)
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
                AddLog(_localization.FormatAiCityAction(factionName, cityName, result.Message));
            }
        }

        _turnManager.AdvanceMonth();
        AddLog(_localization.FormatMonthAdvanced(world.Year, world.Month));
        RefreshMonth();

        if (_selectedCity != null)
        {
            var refreshed = world.GetCity(_selectedCity.Id);
            if (refreshed != null)
            {
                _selectedCity = refreshed;
                RefreshSelectedCity();
            }
        }
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
                    _localization.FormatCityStats(0, 0, 0, 0);
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
                _localization.FormatCityStats(
                    _selectedCity.Gold,
                    _selectedCity.Food,
                    _selectedCity.Troops,
                    _selectedCity.OfficerIds.Count);
        }
    }
}
