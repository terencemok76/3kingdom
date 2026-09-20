using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class MilitaryCommandDialogController : FloatingOverlayController
{
    private readonly MilitaryUiContext _context;
    private readonly System.Func<bool> _openMoveFlow;
    private readonly System.Func<bool> _openAttackFlow;
    private readonly System.Action _showRecruitTroopDialog;
    private OptionButton? _commandOption;
    private Button? _confirmButton;
    private Label? _warningLabel;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(340.0f, 170.0f);

    public MilitaryCommandDialogController(MilitaryUiContext context, System.Func<bool> openMoveFlow, System.Func<bool> openAttackFlow, System.Action showRecruitTroopDialog)
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
        if (_warningLabel != null)
        {
            _warningLabel.Text = string.Empty;
        }

        RefreshCommandOptionTexts();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _commandOption = root.GetNodeOrNull<OptionButton>("CommandOption");
        _warningLabel = root.GetNodeOrNull<Label>("WarningLabel");
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

    private void RefreshCommandOptionTexts()
    {
        if (_commandOption == null || _context.Localization == null)
        {
            return;
        }

        var selectedCommandType = GetSelectedCommandType();
        _commandOption.Clear();
        AddCommandOption("ui.military_recruit", CommandType.Recruit);
        AddCommandOption("ui.military_move", CommandType.Move);
        AddCommandOption("ui.military_attack", CommandType.Attack);

        for (var index = 0; index < _commandOption.ItemCount; index += 1)
        {
            var metadata = _commandOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == (int)selectedCommandType)
            {
                _commandOption.Select(index);
                return;
            }
        }

        if (_commandOption.ItemCount > 0)
        {
            _commandOption.Select(0);
        }
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
                if (!_openAttackFlow())
                {
                    ShowWarning("ui.no_connected_enemy_city");
                }
                return;
            case CommandType.Move:
                if (!_openMoveFlow())
                {
                    ShowWarning("ui.no_connected_friendly_city");
                }
                return;
            default:
                _showRecruitTroopDialog();
                return;
        }
    }

    private void ShowWarning(string localeKey)
    {
        if (_warningLabel != null && _context.Localization != null)
        {
            _warningLabel.Text = _context.Localization.T(localeKey);
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
