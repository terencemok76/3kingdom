using Godot;

namespace ThreeKingdom.UI;

internal sealed class SystemEntryButtonController
{
    private readonly SystemUiContext _context;
    private Button? _button;
    private bool _signalConnected;

    public SystemEntryButtonController(SystemUiContext context)
    {
        _context = context;
    }

    public void Initialize(System.Action showOptionDialog)
    {
        _button = EnsureButton();
        if (_button == null || _signalConnected)
        {
            RefreshText();
            return;
        }

        _button.Pressed += showOptionDialog;
        _signalConnected = true;
        RefreshText();
    }

    public void Shutdown(System.Action showOptionDialog)
    {
        if (_button == null || !_signalConnected)
        {
            return;
        }

        _button.Pressed -= showOptionDialog;
        _signalConnected = false;
    }

    public void RefreshText()
    {
        if (_button != null)
        {
            _button.Text = _context.GetOptionButtonText();
        }
    }

    private Button? EnsureButton()
    {
        var commandButtons = _context.CommandButtons;
        var topBar = _context.TopBar;
        if (topBar == null)
        {
            return null;
        }

        if (commandButtons?.GetNodeOrNull<Button>("OptionButton") is { } legacyButton)
        {
            legacyButton.QueueFree();
        }

        var existingButton = topBar.GetNodeOrNull<Button>("OptionButton");
        if (existingButton != null)
        {
            return existingButton;
        }

        var button = new Button
        {
            Name = "OptionButton",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            FocusMode = Control.FocusModeEnum.None
        };

        _context.ApplyOptionEntryTheme(button);
        topBar.AddChild(button);
        topBar.MoveChild(button, Mathf.Max(topBar.GetChildCount() - 1, 0));
        return button;
    }
}
