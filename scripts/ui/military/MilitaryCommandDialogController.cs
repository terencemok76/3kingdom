using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class MilitaryCommandDialogController
{
    private readonly MilitaryUiContext _context;
    private readonly System.Action _showRecruitTroopDialog;
    private Window? _dialog;
    private OptionButton? _commandOption;
    private Button? _confirmButton;
    private bool _signalsConnected;

    public MilitaryCommandDialogController(MilitaryUiContext context, System.Action showRecruitTroopDialog)
    {
        _context = context;
        _showRecruitTroopDialog = showRecruitTroopDialog;
    }

    public void Initialize()
    {
        _dialog = _context.CreateWindow("res://scenes/ui/military/MilitaryDialog.tscn", dialog => dialog.Hide());
        EnsureWidgets();
        _dialog.Hide();
    }

    public void Hide() => _dialog?.Hide();

    public void Show()
    {
        if (_context.SelectedCity == null || _dialog == null || _context.Localization == null)
        {
            return;
        }

        EnsureWidgets();
        RefreshText();
        Populate();
        _context.PopupDialog(_dialog);
    }

    public void RefreshText()
    {
        if (_dialog == null || _context.Localization == null)
        {
            return;
        }

        _dialog.Title = _context.Localization.T("ui.military");
        var label = _dialog.GetNodeOrNull<Label>("MilitaryDialogRoot/CommandLabel");
        if (label != null)
        {
            label.Text = _context.Localization.T("ui.military_command");
        }

        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_military");
        }
    }

    private void EnsureWidgets()
    {
        if (_dialog == null)
        {
            return;
        }

        var root = _dialog.GetNodeOrNull<VBoxContainer>("MilitaryDialogRoot");
        if (root == null)
        {
            GD.PushError("MilitaryDialogRoot not found in MilitaryDialog.tscn.");
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

    private void Populate()
    {
        if (_commandOption == null || _context.Localization == null)
        {
            return;
        }

        _commandOption.Clear();
        AddCommandOption("ui.military_recruit", CommandType.Recruit);
        AddCommandOption("ui.military_move", CommandType.Move);
        AddCommandOption("ui.military_attack", CommandType.Attack);
        if (_commandOption.ItemCount > 0)
        {
            _commandOption.Select(0);
        }
    }

    private void AddCommandOption(string localeKey, CommandType commandType)
    {
        if (_commandOption == null || _context.Localization == null)
        {
            return;
        }

        _commandOption.AddItem(_context.Localization.T(localeKey));
        _commandOption.SetItemMetadata(_commandOption.ItemCount - 1, (int)commandType);
    }

    private void OnConfirmPressed()
    {
        _dialog?.Hide();

        switch (GetSelectedCommandType())
        {
            case CommandType.Attack:
                _context.OpenAttackFlow();
                return;
            case CommandType.Move:
                _context.OpenMoveFlow();
                return;
            default:
                _showRecruitTroopDialog();
                return;
        }
    }

    private CommandType GetSelectedCommandType()
    {
        if (_commandOption == null || _commandOption.Selected < 0)
        {
            return CommandType.Recruit;
        }

        var metadata = _commandOption.GetItemMetadata(_commandOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (CommandType)metadata.AsInt32()
            : CommandType.Recruit;
    }
}
