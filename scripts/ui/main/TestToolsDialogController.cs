using Godot;
using System.Linq;

namespace ThreeKingdom.UI;

internal sealed class TestToolsDialogController : FloatingOverlayController
{
    private readonly MainHudUiContext _context;
    private Label? _summaryLabel;
    private Button? _testCaptiveButton;
    private Button? _battleEquipmentButton;
    private Button? _engineerButton;
    private Button? _bowWorkshopButton;
    private Button? _horsePastureButton;
    private bool _signalsConnected;

    protected override Vector2 MinimumOverlaySize => new(420.0f, 210.0f);

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

        var localization = _context.Localization;
        SetOverlayTitleText(localization?.T("ui.test_tools") ?? "Test Tools");
        if (_summaryLabel != null)
        {
            _summaryLabel.Text = localization?.T("ui.test_tools_open") ?? "Open a test tool.";
        }

        if (_testCaptiveButton != null)
        {
            _testCaptiveButton.Text = localization?.T("ui.test_captive") ?? "Test Captive";
        }
        if (_battleEquipmentButton != null)
        {
            _battleEquipmentButton.Text = localization?.T("ui.test_battle_equipment") ?? "Battle Equipment +1";
        }
        if (_engineerButton != null)
        {
            _engineerButton.Text = localization?.T("ui.test_engineers") ?? "Engineers +300";
        }
        if (_bowWorkshopButton != null)
        {
            _bowWorkshopButton.Text = localization == null
                ? "Upgrade Bow Workshop"
                : $"{localization.T("ui.bow_workshop")} {localization.T("ui.test_upgrade")}";
        }
        if (_horsePastureButton != null)
        {
            _horsePastureButton.Text = localization == null
                ? "Upgrade Horse Pasture"
                : $"{localization.T("ui.horse_pasture")} {localization.T("ui.test_upgrade")}";
        }
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _testCaptiveButton = root.GetNodeOrNull<Button>("ActionRow/TestCaptiveButton");
        _battleEquipmentButton = root.GetNodeOrNull<Button>("ActionRow/BattleEquipmentButton");
        _engineerButton = root.GetNodeOrNull<Button>("ActionRow/EngineerButton");
        _bowWorkshopButton = root.GetNodeOrNull<Button>("FacilityActionRow/BowWorkshopButton");
        _horsePastureButton = root.GetNodeOrNull<Button>("FacilityActionRow/HorsePastureButton");

        foreach (var button in new[] { _testCaptiveButton, _battleEquipmentButton, _engineerButton, _bowWorkshopButton, _horsePastureButton }.Where(button => button != null))
        {
            if (_context.ViewButton == null)
            {
                continue;
            }
            button!.CustomMinimumSize = _context.ViewButton.CustomMinimumSize;
            foreach (var name in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            {
                var style = _context.ViewButton.GetThemeStylebox(name);
                if (style != null)
                {
                    button.AddThemeStyleboxOverride(name, style);
                }
            }

            foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_disabled_color", "font_focus_color" })
            {
                if (_context.ViewButton.HasThemeColorOverride(name))
                {
                    button.AddThemeColorOverride(name, _context.ViewButton.GetThemeColor(name));
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
        if (_battleEquipmentButton != null)
        {
            _battleEquipmentButton.Pressed += OnBattleEquipmentPressed;
        }
        if (_engineerButton != null)
        {
            _engineerButton.Pressed += OnEngineersPressed;
        }
        if (_bowWorkshopButton != null)
        {
            _bowWorkshopButton.Pressed += OnBowWorkshopPressed;
        }
        if (_horsePastureButton != null)
        {
            _horsePastureButton.Pressed += OnHorsePasturePressed;
        }

        _signalsConnected = true;
    }

    private void OnTestCaptivePressed()
    {
        _context.OpenTestCapture();
        HideOverlay();
    }

    private void OnBattleEquipmentPressed()
    {
        _context.AddTestBattleEquipment();
    }

    private void OnEngineersPressed()
    {
        _context.AddTestEngineers();
    }

    private void OnBowWorkshopPressed()
    {
        _context.UpgradeTestBowWorkshop();
    }

    private void OnHorsePasturePressed()
    {
        _context.UpgradeTestHorsePasture();
    }
}
