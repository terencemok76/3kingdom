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
            _button.Text = string.Empty;
            _button.TooltipText = _context.GetOptionButtonText();
        }
    }

    private Button? EnsureButton()
    {
        var commandButtons = _context.CommandButtons;
        if (commandButtons?.GetNodeOrNull<Button>("OptionButton") is { } legacyButton)
        {
            legacyButton.QueueFree();
        }

        var existingButton = _context.OptionButton;
        if (existingButton != null)
        {
            ConfigureButton(existingButton, applySharedTheme: false);
            return existingButton;
        }

        var topBar = _context.TopBar;
        if (topBar == null)
        {
            return null;
        }

        var button = new Button
        {
            Name = "OptionButton",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            FocusMode = Control.FocusModeEnum.None
        };

        ConfigureButton(button, applySharedTheme: true);
        topBar.AddChild(button);
        topBar.MoveChild(button, Mathf.Max(topBar.GetChildCount() - 1, 0));
        return button;
    }

    private void ConfigureButton(Button button, bool applySharedTheme)
    {
        if (applySharedTheme)
        {
            _context.ApplyOptionEntryTheme(button);
        }
        button.Text = string.Empty;
        button.Icon = GD.Load<Texture2D>("res://assets/ui/icons/64x64/option_btn.png");
        button.ExpandIcon = true;
        button.IconAlignment = HorizontalAlignment.Center;
        button.CustomMinimumSize = new Vector2(48.0f, 42.0f);
        button.AddThemeConstantOverride("icon_max_width", 30);
    }
}
