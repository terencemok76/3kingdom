using Godot;
using ThreeKingdom.Core;

namespace ThreeKingdom.UI;

internal sealed class CivilCommandDialogController : FloatingOverlayController
{
    private readonly CivilUiContext _context;
    private readonly System.Action _showReliefDialog;
    private readonly System.Action _showVisitCitizenDialog;
    private OptionButton? _commandOption;
    private Button? _confirmButton;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(320.0f, 150.0f);

    public CivilCommandDialogController(CivilUiContext context, System.Action showReliefDialog, System.Action showVisitCitizenDialog)
        : base(context, "res://scenes/ui/civil/CivilDialog.tscn")
    {
        _context = context;
        _showReliefDialog = showReliefDialog;
        _showVisitCitizenDialog = showVisitCitizenDialog;
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
        Populate();
        ShowOverlay();
    }

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization.T("ui.civil"));
        var label = GetOverlayContentNode<Label>("CommandLabel");
        if (label != null)
        {
            label.Text = _context.Localization.T("ui.civil_command");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_civil");
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
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        if (_commandOption == null || _context.Localization == null || world == null || city == null)
        {
            return;
        }

        _commandOption.Clear();
        var reliefUsed = city.LastCivilReliefYear == world.Year && city.LastCivilReliefMonth == world.Month;
        var investigateUsed = city.LastSearchYear == world.Year && city.LastSearchMonth == world.Month;

        AddOption("command.civil.relief", reliefUsed);
        AddOption("command.civil.investigate_people", investigateUsed);
        SelectFirstEnabledOption();
    }

    private void AddOption(string localeKey, bool disabled)
    {
        if (_commandOption == null || _context.Localization == null)
        {
            return;
        }

        var text = disabled
            ? _context.Localization.Format("fmt.command_used_this_month", _context.Localization.T(localeKey))
            : _context.Localization.T(localeKey);
        _commandOption.AddItem(text);
        var index = _commandOption.ItemCount - 1;
        _commandOption.SetItemMetadata(index, localeKey);
        _commandOption.SetItemDisabled(index, disabled);
    }

    private void SelectFirstEnabledOption()
    {
        if (_commandOption == null)
        {
            return;
        }

        for (var index = 0; index < _commandOption.ItemCount; index += 1)
        {
            if (_commandOption.IsItemDisabled(index))
            {
                continue;
            }

            _commandOption.Select(index);
            return;
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
        if (commandKey == "command.civil.relief")
        {
            _showReliefDialog();
            return;
        }

        if (commandKey == "command.civil.investigate_people")
        {
            _showVisitCitizenDialog();
            return;
        }

        _context.AddLog(_context.Localization?.Format("log.civil_command_selected", selectedCommandText) ?? selectedCommandText, isPlayerRelated: true);
    }
}
