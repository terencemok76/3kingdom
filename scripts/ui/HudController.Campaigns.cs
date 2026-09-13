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
    private const string BattleReportDialogScenePath = "res://scenes/ui/main/BattleReportDialog.tscn";
    private Button? _continueCampaignButton;
    private Control? _battleReportDialog;

    private void ConfigureCampaignUi()
    {
        var topBar = GetNodeOrNull<HBoxContainer>("Root/TopBar");
        if (topBar == null)
        {
            return;
        }

        if (_continueCampaignButton != null)
        {
            _continueCampaignButton.QueueFree();
            _continueCampaignButton = null;
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

    private bool TryLaunchPlayerCampaignForCurrentMonth()
    {
        var campaign = GetPlayerCampaign();
        if (campaign == null ||
            campaign.Stage == CampaignStage.SiegePreparation ||
            campaign.BattleDaysThisMonth != 0 ||
            string.IsNullOrWhiteSpace(campaign.BattleSnapshotJson))
        {
            return false;
        }

        _turnManager!.World!.IsBattleResolutionPhase = true;
        LaunchCampaignBattle(campaign.Id);
        return true;
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
        // Keep the map in its current month's battle-resolution phase.  The
        // player must acknowledge the report and press End Turn before the next
        // queued battle is launched.
        world.IsBattleResolutionPhase = true;
    }

    private void ShowPendingBattleReportIfNeeded()
    {
        var world = _turnManager?.World;
        var localization = _localization;
        if (world == null || localization == null || _battleReportDialog != null)
        {
            return;
        }

        var playerFactionId = _turnManager!.GetPlayerFactionId();
        var report = world.BattleReports.LastOrDefault(item =>
            !item.PlayerAcknowledged &&
            (item.AttackerFactionId == playerFactionId || item.DefenderFactionId == playerFactionId));
        if (report == null)
        {
            if (_militaryUiController?.HasPendingPlayerCapturedOfficer() == true)
            {
                _militaryUiController.ShowCapturedOfficerDialog();
            }
            return;
        }

        var sourceCity = world.GetCity(report.SourceCityId);
        var targetCity = world.GetCity(report.TargetCityId);
        var winnerName = localization.GetFactionName(world, report.WinnerFactionId);
        var sourceName = sourceCity == null ? "?" : localization.GetCityName(sourceCity);
        var targetName = targetCity == null ? "?" : localization.GetCityName(targetCity);
        var capturedNames = report.CapturedOfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null)
            .Select(officer => localization.GetOfficerName(officer!))
            .ToList();
        var capturedLine = capturedNames.Count == 0
            ? localization.T("ui.campaign.battle_report_captured_none")
            : localization.Format(
                "ui.campaign.battle_report_captured_list",
                string.Join(localization.IsTraditionalChinese ? "、" : ", ", capturedNames));

        _battleReportDialog = GD.Load<PackedScene>(BattleReportDialogScenePath).Instantiate<Control>();
        var overlayRoot = GetNodeOrNull<Control>("Root") as Node ?? this;
        overlayRoot.AddChild(_battleReportDialog);

        _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/RouteLabel").Text =
            localization.Format("ui.campaign.battle_report_route", sourceName, targetName);
        _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/WinnerLabel").Text =
            localization.Format("ui.campaign.battle_report_winner", winnerName);
        _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/ForceRow/AttackerPanel/Margin/Content/SideLabel").Text =
            localization.T("ui.campaign.battle_report_attacker");
        _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/ForceRow/AttackerPanel/Margin/Content/ForceLabel").Text =
            localization.Format("ui.campaign.battle_report_force", report.AttackerActiveTroops, report.AttackerWoundedTroops);
        _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/ForceRow/DefenderPanel/Margin/Content/SideLabel").Text =
            localization.T("ui.campaign.battle_report_defender");
        _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/ForceRow/DefenderPanel/Margin/Content/ForceLabel").Text =
            localization.Format("ui.campaign.battle_report_force", report.DefenderActiveTroops, report.DefenderWoundedTroops);
        _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/CapturedLabel").Text = capturedLine;

        var acknowledgeButton = _battleReportDialog.GetNode<Button>("Center/ReportPanel/Root/Body/Content/ButtonRow/AcknowledgeButton");
        acknowledgeButton.Text = localization.T("ui.campaign.battle_report_acknowledge");
        acknowledgeButton.Pressed += () => CloseBattleReport(report);
        _battleReportDialog.GetNode<Button>("Center/ReportPanel/Root/Header/CloseButton").Pressed += () => CloseBattleReport(report);
        _battleReportDialog.Visible = true;
    }

    private void CloseBattleReport(WorldState.BattleReportData report)
    {
        if (_battleReportDialog == null)
        {
            return;
        }

        report.PlayerAcknowledged = true;
        _battleReportDialog.QueueFree();
        _battleReportDialog = null;
        if (_militaryUiController?.HasPendingPlayerCapturedOfficer() == true)
        {
            _militaryUiController.ShowCapturedOfficerDialog();
        }
    }
}
