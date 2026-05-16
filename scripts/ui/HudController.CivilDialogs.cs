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
    private void EnsureCivilDialogWidgets()
    {
        if (_civilDialog == null)
        {
            return;
        }

        var existingRoot = _civilDialog.GetNodeOrNull<VBoxContainer>("CivilDialogRoot");
        if (existingRoot != null)
        {
            _civilCommandOption = existingRoot.GetNodeOrNull<OptionButton>("CommandOption");
            _civilConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
            if (!_civilDialogSignalsConnected && _civilConfirmButton != null)
            {
                _civilConfirmButton.Pressed += OnCivilDialogConfirmed;
                _civilDialogSignalsConnected = true;
            }
            return;
        }

        var root = new VBoxContainer
        {
            Name = "CivilDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 120.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _civilDialog.AddChild(root);
        root.AddChild(new Label { Name = "CommandLabel" });
        _civilCommandOption = new OptionButton
        {
            Name = "CommandOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_civilCommandOption);

        var confirmRow = new HBoxContainer
        {
            Name = "ConfirmRow",
            Alignment = BoxContainer.AlignmentMode.Center
        };
        _civilConfirmButton = new Button
        {
            Name = "ConfirmButton",
            FocusMode = Control.FocusModeEnum.None
        };
        _civilConfirmButton.Pressed += OnCivilDialogConfirmed;
        _civilDialogSignalsConnected = true;
        confirmRow.AddChild(_civilConfirmButton);
        root.AddChild(confirmRow);
    }

    private void ShowCivilDialog()
    {
        if (_civilDialog == null || _localization == null)
        {
            return;
        }

        EnsureCivilDialogWidgets();
        UpdateCivilDialogText();
        PopulateCivilDialog();
        PopupDialogUsingSceneSize(_civilDialog);
    }

    private void PopulateCivilDialog()
    {
        if (_civilCommandOption == null || _localization == null || _turnManager?.World == null || _selectedCity == null)
        {
            return;
        }

        _civilCommandOption.Clear();
        var world = _turnManager.World;
        var reliefUsed = _selectedCity.LastCivilReliefYear == world.Year && _selectedCity.LastCivilReliefMonth == world.Month;
        var investigateUsed = _selectedCity.LastSearchYear == world.Year && _selectedCity.LastSearchMonth == world.Month;

        AddCivilCommandOption("command.civil.relief", reliefUsed);
        AddCivilCommandOption("command.civil.investigate_people", investigateUsed);
        SelectFirstEnabledCivilCommandOption();
    }

    private void AddCivilCommandOption(string localeKey, bool disabled)
    {
        if (_civilCommandOption == null || _localization == null)
        {
            return;
        }

        var text = disabled
            ? _localization.Format("fmt.command_used_this_month", _localization.T(localeKey))
            : _localization.T(localeKey);
        _civilCommandOption.AddItem(text);
        var index = _civilCommandOption.ItemCount - 1;
        _civilCommandOption.SetItemMetadata(index, localeKey);
        _civilCommandOption.SetItemDisabled(index, disabled);
    }

    private void SelectFirstEnabledCivilCommandOption()
    {
        if (_civilCommandOption == null)
        {
            return;
        }

        for (var index = 0; index < _civilCommandOption.ItemCount; index += 1)
        {
            if (_civilCommandOption.IsItemDisabled(index))
            {
                continue;
            }

            _civilCommandOption.Select(index);
            return;
        }
    }

    private void UpdateCivilDialogText()
    {
        if (_civilDialog == null || _localization == null)
        {
            return;
        }

        _civilDialog.Title = _localization.T("ui.civil");
        var label = _civilDialog.GetNodeOrNull<Label>("CivilDialogRoot/CommandLabel");
        if (label != null)
        {
            label.Text = _localization.T("ui.civil_command");
        }

        if (_civilConfirmButton != null)
        {
            _civilConfirmButton.Text = _localization.T("ui.confirm_civil");
        }
    }

    private void OnCivilDialogConfirmed()
    {
        if (_localization == null || _civilCommandOption == null)
        {
            return;
        }

        _civilDialog?.Hide();

        var metadata = _civilCommandOption.GetItemMetadata(_civilCommandOption.Selected);
        var commandKey = metadata.VariantType == Variant.Type.String ? metadata.AsString() : string.Empty;
        if (commandKey == "command.civil.relief")
        {
            ShowCivilReliefDialog();
            return;
        }

        if (commandKey == "command.civil.investigate_people")
        {
            ShowVisitCitizenDialog();
            return;
        }

        AddLog(_localization.Format("log.civil_command_selected", _civilCommandOption.GetItemText(_civilCommandOption.Selected)), isPlayerRelated: true);
    }

    private void EnsureCivilReliefDialogWidgets()
    {
        if (_civilReliefDialog == null)
        {
            return;
        }

        var existingRoot = _civilReliefDialog.GetNodeOrNull<VBoxContainer>("CivilReliefDialogRoot");
        if (existingRoot == null)
        {
            GD.PushError("CivilReliefDialogRoot not found in CivilReliefDialog.tscn.");
            return;
        }

        _civilReliefSelectedOfficerLabel = existingRoot.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _civilReliefSelectOfficerButton = existingRoot.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _civilReliefGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldRow/GoldSpinBox");
        _civilReliefFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
        _civilReliefSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
        _civilReliefConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (!_civilReliefDialogSignalsConnected)
        {
            if (_civilReliefSelectOfficerButton != null)
            {
                _civilReliefSelectOfficerButton.Pressed += OnCivilReliefSelectOfficerPressed;
            }
            if (_civilReliefGoldSpinBox != null)
            {
                _civilReliefGoldSpinBox.ValueChanged += _ =>
                {
                    UpdateCivilReliefSummary();
                    UpdateCivilReliefConfirmButtonState();
                };
            }
            if (_civilReliefFoodSpinBox != null)
            {
                _civilReliefFoodSpinBox.ValueChanged += _ =>
                {
                    UpdateCivilReliefSummary();
                    UpdateCivilReliefConfirmButtonState();
                };
            }
            if (_civilReliefConfirmButton != null)
            {
                _civilReliefConfirmButton.Pressed += OnCivilReliefDialogConfirmed;
            }
            _civilReliefDialogSignalsConnected = true;
        }
    }

    private void ShowCivilReliefDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _civilReliefDialog == null || _localization == null)
        {
            return;
        }

        EnsureCivilReliefDialogWidgets();
        UpdateCivilReliefDialogText();
        PopulateCivilReliefDialog();
        PopupDialogUsingSceneSize(_civilReliefDialog);
    }

    private void PopulateCivilReliefDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return;
        }

        var availableOfficerIds = _selectedCity.OfficerIds
            .Where(GetAvailableOfficerIdsForOrder().Contains)
            .ToList();
        if (!availableOfficerIds.Contains(_civilReliefSelectedOfficerId))
        {
            _civilReliefSelectedOfficerId = availableOfficerIds.FirstOrDefault();
        }

        ConfigureMoveSpinBox(_civilReliefGoldSpinBox, _selectedCity.Gold, 0);
        ConfigureMoveSpinBox(_civilReliefFoodSpinBox, _selectedCity.Food, 0);
        if (_civilReliefGoldSpinBox != null)
        {
            _civilReliefGoldSpinBox.Step = 100;
        }
        if (_civilReliefFoodSpinBox != null)
        {
            _civilReliefFoodSpinBox.Step = 1000;
        }

        UpdateCivilReliefSummary();
        UpdateCivilReliefSelectedOfficerSummary();
        UpdateCivilReliefConfirmButtonState();
    }

    private void UpdateCivilReliefDialogText()
    {
        if (_civilReliefDialog == null || _localization == null)
        {
            return;
        }

        _civilReliefDialog.Title = _localization.T("command.civil.relief");
        SetCivilReliefDialogLabelText("OfficerListLabel", _localization.T("ui.civil_relief_officer"));
        SetCivilReliefDialogLabelText("GoldLabel", _localization.T("ui.civil_relief_gold"));
        SetCivilReliefDialogLabelText("FoodLabel", _localization.T("ui.civil_relief_food"));
        if (_civilReliefSelectOfficerButton != null)
        {
            _civilReliefSelectOfficerButton.Text = _localization.T("ui.select_officer");
        }
        if (_civilReliefConfirmButton != null)
        {
            _civilReliefConfirmButton.Text = _localization.T("ui.confirm_civil_relief");
        }
        UpdateCivilReliefSelectedOfficerSummary();
    }

    private void SetCivilReliefDialogLabelText(string nodeName, string text)
    {
        var label = _civilReliefDialog?.GetNodeOrNull<Label>($"CivilReliefDialogRoot/{nodeName}") ??
                    _civilReliefDialog?.GetNodeOrNull<Label>($"CivilReliefDialogRoot/GoldRow/{nodeName}") ??
                    _civilReliefDialog?.GetNodeOrNull<Label>($"CivilReliefDialogRoot/FoodRow/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void UpdateCivilReliefSummary()
    {
        if (_civilReliefSummaryLabel == null || _civilReliefGoldSpinBox == null || _civilReliefFoodSpinBox == null || _localization == null)
        {
            return;
        }

        var gold = (int)_civilReliefGoldSpinBox.Value;
        var food = (int)_civilReliefFoodSpinBox.Value;
        var gain = gold / 100 * 10 + food / 1000 * 10;
        _civilReliefSummaryLabel.Text = _localization.Format("fmt.civil_relief_preview", gain);
    }

    private void UpdateCivilReliefConfirmButtonState()
    {
        if (_civilReliefConfirmButton == null)
        {
            return;
        }

        var hasOfficer = _civilReliefSelectedOfficerId > 0;
        var gold = (int)(_civilReliefGoldSpinBox?.Value ?? 0);
        var food = (int)(_civilReliefFoodSpinBox?.Value ?? 0);
        var hasReliefAmount = gold > 0 || food > 0;
        var effectiveGain = gold / 100 * 10 + food / 1000 * 10;
        _civilReliefConfirmButton.Disabled = !hasOfficer || !hasReliefAmount || effectiveGain <= 0;
    }

    private void OnCivilReliefDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        if (_civilReliefSelectedOfficerId <= 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenCivilReliefDialog();
            return;
        }

        var result = _commandResolver.ExecuteCivilRelief(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            _civilReliefSelectedOfficerId,
            (int)(_civilReliefGoldSpinBox?.Value ?? 0),
            (int)(_civilReliefFoodSpinBox?.Value ?? 0));
        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
        _civilReliefDialog?.Hide();
    }

    private void ReopenCivilReliefDialog()
    {
        CallDeferred(nameof(ReopenCivilReliefDialogDeferred));
    }

    private void ReopenCivilReliefDialogDeferred()
    {
        PopupDialogUsingSceneSize(_civilReliefDialog);
    }

    private void EnsureVisitCitizenDialogWidgets()
    {
        if (_visitCitizenDialog == null)
        {
            return;
        }

        var existingRoot = _visitCitizenDialog.GetNodeOrNull<VBoxContainer>("VisitCitizenDialogRoot");
        if (existingRoot == null)
        {
            GD.PushError("VisitCitizenDialogRoot not found in VisitCitizenDialog.tscn.");
            return;
        }

        _visitCitizenSelectedOfficerLabel = existingRoot.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _visitCitizenSelectOfficerButton = existingRoot.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _visitCitizenConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (!_visitCitizenDialogSignalsConnected)
        {
            if (_visitCitizenSelectOfficerButton != null)
            {
                _visitCitizenSelectOfficerButton.Pressed += OnVisitCitizenSelectOfficerPressed;
            }

            if (_visitCitizenConfirmButton != null)
            {
                _visitCitizenConfirmButton.Pressed += OnVisitCitizenDialogConfirmed;
            }

            _visitCitizenDialogSignalsConnected = true;
        }
    }

    private void ShowVisitCitizenDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _visitCitizenDialog == null || _localization == null)
        {
            return;
        }

        var candidateOfficerIds = _selectedCity.OfficerIds
            .Where(GetAvailableOfficerIdsForOrder().Contains)
            .ToList();
        if (candidateOfficerIds.Count == 0)
        {
            AddLog(_localization.Format("ui.no_available_officer_for_command", GetCommandName(CommandType.Search)));
            return;
        }

        EnsureVisitCitizenDialogWidgets();
        UpdateVisitCitizenDialogText();
        PopulateVisitCitizenDialog(candidateOfficerIds);
        PopupDialogUsingSceneSize(_visitCitizenDialog);
    }

    private void PopulateVisitCitizenDialog(System.Collections.Generic.List<int> candidateOfficerIds)
    {
        if (!candidateOfficerIds.Contains(_visitCitizenSelectedOfficerId))
        {
            _visitCitizenSelectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        UpdateVisitCitizenSelectedOfficerSummary();
    }

    private void UpdateVisitCitizenDialogText()
    {
        if (_visitCitizenDialog == null || _localization == null)
        {
            return;
        }

        _visitCitizenDialog.Title = _localization.T("command.civil.investigate_people");
        var officerLabel = _visitCitizenDialog.GetNodeOrNull<Label>("VisitCitizenDialogRoot/OfficerListLabel");
        if (officerLabel != null)
        {
            officerLabel.Text = _localization.T("ui.officers");
        }

        if (_visitCitizenSelectOfficerButton != null)
        {
            _visitCitizenSelectOfficerButton.Text = _localization.T("ui.select_officer");
        }

        if (_visitCitizenConfirmButton != null)
        {
            _visitCitizenConfirmButton.Text = _localization.T("ui.confirm_officer_selection");
        }

        UpdateVisitCitizenSelectedOfficerSummary();
    }

    private void OnVisitCitizenSelectOfficerPressed()
    {
        if (_selectedCity == null || _localization == null)
        {
            return;
        }

        var candidateOfficerIds = _selectedCity.OfficerIds
            .Where(GetAvailableOfficerIdsForOrder().Contains)
            .ToList();
        if (candidateOfficerIds.Count == 0)
        {
            AddLog(_localization.Format("ui.no_available_officer_for_command", GetCommandName(CommandType.Search)));
            return;
        }

        ShowOfficerSelectorDialog(
            _localization.T("command.civil.investigate_people"),
            candidateOfficerIds,
            OfficerSelectorPrimaryStat.Charm,
            SelectVisitCitizenOfficerById);
    }

    private void SelectVisitCitizenOfficerById(int officerId)
    {
        _visitCitizenSelectedOfficerId = officerId;
        UpdateVisitCitizenSelectedOfficerSummary();
    }

    private void UpdateVisitCitizenSelectedOfficerSummary()
    {
        if (_visitCitizenSelectedOfficerLabel == null || _localization == null)
        {
            return;
        }

        var officer = _visitCitizenSelectedOfficerId > 0 ? _turnManager?.World?.GetOfficer(_visitCitizenSelectedOfficerId) : null;
        var officerName = officer != null ? _localization.GetOfficerName(officer) : _localization.T("ui.unassigned");
        _visitCitizenSelectedOfficerLabel.Text = $"{_localization.T("ui.officers")}: {officerName}";
    }

    private void OnVisitCitizenDialogConfirmed()
    {
        if (_localization == null)
        {
            return;
        }

        if (_visitCitizenSelectedOfficerId <= 0)
        {
            AddLog(_localization.T("ui.select_officer_warning"));
            ReopenVisitCitizenDialog();
            return;
        }

        var result = ExecutePlayerCommand(
            CommandType.Search,
            officerIds: new System.Collections.Generic.List<int> { _visitCitizenSelectedOfficerId });
        if (result.Success)
        {
            _visitCitizenDialog?.Hide();
        }
    }

    private void ReopenVisitCitizenDialog()
    {
        CallDeferred(nameof(ReopenVisitCitizenDialogDeferred));
    }

    private void ReopenVisitCitizenDialogDeferred()
    {
        PopupDialogUsingSceneSize(_visitCitizenDialog);
    }

    private void OnCivilReliefSelectOfficerPressed()
    {
        if (_selectedCity == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        var candidateOfficerIds = _selectedCity.OfficerIds
            .Where(GetAvailableOfficerIdsForOrder().Contains)
            .ToList();
        if (candidateOfficerIds.Count == 0)
        {
            AddLog(_localization.T("ui.select_officer_warning"));
            return;
        }

        ShowOfficerSelectorDialog(
            _localization.T("ui.civil_relief_officer"),
            candidateOfficerIds,
            OfficerSelectorPrimaryStat.Charm,
            SelectCivilReliefOfficerById);
    }

    private void SelectCivilReliefOfficerById(int officerId)
    {
        _civilReliefSelectedOfficerId = officerId;
        UpdateCivilReliefSelectedOfficerSummary();
        UpdateCivilReliefConfirmButtonState();
    }

    private void UpdateCivilReliefSelectedOfficerSummary()
    {
        if (_civilReliefSelectedOfficerLabel == null || _localization == null)
        {
            return;
        }

        var officer = _civilReliefSelectedOfficerId > 0 ? _turnManager?.World?.GetOfficer(_civilReliefSelectedOfficerId) : null;
        var officerName = officer != null ? _localization.GetOfficerName(officer) : _localization.T("ui.unassigned");
        _civilReliefSelectedOfficerLabel.Text = $"{_localization.T("ui.civil_relief_officer")}: {officerName}";
    }


}
