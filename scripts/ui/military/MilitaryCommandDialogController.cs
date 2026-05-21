using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class MilitaryCommandDialogController : FloatingOverlayController
{
    private readonly MilitaryUiContext _context;
    private readonly System.Action _openMoveFlow;
    private readonly System.Action _openAttackFlow;
    private readonly System.Action _showRecruitTroopDialog;
    private OptionButton? _commandOption;
    private Button? _confirmButton;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(340.0f, 145.0f);

    public MilitaryCommandDialogController(MilitaryUiContext context, System.Action openMoveFlow, System.Action openAttackFlow, System.Action showRecruitTroopDialog)
        : base(context, "res://scenes/ui/military/MilitaryDialog.tscn")
    {
        _context = context;
        _openMoveFlow = openMoveFlow;
        _openAttackFlow = openAttackFlow;
        _showRecruitTroopDialog = showRecruitTroopDialog;
    }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public void Show()
    {
        if (_context.SelectedCity == null || _context.Localization == null)
        {
            return;
        }

        RefreshText();
        Populate();
        ShowOverlay();
    }

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization.T("ui.military"));
        var label = GetOverlayContentNode<Label>("CommandLabel");
        if (label != null)
        {
            label.Text = _context.Localization.T("ui.military_command");
        }

        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_military");
        }
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
        var selectedCommand = GetSelectedCommandType();
        Callable.From(() => CompleteConfirm(selectedCommand)).CallDeferred();
    }

    private void CompleteConfirm(CommandType selectedCommand)
    {
        switch (selectedCommand)
        {
            case CommandType.Attack:
                _openAttackFlow();
                return;
            case CommandType.Move:
                _openMoveFlow();
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
