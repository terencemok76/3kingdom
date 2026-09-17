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
    private const string FactionOutcomeDialogScenePath = "res://scenes/ui/main/FactionOutcomeDialog.tscn";
    private readonly Queue<(string Title, string Message, bool ReturnToMainMenu)> _factionOutcomeQueue = new();
    private Control? _factionOutcomeDialog;
    private bool _factionOutcomeReturnsToMainMenu;

    private CommandResult ExecutePlayerCommand(
        CommandType type,
        int? targetCityId = null,
        int troopsToSend = 0,
        int goldToSend = 0,
        int foodToSend = 0,
        int horsesToSend = 0,
        SiegeEngineAllocationData? siegeEngineAllocation = null,
        List<AttackOfficerDeploymentData>? attackOfficerDeployments = null,
        List<int>? officerIds = null,
        List<int>? captiveOfficerIds = null,
        bool sellFood = false,
        MerchantTradeMode merchantTradeMode = MerchantTradeMode.BuyFood,
        TroopType recruitTroopType = TroopType.Infantry,
        DefenderBattlePlan? defenderBattlePlanOverride = null)
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
            HorsesToSend = type == CommandType.Move ? horsesToSend : 0,
            SiegeEngineAllocation = type == CommandType.Move ? (siegeEngineAllocation ?? new SiegeEngineAllocationData()) : new SiegeEngineAllocationData(),
            SellFood = type == CommandType.Merchant && sellFood,
            MerchantTradeMode = merchantTradeMode,
            RecruitTroopType = recruitTroopType,
            AttackOfficerDeployments = type == CommandType.Attack ? (attackOfficerDeployments ?? new List<AttackOfficerDeploymentData>()) : new List<AttackOfficerDeploymentData>(),
            DefenderBattlePlanOverride = type == CommandType.Attack ? defenderBattlePlanOverride : null,
            OfficerIds = type is CommandType.Merchant or CommandType.Pass ? new List<int>() : (officerIds ?? new List<int>()),
            CaptiveOfficerIds = type == CommandType.Move ? (captiveOfficerIds ?? new List<int>()) : new List<int>()
        };

        var result = _commandResolver.Execute(request);
        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);

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

    private void ConfigureMoveSpinBox(SpinBox? spinBox, int maxValue, int defaultValue)
    {
        if (spinBox == null)
        {
            return;
        }

        spinBox.MinValue = 0;
        spinBox.MaxValue = maxValue;
        spinBox.Value = maxValue <= 0 ? 0 : Mathf.Clamp(defaultValue, 0, maxValue);
    }


    private void OnEndTurnPressed()
    {
        if (_gameEnded || _turnManager?.World == null || _localization == null || _aiController == null)
        {
            return;
        }

        var world = _turnManager.World;
        AddLog(_localization.T("log.player_end_turn"), isPlayerRelated: true);
        if (world.IsBattleResolutionPhase)
        {
            _isResolvingEndTurn = true;
            _pendingNonAttackResolutionQueue.Clear();
            _pendingAttackResolutionQueue.Clear();
            _pendingAttackResolutionQueue.AddRange(_turnManager.GetPendingCommandsOfType(CommandType.Attack));
            ContinuePendingAttackResolution();
            return;
        }

        foreach (var faction in world.Factions)
        {
            if (faction.IsPlayer)
            {
                continue;
            }

            foreach (var appointmentResult in _aiController.RunFactionAppointmentDecisions(faction.Id))
            {
                if (!appointmentResult.Success)
                {
                    continue;
                }

                var factionName = _localization.GetFactionName(world, faction.Id);
                AddLog(_localization.FormatAiCityAction(factionName, "-", GetLocalizedResultMessage(appointmentResult)));
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

        ResolveEndTurnPendingCommands();
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
            AddLog(_localization?.T("log.defeat_all_cities") ?? "Defeat: You have lost all cities.", isPlayerRelated: true);
            SetGameplayButtonsEnabled(false);
            QueueFactionOutcome(
                _localization?.T("ui.game_over_title") ?? "Game Over",
                _localization?.T("ui.game_over_no_cities") ?? "You have lost all cities.",
                returnToMainMenu: true);
            return;
        }

        var playerFaction = world.GetFaction(playerFactionId);
        var playerOfficerIds = playerFaction?.OfficerIds
            .Append(playerFaction.RulerOfficerId)
            .Where(id => id > 0)
            .ToHashSet() ?? new HashSet<int>();
        var hasLivingPlayerOfficer = world.Officers.Any(officer =>
            (playerOfficerIds.Contains(officer.Id) || officer.Belongs == playerFactionId.ToString()) &&
            (officer.DeathYear <= 0 || officer.DeathYear >= world.Year));
        if (!hasLivingPlayerOfficer)
        {
            _gameEnded = true;
            AddLog(_localization?.T("log.defeat_all_officers") ?? "Defeat: You have no surviving officers.", isPlayerRelated: true);
            SetGameplayButtonsEnabled(false);
            QueueFactionOutcome(
                _localization?.T("ui.game_over_title") ?? "Game Over",
                _localization?.T("ui.game_over_no_officers") ?? "You have no surviving officers.",
                returnToMainMenu: true);
            return;
        }

        if (playerCityCount == world.Cities.Count)
        {
            _gameEnded = true;
            AddLog(_localization?.T("log.victory_all_cities") ?? "Victory: You control all cities.", isPlayerRelated: true);
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
            QueueFactionOutcome(
                _localization.T("ui.faction_destroyed_title"),
                _localization.Format("ui.faction_destroyed_message", factionName));
        }

        _aliveFactionIds.Clear();
        foreach (var factionId in aliveNow)
        {
            _aliveFactionIds.Add(factionId);
        }
    }

    private void QueueFactionOutcome(string title, string message, bool returnToMainMenu = false)
    {
        _factionOutcomeQueue.Enqueue((title, message, returnToMainMenu));
        Callable.From(ShowNextFactionOutcomeIfPossible).CallDeferred();
    }

    private void ShowNextFactionOutcomeIfPossible()
    {
        if (_factionOutcomeDialog != null || _factionOutcomeQueue.Count == 0 || _battleReportDialog != null)
        {
            return;
        }

        var outcome = _factionOutcomeQueue.Dequeue();
        _factionOutcomeReturnsToMainMenu = outcome.ReturnToMainMenu;
        _factionOutcomeDialog = GD.Load<PackedScene>(FactionOutcomeDialogScenePath).Instantiate<Control>();
        var overlayRoot = GetNodeOrNull<Control>("Root") as Node ?? this;
        overlayRoot.AddChild(_factionOutcomeDialog);
        _factionOutcomeDialog.GetNode<Label>("Center/OutcomePanel/Root/Header/TitleLabel").Text = outcome.Title;
        _factionOutcomeDialog.GetNode<Label>("Center/OutcomePanel/Root/Body/Content/MessageLabel").Text = outcome.Message;
        var acknowledgeButton = _factionOutcomeDialog.GetNode<Button>("Center/OutcomePanel/Root/Body/Content/ButtonRow/AcknowledgeButton");
        acknowledgeButton.Text = _localization?.T("ui.acknowledge") ?? "Acknowledge";
        acknowledgeButton.Pressed += CloseFactionOutcome;
        _factionOutcomeDialog.Visible = true;
    }

    private void CloseFactionOutcome()
    {
        if (_factionOutcomeDialog == null)
        {
            return;
        }

        _factionOutcomeDialog.QueueFree();
        _factionOutcomeDialog = null;
        if (_factionOutcomeReturnsToMainMenu)
        {
            _factionOutcomeReturnsToMainMenu = false;
            (GetParent() as GameBootstrap)?.ReturnToMainMenu();
            return;
        }

        Callable.From(ShowNextFactionOutcomeIfPossible).CallDeferred();
    }

    private void SetGameplayButtonsEnabled(bool enabled)
    {
        if (MainHudEndTurnButton != null)
        {
            MainHudEndTurnButton.Disabled = !enabled;
        }

        if (MainHudDevelopButton != null)
        {
            MainHudDevelopButton.Disabled = !enabled;
        }

        if (MainHudRecruitButton != null)
        {
            MainHudRecruitButton.Disabled = !enabled;
        }

        if (MainHudMoveButton != null)
        {
            MainHudMoveButton.Disabled = !enabled;
        }

        if (MainHudSearchButton != null)
        {
            MainHudSearchButton.Disabled = !enabled;
        }

        if (_advisorButton != null)
        {
            _advisorButton.Disabled = !enabled;
        }

        if (MainHudMerchantButton != null)
        {
            MainHudMerchantButton.Disabled = !enabled;
        }

        if (MainHudDiplomacyButton != null)
        {
            MainHudDiplomacyButton.Disabled = !enabled;
        }

        if (MainHudSpyButton != null)
        {
            MainHudSpyButton.Disabled = !enabled;
        }

        if (MainHudPersonnelButton != null)
        {
            MainHudPersonnelButton.Disabled = !enabled;
        }

        if (MainHudCivilButton != null)
        {
            MainHudCivilButton.Disabled = !enabled;
        }

        if (MainHudAttackButton != null)
        {
            MainHudAttackButton.Disabled = !enabled;
        }

        if (MainHudViewButton != null)
        {
            MainHudViewButton.Disabled = !enabled;
        }

        if (MainHudTestCaptureButton != null)
        {
            MainHudTestCaptureButton.Disabled = !enabled;
        }
    }

    private void UpdateGameplayButtonStates()
    {
        var baseEnabled = !_gameEnded;
        var world = _turnManager?.World;
        var playerFactionId = _turnManager?.GetPlayerFactionId() ?? -1;
        var hasSelectedCity = _selectedCity != null;
        var isPlayerCity = hasSelectedCity && _selectedCity!.OwnerFactionId == playerFactionId;
        var canControlSelectedCity = isPlayerCity && world?.IsBattleResolutionPhase != true;
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

        if (MainHudEndTurnButton != null)
        {
            MainHudEndTurnButton.Disabled = !baseEnabled;
        }

        if (MainHudDevelopButton != null)
        {
            MainHudDevelopButton.Disabled = !baseEnabled || !canControlSelectedCity;
        }

        if (MainHudRecruitButton != null)
        {
            MainHudRecruitButton.Disabled = !baseEnabled || !canControlSelectedCity;
        }

        if (MainHudSearchButton != null)
        {
            MainHudSearchButton.Disabled = !baseEnabled || !canControlSelectedCity || hasUsedSearch;
        }

        if (MainHudMerchantButton != null)
        {
            MainHudMerchantButton.Disabled = !baseEnabled || !canControlSelectedCity;
        }

        if (MainHudDiplomacyButton != null)
        {
            MainHudDiplomacyButton.Disabled = !baseEnabled || !canControlSelectedCity;
        }

        if (MainHudSpyButton != null)
        {
            MainHudSpyButton.Disabled = !baseEnabled || !canControlSelectedCity;
        }

        if (MainHudPersonnelButton != null)
        {
            MainHudPersonnelButton.Disabled = !baseEnabled || !canControlSelectedCity;
        }

        if (_advisorButton != null)
        {
            _advisorButton.Disabled = !baseEnabled || !canControlSelectedCity;
        }

        if (MainHudCivilButton != null)
        {
            MainHudCivilButton.Disabled = !baseEnabled || !canControlSelectedCity;
        }

        if (MainHudMoveButton != null)
        {
            MainHudMoveButton.Disabled = !baseEnabled || !canControlSelectedCity;
        }

        if (MainHudAttackButton != null)
        {
            MainHudAttackButton.Disabled = !baseEnabled || !canControlSelectedCity;
        }

        if (MainHudViewButton != null)
        {
            MainHudViewButton.Disabled = !baseEnabled || !hasSelectedCity;
        }

        if (MainHudTestCaptureButton != null)
        {
            MainHudTestCaptureButton.Disabled = !baseEnabled || !isPlayerCity;
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
