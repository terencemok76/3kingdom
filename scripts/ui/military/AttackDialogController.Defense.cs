using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed partial class AttackDialogController
{
    private void RefreshDefenseSupportControls()
    {
        if (_dialogMode != DialogMode.Defense || _pendingDefenseCommand == null ||
            _context.TurnManager?.World == null || _dialogContextCity == null)
        {
            return;
        }

        var world = _context.TurnManager.World;
        var defendingCity = world.GetCity(_pendingDefenseCommand.TargetCityId) ?? _dialogContextCity;
        var defenderFactionId = defendingCity.OwnerFactionId;
        if (_defenseSupportLabel != null)
        {
            _defenseSupportLabel.Text = _context.Localization?.T("ui.defense_reinforcement") ?? "Reinforce";
        }
        if (_defenseSupportSource != null && !_editingDomesticReinforcement)
        {
            var selectedId = GetSelectedDefenseSupportSourceCity()?.Id ?? 0;
            _defenseSupportSource.Clear();
            foreach (var city in world.Cities
                         .Where(city => city.Id != defendingCity.Id && city.OwnerFactionId > 0 &&
                             city.OwnerFactionId != _pendingDefenseCommand.ActorFactionId &&
                             (city.OwnerFactionId != defenderFactionId || defendingCity.ConnectedCityIds.Contains(city.Id)) &&
                             BattleCampaignService.GetFriendlyRouteLinks(world, city.Id, defendingCity.Id, city.OwnerFactionId) > 0)
                         .OrderBy(city => city.OwnerFactionId == defenderFactionId ? 0 : 1)
                         .ThenBy(city => city.Id))
            {
                var cityName = _context.Localization?.GetCityName(city) ?? city.Name;
                var factionName = _context.Localization?.GetFactionName(world, city.OwnerFactionId) ?? city.OwnerFactionId.ToString();
                var days = BattleCampaignService.GetFriendlyRouteLinks(world, city.Id, defendingCity.Id, city.OwnerFactionId) * 2;
                _defenseSupportSource.AddItem($"{cityName} ({factionName}) · {days} {(_context.Localization?.T("ui.campaign.invite_eta_days") ?? "days")}");
                _defenseSupportSource.SetItemMetadata(_defenseSupportSource.ItemCount - 1, city.Id);
                if (city.Id == selectedId) _defenseSupportSource.Select(_defenseSupportSource.ItemCount - 1);
            }
        }
        if (_defenseSupportTroops != null)
        {
            _defenseSupportTroops.Visible = !_editingDomesticReinforcement;
            _defenseSupportTroops.MinValue = 0;
            _defenseSupportTroops.MaxValue = 10000;
            _defenseSupportTroops.Step = 100;
        }
        if (_defenseSupportSource != null)
        {
            _defenseSupportSource.Visible = !_editingDomesticReinforcement;
        }
        var selectedSupportSource = GetSelectedDefenseSupportSourceCity();
        var isAllyRequest = selectedSupportSource != null && selectedSupportSource.OwnerFactionId != defenderFactionId;
        if (_defenseRequestResources != null)
        {
            _defenseRequestResources.Visible = !_editingDomesticReinforcement && isAllyRequest;
            _defenseRequestResources.GetNode<Label>("Label").Text = _context.Localization?.T("ui.defense_reinforcement_request_resources") ?? "Request";
            if (_defenseRequestGold != null) _defenseRequestGold.Prefix = (_context.Localization?.T("ui.campaign.invite_requested_gold") ?? "Gold") + " ";
            if (_defenseRequestFood != null) _defenseRequestFood.Prefix = (_context.Localization?.T("ui.campaign.invite_requested_food") ?? "Food") + " ";
        }
        if (_defensePromiseRow != null)
        {
            _defensePromiseRow.Visible = !_editingDomesticReinforcement && isAllyRequest;
        }
        if (isAllyRequest)
        {
            if (_defensePromiseLabel != null)
            {
                _defensePromiseLabel.Text = _context.Localization?.T("ui.defense_reinforcement_promise") ?? "Promise";
            }
            var existingRequest = _pendingDefenseCommand.DefenseReinforcementRequests
                .FirstOrDefault(item => item.SourceCityId == selectedSupportSource!.Id && item.IsAllianceRequest);
            ConfigureDefensePromiseSpinBox(_defensePromiseGold, defendingCity.Gold, existingRequest?.PromisedGold ?? 0);
            ConfigureDefensePromiseSpinBox(_defensePromiseFood, defendingCity.Food, existingRequest?.PromisedFood ?? 0);
        }
        if (_defenseSupportAddButton != null)
        {
            UpdateDefenseSupportButtonLabel(defenderFactionId);
        }
        if (_defenseSupportRemoveButton != null)
        {
            _defenseSupportRemoveButton.Text = _context.Localization?.T("ui.defense_reinforcement_remove") ?? "Remove";
            _defenseSupportRemoveButton.Visible = !_editingDomesticReinforcement && selectedSupportSource != null &&
                _pendingDefenseCommand.DefenseReinforcementRequests.Any(item => item.SourceCityId == selectedSupportSource.Id);
        }
        if (_defenseSupportSummary != null)
        {
            _defenseSupportSummary.Visible = _pendingDefenseCommand.DefenseReinforcementRequests.Count > 0;
            _defenseSupportSummary.Text = string.Join("\n", _pendingDefenseCommand.DefenseReinforcementRequests.Select(request =>
            {
                var city = world.GetCity(request.SourceCityId);
                var name = city == null ? request.SourceCityId.ToString() : (_context.Localization?.GetCityName(city) ?? city.Name);
                return request.IsAllianceRequest
                    ? $"{name}: {_context.Localization?.T("ui.defense_reinforcement_foreign") ?? "Foreign support"} {request.RequestedTroops:N0}｜G {request.RequestedGold:N0} / F {request.RequestedFood:N0}｜{_context.Localization?.T("ui.defense_reinforcement_promise_short") ?? "Promise"} G {request.PromisedGold:N0} / F {request.PromisedFood:N0}"
                    : $"{name}: {_context.Localization?.T("ui.defense_reinforcement_domestic") ?? "Domestic reinforcement"} {request.Deployments.Sum(item => item.TroopCount):N0}｜G {request.RequestedGold:N0} / F {request.RequestedFood:N0}";
            }));
        }
    }

    private bool IsActiveAlly(int defenderFactionId, int otherFactionId)
    {
        if (defenderFactionId == otherFactionId || _context.TurnManager?.World == null)
        {
            return false;
        }
        var relation = _context.TurnManager.World.GetDiplomacyRelation(defenderFactionId, otherFactionId);
        return relation is { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 };
    }

    private void UpdateDefenseSupportButtonLabel(int defenderFactionId)
    {
        if (_defenseSupportAddButton == null)
        {
            return;
        }

        var sourceCity = GetSelectedDefenseSupportSourceCity();
        _defenseSupportAddButton.Text = _editingDomesticReinforcement
            ? (_context.Localization?.T("ui.defense_reinforcement_save") ?? "Save Reinforcement")
            : sourceCity != null && sourceCity.OwnerFactionId == defenderFactionId
                ? (_context.Localization?.T("ui.defense_reinforcement_organize") ?? "Organize Reinforcement")
                : (_context.Localization?.T("ui.defense_reinforcement_request") ?? "Request");
    }

    private void RefreshDefenseMessengerOptions()
    {
        if (_defenseMessengerRow == null || _defenseMessengerLabel == null || _defenseMessengerOption == null ||
            _dialogMode != DialogMode.Defense || _pendingDefenseCommand == null || _context.TurnManager?.World == null)
        {
            return;
        }

        var world = _context.TurnManager.World;
        var defendingCity = world.GetCity(_pendingDefenseCommand.TargetCityId);
        var sourceCity = GetSelectedDefenseSupportSourceCity();
        var requiresEnvoy = defendingCity != null && sourceCity != null && sourceCity.OwnerFactionId != defendingCity.OwnerFactionId;
        _defenseMessengerRow.Visible = requiresEnvoy && !_editingDomesticReinforcement;
        if (_defenseResponseEstimate != null) _defenseResponseEstimate.Visible = requiresEnvoy && !_editingDomesticReinforcement && _selectedDefenseEnvoyOfficerId > 0;
        if (!requiresEnvoy || defendingCity == null)
        {
            return;
        }

        _defenseMessengerLabel.Text = _context.Localization?.T("ui.defense_reinforcement_envoy") ?? "Envoy";
        var selectedDefenderIds = _context.GetCheckedTreeMetadataIds(_officerList).ToHashSet();
        var usedEnvoys = _pendingDefenseCommand.DefenseReinforcementRequests
            .Where(request => request.SourceCityId != sourceCity?.Id && request.EnvoyOfficerId > 0)
            .Select(request => request.EnvoyOfficerId).ToHashSet();
        _defenseMessengerOption.Clear();
        _defenseMessengerOption.AddItem(_context.Localization?.T("ui.defense_reinforcement_envoy_select") ?? "Select envoy");
        _defenseMessengerOption.SetItemMetadata(0, 0);
        var selectedIndex = 0;
        foreach (var officer in defendingCity.OfficerIds
                     .Where(id => !selectedDefenderIds.Contains(id) && !usedEnvoys.Contains(id) && !BattleCampaignService.IsOfficerCommitted(world, id))
                     .Select(world.GetOfficer)
                     .Where(officer => officer != null)
                     .Cast<OfficerData>()
                     .OrderByDescending(officer => officer.Intelligence + officer.Charm))
        {
            _defenseMessengerOption.AddItem($"{_context.Localization?.GetOfficerName(officer) ?? officer.Name}  智 {officer.Intelligence}／魅 {officer.Charm}");
            _defenseMessengerOption.SetItemMetadata(_defenseMessengerOption.ItemCount - 1, officer.Id);
            if (officer.Id == _selectedDefenseEnvoyOfficerId)
            {
                selectedIndex = _defenseMessengerOption.ItemCount - 1;
            }
        }
        _defenseMessengerOption.Select(selectedIndex);
        RefreshDefenseResponseEstimate();
    }

    private void RefreshDefenseResponseEstimate()
    {
        if (_defenseResponseEstimate == null || _context.TurnManager?.World is not { } world ||
            _pendingDefenseCommand == null || GetSelectedDefenseSupportSourceCity() is not { } source || _selectedDefenseEnvoyOfficerId <= 0)
        {
            if (_defenseResponseEstimate != null) _defenseResponseEstimate.Visible = false;
            return;
        }
        _defenseResponseEstimate.Visible = !_editingDomesticReinforcement;
        var days = BattleCampaignService.GetInvitationResponseDays(world, _pendingDefenseCommand.TargetCityId, source.OwnerFactionId, source.Id, _selectedDefenseEnvoyOfficerId);
        var travel = BattleCampaignService.GetFriendlyRouteLinks(world, source.Id, _pendingDefenseCommand.TargetCityId, source.OwnerFactionId) * 2;
        _defenseResponseEstimate.Text = _context.Localization?.Format("ui.defense_reinforcement_timing", days, travel) ?? $"Reply: {days} battle days; travel: {travel} days.";
    }

    private CityData? GetSelectedDefenseSupportSourceCity()
    {
        var sourceOption = _defenseSupportSource;
        if (sourceOption == null || sourceOption.Selected < 0 || _context.TurnManager?.World == null)
        {
            return null;
        }
        var metadata = sourceOption.GetItemMetadata(sourceOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? _context.TurnManager.World.GetCity(metadata.AsInt32())
            : null;
    }

    private void OnDefenseMessengerSelected(long index)
    {
        if (_defenseMessengerOption == null || index < 0 || index >= _defenseMessengerOption.ItemCount)
        {
            return;
        }
        var metadata = _defenseMessengerOption.GetItemMetadata((int)index);
        _selectedDefenseEnvoyOfficerId = metadata.VariantType == Variant.Type.Int ? metadata.AsInt32() : 0;
        RefreshDefenseResponseEstimate();
        RefreshOfficerTableText();
        RefreshDeploymentEditor();
    }

    private void OnDefenseSupportSourceSelected()
    {
        _selectedDefenseEnvoyOfficerId = 0;
        RefreshDefenseSupportControls();
        var defenderFactionId = _context.TurnManager?.World?.GetCity(_pendingDefenseCommand?.TargetCityId ?? 0)?.OwnerFactionId ?? -1;
        UpdateDefenseSupportButtonLabel(defenderFactionId);
        RefreshDefenseMessengerOptions();
        RefreshOfficerTableText();
        RefreshDeploymentEditor();
    }

    private void OnDefenseSupportAddPressed()
    {
        if (_editingDomesticReinforcement)
        {
            OnConfirmPressed();
            return;
        }
        var sourceOption = _defenseSupportSource;
        if (sourceOption == null || sourceOption.Selected < 0 || _context.TurnManager?.World == null || _pendingDefenseCommand == null)
        {
            return;
        }
        var metadata = sourceOption.GetItemMetadata(sourceOption.Selected);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return;
        }
        var source = _context.TurnManager.World.GetCity(metadata.AsInt32());
        if (source == null || _dialogContextCity == null)
        {
            return;
        }
        if (source.OwnerFactionId != _dialogContextCity.OwnerFactionId)
        {
            if (_selectedDefenseEnvoyOfficerId <= 0)
            {
                SetWarning(_context.Localization?.T("ui.defense_reinforcement_envoy_required") ?? "Select an envoy for this allied request.");
                return;
            }
            var requestedTroops = _defenseSupportTroops == null ? 1000 : (int)_defenseSupportTroops.Value;
            var requestedGold = _defenseRequestGold == null ? 0 : (int)_defenseRequestGold.Value;
            var requestedFood = _defenseRequestFood == null ? 0 : (int)_defenseRequestFood.Value;
            if (requestedTroops <= 0 && requestedGold <= 0 && requestedFood <= 0)
            {
                SetWarning(_context.Localization?.T("ui.campaign.invite_support_required") ?? "Enter the requested troops or resources.");
                return;
            }
            if (requestedTroops > 0 && !IsActiveAlly(_dialogContextCity.OwnerFactionId, source.OwnerFactionId))
            {
                SetWarning(_context.Localization?.T("ui.campaign.invite_alliance_required") ?? "Troop support requires an alliance.");
                return;
            }
            UpsertDefenseReinforcement(new DefenseReinforcementRequestData
            {
                SourceCityId = source.Id,
                IsAllianceRequest = true,
                EnvoyOfficerId = _selectedDefenseEnvoyOfficerId,
                RequestedTroops = requestedTroops, RequestedGold = requestedGold, RequestedFood = requestedFood,
                PromisedGold = _defensePromiseGold == null ? 0 : (int)_defensePromiseGold.Value,
                PromisedFood = _defensePromiseFood == null ? 0 : (int)_defensePromiseFood.Value
            });
            RefreshDefenseSupportControls();
            return;
        }

        _draft.BeginReinforcement();
        _dialogContextCity = source;
        _editingDomesticReinforcement = true;
        _domesticReinforcementSourceCityId = source.Id;
        ConfigureSpinBox(_goldSpinBox, source.Gold, 0);
        ConfigureSpinBox(_foodSpinBox, source.Food, 0);
        PopulateOfficerList(source, source.OfficerIds.ToList());
        RefreshText();
        RefreshDeploymentEditor();
    }

    private void SaveDomesticDefenseReinforcement(List<AttackOfficerDeploymentData> deployments)
    {
        UpsertDefenseReinforcement(new DefenseReinforcementRequestData
        {
            SourceCityId = _domesticReinforcementSourceCityId > 0 ? _domesticReinforcementSourceCityId : GetDialogCityContext()?.Id ?? 0,
            RequestedGold = (int)(_goldSpinBox?.Value ?? 0), RequestedFood = (int)(_foodSpinBox?.Value ?? 0),
            Deployments = deployments.Select(DeploymentDraft.CloneDeployment).ToList()
        });
        _draft.RestoreDefenders();
        _dialogContextCity = _context.TurnManager?.World?.GetCity(_pendingDefenseCommand?.TargetCityId ?? 0);
        _editingDomesticReinforcement = false;
        _domesticReinforcementSourceCityId = 0;
        _selectedDefenseEnvoyOfficerId = 0;
        if (_dialogContextCity != null)
        {
            PopulateOfficerList(_dialogContextCity, _dialogContextCity.OfficerIds.ToList());
        }
        RefreshText();
        RefreshDeploymentEditor();
    }

    private void OnDefenseSupportRemovePressed()
    {
        var source = GetSelectedDefenseSupportSourceCity();
        if (_pendingDefenseCommand == null || source == null) return;
        _pendingDefenseCommand.DefenseReinforcementRequests.RemoveAll(item => item.SourceCityId == source.Id);
        _selectedDefenseEnvoyOfficerId = 0;
        RefreshDefenseSupportControls();
        RefreshDefenseMessengerOptions();
        RefreshOfficerTableText();
        RefreshDeploymentEditor();
    }

    private void UpsertDefenseReinforcement(DefenseReinforcementRequestData request)
    {
        if (_pendingDefenseCommand == null || request.SourceCityId <= 0)
        {
            return;
        }
        _pendingDefenseCommand.DefenseReinforcementRequests.RemoveAll(item => item.SourceCityId == request.SourceCityId);
        _pendingDefenseCommand.DefenseReinforcementRequests.Add(request);
    }

    private static void ConfigureDefensePromiseSpinBox(SpinBox? spinBox, int available, int value)
    {
        if (spinBox == null)
        {
            return;
        }

        spinBox.MinValue = 0;
        spinBox.MaxValue = Math.Max(0, available);
        spinBox.Step = 50;
        spinBox.Value = Math.Clamp(value, 0, (int)spinBox.MaxValue);
    }

}
