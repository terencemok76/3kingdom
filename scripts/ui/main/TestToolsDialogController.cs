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
    private Button? _caravanRoadBattleButton;
    private Button? _forceSurrenderButton;
    private Button? _cycleCityOwnerButton;
    private bool _signalsConnected;

    protected override Vector2 MinimumOverlaySize => new(420.0f, 350.0f);

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
            _summaryLabel.Text = _context.World?.ForceNextSurrenderSuccessForDebug == true
                ? localization?.T("ui.test_surrender_armed") ?? "Surrender test armed: the next safe player-led surrender proposal will succeed."
                : _context.World?.ForceNextCaravanAmbushForDebug == true
                ? localization?.T("ui.test_caravan_armed") ?? "Caravan ambush armed: schedule a Move carrying cargo."
                : localization?.T("ui.test_tools_open") ?? "Open a test tool.";
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
        if (_caravanRoadBattleButton != null)
        {
            _caravanRoadBattleButton.Text = localization?.T("ui.test_caravan_road_battle") ?? "Force Next Caravan Ambush";
        }
        if (_forceSurrenderButton != null)
        {
            _forceSurrenderButton.Text = localization?.T("ui.test_force_surrender") ?? "Force Next Surrender";
        }
        if (_cycleCityOwnerButton != null)
        {
            _cycleCityOwnerButton.Text = localization?.T("ui.test_cycle_city_owner") ?? "Switch City Faction";
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
        _caravanRoadBattleButton = root.GetNodeOrNull<Button>("CaravanActionRow/CaravanRoadBattleButton");
        _forceSurrenderButton = root.GetNodeOrNull<Button>("SurrenderActionRow/ForceSurrenderButton");
        _cycleCityOwnerButton = root.GetNodeOrNull<Button>("OwnershipActionRow/CycleCityOwnerButton");

        foreach (var button in new[] { _testCaptiveButton, _battleEquipmentButton, _engineerButton, _bowWorkshopButton, _horsePastureButton, _caravanRoadBattleButton, _forceSurrenderButton, _cycleCityOwnerButton }.Where(button => button != null))
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

            // Debug actions retain the shared dark command-button frame, but
            // use the readable light-gold text treatment used by dialog actions.
            button.AddThemeColorOverride("font_color", new Color(0.93f, 0.78f, 0.42f, 1.0f));
            button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.9f, 0.63f, 1.0f));
            button.AddThemeColorOverride("font_pressed_color", new Color(0.78f, 0.61f, 0.3f, 1.0f));
            button.AddThemeColorOverride("font_focus_color", new Color(1.0f, 0.9f, 0.63f, 1.0f));
            button.AddThemeColorOverride("font_disabled_color", new Color(0.48f, 0.43f, 0.34f, 1.0f));
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
        if (_caravanRoadBattleButton != null)
        {
            _caravanRoadBattleButton.Pressed += OnCaravanRoadBattlePressed;
        }
        if (_forceSurrenderButton != null)
        {
            _forceSurrenderButton.Pressed += OnForceSurrenderPressed;
        }
        if (_cycleCityOwnerButton != null)
        {
            _cycleCityOwnerButton.Pressed += OnCycleCityOwnerPressed;
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

    private void OnCaravanRoadBattlePressed()
    {
        _context.TriggerTestCaravanRoadBattle();
    }

    private void OnForceSurrenderPressed()
    {
        _context.ForceTestSurrender();
        RefreshText();
    }

    private void OnCycleCityOwnerPressed()
    {
        _context.CycleTestCityOwner();
    }
}
