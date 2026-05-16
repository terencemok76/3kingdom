using Godot;

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
        if (existingRoot == null)
        {
            GD.PushError("PersonnelDialogRoot not found in PersonnelDialog.tscn.");
            return;
        }

        _personnelCommandOption = existingRoot.GetNodeOrNull<OptionButton>("CommandOption");
        _personnelConfirmButton = existingRoot.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (!_personnelDialogSignalsConnected && _personnelConfirmButton != null)
        {
            _personnelConfirmButton.Pressed += OnPersonnelDialogConfirmed;
            _personnelDialogSignalsConnected = true;
        }
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
        PopupDialogUsingSceneSize(_personnelDialog);
    }

    private void PopulatePersonnelDialog()
    {
        if (_personnelCommandOption == null || _localization == null)
        {
            return;
        }

        _personnelCommandOption.Clear();
        AddPersonnelCommandOption("command.personnel.give_bonus");
        AddPersonnelCommandOption("command.personnel.assign_title");
        AddPersonnelCommandOption("command.personnel.fire_officer");
        AddPersonnelCommandOption("command.personnel.request_item");
        AddPersonnelCommandOption("command.personnel.hire_officer");
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
        var label = _personnelDialog.GetNodeOrNull<Label>("PersonnelDialogRoot/CommandLabel");
        if (label != null)
        {
            label.Text = _localization.T("ui.personnel_command");
        }
        if (_personnelConfirmButton != null)
        {
            _personnelConfirmButton.Text = _localization.T("ui.confirm_personnel");
        }
    }

    private void OnPersonnelDialogConfirmed()
    {
        if (_localization == null || _personnelCommandOption == null)
        {
            return;
        }

        _personnelDialog?.Hide();

        var metadata = _personnelCommandOption.GetItemMetadata(_personnelCommandOption.Selected);
        var commandKey = metadata.VariantType == Variant.Type.String ? metadata.AsString() : string.Empty;
        if (commandKey == "command.personnel.give_bonus")
        {
            ShowPersonnelBonusDialog();
            return;
        }

        if (commandKey == "command.personnel.assign_title")
        {
            ShowAssignRoleDialog();
            return;
        }

        if (commandKey == "command.personnel.fire_officer")
        {
            ShowFireOfficerDialog();
            return;
        }

        if (commandKey == "command.personnel.request_item")
        {
            ShowRequestItemDialog();
            return;
        }

        if (commandKey == "command.personnel.hire_officer")
        {
            ShowHireOfficerDialog();
            return;
        }

        AddLog(_localization.Format("log.personnel_command_selected", _personnelCommandOption.GetItemText(_personnelCommandOption.Selected)), isPlayerRelated: true);
    }
}
