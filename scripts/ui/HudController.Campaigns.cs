using System;
using System.Linq;
using Godot;
using ThreeKingdom.Battle;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private const string MainScenePath = "res://scenes/main/Main.tscn";
    private Button? _continueCampaignButton;

    private void ConfigureCampaignUi()
    {
        var topBar = GetNodeOrNull<HBoxContainer>("Root/TopBar");
        if (topBar == null)
        {
            return;
        }

        if (_continueCampaignButton == null)
        {
            _continueCampaignButton = new Button
            {
                Name = "ContinueCampaignButton",
                Text = "持續戰役 / Campaign",
                CustomMinimumSize = new Vector2(150.0f, 36.0f)
            };
            _continueCampaignButton.Pressed += OnContinueCampaignPressed;
            topBar.AddChild(_continueCampaignButton);
            topBar.MoveChild(_continueCampaignButton, Math.Max(0, topBar.GetChildCount() - 2));
        }

        var campaign = GetPlayerCampaign();
        _continueCampaignButton.Visible = campaign != null;
        _continueCampaignButton.Disabled = campaign?.Stage == CampaignStage.SiegePreparation;
        _continueCampaignButton.Text = campaign?.Stage == CampaignStage.SiegePreparation
            ? _localization?.T("ui.campaign.preparing") ?? "Preparing next month"
            : campaign == null
                ? "持續戰役 / Campaign"
                : _localization?.Format("ui.campaign.continue", campaign.BattleDaysThisMonth) ?? $"Campaign {campaign.BattleDaysThisMonth}/10";
        if (campaign != null)
        {
            var active = campaign.Teams.Count(team => team.Location is CampaignTeamLocation.Field or CampaignTeamLocation.InnerCity);
            var reserve = campaign.Teams.Count(team => team.Location == CampaignTeamLocation.Reserve) +
                          campaign.Reinforcements.Sum(order => order.Teams.Count(team => team.Location == CampaignTeamLocation.Reserve));
            var nextEta = campaign.Reinforcements
                .Where(order => order.Status == ReinforcementStatus.Traveling)
                .Select(order => order.RemainingBattleDays)
                .DefaultIfEmpty(-1)
                .Min();
            _continueCampaignButton.TooltipText = nextEta >= 0
                ? _localization?.Format("ui.campaign.status_eta", active, reserve, nextEta) ?? $"Active {active}, reserve {reserve}, ETA {nextEta}"
                : _localization?.Format("ui.campaign.status_no_eta", active, reserve) ?? $"Active {active}, reserve {reserve}";
        }
    }

    private ActiveBattleCampaignData? GetPlayerCampaign()
    {
        if (_turnManager?.World == null)
        {
            return null;
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        return _turnManager.World.ActiveBattleCampaigns.Find(campaign =>
            campaign.Stage != CampaignStage.Resolved &&
            (campaign.AttackerFactionId == playerFactionId || campaign.DefenderFactionId == playerFactionId));
    }

    private void OnContinueCampaignPressed()
    {
        var campaign = GetPlayerCampaign();
        if (campaign != null)
        {
            LaunchCampaignBattle(campaign.Id);
        }
    }

    private void LaunchCampaignBattle(int campaignId)
    {
        if (_turnManager?.World == null)
        {
            return;
        }

        var campaign = _turnManager.World.ActiveBattleCampaigns.Find(item => item.Id == campaignId);
        var targetCity = campaign == null ? null : _turnManager.World.GetCity(campaign.TargetCityId);
        if (campaign == null || targetCity == null)
        {
            return;
        }

        CampaignRuntimeContext.Launch(_turnManager.World, campaign.Id);
        var scenarioType = campaign.Stage == CampaignStage.FieldBattle
            ? BattleScenarioType.FieldBattle
            : BattleScenarioType.SiegeAssault;
        var scenePath = ResolveCampaignScenePath(targetCity, scenarioType);
        BattleSceneController.PendingLaunchOptions =
            new BattleSceneController.LaunchOptions(scenarioType, true, campaign.Id);
        GetTree().ChangeSceneToFile(scenePath);
    }

    private static string ResolveCampaignScenePath(CityData city, BattleScenarioType scenarioType)
    {
        if (scenarioType != BattleScenarioType.FieldBattle)
        {
            return city.Id % 2 == 0
                ? "res://scenes/battle/BattleSceneNorthWest.tscn"
                : "res://scenes/battle/BattleScene.tscn";
        }

        return city.NameEn.Trim().ToUpperInvariant() switch
        {
            "HANZHONG" => "res://scenes/battle/field/FieldBattleHanzhong.tscn",
            "YE" => "res://scenes/battle/field/FieldBattleYe.tscn",
            "JINYANG" => "res://scenes/battle/field/FieldBattleJinyang.tscn",
            "XIAPI" => "res://scenes/battle/field/FieldBattleXiapi.tscn",
            "JIANYE" => "res://scenes/battle/field/FieldBattleJianye.tscn",
            "XIANGYANG" => "res://scenes/battle/field/FieldBattleXiangyang.tscn",
            "JIANGLING" => "res://scenes/battle/field/FieldBattleJiangling.tscn",
            _ => "res://scenes/battle/field/FieldBattleLuoyang.tscn"
        };
    }

    private void ResumeAttackResolutionAfterCampaignIfNeeded()
    {
        if (_turnManager?.World is not { ResumeAttackResolutionAfterCampaign: true } world)
        {
            return;
        }

        world.ResumeAttackResolutionAfterCampaign = false;
        _isResolvingEndTurn = true;
        _pendingAttackResolutionQueue.Clear();
        _pendingAttackResolutionQueue.AddRange(_turnManager.GetPendingCommandsOfType(CommandType.Attack));
        ContinuePendingAttackResolution();
    }
}
