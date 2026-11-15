using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

// Owns the dynamic deployment rows; the dialog owns the surrounding flow and selection table.
internal sealed class DeploymentEditorController
{
    private readonly MilitaryUiContext _context;
    private readonly DeploymentDraft _draft;
    private readonly Func<CityData?> _city;
    private readonly Action<Node> _applyInputTheme;
    private readonly Action _refresh;
    private readonly Action<string> _warning;
    private readonly Action _show;
    private readonly Func<TroopType, string> _troopName;

    public DeploymentEditorController(MilitaryUiContext context, DeploymentDraft draft, Func<CityData?> city,
        Action<Node> applyInputTheme, Action refresh, Action<string> warning, Action show, Func<TroopType, string> troopName)
    {
        _context = context;
        _draft = draft;
        _city = city;
        _applyInputTheme = applyInputTheme;
        _refresh = refresh;
        _warning = warning;
        _show = show;
        _troopName = troopName;
    }

    public List<TroopType> AvailableTroopTypes()
    {
        var types = new[] { TroopType.Infantry, TroopType.Spearman, TroopType.Cavalry, TroopType.Archer, TroopType.Crossbow }
            .Where(type => AvailableTroops(type) > 0).ToList();
        return types.Count == 0 ? new List<TroopType> { TroopType.Infantry } : types;
    }

    private int AvailableTroops(TroopType type) => _city()?.GetTroops(type) ?? 0;

    public Control CreateOfficerRow(OfficerData officer)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 32), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = _context.Localization?.GetOfficerName(officer) ?? officer.Name,
            CustomMinimumSize = new Vector2(100, 0), VerticalAlignment = VerticalAlignment.Center });
        var typeOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(190, 0) };
        var deployment = _draft.GetDeployment(officer.Id);
        foreach (var type in AvailableTroopTypes())
        {
            typeOption.AddItem(_troopName(type));
            typeOption.SetItemMetadata(typeOption.ItemCount - 1, (int)type);
            if (type == deployment.TroopType) typeOption.Select(typeOption.ItemCount - 1);
        }
        typeOption.ItemSelected += index =>
        {
            var type = (TroopType)typeOption.GetItemMetadata((int)index).AsInt32();
            _draft.SetTroopType(officer.Id, type, AvailableTroops(type));
            _refresh();
        };
        _applyInputTheme(typeOption);
        row.AddChild(typeOption);

        var count = new SpinBox { MinValue = 0, Step = 1, Rounded = true, CustomMinimumSize = new Vector2(90, 0) };
        ConfigureCount(count, _draft.MaxTroops(officer.Id, deployment.TroopType, AvailableTroops(deployment.TroopType)), deployment.TroopCount);
        count.ValueChanged += value => { _draft.SetTroopCount(officer.Id, (int)value, AvailableTroops(deployment.TroopType)); _refresh(); };
        _applyInputTheme(count);
        row.AddChild(count);
        var max = new Button { Text = _context.Localization?.T("ui.max") ?? "Max", CustomMinimumSize = new Vector2(58, 0) };
        max.Pressed += () => { _draft.SetTroopCount(officer.Id, int.MaxValue, AvailableTroops(deployment.TroopType)); _refresh(); };
        _context.ApplyCommandButtonTheme(max);
        row.AddChild(max);
        return row;
    }

    public Control CreateSupportRow()
    {
        var city = _city();
        var container = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        container.AddThemeConstantOverride("separation", 4);
        container.AddChild(new Label { Text = _context.Localization?.T("ui.battle_support") ?? "戰役支援（不需武將統率）" });
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(new Label { Text = "工兵隊", VerticalAlignment = VerticalAlignment.Center });
        var engineers = new SpinBox { MinValue = 0, Step = 1, Rounded = true, CustomMinimumSize = new Vector2(90, 0) };
        ConfigureCount(engineers, _draft.MaxEngineerTeams(city?.EngineerTroops ?? 0), _draft.Support.EngineerTeamCount);
        engineers.ValueChanged += value => { _draft.SetEngineerTeams((int)value, city?.EngineerTroops ?? 0); _refresh(); };
        _applyInputTheme(engineers);
        row.AddChild(engineers);
        var max = new Button { Text = _context.Localization?.T("ui.max") ?? "Max" };
        max.Pressed += () => { _draft.SetEngineerTeams(int.MaxValue, city?.EngineerTroops ?? 0); _refresh(); };
        _context.ApplyCommandButtonTheme(max);
        row.AddChild(max);
        container.AddChild(row);
        var support = _draft.Support;
        if ((city?.SupplyCartCount ?? 0) > 0) container.AddChild(CreateEquipmentRow(_context.Localization?.T("ui.supply_cart") ?? "補給車", BattleEquipmentType.SupplyCart, support.SupplyCart));
        if ((city?.RamCount ?? 0) > 0) container.AddChild(CreateEquipmentRow(_context.Localization?.T("ui.ram") ?? "衝車", BattleEquipmentType.Ram, support.Ram));
        if ((city?.LadderCount ?? 0) > 0) container.AddChild(CreateEquipmentRow(_context.Localization?.T("ui.ladder") ?? "雲梯", BattleEquipmentType.Ladder, support.Ladder));
        if ((city?.CatapultCount ?? 0) > 0) container.AddChild(CreateEquipmentRow(_context.Localization?.T("ui.catapult") ?? "投石車", BattleEquipmentType.Catapult, support.Catapult));
        return container;
    }

    private Control CreateEquipmentRow(string text, BattleEquipmentType type, bool selected)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var check = new CheckBox { Text = $"{text}（需工兵 {BattleSupportRules.GetRequiredEngineerCount(type)}）", ButtonPressed = selected, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        check.Toggled += value =>
        {
            var available = _city()?.EngineerTroops ?? 0;
            if (!_draft.TrySetEquipment(type, value, available, out var required))
            {
                check.SetPressedNoSignal(false);
                _warning($"工兵不足：{text}需要 {required}，本城只有 {available}。");
                _show();
                return;
            }
            if (value) _warning(string.Empty);
            _refresh();
        };
        row.AddChild(check);
        return row;
    }

    private static void ConfigureCount(SpinBox spin, int maximum, int value)
    {
        spin.MinValue = 0;
        spin.MaxValue = Math.Max(0, maximum);
        spin.Step = 1;
        spin.Value = Math.Clamp(value, 0, (int)spin.MaxValue);
    }
}
