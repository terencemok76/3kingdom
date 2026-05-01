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

        AutoSelectPlayerCityForNewRound();

        RefreshSelectedCity();
        EvaluateWinLose();
        _mapController?.RefreshVisuals();
    }

    private void AutoSelectPlayerCityForNewRound()
    {
        if (_turnManager?.World == null)
        {
            return;
        }

        var world = _turnManager.World;
        var playerFactionId = _turnManager.GetPlayerFactionId();
        var refreshedSelectedCity = _selectedCity != null ? world.GetCity(_selectedCity.Id) : null;
        if (refreshedSelectedCity != null && refreshedSelectedCity.OwnerFactionId == playerFactionId)
        {
            _selectedCity = refreshedSelectedCity;
            return;
        }

        var fallbackCity = world.Cities.FirstOrDefault(city =>
            city.OwnerFactionId == playerFactionId &&
            city.OfficerIds.Count > 0)
            ?? world.Cities.FirstOrDefault(city => city.OwnerFactionId == playerFactionId);

        if (fallbackCity == null)
        {
            _selectedCity = refreshedSelectedCity;
            return;
        }

        _selectedCity = fallbackCity;
        _mapController?.SelectCityById(fallbackCity.Id);
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


}
