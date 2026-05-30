using Godot;

namespace ThreeKingdom.UI;

internal sealed class PersonnelCommandDialogController : FloatingOverlayController
{
    private readonly PersonnelUiContext _context;
    private readonly System.Action _showBonusDialog;
    private readonly System.Action _showAssignRoleDialog;
    private readonly System.Action _showPrefectAuthorizationDialog;
    private readonly System.Action _showFireOfficerDialog;
    private readonly System.Action _showRequestItemDialog;
    private readonly System.Action _showHireOfficerDialog;

    private OptionButton? _commandOption;
    private Button? _confirmButton;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(440.0f, 150.0f);

    public PersonnelCommandDialogController(
        PersonnelUiContext context,
        System.Action showBonusDialog,
        System.Action showAssignRoleDialog,
        System.Action showPrefectAuthorizationDialog,
        System.Action showFireOfficerDialog,
        System.Action showRequestItemDialog,
        System.Action showHireOfficerDialog)
        : base(context, "res://scenes/ui/personnel/PersonnelDialog.tscn")
    {
        _context = context;
        _showBonusDialog = showBonusDialog;
        _showAssignRoleDialog = showAssignRoleDialog;
        _showPrefectAuthorizationDialog = showPrefectAuthorizationDialog;
        _showFireOfficerDialog = showFireOfficerDialog;
        _showRequestItemDialog = showRequestItemDialog;
        _showHireOfficerDialog = showHireOfficerDialog;
    }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public void Show()
    {
        if (_context.Localization == null)
        {
            return;
        }

        RefreshText();
        PopulateOptions();
        ShowOverlay();
    }

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization.T("ui.personnel"));
        var label = GetOverlayContentNode<Label>("CommandLabel");
        if (label != null) label.Text = _context.Localization.T("ui.personnel_command");
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_personnel");
        }

        RefreshCommandOptionTexts();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _commandOption = root.GetNodeOrNull<OptionButton>("CommandOption");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }
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
        AddOption("command.personnel.prefect_authorization");
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

    private void RefreshCommandOptionTexts()
    {
        if (_commandOption == null || _context.Localization == null)
        {
            return;
        }

        var selectedCommandKey = _commandOption.Selected >= 0
            ? _commandOption.GetItemMetadata(_commandOption.Selected).AsString()
            : string.Empty;

        PopulateOptions();

        if (string.IsNullOrWhiteSpace(selectedCommandKey))
        {
            return;
        }

        for (var index = 0; index < _commandOption.ItemCount; index += 1)
        {
            var metadata = _commandOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.String && metadata.AsString() == selectedCommandKey)
            {
                _commandOption.Select(index);
                return;
            }
        }
    }

    private void OnConfirmPressed()
    {
        if (_context.Localization == null || _commandOption == null)
        {
            return;
        }

        var metadata = _commandOption.GetItemMetadata(_commandOption.Selected);
        var commandKey = metadata.VariantType == Variant.Type.String ? metadata.AsString() : string.Empty;
        var selectedCommandText = _commandOption.GetItemText(_commandOption.Selected);
        Callable.From(() => CompleteConfirm(commandKey, selectedCommandText)).CallDeferred();
    }

    private void CompleteConfirm(string commandKey, string selectedCommandText)
    {
        switch (commandKey)
        {
            case "command.personnel.give_bonus":
                _showBonusDialog();
                return;
            case "command.personnel.assign_title":
                _showAssignRoleDialog();
                return;
            case "command.personnel.prefect_authorization":
                _showPrefectAuthorizationDialog();
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
                    _context.Localization?.Format("log.personnel_command_selected", selectedCommandText) ?? selectedCommandText,
                    isPlayerRelated: true);
                return;
        }
    }
}
