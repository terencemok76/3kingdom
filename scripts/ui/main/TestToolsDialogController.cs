using Godot;

namespace ThreeKingdom.UI;

internal sealed class TestToolsDialogController : FloatingOverlayController
{
    private readonly MainHudUiContext _context;
    private Label? _summaryLabel;
    private Button? _testCaptiveButton;
    private bool _signalsConnected;

    protected override Vector2 MinimumOverlaySize => new(420.0f, 180.0f);

    public TestToolsDialogController(MainHudUiContext context)
        : base(context, "res://scenes/ui/main/TestToolsDialog.tscn")
    {
        _context = context;
    }

    public void Initialize() => InitializeOverlay();

    public void Hide() => HideOverlay();

    public void Show()
    {
        RefreshText();
        ShowOverlay();
    }

    public void RefreshText()
    {
        if (!EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization?.IsTraditionalChinese == true ? "測試工具" : "Test Tools");
        if (_summaryLabel != null)
        {
            _summaryLabel.Text = _context.Localization?.IsTraditionalChinese == true
                ? "開啟測試工具。"
                : "Open a test tool.";
        }

        if (_testCaptiveButton != null)
        {
            _testCaptiveButton.Text = _context.Localization?.IsTraditionalChinese == true
                ? "測試俘虜"
                : "Test Captive";
        }
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _testCaptiveButton = root.GetNodeOrNull<Button>("ActionRow/TestCaptiveButton");

        if (_context.ViewButton != null && _testCaptiveButton != null)
        {
            _testCaptiveButton.CustomMinimumSize = _context.ViewButton.CustomMinimumSize;
            foreach (var name in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            {
                var style = _context.ViewButton.GetThemeStylebox(name);
                if (style != null)
                {
                    _testCaptiveButton.AddThemeStyleboxOverride(name, style);
                }
            }

            foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_disabled_color", "font_focus_color" })
            {
                if (_context.ViewButton.HasThemeColorOverride(name))
                {
                    _testCaptiveButton.AddThemeColorOverride(name, _context.ViewButton.GetThemeColor(name));
                }
            }
        }

        if (_signalsConnected)
        {
            return;
        }

        if (_testCaptiveButton != null)
        {
            _testCaptiveButton.Pressed += OnTestCaptivePressed;
        }

        _signalsConnected = true;
    }

    private void OnTestCaptivePressed()
    {
        _context.OpenTestCapture();
        HideOverlay();
    }
}
