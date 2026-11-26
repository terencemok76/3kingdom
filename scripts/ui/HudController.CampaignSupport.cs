using System;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private CanvasLayer? _aiPlayerSupportLayer;

    private void ShowAiPlayerSupportRequest(PendingCommandData attack, PlayerCampaignSupportRequestData request,
        Func<PlayerCampaignSupportRequestData, bool>? acceptHandler = null, Action? onResolved = null)
    {
        if (_aiPlayerSupportLayer != null || _turnManager?.World == null || _localization == null) return;
        var world = _turnManager.World;
        var localization = _localization;
        var playerId = _turnManager.GetPlayerFactionId();
        var target = world.GetCity(attack.TargetCityId);
        _aiPlayerSupportLayer = new CanvasLayer { Layer = 250 };
        AddChild(_aiPlayerSupportLayer);
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.72f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _aiPlayerSupportLayer.AddChild(shade);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _aiPlayerSupportLayer.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(620, 0) };
        center.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        box.AddChild(new Label { Text = localization.T("ui.ai_support.title"), HorizontalAlignment = HorizontalAlignment.Center });
        box.AddChild(new Label
        {
            Text = localization.Format("ui.ai_support.summary", localization.GetFactionName(world, request.InviterFactionId),
                target == null ? "?" : localization.GetCityName(target), request.RequestedTroops, request.RequestedGold, request.RequestedFood),
            AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0)
        });
        var opponentId = request.Side == CampaignBattleSide.Defender ? attack.ActorFactionId : target?.OwnerFactionId ?? 0;
        if (request.RequestedTroops > 0 && opponentId > 0)
        {
            var relation = world.GetDiplomacyRelation(playerId, opponentId);
            var hasPact = relation is { RemainingMonths: > 0, Status: DiplomacyStatusType.Alliance or DiplomacyStatusType.Truce };
            box.AddChild(new Label
            {
                Text = localization.Format(hasPact ? "ui.ai_support.troops_pact_warning" : "ui.ai_support.troops_hostility_warning",
                    localization.GetFactionName(world, opponentId)),
                AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0)
            });
        }
        var source = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var officer = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var troopType = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var troops = new SpinBox { MinValue = 0, Step = 1, Rounded = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var gold = new SpinBox { MinValue = 0, Step = 1, Rounded = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var food = new SpinBox { MinValue = 0, Step = 1, Rounded = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var warning = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 24) };
        void AddRow(string key, Control control)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = localization.T(key), CustomMinimumSize = new Vector2(120, 0) });
            row.AddChild(control); box.AddChild(row);
        }
        AddRow("ui.ai_support.source", source);
        AddRow("ui.ai_support.officer", officer);
        AddRow("ui.ai_support.troop_type", troopType);
        AddRow("ui.ai_support.troops", troops);
        AddRow("ui.ai_support.gold", gold);
        AddRow("ui.ai_support.food", food);
        box.AddChild(warning);
        foreach (var type in new[] { TroopType.Infantry, TroopType.Spearman, TroopType.Cavalry, TroopType.Archer, TroopType.Crossbow })
        {
            troopType.AddItem(localization.T("ui.ai_support.type_" + type.ToString().ToLowerInvariant()));
            troopType.SetItemMetadata(troopType.ItemCount - 1, (int)type);
        }
        foreach (var city in world.Cities.Where(city => city.OwnerFactionId == playerId &&
                     BattleCampaignService.GetFriendlyRouteLinks(world, city.Id, attack.TargetCityId, playerId) > 0).OrderBy(city => city.Id))
        {
            var days = BattleCampaignService.GetFriendlyRouteLinks(world, city.Id, attack.TargetCityId, playerId) * 2;
            source.AddItem($"{localization.GetCityName(city)} · {days} {localization.T("ui.campaign.invite_eta_days")}");
            source.SetItemMetadata(source.ItemCount - 1, city.Id);
        }
        int Selected(OptionButton option) => option.Selected < 0 ? 0 : option.GetItemMetadata(option.Selected).AsInt32();
        void RefreshLimits()
        {
            var city = world.GetCity(Selected(source));
            var type = (TroopType)Selected(troopType);
            troops.MaxValue = city == null ? 0 : Math.Max(0, Math.Min(request.RequestedTroops,
                Math.Min(city.GetTroops(type), city.Troops - BattleCampaignService.MinimumCityGarrison)));
            gold.MaxValue = city == null ? 0 : Math.Max(0, Math.Min(request.RequestedGold, city.Gold));
            food.MaxValue = city == null ? 0 : Math.Max(0, Math.Min(request.RequestedFood, city.Food));
        }
        void RefreshSource()
        {
            officer.Clear();
            officer.AddItem(localization.T("ui.ai_support.choose_officer")); officer.SetItemMetadata(0, 0);
            var city = world.GetCity(Selected(source));
            if (city != null)
                foreach (var item in city.OfficerIds.Select(world.GetOfficer).Where(item => item != null &&
                             item.CityId == city.Id && world.GetFaction(playerId)?.OfficerIds.Contains(item.Id) == true &&
                             item.CaptiveFactionId <= 0 && (item.DeathYear <= 0 || world.Year <= item.DeathYear) &&
                             !BattleCampaignService.IsOfficerCommitted(world, item.Id) && !OfficerAvailabilityRules.IsTravelingWithLogistics(world, item.Id) &&
                             !(item.LastAssignedYear == world.Year && item.LastAssignedMonth == world.Month) &&
                             !world.InternalAffairsSchedules.Any(schedule => schedule.State == InternalAffairsScheduleState.Active && schedule.OfficerId == item.Id)).OrderBy(item => item!.Id))
                {
                    officer.AddItem(localization.GetOfficerName(item!)); officer.SetItemMetadata(officer.ItemCount - 1, item!.Id);
                }
            RefreshLimits();
        }
        source.ItemSelected += _ => RefreshSource();
        troopType.ItemSelected += _ => RefreshLimits();
        RefreshSource();
        var buttons = new HBoxContainer();
        var accept = new Button { Text = localization.T("ui.accept"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var reject = new Button { Text = localization.T("ui.reject"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        void Finish()
        {
            _aiPlayerSupportLayer?.QueueFree(); _aiPlayerSupportLayer = null;
            Callable.From(onResolved ?? ContinuePendingAttackResolution).CallDeferred();
        }
        accept.Pressed += () =>
        {
            request.SourceCityId = Selected(source); request.OfficerId = Selected(officer); request.TroopType = (TroopType)Selected(troopType);
            request.Troops = (int)troops.Value; request.Gold = (int)gold.Value; request.Food = (int)food.Value;
            if (!AiPlayerCampaignSupportRules.ValidateResponse(world, attack, request))
            {
                warning.Text = localization.T("ui.ai_support.invalid");
                return;
            }
            if (acceptHandler != null && !acceptHandler(request))
            {
                warning.Text = localization.T("ui.ai_support.invalid");
                return;
            }
            request.Status = BattleInvitationStatus.Accepted;
            Finish();
        };
        reject.Pressed += () => { request.Status = BattleInvitationStatus.Declined; Finish(); };
        buttons.AddChild(accept); buttons.AddChild(reject); box.AddChild(buttons);
    }

    private bool TryShowPendingAiCampaignSupport(Action onResolved, int campaignId = 0)
    {
        if (_turnManager?.World is not { } world || _localization == null) return false;
        var playerId = _turnManager.GetPlayerFactionId();
        foreach (var active in world.ActiveBattleCampaigns.Where(item => item.Stage != CampaignStage.Resolved &&
                     (campaignId <= 0 || item.Id == campaignId)))
            foreach (var pending in active.Invitations.Where(item => item.InvitedFactionId == playerId &&
                item.Status == BattleInvitationStatus.Pending && item.ResponseBattleDays <= 0).ToList())
                BattleCampaignService.DeclineUnavailableInvitationEnvoy(world, active, pending);
        var entry = AiPlayerCampaignSupportRules.FindPendingCampaignRequest(world, playerId, campaignId);
        if (entry.Campaign == null || entry.Invitation == null) return false;
        var campaign = entry.Campaign;
        var invitation = entry.Invitation;
        var attack = new PendingCommandData { TargetCityId = campaign.TargetCityId, ActorFactionId = campaign.AttackerFactionId };
        var request = new PlayerCampaignSupportRequestData { Side = invitation.Side, InviterFactionId = invitation.InviterFactionId,
            EnvoyOfficerId = invitation.EnvoyOfficerId, RequestedTroops = invitation.RequestedTroops,
            RequestedGold = invitation.RequestedGold, RequestedFood = invitation.RequestedFood };
        bool Accept(PlayerCampaignSupportRequestData response) =>
            AiPlayerCampaignSupportRules.RespondToCampaignRequest(world, campaign, invitation, response, true);
        void Resolved()
        {
            if (request.Status == BattleInvitationStatus.Declined)
            {
                AiPlayerCampaignSupportRules.RespondToCampaignRequest(world, campaign, invitation, request, false);
            }
            onResolved();
        }
        ShowAiPlayerSupportRequest(attack, request, Accept, Resolved);
        return true;
    }

    private void ShowPendingAiCampaignSupportAfterTurn()
    {
        if (_aiPlayerSupportLayer != null || _gameEnded) return;
        TryShowPendingAiCampaignSupport(ShowPendingAiCampaignSupportAfterTurn);
    }

    private void ResumeCampaignSupportResponseIfNeeded()
    {
        if (_turnManager?.World is not { } world) return;
        var campaign = world.ActiveBattleCampaigns.FirstOrDefault(item => item.Stage != CampaignStage.Resolved && item.IsAwaitingPlayerSupportResponse);
        if (campaign == null) return;
        void Resume()
        {
            if (TryShowPendingAiCampaignSupport(Resume, campaign.Id)) return;
            campaign.IsAwaitingPlayerSupportResponse = false;
            LaunchCampaignBattle(campaign.Id);
        }
        Resume();
    }
}
