using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private void ResolveEndTurnPendingCommands()
    {
        if (_turnManager == null || _commandResolver == null || _localization == null)
        {
            return;
        }

        var world = _turnManager.World;
        if (world == null)
        {
            return;
        }

        _isResolvingEndTurn = true;
        var activeSchedules = world.InternalAffairsSchedules
            .Where(schedule => schedule.State == InternalAffairsScheduleState.Active)
            .ToList();
        var playerFactionId = _turnManager.GetPlayerFactionId();
        var pendingResults = _commandResolver.ResolveInternalAffairsSchedules();
        for (var index = 0; index < pendingResults.Count; index += 1)
        {
            AddLog(GetLocalizedResultMessage(pendingResults[index]), index < activeSchedules.Count && IsPlayerRelatedInternalAffairsSchedule(activeSchedules[index], playerFactionId));
            CheckFactionEliminations();
        }

        _pendingNonAttackResolutionQueue.Clear();
        _pendingNonAttackResolutionQueue.AddRange(_turnManager.GetPendingCommandsExceptAttackInResolutionOrder());
        ContinuePendingNonAttackResolution();
    }

    private void ContinuePendingNonAttackResolution()
    {
        if (!_isResolvingEndTurn || _turnManager?.World == null || _commandResolver == null)
        {
            return;
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        while (_pendingNonAttackResolutionQueue.Count > 0)
        {
            var pendingCommand = _pendingNonAttackResolutionQueue[0];
            _pendingNonAttackResolutionQueue.RemoveAt(0);
            if (ShouldPromptForPlayerDiplomacyProposal(pendingCommand))
            {
                ShowDiplomacyProposalDialog(pendingCommand);
                return;
            }

            var result = _commandResolver.ResolvePendingCommand(pendingCommand);
            _turnManager.World.PendingCommands.Remove(pendingCommand);
            AddLog(GetLocalizedResultMessage(result), IsPlayerRelatedPendingCommand(pendingCommand, playerFactionId));
            CheckFactionEliminations();
        }

        _pendingAttackResolutionQueue.Clear();
        _pendingAttackResolutionQueue.AddRange(_turnManager.GetPendingCommandsOfType(CommandType.Attack));
        ContinuePendingAttackResolution();
    }

    private bool ShouldPromptForPlayerDiplomacyProposal(PendingCommandData pendingCommand)
    {
        if (_turnManager == null)
        {
            return false;
        }

        return pendingCommand.Type == CommandType.Diplomacy &&
               pendingCommand.ActorFactionId != _turnManager.GetPlayerFactionId() &&
               pendingCommand.TargetFactionId == _turnManager.GetPlayerFactionId() &&
               pendingCommand.DiplomacyActionType is DiplomacyActionType.Alliance or DiplomacyActionType.Truce or DiplomacyActionType.Gift or DiplomacyActionType.Demand or DiplomacyActionType.BreakPact;
    }

    private void ContinuePendingAttackResolution()
    {
        if (!_isResolvingEndTurn || _turnManager?.World == null || _commandResolver == null)
        {
            return;
        }

        var world = _turnManager.World;
        while (_pendingAttackResolutionQueue.Count > 0)
        {
            var pendingCommand = _pendingAttackResolutionQueue[0];
            _pendingAttackResolutionQueue.RemoveAt(0);

            var sourceCity = world.GetCity(pendingCommand.SourceCityId);
            var targetCity = world.GetCity(pendingCommand.TargetCityId);
            if (sourceCity == null || targetCity == null)
            {
                var missingResult = _commandResolver.ResolvePendingCommand(pendingCommand);
                AddLog(GetLocalizedResultMessage(missingResult), IsPlayerRelatedPendingCommand(pendingCommand, _turnManager.GetPlayerFactionId()));
                CheckFactionEliminations();
                continue;
            }

            if (ShouldPromptForPlayerDefense(targetCity, pendingCommand))
            {
                ShowDefenseAttackDialog(pendingCommand, targetCity, sourceCity);
                return;
            }

            var result = _commandResolver.ResolvePendingCommand(pendingCommand);
            AddLog(GetLocalizedResultMessage(result), IsPlayerRelatedAttackCommand(sourceCity, targetCity));
            CheckFactionEliminations();
        }

        FinishEndTurnResolution();
    }

    private bool ShouldPromptForPlayerDefense(CityData targetCity, PendingCommandData pendingCommand)
    {
        if (_turnManager?.World == null)
        {
            return false;
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        if (targetCity.OwnerFactionId != playerFactionId)
        {
            return false;
        }

        if (targetCity.Troops <= 0 || targetCity.OfficerIds.Count == 0)
        {
            return false;
        }

        return pendingCommand.DefenderOfficerDeployments.Count == 0;
    }

    private void FinishEndTurnResolution()
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return;
        }

        var world = _turnManager.World;
        _turnManager.RemovePendingCommandsOfType(CommandType.Attack);
        _isResolvingEndTurn = false;
        _pendingNonAttackResolutionQueue.Clear();
        _pendingDefenseCommand = null;
        _pendingDiplomacyProposalCommand = null;
        _attackDiplomacyWarningAcknowledgedTargetCityId = -1;
        _attackDialogContextCity = null;
        _attackDialogMode = AttackDialogMode.Attack;

        _turnManager.AdvanceMonth();
        var economyMonth = world.Month;
        var economyResult = _turnManager.ApplyMonthlyEconomy();
        AddLog(_localization.T("log.monthly_economy"), isPlayerRelated: true);
        if (economyMonth == 1)
        {
            AddLog(_localization.T("log.player_city_horse_birth_header"), isPlayerRelated: true);
            foreach (var entry in economyResult.PlayerCityHorseBirths)
            {
                var city = world.GetCity(entry.CityId);
                if (city == null)
                {
                    continue;
                }

                AddLog(_localization.Format("log.player_city_income_line", _localization.GetCityName(city), entry.Amount), isPlayerRelated: true);
            }
        }

        if (economyMonth == 4)
        {
            AddLog(_localization.T("log.player_city_gold_income_header"), isPlayerRelated: true);
            foreach (var entry in economyResult.PlayerCityGoldIncome)
            {
                var city = world.GetCity(entry.CityId);
                if (city == null)
                {
                    continue;
                }

                AddLog(_localization.Format("log.player_city_income_line", _localization.GetCityName(city), entry.Amount), isPlayerRelated: true);
            }
        }

        if (economyMonth == 8)
        {
            AddLog(_localization.T("log.player_city_food_income_header"), isPlayerRelated: true);
            foreach (var entry in economyResult.PlayerCityFoodIncome)
            {
                var city = world.GetCity(entry.CityId);
                if (city == null)
                {
                    continue;
                }

                AddLog(_localization.Format("log.player_city_income_line", _localization.GetCityName(city), entry.Amount), isPlayerRelated: true);
            }
        }

        foreach (var disaster in economyResult.PlayerCityDisasters)
        {
            var city = world.GetCity(disaster.CityId);
            if (city == null)
            {
                continue;
            }

            AddLog(_localization.Format(
                "log.city_disaster",
                _localization.GetCityName(city),
                disaster.GoldLoss,
                disaster.FoodLoss,
                disaster.LoyaltyLoss), isPlayerRelated: true);
        }

        AddLog(_localization.FormatMonthAdvanced(world.Year, world.Month), isPlayerRelated: true);
        RefreshMonth();
        AutoSelectPlayerCityForNewRound();
        RefreshSelectedCity();
        EvaluateWinLose();
        _mapController?.RefreshVisuals();
    }

    private bool IsPlayerRelatedAttackCommand(CityData sourceCity, CityData targetCity)
    {
        if (_turnManager == null)
        {
            return false;
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        return sourceCity.OwnerFactionId == playerFactionId || targetCity.OwnerFactionId == playerFactionId;
    }

    private bool IsPlayerRelatedPendingCommand(PendingCommandData pendingCommand, int playerFactionId)
    {
        if (_turnManager?.World == null || playerFactionId <= 0)
        {
            return false;
        }

        if (pendingCommand.ActorFactionId == playerFactionId || pendingCommand.TargetFactionId == playerFactionId)
        {
            return true;
        }

        var sourceCity = _turnManager.World.GetCity(pendingCommand.SourceCityId);
        if (sourceCity != null && sourceCity.OwnerFactionId == playerFactionId)
        {
            return true;
        }

        var targetCity = _turnManager.World.GetCity(pendingCommand.TargetCityId);
        return targetCity != null && targetCity.OwnerFactionId == playerFactionId;
    }

    private bool IsPlayerRelatedInternalAffairsSchedule(InternalAffairsScheduleData schedule, int playerFactionId)
    {
        if (_turnManager?.World == null || playerFactionId <= 0)
        {
            return false;
        }

        var city = _turnManager.World.GetCity(schedule.CityId);
        return city != null && city.OwnerFactionId == playerFactionId;
    }
}
