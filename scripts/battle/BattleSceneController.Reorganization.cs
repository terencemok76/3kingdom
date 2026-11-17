using System;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private CanvasLayer? _reorganizationLayer;

    private void ShowCampaignReorganization()
    {
        var world = CampaignRuntimeContext.World;
        if (_activeCampaign == null || world == null || _reorganizationLayer != null) return;
        var factionId = world.Factions.FirstOrDefault(faction => faction.IsPlayer)?.Id ?? 0;
        CampaignReorganizationRules.InitializeCapacities(world, _activeCampaign);
        var teams = CampaignReorganizationRules.GetEligibleTeams(_activeCampaign, factionId).OrderBy(team => team.Id).ToList();
        _campaignDecisionPanel!.Visible = false;
        _reorganizationLayer = new CanvasLayer { Layer = 210 };
        AddChild(_reorganizationLayer);
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.72f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _reorganizationLayer.AddChild(shade);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _reorganizationLayer.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(600, 0) };
        center.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        panel.AddChild(box);
        box.AddChild(new Label { Text = BattleText("ui.battle.reorganize", "Reorganize troops"), HorizontalAlignment = HorizontalAlignment.Center });
        box.AddChild(new Label { Text = BattleText("ui.battle.reorganize_hint", "Transfer between compatible teams."), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(580, 0) });
        var from = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var to = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var active = new SpinBox { MinValue = 0, MaxValue = 9999999, Step = 1, Rounded = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var wounded = new SpinBox { MinValue = 0, MaxValue = 9999999, Step = 1, Rounded = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(580, 24) };
        var origins = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(580, 0) };
        void AddRow(string title, Control control)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = title, CustomMinimumSize = new Vector2(120, 0) });
            row.AddChild(control);
            box.AddChild(row);
        }
        AddRow(BattleText("ui.battle.reorganize_from", "From team"), from);
        AddRow(BattleText("ui.battle.reorganize_to", "To team"), to);
        AddRow(BattleText("ui.battle.reorganize_active", "Active"), active);
        AddRow(BattleText("ui.battle.reorganize_wounded", "Wounded"), wounded);
        box.AddChild(origins);
        box.AddChild(status);
        void RefreshOrigins()
        {
            if (from.Selected < 0 || to.Selected < 0) { origins.Text = string.Empty; return; }
            string Describe(CampaignBattleTeamData team)
            {
                CampaignTroopOriginRules.EnsureInitialized(team);
                return string.Join("; ", team.TroopOrigins.OrderBy(item => item.CityId).Select(item =>
                {
                    var city = world.GetCity(item.CityId);
                    var name = city == null ? item.CityId.ToString() : _localization.GetCityName(city);
                    return $"{name}: {BattleText("ui.battle.reorganize_active", "Active")} {item.ActiveTroops}, {BattleText("ui.battle.reorganize_wounded", "Wounded")} {item.WoundedTroops}";
                }));
            }
            origins.Text = $"{BattleText("ui.battle.reorganize_from", "From team")}: {Describe(teams[from.Selected])}\n{BattleText("ui.battle.reorganize_to", "To team")}: {Describe(teams[to.Selected])}";
        }
        var updatingLimits = false;
        void RefreshLimits(bool reset)
        {
            if (updatingLimits) return;
            updatingLimits = true;
            if (reset) { active.SetValueNoSignal(0); wounded.SetValueNoSignal(0); }
            if (from.Selected < 0 || to.Selected < 0)
            {
                active.MaxValue = 0; wounded.MaxValue = 0;
            }
            else
            {
                var source = teams[from.Selected];
                var target = teams[to.Selected];
                var compatible = source != target && source.Side == target.Side && source.TroopType == target.TroopType;
                var capacity = compatible ? Math.Max(0, CampaignReorganizationRules.GetCapacity(target) - target.ActiveTroops - target.WoundedTroops) : 0;
                active.MaxValue = Math.Max(0, Math.Min(source.ActiveTroops, capacity - (int)wounded.Value));
                wounded.MaxValue = Math.Max(0, Math.Min(source.WoundedTroops, capacity - (int)active.Value));
            }
            updatingLimits = false;
        }
        from.ItemSelected += _ => { RefreshOrigins(); RefreshLimits(true); status.Text = string.Empty; };
        to.ItemSelected += _ => { RefreshOrigins(); RefreshLimits(true); status.Text = string.Empty; };
        active.ValueChanged += _ => RefreshLimits(false);
        wounded.ValueChanged += _ => RefreshLimits(false);
        void RefreshOptions(int sourceIndex, int targetIndex)
        {
            from.Clear(); to.Clear();
            foreach (var team in teams)
            {
                var officer = world.GetOfficer(team.OfficerId);
                var name = officer == null ? team.OfficerId.ToString() : _localization.GetOfficerName(officer);
                var waiting = !_activeCampaign.Teams.Contains(team)
                    ? $" | {BattleText("ui.battle.reorganize_waiting", "Arrived · awaiting deployment")} #{team.ReinforcementOrderId}"
                    : $" | #{team.Id}";
                var label = $"{name}  |  {team.TroopType}  |  {BattleText("ui.battle.reorganize_active", "Active")} {team.ActiveTroops}  {BattleText("ui.battle.reorganize_wounded", "Wounded")} {team.WoundedTroops}  / {CampaignReorganizationRules.GetCapacity(team)}{waiting}";
                // Unpromoted orders may reuse team IDs; selection is by list index.
                from.AddItem(label); to.AddItem(label);
            }
            if (teams.Count > 0) { from.Select(Math.Clamp(sourceIndex, 0, teams.Count - 1)); to.Select(Math.Clamp(targetIndex, 0, teams.Count - 1)); }
            else status.Text = BattleText("ui.battle.reorganize_empty", "No eligible player teams are available.");
            RefreshOrigins();
            RefreshLimits(true);
        }
        RefreshOptions(0, teams.Count > 1 ? 1 : 0);
        var buttons = new HBoxContainer();
        var transfer = new Button { Text = BattleText("ui.battle.reorganize_transfer", "Transfer"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Disabled = teams.Count < 2 };
        transfer.Pressed += () =>
        {
            if (_activeCampaign == null || from.Selected < 0 || to.Selected < 0) return;
            if (!CampaignReorganizationRules.TryTransfer(_activeCampaign, factionId, teams[from.Selected], teams[to.Selected], (int)active.Value, (int)wounded.Value, out _))
            {
                status.Text = BattleText("ui.battle.reorganize_invalid", "Transfer failed: check team compatibility, amounts and capacity.");
                return;
            }
            var sourceIndex = from.Selected; var targetIndex = to.Selected;
            RefreshOptions(sourceIndex, targetIndex);
            active.Value = 0; wounded.Value = 0; status.Text = string.Empty;
        };
        var done = new Button { Text = BattleText("ui.battle.reorganize_done", "Done"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        done.Pressed += () => { _reorganizationLayer?.QueueFree(); _reorganizationLayer = null; if (_campaignDecisionPanel != null) _campaignDecisionPanel.Visible = true; };
        buttons.AddChild(transfer); buttons.AddChild(done); box.AddChild(buttons);
    }
}
