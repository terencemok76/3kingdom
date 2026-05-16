using System;
using System.Linq;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController : CanvasLayer
{
    private void EnsureSpyDialogWidgets()
    {
        if (_spyDialog == null)
        {
            return;
        }

        var existingRoot = _spyDialog.GetNodeOrNull<VBoxContainer>("SpyDialogRoot");
        if (existingRoot == null)
        {
            GD.PushError("SpyDialogRoot not found in SpyDialog.tscn.");
            return;
        }

        _spyActionOption = existingRoot.GetNodeOrNull<OptionButton>("ActionRow/ActionOption");
        _spyTargetCityOption = existingRoot.GetNodeOrNull<OptionButton>("TargetCityRow/TargetCityOption");
        _spyTargetOfficerOption = existingRoot.GetNodeOrNull<OptionButton>("TargetOfficerRow/TargetOfficerOption");
        _spySelectedOfficerLabel = existingRoot.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _spySelectOfficerButton = existingRoot.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _spySummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
        _spyWarningLabel = existingRoot.GetNodeOrNull<Label>("WarningLabel");
        _spyConfirmButton = existingRoot.GetNodeOrNull<Button>("FooterRow/ConfirmButton");
        ConnectSpyDialogSignals();
    }

    private void ShowSpyDialog()
    {
        if (_selectedCity == null || _spyDialog == null || _localization == null)
        {
            return;
        }

        EnsureSpyDialogWidgets();
        UpdateSpyDialogText();
        PopulateSpyDialog();
        PopupDialogUsingSceneSize(_spyDialog);
    }

    private void PopulateSpyDialog()
    {
        if (_turnManager?.World == null || _selectedCity == null || _localization == null ||
            _spyActionOption == null || _spyTargetCityOption == null)
        {
            return;
        }

        _spyActionOption.Clear();
        AddSpyActionOption(SpyActionType.Reconnaissance);
        AddSpyActionOption(SpyActionType.Sabotage);
        AddSpyActionOption(SpyActionType.Incite);
        AddSpyActionOption(SpyActionType.Assassination);

        _spyTargetCityOption.Clear();
        foreach (var city in _turnManager.World.Cities.Where(city => city.OwnerFactionId != _selectedCity.OwnerFactionId))
        {
            var ownerName = _localization.GetFactionName(_turnManager.World, city.OwnerFactionId);
            _spyTargetCityOption.AddItem($"{_localization.GetCityName(city)} | {ownerName}");
            _spyTargetCityOption.SetItemMetadata(_spyTargetCityOption.ItemCount - 1, city.Id);
        }

        var candidateOfficerIds = GetAvailableSpyOfficerIds();
        if (!candidateOfficerIds.Contains(_spySelectedOfficerId))
        {
            _spySelectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }
        PopulateSpyTargetOfficerOptions();
        SetSpyWarning(string.Empty);
        UpdateSpySelectedOfficerSummary();
        UpdateSpySummary();
        UpdateSpyConfirmButtonState();
    }

    private void UpdateSpyDialogText()
    {
        if (_spyDialog == null || _localization == null)
        {
            return;
        }

        _spyDialog.Title = _localization.T("ui.spy");
        SetSpyDialogLabelText("ActionLabel", _localization.T("ui.spy_action"));
        SetSpyDialogLabelText("TargetCityLabel", _localization.T("ui.spy_target_city"));
        SetSpyDialogLabelText("TargetOfficerLabel", _localization.T("ui.spy_target_officer"));
        SetSpyDialogLabelText("OfficerListLabel", _localization.T("ui.spy_officer"));
        if (_spySelectOfficerButton != null)
        {
            _spySelectOfficerButton.Text = _localization.T("ui.select_officer");
        }
        UpdateSpySelectedOfficerSummary();
        if (_spyConfirmButton != null)
        {
            _spyConfirmButton.Text = _localization.T("ui.confirm_spy");
        }
    }

    private void SetSpyDialogLabelText(string nodeName, string text)
    {
        var root = _spyDialog?.GetNodeOrNull<Control>("SpyDialogRoot");
        var label = root?.FindChild(nodeName, true, false) as Label;
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void AddSpyActionOption(SpyActionType actionType)
    {
        if (_spyActionOption == null || _localization == null)
        {
            return;
        }

        var key = actionType switch
        {
            SpyActionType.Reconnaissance => "command.spy.reconnaissance",
            SpyActionType.Sabotage => "command.spy.sabotage",
            SpyActionType.Incite => "command.spy.incite",
            SpyActionType.Assassination => "command.spy.assassination",
            _ => "command.spy.reconnaissance"
        };
        _spyActionOption.AddItem(_localization.T(key));
        _spyActionOption.SetItemMetadata(_spyActionOption.ItemCount - 1, (int)actionType);
    }

    private SpyActionType GetSelectedSpyActionType()
    {
        if (_spyActionOption == null)
        {
            return SpyActionType.Reconnaissance;
        }

        var metadata = _spyActionOption.GetItemMetadata(_spyActionOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (SpyActionType)metadata.AsInt32()
            : SpyActionType.Reconnaissance;
    }

    private int GetSelectedSpyTargetCityId()
    {
        if (_spyTargetCityOption == null || _spyTargetCityOption.ItemCount == 0)
        {
            return -1;
        }

        var metadata = _spyTargetCityOption.GetItemMetadata(_spyTargetCityOption.Selected);
        return metadata.VariantType == Variant.Type.Int ? metadata.AsInt32() : -1;
    }

    private void UpdateSpySummary()
    {
        if (_turnManager?.World == null || _localization == null || _spySummaryLabel == null)
        {
            return;
        }

        var actionType = GetSelectedSpyActionType();
        var targetCityId = GetSelectedSpyTargetCityId();
        var targetCity = _turnManager.World.GetCity(targetCityId);
        var targetCityName = targetCity != null ? _localization.GetCityName(targetCity) : "-";
        var actionName = _localization.T(actionType switch
        {
            SpyActionType.Reconnaissance => "command.spy.reconnaissance",
            SpyActionType.Sabotage => "command.spy.sabotage",
            SpyActionType.Incite => "command.spy.incite",
            SpyActionType.Assassination => "command.spy.assassination",
            _ => "command.spy.reconnaissance"
        });
        if (actionType == SpyActionType.Assassination)
        {
            var targetOfficerName = GetSelectedSpyTargetOfficerName();
            _spySummaryLabel.Text = _localization.Format("fmt.spy_assassination_summary", actionName, targetCityName, targetOfficerName);
            return;
        }

        _spySummaryLabel.Text = _localization.Format("fmt.spy_summary", actionName, targetCityName);
    }

    private void UpdateSpyConfirmButtonState()
    {
        if (_spyConfirmButton == null)
        {
            return;
        }

        var hasOfficer = _spySelectedOfficerId > 0;
        var hasTarget = GetSelectedSpyTargetCityId() > 0;
        var needsTargetOfficer = GetSelectedSpyActionType() == SpyActionType.Assassination;
        var hasTargetOfficer = !needsTargetOfficer || GetSelectedSpyTargetOfficerId() > 0;
        _spyConfirmButton.Disabled = !hasOfficer || !hasTarget || !hasTargetOfficer;
    }

    private void SetSpyWarning(string text)
    {
        if (_spyWarningLabel != null)
        {
            _spyWarningLabel.Text = text;
        }
    }

    private void ConnectSpyDialogSignals()
    {
        if (_spyDialogSignalsConnected)
        {
            return;
        }

        if (_spyActionOption != null)
        {
            _spyActionOption.ItemSelected += _ =>
            {
                PopulateSpyTargetOfficerOptions();
                UpdateSpySummary();
                UpdateSpyConfirmButtonState();
            };
        }

        if (_spyTargetCityOption != null)
        {
            _spyTargetCityOption.ItemSelected += _ =>
            {
                PopulateSpyTargetOfficerOptions();
                UpdateSpySummary();
                UpdateSpyConfirmButtonState();
            };
        }

        if (_spyTargetOfficerOption != null)
        {
            _spyTargetOfficerOption.ItemSelected += _ =>
            {
                UpdateSpySummary();
                UpdateSpyConfirmButtonState();
            };
        }

        if (_spySelectOfficerButton != null)
        {
            _spySelectOfficerButton.Pressed += OnSpySelectOfficerPressed;
        }

        if (_spyConfirmButton != null)
        {
            _spyConfirmButton.Pressed += OnSpyConfirmPressed;
        }

        _spyDialogSignalsConnected = true;
    }

    private void OnSpyConfirmPressed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null || _localization == null)
        {
            return;
        }

        if (_spySelectedOfficerId <= 0)
        {
            SetSpyWarning(_localization.T("ui.select_officer_warning"));
            return;
        }

        var targetCityId = GetSelectedSpyTargetCityId();
        if (targetCityId <= 0)
        {
            SetSpyWarning(_localization.T("ui.spy_target_required_warning"));
            return;
        }

        var actionType = GetSelectedSpyActionType();
        var targetOfficerId = GetSelectedSpyTargetOfficerId();
        if (actionType == SpyActionType.Assassination && targetOfficerId <= 0)
        {
            SetSpyWarning(_localization.T("ui.spy_target_officer_required_warning"));
            return;
        }

        var result = _commandResolver.Execute(new CommandRequest
        {
            Type = CommandType.Spy,
            ActorFactionId = _turnManager.GetPlayerFactionId(),
            SourceCityId = _selectedCity.Id,
            TargetCityId = targetCityId,
            TargetOfficerId = actionType == SpyActionType.Assassination ? targetOfficerId : null,
            OfficerIds = new System.Collections.Generic.List<int> { _spySelectedOfficerId },
            SpyActionType = actionType
        });

        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        if (result.Success)
        {
            _spyDialog?.Hide();
            RefreshSelectedCity();
            return;
        }

        SetSpyWarning(GetLocalizedResultMessage(result));
    }

    private void PopulateSpyTargetOfficerOptions()
    {
        if (_turnManager?.World == null || _spyTargetOfficerOption == null || _localization == null)
        {
            return;
        }

        _spyTargetOfficerOption.Clear();
        _spyTargetOfficerOption.AddItem(_localization.T("ui.none"));
        _spyTargetOfficerOption.SetItemMetadata(0, -1);

        var targetCity = _turnManager.World.GetCity(GetSelectedSpyTargetCityId());
        if (targetCity != null)
        {
            foreach (var officerId in targetCity.OfficerIds)
            {
                var officer = _turnManager.World.GetOfficer(officerId);
                if (officer == null || officer.DeathYear > 0 && _turnManager.World.Year >= officer.DeathYear)
                {
                    continue;
                }

                _spyTargetOfficerOption.AddItem($"{_localization.GetOfficerName(officer)} | {_localization.GetOfficerRole(officer)}");
                _spyTargetOfficerOption.SetItemMetadata(_spyTargetOfficerOption.ItemCount - 1, officer.Id);
            }
        }

        var isAssassination = GetSelectedSpyActionType() == SpyActionType.Assassination;
        _spyTargetOfficerOption.Disabled = !isAssassination;
        if (isAssassination && _spyTargetOfficerOption.ItemCount > 1)
        {
            _spyTargetOfficerOption.Select(1);
        }
        else
        {
            _spyTargetOfficerOption.Select(0);
        }
    }

    private int GetSelectedSpyTargetOfficerId()
    {
        if (_spyTargetOfficerOption == null || _spyTargetOfficerOption.ItemCount == 0)
        {
            return -1;
        }

        var metadata = _spyTargetOfficerOption.GetItemMetadata(_spyTargetOfficerOption.Selected);
        return metadata.VariantType == Variant.Type.Int ? metadata.AsInt32() : -1;
    }

    private string GetSelectedSpyTargetOfficerName()
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return "-";
        }

        var officer = _turnManager.World.GetOfficer(GetSelectedSpyTargetOfficerId());
        return officer != null ? _localization.GetOfficerName(officer) : "-";
    }

    private void OnSpySelectOfficerPressed()
    {
        if (_localization == null)
        {
            return;
        }

        var candidateOfficerIds = GetAvailableSpyOfficerIds();
        if (candidateOfficerIds.Count == 0)
        {
            SetSpyWarning(_localization.T("ui.select_officer_warning"));
            return;
        }

        ShowOfficerSelectorDialog(
            _localization.T("ui.spy_officer"),
            candidateOfficerIds,
            OfficerSelectorPrimaryStat.Intelligence,
            SelectSpyOfficerById);
    }

    private void SelectSpyOfficerById(int officerId)
    {
        _spySelectedOfficerId = officerId;
        UpdateSpySelectedOfficerSummary();
        UpdateSpyConfirmButtonState();
        SetSpyWarning(string.Empty);
    }

    private void UpdateSpySelectedOfficerSummary()
    {
        if (_spySelectedOfficerLabel == null || _localization == null)
        {
            return;
        }

        var officer = _spySelectedOfficerId > 0 ? _turnManager?.World?.GetOfficer(_spySelectedOfficerId) : null;
        var officerName = officer != null ? _localization.GetOfficerName(officer) : _localization.T("ui.unassigned");
        _spySelectedOfficerLabel.Text = $"{_localization.T("ui.spy_officer")}: {officerName}";
    }

    private System.Collections.Generic.List<int> GetAvailableSpyOfficerIds()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return new System.Collections.Generic.List<int>();
        }

        return _selectedCity.OfficerIds
            .Select(id => _turnManager.World.GetOfficer(id))
            .Where(officer =>
                officer != null &&
                !(officer.LastAssignedYear == _turnManager.World.Year &&
                  officer.LastAssignedMonth == _turnManager.World.Month) &&
                !HasActiveInternalAffairsSchedule(officer.Id))
            .Select(officer => officer!.Id)
            .ToList();
    }
}
