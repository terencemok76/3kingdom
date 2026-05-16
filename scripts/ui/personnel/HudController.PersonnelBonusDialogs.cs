using System;
using System.Linq;
using Godot;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private void EnsurePersonnelBonusDialogWidgets()
    {
        if (_personnelBonusDialog == null)
        {
            return;
        }

        var existingRoot = _personnelBonusDialog.GetNodeOrNull<VBoxContainer>("PersonnelBonusDialogRoot");
        if (existingRoot == null)
        {
            GD.PushError("PersonnelBonusDialogRoot not found in PersonnelBonusDialog.tscn.");
            return;
        }

        _personnelBonusSelectedOfficerLabel = existingRoot.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _personnelBonusSelectOfficerButton = existingRoot.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _personnelBonusGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldRow/GoldSpinBox");
        _personnelBonusFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodRow/FoodSpinBox");
        _personnelBonusItemOption = existingRoot.GetNodeOrNull<OptionButton>("ItemRow/ItemOption");
        _personnelBonusSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
        _personnelBonusConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (!_personnelBonusDialogSignalsConnected)
        {
            if (_personnelBonusSelectOfficerButton != null)
            {
                _personnelBonusSelectOfficerButton.Pressed += OnPersonnelBonusSelectOfficerPressed;
            }
            if (_personnelBonusGoldSpinBox != null)
            {
                _personnelBonusGoldSpinBox.ValueChanged += _ => UpdatePersonnelBonusSummary();
            }
            if (_personnelBonusFoodSpinBox != null)
            {
                _personnelBonusFoodSpinBox.ValueChanged += _ => UpdatePersonnelBonusSummary();
            }
            if (_personnelBonusItemOption != null)
            {
                _personnelBonusItemOption.ItemSelected += _ => UpdatePersonnelBonusSummary();
            }
            if (_personnelBonusConfirmButton != null)
            {
                _personnelBonusConfirmButton.Pressed += OnPersonnelBonusDialogConfirmed;
            }
            _personnelBonusDialogSignalsConnected = true;
        }
    }

    private void ShowPersonnelBonusDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _personnelBonusDialog == null || _localization == null)
        {
            return;
        }

        EnsurePersonnelBonusDialogWidgets();
        UpdatePersonnelBonusDialogText();
        PopulatePersonnelBonusDialog();
        PopupDialogUsingSceneSize(_personnelBonusDialog);
    }

    private void PopulatePersonnelBonusDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return;
        }

        var candidateOfficerIds = _selectedCity.OfficerIds
            .Where(officerId =>
            {
                var officer = _turnManager.World.GetOfficer(officerId);
                return officer != null && !IsFactionRuler(_turnManager.World, officer);
            })
            .ToList();
        if (!candidateOfficerIds.Contains(_personnelBonusSelectedOfficerId))
        {
            _personnelBonusSelectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        ConfigureMoveSpinBox(_personnelBonusGoldSpinBox, _selectedCity.Gold, 0);
        ConfigureMoveSpinBox(_personnelBonusFoodSpinBox, _selectedCity.Food, 0);
        if (_personnelBonusGoldSpinBox != null)
        {
            _personnelBonusGoldSpinBox.Step = 100;
        }
        if (_personnelBonusFoodSpinBox != null)
        {
            _personnelBonusFoodSpinBox.Step = 500;
        }

        PopulateFactionInventoryOption(_personnelBonusItemOption);
        UpdatePersonnelBonusSelectedOfficerSummary();
        UpdatePersonnelBonusSummary();
    }

    private void UpdatePersonnelBonusDialogText()
    {
        if (_personnelBonusDialog == null || _localization == null)
        {
            return;
        }

        _personnelBonusDialog.Title = _localization.T("command.personnel.give_bonus");
        SetPersonnelBonusDialogLabelText("OfficerListLabel", _localization.T("ui.personnel_bonus_officer"));
        SetPersonnelBonusDialogLabelText("GoldLabel", _localization.T("ui.personnel_bonus_gold"));
        SetPersonnelBonusDialogLabelText("FoodLabel", _localization.T("ui.personnel_bonus_food"));
        SetPersonnelBonusDialogLabelText("ItemLabel", _localization.T("ui.personnel_bonus_item"));
        if (_personnelBonusSelectOfficerButton != null)
        {
            _personnelBonusSelectOfficerButton.Text = _localization.T("ui.select_officer");
        }
        if (_personnelBonusConfirmButton != null)
        {
            _personnelBonusConfirmButton.Text = _localization.T("ui.confirm_personnel_bonus");
        }
        UpdatePersonnelBonusSelectedOfficerSummary();
    }

    private void SetPersonnelBonusDialogLabelText(string nodeName, string text)
    {
        var label = _personnelBonusDialog?.GetNodeOrNull<Label>($"PersonnelBonusDialogRoot/{nodeName}") ??
                    _personnelBonusDialog?.GetNodeOrNull<Label>($"PersonnelBonusDialogRoot/GoldRow/{nodeName}") ??
                    _personnelBonusDialog?.GetNodeOrNull<Label>($"PersonnelBonusDialogRoot/FoodRow/{nodeName}") ??
                    _personnelBonusDialog?.GetNodeOrNull<Label>($"PersonnelBonusDialogRoot/ItemRow/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void UpdatePersonnelBonusSummary()
    {
        if (_personnelBonusSummaryLabel == null || _personnelBonusGoldSpinBox == null || _personnelBonusFoodSpinBox == null || _localization == null)
        {
            return;
        }

        var gold = (int)_personnelBonusGoldSpinBox.Value;
        var food = (int)_personnelBonusFoodSpinBox.Value;
        var gain = gold / 100 + food / 500;
        var item = GetSelectedItemFromOption(_personnelBonusItemOption);
        _personnelBonusSummaryLabel.Text = item == null
            ? _localization.Format("fmt.personnel_bonus_preview", gain)
            : _localization.Format("fmt.personnel_bonus_preview_with_item", gain + Math.Max(1, item.LoyaltyBonus), _localization.GetItemName(item));
    }

    private void OnPersonnelBonusDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        if (_personnelBonusSelectedOfficerId <= 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenPersonnelBonusDialog();
            return;
        }

        var result = _commandResolver.ExecutePersonnelBonus(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            _personnelBonusSelectedOfficerId,
            (int)(_personnelBonusGoldSpinBox?.Value ?? 0),
            (int)(_personnelBonusFoodSpinBox?.Value ?? 0),
            GetSelectedItemFromOption(_personnelBonusItemOption)?.Id ?? 0);
        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
        _personnelBonusDialog?.Hide();
    }

    private void ReopenPersonnelBonusDialog()
    {
        CallDeferred(nameof(ReopenPersonnelBonusDialogDeferred));
    }

    private void ReopenPersonnelBonusDialogDeferred()
    {
        PopupDialogUsingSceneSize(_personnelBonusDialog);
    }

    private void OnPersonnelBonusSelectOfficerPressed()
    {
        if (_selectedCity == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        var candidateOfficerIds = _selectedCity.OfficerIds
            .Where(officerId =>
            {
                var officer = _turnManager.World.GetOfficer(officerId);
                return officer != null && !IsFactionRuler(_turnManager.World, officer);
            })
            .ToList();
        if (candidateOfficerIds.Count == 0)
        {
            AddLog(_localization.T("ui.select_officer_warning"));
            return;
        }

        ShowOfficerSelectorDialog(
            _localization.T("ui.personnel_bonus_officer"),
            candidateOfficerIds,
            OfficerSelectorPrimaryStat.Charm,
            SelectPersonnelBonusOfficerById);
    }

    private void SelectPersonnelBonusOfficerById(int officerId)
    {
        _personnelBonusSelectedOfficerId = officerId;
        UpdatePersonnelBonusSelectedOfficerSummary();
    }

    private void UpdatePersonnelBonusSelectedOfficerSummary()
    {
        if (_personnelBonusSelectedOfficerLabel == null || _localization == null)
        {
            return;
        }

        var officer = _personnelBonusSelectedOfficerId > 0 ? _turnManager?.World?.GetOfficer(_personnelBonusSelectedOfficerId) : null;
        var officerName = officer != null ? _localization.GetOfficerName(officer) : _localization.T("ui.unassigned");
        _personnelBonusSelectedOfficerLabel.Text = $"{_localization.T("ui.personnel_bonus_officer")}: {officerName}";
    }
}
