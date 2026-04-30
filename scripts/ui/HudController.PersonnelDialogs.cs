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
    private void EnsurePersonnelDialogWidgets()
    {
        if (_personnelDialog == null)
        {
            return;
        }

        var existingRoot = _personnelDialog.GetNodeOrNull<VBoxContainer>("PersonnelDialogRoot");
        if (existingRoot != null)
        {
            _personnelCommandOption = existingRoot.GetNodeOrNull<OptionButton>("CommandOption");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "PersonnelDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 130.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _personnelDialog.AddChild(root);
        root.AddChild(new Label { Name = "CommandLabel" });
        _personnelCommandOption = new OptionButton
        {
            Name = "CommandOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_personnelCommandOption);
    }

    private void ShowPersonnelDialog()
    {
        if (_personnelDialog == null || _localization == null)
        {
            return;
        }

        EnsurePersonnelDialogWidgets();
        UpdatePersonnelDialogText();
        PopulatePersonnelDialog();
        _personnelDialog.PopupCentered(new Vector2I(440, 170));
    }

    private void PopulatePersonnelDialog()
    {
        if (_personnelCommandOption == null || _localization == null)
        {
            return;
        }

        _personnelCommandOption.Clear();
        AddPersonnelCommandOption("personnel.command.give_bonus");
        AddPersonnelCommandOption("personnel.command.assign_title");
        AddPersonnelCommandOption("personnel.command.hire_officer");
    }

    private void AddPersonnelCommandOption(string localeKey)
    {
        if (_personnelCommandOption == null || _localization == null)
        {
            return;
        }

        _personnelCommandOption.AddItem(_localization.T(localeKey));
        _personnelCommandOption.SetItemMetadata(_personnelCommandOption.ItemCount - 1, localeKey);
    }

    private void UpdatePersonnelDialogText()
    {
        if (_personnelDialog == null || _localization == null)
        {
            return;
        }

        _personnelDialog.Title = _localization.T("ui.personnel");
        _personnelDialog.OkButtonText = _localization.T("ui.confirm_personnel");
        var label = _personnelDialog.GetNodeOrNull<Label>("PersonnelDialogRoot/CommandLabel");
        if (label != null)
        {
            label.Text = _localization.T("ui.personnel_command");
        }
    }

    private void OnPersonnelDialogConfirmed()
    {
        if (_localization == null || _personnelCommandOption == null)
        {
            return;
        }

        var metadata = _personnelCommandOption.GetItemMetadata(_personnelCommandOption.Selected);
        var commandKey = metadata.VariantType == Variant.Type.String ? metadata.AsString() : string.Empty;
        if (commandKey == "personnel.command.give_bonus")
        {
            ShowPersonnelBonusDialog();
            return;
        }

        if (commandKey == "personnel.command.assign_title")
        {
            ShowAssignRoleDialog();
            return;
        }

        if (commandKey == "personnel.command.hire_officer")
        {
            ShowHireOfficerDialog();
            return;
        }

        AddLog(_localization.Format("log.personnel_command_selected", _personnelCommandOption.GetItemText(_personnelCommandOption.Selected)));
    }

    private void EnsurePersonnelBonusDialogWidgets()
    {
        if (_personnelBonusDialog == null)
        {
            return;
        }

        var existingRoot = _personnelBonusDialog.GetNodeOrNull<VBoxContainer>("PersonnelBonusDialogRoot");
        if (existingRoot != null)
        {
            _personnelBonusOfficerList = existingRoot.GetNodeOrNull<ItemList>("OfficerList");
            _personnelBonusGoldSpinBox = existingRoot.GetNodeOrNull<SpinBox>("GoldSpinBox");
            _personnelBonusFoodSpinBox = existingRoot.GetNodeOrNull<SpinBox>("FoodSpinBox");
            _personnelBonusSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "PersonnelBonusDialogRoot",
            CustomMinimumSize = new Vector2(460.0f, 360.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _personnelBonusDialog.AddChild(root);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _personnelBonusOfficerList = new ItemList
        {
            Name = "OfficerList",
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0.0f, 150.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_personnelBonusOfficerList);

        root.AddChild(new Label { Name = "GoldLabel" });
        _personnelBonusGoldSpinBox = CreateMoveSpinBox("GoldSpinBox");
        _personnelBonusGoldSpinBox.Step = 100;
        _personnelBonusGoldSpinBox.ValueChanged += _ => UpdatePersonnelBonusSummary();
        root.AddChild(_personnelBonusGoldSpinBox);

        root.AddChild(new Label { Name = "FoodLabel" });
        _personnelBonusFoodSpinBox = CreateMoveSpinBox("FoodSpinBox");
        _personnelBonusFoodSpinBox.Step = 500;
        _personnelBonusFoodSpinBox.ValueChanged += _ => UpdatePersonnelBonusSummary();
        root.AddChild(_personnelBonusFoodSpinBox);

        _personnelBonusSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_personnelBonusSummaryLabel);
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
        _personnelBonusDialog.PopupCentered(new Vector2I(480, 400));
    }

    private void PopulatePersonnelBonusDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _personnelBonusOfficerList == null)
        {
            return;
        }

        _personnelBonusOfficerList.Clear();
        foreach (var officerId in _selectedCity.OfficerIds)
        {
            var officer = _turnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (IsFactionRuler(_turnManager.World, officer))
            {
                continue;
            }

            var itemIndex = _personnelBonusOfficerList.AddItem(BuildPersonnelBonusOfficerRowText(officer));
            _personnelBonusOfficerList.SetItemMetadata(itemIndex, officer.Id);
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

        UpdatePersonnelBonusSummary();
    }

    private void UpdatePersonnelBonusDialogText()
    {
        if (_personnelBonusDialog == null || _localization == null)
        {
            return;
        }

        _personnelBonusDialog.Title = _localization.T("personnel.command.give_bonus");
        _personnelBonusDialog.OkButtonText = _localization.T("ui.confirm_personnel_bonus");
        SetPersonnelBonusDialogLabelText("OfficerListLabel", _localization.T("ui.personnel_bonus_officer"));
        SetPersonnelBonusDialogLabelText("GoldLabel", _localization.T("ui.personnel_bonus_gold"));
        SetPersonnelBonusDialogLabelText("FoodLabel", _localization.T("ui.personnel_bonus_food"));
    }

    private string BuildPersonnelBonusOfficerRowText(OfficerData officer)
    {
        var officerName = _localization?.GetOfficerName(officer) ?? officer.Name;
        var roleName = _localization?.GetOfficerRole(officer) ?? officer.Role;
        var loyaltyLabel = _localization?.T("ui.loyalty") ?? "Loyalty";
        return $"{officerName} | {roleName} | {loyaltyLabel} {officer.Loyalty}";
    }

    private void SetPersonnelBonusDialogLabelText(string nodeName, string text)
    {
        var label = _personnelBonusDialog?.GetNodeOrNull<Label>($"PersonnelBonusDialogRoot/{nodeName}");
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
        _personnelBonusSummaryLabel.Text = _localization.Format("fmt.personnel_bonus_preview", gain);
    }

    private void OnPersonnelBonusDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var selectedOfficerIds = GetSelectedItemMetadataIds(_personnelBonusOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenPersonnelBonusDialog();
            return;
        }

        var result = _commandResolver.ExecutePersonnelBonus(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            selectedOfficerIds[0],
            (int)(_personnelBonusGoldSpinBox?.Value ?? 0),
            (int)(_personnelBonusFoodSpinBox?.Value ?? 0));
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
    }

    private void ReopenPersonnelBonusDialog()
    {
        CallDeferred(nameof(ReopenPersonnelBonusDialogDeferred));
    }

    private void ReopenPersonnelBonusDialogDeferred()
    {
        _personnelBonusDialog?.PopupCentered(new Vector2I(480, 400));
    }

    private void EnsureAssignRoleDialogWidgets()
    {
        if (_assignRoleDialog == null)
        {
            return;
        }

        var existingRoot = _assignRoleDialog.GetNodeOrNull<VBoxContainer>("AssignRoleDialogRoot");
        if (existingRoot != null)
        {
            _assignRoleOfficerList = existingRoot.GetNodeOrNull<ItemList>("OfficerList");
            _assignRoleOption = existingRoot.GetNodeOrNull<OptionButton>("RoleOption");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "AssignRoleDialogRoot",
            CustomMinimumSize = new Vector2(460.0f, 330.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _assignRoleDialog.AddChild(root);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _assignRoleOfficerList = new ItemList
        {
            Name = "OfficerList",
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0.0f, 160.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_assignRoleOfficerList);

        root.AddChild(new Label { Name = "RoleLabel" });
        _assignRoleOption = new OptionButton
        {
            Name = "RoleOption",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_assignRoleOption);
    }

    private void ShowAssignRoleDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _assignRoleDialog == null || _localization == null)
        {
            return;
        }

        EnsureAssignRoleDialogWidgets();
        UpdateAssignRoleDialogText();
        PopulateAssignRoleDialog();
        _assignRoleDialog.PopupCentered(new Vector2I(480, 370));
    }

    private void PopulateAssignRoleDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null)
        {
            return;
        }

        if (_assignRoleOfficerList != null)
        {
            _assignRoleOfficerList.Clear();
            foreach (var officerId in _selectedCity.OfficerIds)
            {
                var officer = _turnManager.World.GetOfficer(officerId);
                if (officer == null || IsFactionRuler(_turnManager.World, officer))
                {
                    continue;
                }

                var itemIndex = _assignRoleOfficerList.AddItem(BuildAssignRoleOfficerRowText(officer));
                _assignRoleOfficerList.SetItemMetadata(itemIndex, officer.Id);
            }
        }

        if (_assignRoleOption != null)
        {
            _assignRoleOption.Clear();
            AddAssignRoleOption("General");
            AddAssignRoleOption("Strategist");
            AddAssignRoleOption("Advisor");
            AddAssignRoleOption("Governor");
        }
    }

    private void AddAssignRoleOption(string role)
    {
        if (_assignRoleOption == null || _localization == null)
        {
            return;
        }

        _assignRoleOption.AddItem(GetRoleDisplayName(role));
        _assignRoleOption.SetItemMetadata(_assignRoleOption.ItemCount - 1, role);
    }

    private void UpdateAssignRoleDialogText()
    {
        if (_assignRoleDialog == null || _localization == null)
        {
            return;
        }

        _assignRoleDialog.Title = _localization.T("personnel.command.assign_title");
        _assignRoleDialog.OkButtonText = _localization.T("ui.confirm_assign_role");
        SetAssignRoleDialogLabelText("OfficerListLabel", _localization.T("ui.assign_role_officer"));
        SetAssignRoleDialogLabelText("RoleLabel", _localization.T("ui.assign_role_title"));
    }

    private void SetAssignRoleDialogLabelText(string nodeName, string text)
    {
        var label = _assignRoleDialog?.GetNodeOrNull<Label>($"AssignRoleDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private string BuildAssignRoleOfficerRowText(OfficerData officer)
    {
        var officerName = _localization?.GetOfficerName(officer) ?? officer.Name;
        var roleName = _localization?.GetOfficerRole(officer) ?? officer.Role;
        return $"{officerName} | {roleName}";
    }

    private string GetRoleDisplayName(string role)
    {
        if (_localization == null)
        {
            return role;
        }

        return role.ToLowerInvariant() switch
        {
            "general" => _localization.T("role.general"),
            "strategist" => _localization.T("role.strategist"),
            "advisor" => _localization.T("role.advisor"),
            "governor" => _localization.T("role.governor"),
            _ => role
        };
    }

    private void OnAssignRoleDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var selectedOfficerIds = GetSelectedItemMetadataIds(_assignRoleOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenAssignRoleDialog();
            return;
        }

        var roleMetadata = _assignRoleOption?.GetItemMetadata(_assignRoleOption.Selected);
        var role = roleMetadata?.VariantType == Variant.Type.String ? roleMetadata.Value.AsString() : "General";
        var result = _commandResolver.ExecuteAssignOfficerRole(
            _turnManager.GetPlayerFactionId(),
            _selectedCity.Id,
            selectedOfficerIds[0],
            role);
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
    }

    private void ReopenAssignRoleDialog()
    {
        CallDeferred(nameof(ReopenAssignRoleDialogDeferred));
    }

    private void ReopenAssignRoleDialogDeferred()
    {
        _assignRoleDialog?.PopupCentered(new Vector2I(480, 370));
    }

    private void EnsureHireOfficerDialogWidgets()
    {
        if (_hireOfficerDialog == null)
        {
            return;
        }

        var existingRoot = _hireOfficerDialog.GetNodeOrNull<VBoxContainer>("HireOfficerDialogRoot");
        if (existingRoot != null)
        {
            _hireOfficerList = existingRoot.GetNodeOrNull<ItemList>("OfficerList");
            _hireOfficerSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "HireOfficerDialogRoot",
            CustomMinimumSize = new Vector2(560.0f, 360.0f)
        };
        root.AddThemeConstantOverride("separation", 8);
        _hireOfficerDialog.AddChild(root);

        root.AddChild(new Label { Name = "OfficerListLabel" });
        _hireOfficerList = new ItemList
        {
            Name = "OfficerList",
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0.0f, 220.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_hireOfficerList);

        _hireOfficerSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_hireOfficerSummaryLabel);
    }

    private void ShowHireOfficerDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _hireOfficerDialog == null || _localization == null)
        {
            return;
        }

        EnsureHireOfficerDialogWidgets();
        UpdateHireOfficerDialogText();
        PopulateHireOfficerDialog();
        _hireOfficerDialog.PopupCentered(new Vector2I(580, 400));
    }

    private void PopulateHireOfficerDialog()
    {
        if (_selectedCity == null || _turnManager?.World == null || _hireOfficerList == null || _localization == null)
        {
            return;
        }

        _hireOfficerList.Clear();
        var playerFactionId = _turnManager.GetPlayerFactionId();
        foreach (var officer in _turnManager.World.Officers.OrderBy(officer => _localization.GetOfficerName(officer)))
        {
            if (!IsHireOfficerCandidate(_turnManager.World, playerFactionId, officer))
            {
                continue;
            }

            var itemIndex = _hireOfficerList.AddItem(BuildHireOfficerRowText(officer));
            _hireOfficerList.SetItemMetadata(itemIndex, officer.Id);
        }

        if (_hireOfficerList.ItemCount == 0)
        {
            _hireOfficerList.AddItem(_localization.T("ui.no_hireable_officer"));
            _hireOfficerList.SetItemDisabled(0, true);
        }

        if (_hireOfficerSummaryLabel != null)
        {
            _hireOfficerSummaryLabel.Text = _localization.Format("fmt.hire_officer_preview", HireOfficerGoldCost);
        }
    }

    private void UpdateHireOfficerDialogText()
    {
        if (_hireOfficerDialog == null || _localization == null)
        {
            return;
        }

        _hireOfficerDialog.Title = _localization.T("personnel.command.hire_officer");
        _hireOfficerDialog.OkButtonText = _localization.T("ui.confirm_hire_officer");
        var label = _hireOfficerDialog.GetNodeOrNull<Label>("HireOfficerDialogRoot/OfficerListLabel");
        if (label != null)
        {
            label.Text = _localization.T("ui.hire_officer_target");
        }
    }

    private string BuildHireOfficerRowText(OfficerData officer)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return officer.Name;
        }

        var sourceCity = officer.CityId > 0 ? _turnManager.World.GetCity(officer.CityId) : null;
        var sourceCityName = sourceCity != null ? _localization.GetCityName(sourceCity) : _localization.T("ui.none");
        var sourceFactionName = sourceCity != null ? _localization.GetFactionName(_turnManager.World, sourceCity.OwnerFactionId) : _localization.T("ui.none");
        return _localization.Format(
            "fmt.hire_officer_row",
            _localization.GetOfficerName(officer),
            _localization.GetOfficerRole(officer),
            officer.Loyalty,
            sourceCityName,
            sourceFactionName);
    }

    private static bool IsHireOfficerCandidate(WorldState world, int playerFactionId, OfficerData officer)
    {
        if (IsFactionRuler(world, officer))
        {
            return false;
        }

        if (!IsOfficerOldEnoughToJoin(world, officer))
        {
            return false;
        }

        var sourceCity = officer.CityId > 0 ? world.GetCity(officer.CityId) : null;
        return sourceCity == null || sourceCity.OwnerFactionId != playerFactionId;
    }

    private void OnHireOfficerDialogConfirmed()
    {
        if (_selectedCity == null || _turnManager == null || _commandResolver == null)
        {
            return;
        }

        var selectedOfficerIds = GetSelectedItemMetadataIds(_hireOfficerList);
        if (selectedOfficerIds.Count == 0)
        {
            AddLog(_localization?.T("ui.select_officer_warning") ?? string.Empty);
            ReopenHireOfficerDialog();
            return;
        }

        var result = _commandResolver.ExecuteHireOfficer(_turnManager.GetPlayerFactionId(), _selectedCity.Id, selectedOfficerIds[0]);
        AddLog(GetLocalizedResultMessage(result));
        RefreshSelectedCity();
        _mapController?.RefreshVisuals();
    }

    private void ReopenHireOfficerDialog()
    {
        CallDeferred(nameof(ReopenHireOfficerDialogDeferred));
    }

    private void ReopenHireOfficerDialogDeferred()
    {
        _hireOfficerDialog?.PopupCentered(new Vector2I(580, 400));
    }


}
