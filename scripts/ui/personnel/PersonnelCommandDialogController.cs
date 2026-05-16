using Godot;

namespace ThreeKingdom.UI;

internal sealed class PersonnelCommandDialogController
{
    private readonly PersonnelUiContext _context;
    private readonly System.Action _showBonusDialog;
    private readonly System.Action _showAssignRoleDialog;
    private readonly System.Action _showFireOfficerDialog;
    private readonly System.Action _showRequestItemDialog;
    private readonly System.Action _showHireOfficerDialog;

    private Window? _dialog;
    private OptionButton? _commandOption;
    private Button? _confirmButton;
    private bool _signalsConnected;

    public PersonnelCommandDialogController(
        PersonnelUiContext context,
        System.Action showBonusDialog,
        System.Action showAssignRoleDialog,
        System.Action showFireOfficerDialog,
        System.Action showRequestItemDialog,
        System.Action showHireOfficerDialog)
    {
        _context = context;
        _showBonusDialog = showBonusDialog;
        _showAssignRoleDialog = showAssignRoleDialog;
        _showFireOfficerDialog = showFireOfficerDialog;
        _showRequestItemDialog = showRequestItemDialog;
        _showHireOfficerDialog = showHireOfficerDialog;
    }

    public void Initialize()
    {
        _dialog = _context.CreateWindow("res://scenes/ui/personnel/PersonnelDialog.tscn", dialog => dialog.Hide());
        EnsureWidgets();
        _dialog.Hide();
    }

    public void Hide() => _dialog?.Hide();

    public void Show()
    {
        if (_dialog == null || _context.Localization == null)
        {
            return;
        }

        EnsureWidgets();
        RefreshText();
        PopulateOptions();
        _context.PopupDialog(_dialog);
    }

    public void RefreshText()
    {
        if (_dialog == null || _context.Localization == null)
        {
            return;
        }

        _dialog.Title = _context.Localization.T("ui.personnel");
        var label = _dialog.GetNodeOrNull<Label>("PersonnelDialogRoot/CommandLabel");
        if (label != null)
        {
            label.Text = _context.Localization.T("ui.personnel_command");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_personnel");
        }
    }

    private void EnsureWidgets()
    {
        if (_dialog == null)
        {
            return;
        }

        var root = _dialog.GetNodeOrNull<VBoxContainer>("PersonnelDialogRoot");
        if (root == null)
        {
            GD.PushError("PersonnelDialogRoot not found in PersonnelDialog.tscn.");
            return;
        }

        _commandOption = root.GetNodeOrNull<OptionButton>("CommandOption");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (!_signalsConnected && _confirmButton != null)
        {
            _confirmButton.Pressed += OnConfirmPressed;
            _signalsConnected = true;
        }
    }

    private void PopulateOptions()
    {
        if (_commandOption == null || _context.Localization == null)
        {
            return;
        }

        _commandOption.Clear();
        AddOption("command.personnel.give_bonus");
        AddOption("command.personnel.assign_title");
        AddOption("command.personnel.fire_officer");
        AddOption("command.personnel.request_item");
        AddOption("command.personnel.hire_officer");
    }

    private void AddOption(string localeKey)
    {
        if (_commandOption == null || _context.Localization == null)
        {
            return;
        }

        _commandOption.AddItem(_context.Localization.T(localeKey));
        _commandOption.SetItemMetadata(_commandOption.ItemCount - 1, localeKey);
    }

    private void OnConfirmPressed()
    {
        if (_context.Localization == null || _commandOption == null)
        {
            return;
        }

        _dialog?.Hide();

        var metadata = _commandOption.GetItemMetadata(_commandOption.Selected);
        var commandKey = metadata.VariantType == Variant.Type.String ? metadata.AsString() : string.Empty;
        switch (commandKey)
        {
            case "command.personnel.give_bonus":
                _showBonusDialog();
                return;
            case "command.personnel.assign_title":
                _showAssignRoleDialog();
                return;
            case "command.personnel.fire_officer":
                _showFireOfficerDialog();
                return;
            case "command.personnel.request_item":
                _showRequestItemDialog();
                return;
            case "command.personnel.hire_officer":
                _showHireOfficerDialog();
                return;
            default:
                _context.AddLog(
                    _context.Localization.Format("log.personnel_command_selected", _commandOption.GetItemText(_commandOption.Selected)),
                    isPlayerRelated: true);
                return;
        }
    }
}
