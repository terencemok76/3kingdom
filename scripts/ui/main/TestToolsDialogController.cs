using Godot;
using System.Linq;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

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
    private Button? _forceCitySurrenderButton;
    private Button? _cycleCityOwnerButton;
    private Button? _maxAffinityButton;
    private Button? _forceCovertButton;
    private Button? _assignCovertButton;
    private OptionButton? _covertOfficerOption;
    private OptionButton? _loyaltyOfficerOption;
    private Button? _loyaltyLowButton;
    private Button? _loyaltyMiddleButton;
    private Button? _loyaltyHighButton;
    private int _loyaltyCityId;
    private bool _signalsConnected;

    protected override Vector2 MinimumOverlaySize => new(420.0f, 350.0f);
    protected override bool AutoFitOverlayHeight => true;

    public TestToolsDialogController(MainHudUiContext context)
        : base(context, "res://scenes/ui/main/TestToolsDialog.tscn")
    {
        _context = context;
    }

    public void Initialize() => InitializeOverlay();

    public void Hide() => HideOverlay();

    public void RefreshSelectedCity()
    {
        if (IsOverlayVisible) RefreshLoyaltyOfficerOptions();
    }

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
            _summaryLabel.Text = _context.World?.ForceNextCitySurrenderSuccessForDebug == true
                ? localization?.T("ui.test_city_surrender_armed") ?? "The next valid player governor handover will succeed."
                : _context.World?.ForceNextCovertRecruitmentForDebug == true
                ? localization?.T("ui.test_covert_armed") ?? "Armed: the next valid player covert recruitment succeeds without exposure."
                : _context.World?.ForceNextSurrenderSuccessForDebug == true
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
        if (_forceCitySurrenderButton != null)
            _forceCitySurrenderButton.Text = localization?.T("ui.test_force_city_surrender") ?? "Force Next City Handover";
        if (_cycleCityOwnerButton != null)
        {
            _cycleCityOwnerButton.Text = localization?.T("ui.test_cycle_city_owner") ?? "Switch City Faction";
        }
        if (_maxAffinityButton != null)
            _maxAffinityButton.Text = localization?.T("ui.test_max_affinity") ?? "Max All Officer Affinity";
        if (_forceCovertButton != null)
            _forceCovertButton.Text = localization?.T("ui.test_force_covert") ?? "Force Next Covert Recruitment";
        if (_assignCovertButton != null)
            _assignCovertButton.Text = localization?.T("ui.test_assign_covert") ?? "Assign Covert Agent";
        if (_loyaltyLowButton != null) _loyaltyLowButton.Text = localization?.T("ui.test_loyalty_low") ?? "Low 30";
        if (_loyaltyMiddleButton != null) _loyaltyMiddleButton.Text = localization?.T("ui.test_loyalty_middle") ?? "Medium 60";
        if (_loyaltyHighButton != null) _loyaltyHighButton.Text = localization?.T("ui.test_loyalty_high") ?? "High 90";
        RefreshCovertOfficerOptions();
        RefreshLoyaltyOfficerOptions();
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
        _forceCitySurrenderButton = root.GetNodeOrNull<Button>("CitySurrenderActionRow/ForceCitySurrenderButton");
        _cycleCityOwnerButton = root.GetNodeOrNull<Button>("OwnershipActionRow/CycleCityOwnerButton");
        _maxAffinityButton = root.GetNodeOrNull<Button>("AffinityActionRow/MaxAffinityButton");
        _forceCovertButton = root.GetNodeOrNull<Button>("CovertActionRow/ForceCovertButton");
        _covertOfficerOption = root.GetNodeOrNull<OptionButton>("AssignCovertRow/OfficerOption");
        _assignCovertButton = root.GetNodeOrNull<Button>("AssignCovertRow/AssignButton");
        _loyaltyOfficerOption = root.GetNodeOrNull<OptionButton>("LoyaltyOfficerRow/OfficerOption");
        _loyaltyLowButton = root.GetNodeOrNull<Button>("LoyaltyActionRow/LowButton");
        _loyaltyMiddleButton = root.GetNodeOrNull<Button>("LoyaltyActionRow/MiddleButton");
        _loyaltyHighButton = root.GetNodeOrNull<Button>("LoyaltyActionRow/HighButton");

        foreach (var button in new[] { _testCaptiveButton, _battleEquipmentButton, _engineerButton, _bowWorkshopButton, _horsePastureButton, _caravanRoadBattleButton, _forceSurrenderButton, _forceCitySurrenderButton, _cycleCityOwnerButton, _maxAffinityButton, _forceCovertButton, _assignCovertButton, _loyaltyLowButton, _loyaltyMiddleButton, _loyaltyHighButton }.Where(button => button != null))
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
        if (_forceCitySurrenderButton != null) _forceCitySurrenderButton.Pressed += OnForceCitySurrenderPressed;
        if (_cycleCityOwnerButton != null)
        {
            _cycleCityOwnerButton.Pressed += OnCycleCityOwnerPressed;
        }
        if (_maxAffinityButton != null) _maxAffinityButton.Pressed += OnMaxAffinityPressed;
        if (_forceCovertButton != null) _forceCovertButton.Pressed += OnForceCovertPressed;
        if (_assignCovertButton != null) _assignCovertButton.Pressed += OnAssignCovertPressed;
        if (_loyaltyLowButton != null) _loyaltyLowButton.Pressed += () => SetTestOfficerLoyalty(30);
        if (_loyaltyMiddleButton != null) _loyaltyMiddleButton.Pressed += () => SetTestOfficerLoyalty(60);
        if (_loyaltyHighButton != null) _loyaltyHighButton.Pressed += () => SetTestOfficerLoyalty(90);
        if (_loyaltyOfficerOption != null) _loyaltyOfficerOption.ItemSelected += _ => UpdateLoyaltyButtons();
        if (_covertOfficerOption != null) _covertOfficerOption.ItemSelected += _ => UpdateAssignCovertButton();

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

    private void OnForceCitySurrenderPressed()
    {
        var world = _context.World;
        if (world == null || _context.PlayerFactionId <= 0 || !_context.IsGodModeEnabled()) return;
        world.ForceNextCitySurrenderSuccessForDebug = true;
        RefreshText();
        Callable.From(UpdateOverlayLayoutNow).CallDeferred();
    }

    private void OnCycleCityOwnerPressed()
    {
        _context.CycleTestCityOwner();
    }

    private void OnMaxAffinityPressed()
    {
        var world = _context.World;
        var factionId = _context.PlayerFactionId;
        if (world == null || factionId <= 0 || !_context.IsGodModeEnabled()) return;
        foreach (var officer in world.Officers)
        {
            var record = world.GetOfficerAffection(factionId, officer.Id);
            if (record == null)
            {
                record = new ThreeKingdom.Data.WorldState.OfficerAffectionData { FactionId = factionId, OfficerId = officer.Id, LastContactMonth = -1 };
                world.OfficerAffectionRecords.Add(record);
            }
            record.Score = 100;
        }
        if (_summaryLabel != null) _summaryLabel.Text = _context.Localization?.T("ui.test_max_affinity_done") ?? "All officers' affinity with the player is now 100.";
        _context.UiEventHub.PublishOfficerStateChanged(0, _context.SelectedCity?.Id ?? 0, factionId);
        Callable.From(UpdateOverlayLayoutNow).CallDeferred();
    }

    private void RefreshCovertOfficerOptions()
    {
        var world = _context.World;
        var option = _covertOfficerOption;
        var localization = _context.Localization;
        if (world == null || option == null) return;
        var previousId = option.Selected >= 0 ? option.GetItemMetadata(option.Selected).AsInt32() : 0;
        option.Clear();
        option.AddItem(localization?.T("ui.test_covert_choose") ?? "Select foreign officer");
        option.SetItemMetadata(0, 0);
        foreach (var city in world.Cities.Where(city => city.OwnerFactionId > 0 && city.OwnerFactionId != _context.PlayerFactionId).OrderBy(city => city.Id))
        {
            foreach (var id in city.OfficerIds.Distinct().OrderBy(id => id))
            {
                var officer = world.GetOfficer(id);
                var candidate = new WorldState.CovertOfficerData { SponsorFactionId = _context.PlayerFactionId, OriginalFactionId = city.OwnerFactionId, OfficerId = id };
                if (officer == null || officer.CityId != city.Id || !CovertOfficerRules.IsValid(world, candidate) || world.CovertOfficerRecords.Any(agent => agent.OfficerId == id)) continue;
                option.AddItem($"{localization?.GetCityName(city) ?? city.Name} | {localization?.GetOfficerName(officer) ?? officer.Name}");
                option.SetItemMetadata(option.ItemCount - 1, id);
                if (id == previousId) option.Select(option.ItemCount - 1);
            }
        }
        UpdateAssignCovertButton();
    }

    private void RefreshLoyaltyOfficerOptions()
    {
        var world = _context.World;
        var city = _context.SelectedCity;
        var option = _loyaltyOfficerOption;
        var localization = _context.Localization;
        if (world == null || option == null) return;
        var previousId = _loyaltyCityId == (city?.Id ?? 0) && option.Selected >= 0 ? option.GetItemMetadata(option.Selected).AsInt32() : 0;
        _loyaltyCityId = city?.Id ?? 0;
        option.Clear();
        option.AddItem(city != null && localization != null
            ? localization.Format("ui.test_loyalty_choose_city", localization.GetCityName(city))
            : localization?.T("ui.test_loyalty_choose") ?? "Select officer in selected city");
        option.SetItemMetadata(0, 0);
        if (city != null)
        {
            foreach (var officer in city.OfficerIds.Distinct().Select(world.GetOfficer)
                         .Where(officer => officer != null && officer.CityId == city.Id && officer.CaptiveFactionId <= 0 &&
                                           (officer.DeathYear <= 0 || world.Year <= officer.DeathYear))
                         .OrderBy(officer => officer!.Id))
            {
                option.AddItem($"{localization?.GetOfficerName(officer!) ?? officer!.Name} | {localization?.T("ui.loyalty") ?? "Loyalty"} {officer!.Loyalty}");
                option.SetItemMetadata(option.ItemCount - 1, officer.Id);
                if (officer.Id == previousId) option.Select(option.ItemCount - 1);
            }
        }
        UpdateLoyaltyButtons();
    }

    private void UpdateLoyaltyButtons()
    {
        var disabled = _loyaltyOfficerOption == null || _loyaltyOfficerOption.Selected <= 0;
        foreach (var button in new[] { _loyaltyLowButton, _loyaltyMiddleButton, _loyaltyHighButton })
            if (button != null) button.Disabled = disabled;
    }

    private void SetTestOfficerLoyalty(int value)
    {
        var world = _context.World;
        var city = _context.SelectedCity;
        var option = _loyaltyOfficerOption;
        if (world == null || city == null || option == null || option.Selected <= 0 || !_context.IsGodModeEnabled()) return;
        var officer = world.GetOfficer(option.GetItemMetadata(option.Selected).AsInt32());
        if (officer == null || officer.CityId != city.Id || !city.OfficerIds.Contains(officer.Id) || officer.CaptiveFactionId > 0) { RefreshLoyaltyOfficerOptions(); return; }
        officer.Loyalty = value;
        var localization = _context.Localization;
        if (_summaryLabel != null) _summaryLabel.Text = localization?.Format("ui.test_loyalty_done", localization.GetOfficerName(officer), value) ?? $"{officer.Name}: loyalty {value}.";
        RefreshLoyaltyOfficerOptions();
        _context.UiEventHub.PublishOfficerStateChanged(officer.Id, city.Id, city.OwnerFactionId);
        Callable.From(UpdateOverlayLayoutNow).CallDeferred();
    }

    private void UpdateAssignCovertButton()
    {
        if (_assignCovertButton != null)
            _assignCovertButton.Disabled = _covertOfficerOption == null || _covertOfficerOption.Selected <= 0;
    }

    private void OnAssignCovertPressed()
    {
        var world = _context.World;
        var option = _covertOfficerOption;
        if (world == null || option == null || option.Selected <= 0 || !_context.IsGodModeEnabled()) return;
        var officer = world.GetOfficer(option.GetItemMetadata(option.Selected).AsInt32());
        var city = officer == null ? null : world.GetCity(officer.CityId);
        if (officer == null || city == null || !city.OfficerIds.Contains(officer.Id)) { RefreshCovertOfficerOptions(); return; }
        var agent = new WorldState.CovertOfficerData { SponsorFactionId = _context.PlayerFactionId, OriginalFactionId = city.OwnerFactionId, OfficerId = officer.Id };
        if (!CovertOfficerRules.IsValid(world, agent) || world.CovertOfficerRecords.Any(item => item.OfficerId == officer.Id)) { RefreshCovertOfficerOptions(); return; }
        world.CovertOfficerRecords.Add(agent);
        if (_summaryLabel != null)
            _summaryLabel.Text = _context.Localization?.Format("ui.test_assign_covert_done", _context.Localization.GetOfficerName(officer)) ?? $"{officer.Name} is now your covert agent.";
        RefreshCovertOfficerOptions();
        _context.UiEventHub.PublishOfficerStateChanged(officer.Id, city.Id, _context.PlayerFactionId);
        Callable.From(UpdateOverlayLayoutNow).CallDeferred();
    }

    private void OnForceCovertPressed()
    {
        var world = _context.World;
        if (world == null || _context.PlayerFactionId <= 0 || !_context.IsGodModeEnabled()) return;
        world.ForceNextCovertRecruitmentForDebug = true;
        RefreshText();
        Callable.From(UpdateOverlayLayoutNow).CallDeferred();
    }
}
