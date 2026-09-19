using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private readonly Queue<string> _allianceReinforcementResponseQueue = new();
    private readonly Queue<string> _playerAllianceResultQueue = new();
    private int _campaignWaitingForAllianceResponse;
    private Control? _allianceReinforcementResponseDialog;
    private Control? _playerAllianceResultDialog;
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
            var isPlayerRelated = pendingResults[index].IsPlayerRelated ??
                (index < activeSchedules.Count && IsPlayerRelatedInternalAffairsSchedule(activeSchedules[index], playerFactionId));
            AddLog(GetLocalizedResultMessage(pendingResults[index]), isPlayerRelated);
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
                _diplomacyUiController?.ShowProposalDialog(pendingCommand);
                return;
            }

            var result = _commandResolver.ResolvePendingCommand(pendingCommand);
            _turnManager.World.PendingCommands.Remove(pendingCommand);
            AddLog(GetLocalizedResultMessage(result), IsPlayerRelatedPendingCommand(pendingCommand, playerFactionId));
            CheckFactionEliminations();
            if (QueuePlayerAllianceResultNotification(pendingCommand, result, playerFactionId))
            {
                return;
            }
            if (_personnelUiController?.HasPendingPlayerSuccession() == true)
            {
                _personnelUiController.ShowSuccessionDialog();
                return;
            }
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

    private bool QueuePlayerAllianceResultNotification(
        PendingCommandData pendingCommand,
        CommandResult result,
        int playerFactionId)
    {
        if (_localization == null ||
            pendingCommand.Type != CommandType.Diplomacy ||
            pendingCommand.DiplomacyActionType != DiplomacyActionType.Alliance ||
            pendingCommand.ActorFactionId != playerFactionId)
        {
            return false;
        }

        _playerAllianceResultQueue.Enqueue(GetLocalizedResultMessage(result));
        ShowNextPlayerAllianceResult();
        return true;
    }

    private void ShowNextPlayerAllianceResult()
    {
        if (_playerAllianceResultDialog != null || _playerAllianceResultQueue.Count == 0)
        {
            return;
        }

        _playerAllianceResultDialog = GD.Load<PackedScene>(FactionOutcomeDialogScenePath).Instantiate<Control>();
        var overlayRoot = GetNodeOrNull<Control>("Root") as Node ?? this;
        overlayRoot.AddChild(_playerAllianceResultDialog);
        _playerAllianceResultDialog.GetNode<Label>("Center/OutcomePanel/Root/Header/TitleLabel").Text =
            _localization?.T("ui.diplomacy_alliance_result_title") ?? "Alliance Result";
        _playerAllianceResultDialog.GetNode<Label>("Center/OutcomePanel/Root/Body/Content/MessageLabel").Text =
            _playerAllianceResultQueue.Dequeue();
        var acknowledgeButton = _playerAllianceResultDialog.GetNode<Button>("Center/OutcomePanel/Root/Body/Content/ButtonRow/AcknowledgeButton");
        acknowledgeButton.Text = _localization?.T("ui.acknowledge") ?? "Acknowledge";
        acknowledgeButton.Pressed += ClosePlayerAllianceResult;
        _playerAllianceResultDialog.Visible = true;
    }

    private void ClosePlayerAllianceResult()
    {
        if (_playerAllianceResultDialog == null)
        {
            return;
        }

        _playerAllianceResultDialog.QueueFree();
        _playerAllianceResultDialog = null;
        if (_playerAllianceResultQueue.Count > 0)
        {
            Callable.From(ShowNextPlayerAllianceResult).CallDeferred();
            return;
        }

        Callable.From(ContinuePendingNonAttackResolution).CallDeferred();
    }

    private void ContinuePendingAttackResolution()
    {
        if (!_isResolvingEndTurn || _turnManager?.World == null || _commandResolver == null)
        {
            return;
        }

        var world = _turnManager.World;
        if (TryLaunchPlayerCampaignForCurrentMonth())
        {
            return;
        }

        while (_pendingAttackResolutionQueue.Count > 0)
        {
            var pendingCommand = _pendingAttackResolutionQueue[0];
            if (!world.PendingCommands.Contains(pendingCommand))
            {
                _pendingAttackResolutionQueue.RemoveAt(0);
                continue;
            }

            var sourceCity = world.GetCity(pendingCommand.SourceCityId);
            var targetCity = world.GetCity(pendingCommand.TargetCityId);
            if (sourceCity == null || targetCity == null)
            {
                _pendingAttackResolutionQueue.RemoveAt(0);
                var missingResult = _commandResolver.ResolvePendingCommand(pendingCommand);
                AddLog(GetLocalizedResultMessage(missingResult), IsPlayerRelatedPendingCommand(pendingCommand, _turnManager.GetPlayerFactionId()));
                CheckFactionEliminations();
                continue;
            }

            if (ShouldPromptForPlayerDefense(targetCity, pendingCommand))
            {
                _militaryUiController?.ShowDefenseAttackDialog(pendingCommand, targetCity, sourceCity);
                return;
            }

            // Keep the pending command at the front of the queue while the
            // player chooses defenders.  Confirm Defense resumes this method;
            // only then is the same configured attack removed and resolved.
            _pendingAttackResolutionQueue.RemoveAt(0);
            var result = _commandResolver.ResolvePendingCommand(pendingCommand);
            if (result.ActiveBattleCampaignId > 0)
            {
                world.PendingCommands.Remove(pendingCommand);
                world.ResumeAttackResolutionAfterCampaign = true;
                world.IsBattleResolutionPhase = true;
                AddLog(GetLocalizedResultMessage(result), IsPlayerRelatedAttackCommand(sourceCity, targetCity));
                QueueAllianceReinforcementResponses(result.ActiveBattleCampaignId);
                return;
            }

            AddLog(GetLocalizedResultMessage(result), IsPlayerRelatedAttackCommand(sourceCity, targetCity));
            CheckFactionEliminations();
            if (_personnelUiController?.HasPendingPlayerSuccession() == true)
            {
                _personnelUiController.ShowSuccessionDialog();
                return;
            }
        }

        FinishEndTurnResolution();
    }

    private void QueueAllianceReinforcementResponses(int campaignId)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            LaunchCampaignBattle(campaignId);
            return;
        }
        var campaign = _turnManager.World.ActiveBattleCampaigns.FirstOrDefault(item => item.Id == campaignId);
        if (campaign == null)
        {
            LaunchCampaignBattle(campaignId);
            return;
        }
        foreach (var invitation in campaign.Invitations.Where(item => item.Side == CampaignBattleSide.Defender))
        {
            var factionName = _localization.GetFactionName(_turnManager.World, invitation.InvitedFactionId);
            var message = invitation.Status == BattleInvitationStatus.Accepted
                ? _localization.Format("log.alliance_reinforcement_accepted", factionName, invitation.RequestedTroops)
                : _localization.Format("log.alliance_reinforcement_declined", factionName, invitation.DecisionReason);
            AddLog(message, isPlayerRelated: true);
            _allianceReinforcementResponseQueue.Enqueue(message);
        }
        _campaignWaitingForAllianceResponse = campaignId;
        ShowNextAllianceReinforcementResponse();
    }

    private void ShowNextAllianceReinforcementResponse()
    {
        if (_allianceReinforcementResponseDialog != null)
        {
            return;
        }

        if (_allianceReinforcementResponseQueue.Count == 0)
        {
            var campaignId = _campaignWaitingForAllianceResponse;
            _campaignWaitingForAllianceResponse = 0;
            LaunchCampaignBattle(campaignId);
            return;
        }

        _allianceReinforcementResponseDialog = GD.Load<PackedScene>(FactionOutcomeDialogScenePath).Instantiate<Control>();
        var overlayRoot = GetNodeOrNull<Control>("Root") as Node ?? this;
        overlayRoot.AddChild(_allianceReinforcementResponseDialog);
        _allianceReinforcementResponseDialog.GetNode<Label>("Center/OutcomePanel/Root/Header/TitleLabel").Text =
            _localization?.T("ui.alliance_reinforcement_response_title") ?? "Alliance Response";
        _allianceReinforcementResponseDialog.GetNode<Label>("Center/OutcomePanel/Root/Body/Content/MessageLabel").Text =
            _allianceReinforcementResponseQueue.Dequeue();
        var acknowledgeButton = _allianceReinforcementResponseDialog.GetNode<Button>("Center/OutcomePanel/Root/Body/Content/ButtonRow/AcknowledgeButton");
        acknowledgeButton.Text = _localization?.T("ui.acknowledge") ?? "Acknowledge";
        acknowledgeButton.Pressed += CloseAllianceReinforcementResponse;
        _allianceReinforcementResponseDialog.Visible = true;
    }

    private void CloseAllianceReinforcementResponse()
    {
        if (_allianceReinforcementResponseDialog == null)
        {
            return;
        }

        _allianceReinforcementResponseDialog.QueueFree();
        _allianceReinforcementResponseDialog = null;
        Callable.From(ShowNextAllianceReinforcementResponse).CallDeferred();
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
        if (_diplomacyUiController != null)
        {
            _diplomacyUiController.PendingProposalCommand = null;
        }
        if (_personnelUiController != null)
        {
            _personnelUiController.PendingSuccessionFactionId = -1;
        }
        _militaryUiController?.ResetAttackDialogState();

        var playerFactionId = _turnManager.GetPlayerFactionId();
        _turnManager.AdvanceMonth();
        world.IsBattleResolutionPhase = false;
        foreach (var escapeEvent in _turnManager.ConsumeOfficerEscapeEvents())
        {
            var officer = world.GetOfficer(escapeEvent.OfficerId);
            var city = world.GetCity(escapeEvent.JailedCityId);
            if (officer == null || city == null)
            {
                continue;
            }

            AddLog(
                _localization.Format(
                    "log.captured_officer_escaped",
                    _localization.GetOfficerName(officer),
                    _localization.GetCityName(city)),
                isPlayerRelated: escapeEvent.CaptorFactionId == playerFactionId);
        }
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

        foreach (var cityEvent in economyResult.AllCityEvents)
        {
            var city = world.GetCity(cityEvent.CityId);
            if (city == null)
            {
                continue;
            }

            var eventKey = cityEvent.EventType switch
            {
                MonthlyCityEventType.Flooding => "log.city_event_flooding",
                MonthlyCityEventType.Drought => "log.city_event_drought",
                MonthlyCityEventType.Earthquake => "log.city_event_earthquake",
                MonthlyCityEventType.InsectDisaster => "log.city_event_insect_disaster",
                MonthlyCityEventType.Plague => "log.city_event_plague",
                MonthlyCityEventType.Rebellion => "log.city_event_rebellion",
                MonthlyCityEventType.Bandit => "log.city_event_bandit",
                MonthlyCityEventType.Snow => "log.city_event_snow",
                MonthlyCityEventType.Typhoon => "log.city_event_typhoon",
                MonthlyCityEventType.BumperHarvest => "log.city_event_bumper_harvest",
                MonthlyCityEventType.Fire => "log.city_event_fire",
                _ => "log.city_disaster"
            };
            AddLog(_localization.Format(
                eventKey,
                _localization.GetCityName(city),
                System.Math.Abs(cityEvent.GoldDelta),
                System.Math.Abs(cityEvent.FoodDelta),
                System.Math.Abs(cityEvent.LoyaltyDelta),
                System.Math.Abs(cityEvent.FarmDelta),
                System.Math.Abs(cityEvent.DefenseDelta),
                System.Math.Abs(cityEvent.PopulationDelta),
                System.Math.Abs(cityEvent.TroopDelta)), isPlayerRelated: city.OwnerFactionId == playerFactionId);
        }

        QueueMonthlyCityEventPresentations(economyResult.AllCityEvents);

        AddLog(_localization.FormatMonthAdvanced(world.Year, world.Month), isPlayerRelated: true);
        RefreshMonth();
        AutoSelectPlayerCityForNewRound();
        RefreshSelectedCity();
        _internalAffairsUiController?.RefreshIfOpen();
        _personnelUiController?.RefreshIfOpen();
        EvaluateWinLose();
        _mapController?.RefreshVisuals();

        if (_militaryUiController?.HasPendingPlayerCapturedOfficer() == true)
        {
            _militaryUiController.ShowCapturedOfficerDialog();
        }
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
