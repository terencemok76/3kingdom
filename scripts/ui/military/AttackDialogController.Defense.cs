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
            _defenseSupportSource.Clear();
            foreach (var city in defendingCity.ConnectedCityIds
                         .Select(world.GetCity)
                         .Where(city => city != null && city.OwnerFactionId > 0)
                         .Cast<CityData>()
                         .Where(city => city.OwnerFactionId == defenderFactionId || IsActiveAlly(defenderFactionId, city.OwnerFactionId))
                         .OrderBy(city => city.OwnerFactionId == defenderFactionId ? 0 : 1)
                         .ThenBy(city => city.Id))
            {
                var cityName = _context.Localization?.GetCityName(city) ?? city.Name;
                var factionName = _context.Localization?.GetFactionName(world, city.OwnerFactionId) ?? city.OwnerFactionId.ToString();
                _defenseSupportSource.AddItem($"{cityName} ({factionName})");
                _defenseSupportSource.SetItemMetadata(_defenseSupportSource.ItemCount - 1, city.Id);
            }
        }
        if (_defenseSupportTroops != null)
        {
            _defenseSupportTroops.Visible = !_editingDomesticReinforcement;
            _defenseSupportTroops.MinValue = 100;
            _defenseSupportTroops.MaxValue = 10000;
            _defenseSupportTroops.Step = 100;
            _defenseSupportTroops.Value = Math.Max(100, _defenseSupportTroops.Value);
        }
        if (_defenseSupportSource != null)
        {
            _defenseSupportSource.Visible = !_editingDomesticReinforcement;
        }
        var selectedSupportSource = GetSelectedDefenseSupportSourceCity();
        var isAllyRequest = selectedSupportSource != null && selectedSupportSource.OwnerFactionId != defenderFactionId;
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
        if (_defenseSupportSummary != null)
        {
            _defenseSupportSummary.Visible = _pendingDefenseCommand.DefenseReinforcementRequests.Count > 0;
            _defenseSupportSummary.Text = string.Join("\n", _pendingDefenseCommand.DefenseReinforcementRequests.Select(request =>
            {
                var city = world.GetCity(request.SourceCityId);
                var name = city == null ? request.SourceCityId.ToString() : (_context.Localization?.GetCityName(city) ?? city.Name);
                return request.IsAllianceRequest
                    ? $"{name}: {_context.Localization?.T("ui.defense_reinforcement_ally") ?? "Ally request"} {request.RequestedTroops:N0}｜{_context.Localization?.T("ui.defense_reinforcement_promise_short") ?? "Promise"} G {request.PromisedGold:N0} / F {request.PromisedFood:N0}"
                    : $"{name}: {_context.Localization?.T("ui.defense_reinforcement_domestic") ?? "Domestic reinforcement"} {request.Deployments.Sum(item => item.TroopCount):N0}";
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
        if (!requiresEnvoy || defendingCity == null)
        {
            return;
        }

        _defenseMessengerLabel.Text = _context.Localization?.T("ui.defense_reinforcement_envoy") ?? "Envoy";
        var selectedDefenderIds = _context.GetCheckedTreeMetadataIds(_officerList).ToHashSet();
        _defenseMessengerOption.Clear();
        _defenseMessengerOption.AddItem(_context.Localization?.T("ui.defense_reinforcement_envoy_select") ?? "Select envoy");
        _defenseMessengerOption.SetItemMetadata(0, 0);
        var selectedIndex = 0;
        foreach (var officer in defendingCity.OfficerIds
                     .Where(id => !selectedDefenderIds.Contains(id) && !BattleCampaignService.IsOfficerCommitted(world, id))
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
        RefreshOfficerTableText();
        RefreshDeploymentEditor();
    }

    private void OnDefenseSupportSourceSelected()
    {
        _selectedDefenseEnvoyOfficerId = 0;
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
            UpsertDefenseReinforcement(new DefenseReinforcementRequestData
            {
                SourceCityId = source.Id,
                IsAllianceRequest = true,
                EnvoyOfficerId = _selectedDefenseEnvoyOfficerId,
                RequestedTroops = _defenseSupportTroops == null ? 1000 : (int)_defenseSupportTroops.Value,
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
        PopulateOfficerList(source, source.OfficerIds.ToList());
        RefreshText();
        RefreshDeploymentEditor();
    }

    private void SaveDomesticDefenseReinforcement(List<AttackOfficerDeploymentData> deployments)
    {
        UpsertDefenseReinforcement(new DefenseReinforcementRequestData
        {
            SourceCityId = _domesticReinforcementSourceCityId > 0 ? _domesticReinforcementSourceCityId : GetDialogCityContext()?.Id ?? 0,
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
