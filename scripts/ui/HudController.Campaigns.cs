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
    private const string CampaignReturnLoadingOverlayName = "CampaignReturnLoadingOverlay";
    private const string CampaignBattleLoadingOverlayName = "CampaignBattleLoadingOverlay";
    private Button? _continueCampaignButton;
    private Control? _battleReportDialog;
    private bool _campaignBattleSceneLoading;

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

    internal bool TryResumeSavedCampaignBattle()
    {
        var campaign = GetPlayerCampaign();
        if (campaign == null || string.IsNullOrWhiteSpace(campaign.BattleSnapshotJson))
        {
            return false;
        }

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
        LoadCampaignBattleScene(scenePath);
    }

    private async void LoadCampaignBattleScene(string scenePath)
    {
        if (_campaignBattleSceneLoading)
        {
            return;
        }

        _campaignBattleSceneLoading = true;
        ShowCampaignBattleLoadingOverlay();
        // Give the persistent overlay one frame to draw before the map's battle
        // scene and its large assets begin loading in the background.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var requestError = ResourceLoader.LoadThreadedRequest(scenePath, "PackedScene");
        if (requestError != Error.Ok)
        {
            GD.PushWarning($"Could not start threaded battle load: {requestError}.");
            GetTree().ChangeSceneToFile(scenePath);
            return;
        }

        while (IsInsideTree())
        {
            var status = ResourceLoader.LoadThreadedGetStatus(scenePath);
            if (status == ResourceLoader.ThreadLoadStatus.Loaded)
            {
                var battleScene = ResourceLoader.LoadThreadedGet(scenePath) as PackedScene;
                if (battleScene != null)
                {
                    GetTree().ChangeSceneToPacked(battleScene);
                    return;
                }

                GD.PushWarning("Threaded battle load completed without a PackedScene.");
                break;
            }

            if (status == ResourceLoader.ThreadLoadStatus.Failed ||
                status == ResourceLoader.ThreadLoadStatus.InvalidResource)
            {
                GD.PushWarning($"Threaded battle load failed: {status}.");
                break;
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        GetTree().ChangeSceneToFile(scenePath);
    }

    private void ShowCampaignBattleLoadingOverlay()
    {
        var sceneRoot = GetTree().Root;
        if (sceneRoot.GetNodeOrNull<CanvasLayer>(CampaignBattleLoadingOverlayName) != null)
        {
            return;
        }

        var layer = new CanvasLayer
        {
            Name = CampaignBattleLoadingOverlayName,
            Layer = 100
        };
        sceneRoot.AddChild(layer);

        var shade = new ColorRect
        {
            Color = new Color(0.02f, 0.025f, 0.04f, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(shade);

        var center = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(420.0f, 92.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.055f, 0.06f, 0.09f, 0.97f),
            BorderColor = new Color(0.68f, 0.51f, 0.22f, 0.95f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        });
        center.AddChild(panel);

        var message = new Label
        {
            Text = _localization?.T("ui.battle.entering_battle") ?? "Entering battle…",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        message.AddThemeFontSizeOverride("font_size", 22);
        message.AddThemeColorOverride("font_color", new Color(0.94f, 0.86f, 0.68f, 1.0f));
        panel.AddChild(message);
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
        foreach (var dispositionResult in _commandResolver?.ResolveAiCapturedOfficerDispositions() ?? Enumerable.Empty<CommandResult>())
        {
            AddAiCapturedOfficerDispositionLog(dispositionResult);
        }
        var report = world.BattleReports.LastOrDefault(item =>
            !item.PlayerAcknowledged &&
            (item.AttackerFactionId == playerFactionId || item.DefenderFactionId == playerFactionId));
        if (report == null)
        {
            DismissCampaignReturnLoadingOverlay();
            if (_militaryUiController?.HasPendingPlayerCapturedOfficer() == true)
            {
                _militaryUiController.ShowCapturedOfficerDialog();
            }
            return;
        }

        var sourceCity = world.GetCity(report.SourceCityId);
        var targetCity = world.GetCity(report.TargetCityId);
        var isCaravanEscortReport = report.CaravanEscortOutcome != CaravanEscortOutcome.None;
        var winnerName = isCaravanEscortReport
            ? report.CaravanEscortOutcome == CaravanEscortOutcome.Plundered
                ? localization.T("ui.battle.caravan_report_ambush_side")
                : localization.Format(
                    "ui.battle.caravan_report_transport_side",
                    localization.GetFactionName(world, report.DefenderFactionId))
            : localization.GetFactionName(world, report.WinnerFactionId);
        var sourceName = sourceCity == null ? "?" : localization.GetCityName(sourceCity);
        var targetName = targetCity == null ? "?" : localization.GetCityName(targetCity);
        var capturedNames = report.CapturedOfficerIds
            .Distinct()
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
        var playerWon = isCaravanEscortReport
            ? report.CaravanEscortOutcome != CaravanEscortOutcome.Plundered
            : report.WinnerFactionId == playerFactionId;
        var outcomeLabel = _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/OutcomeLabel");
        outcomeLabel.Text = isCaravanEscortReport
            ? report.CaravanEscortOutcome switch
            {
                CaravanEscortOutcome.Delivered => localization.T("ui.battle.caravan_report_delivered"),
                CaravanEscortOutcome.ReturnedToSource => localization.T("ui.battle.caravan_report_returned"),
                _ => localization.T("ui.campaign.battle_report_defeat")
            }
            : localization.T(playerWon
                ? "ui.campaign.battle_report_victory"
                : "ui.campaign.battle_report_defeat");
        outcomeLabel.AddThemeColorOverride("font_color", playerWon
            ? new Color(0.94f, 0.84f, 0.62f, 1.0f)
            : new Color(0.9f, 0.38f, 0.34f, 1.0f));
        GameAudioController.Instance?.PlayBattleOutcomeBgm(playerWon);
        _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/WinnerLabel").Text =
            localization.Format("ui.campaign.battle_report_winner", winnerName);
        var attackerRulerName = isCaravanEscortReport
            ? localization.T("ui.battle.caravan_report_ambush_side")
            : localization.GetFactionName(world, report.AttackerFactionId);
        var defenderRulerName = localization.GetFactionName(world, report.DefenderFactionId);
        _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/ForceRow/AttackerPanel/Margin/Content/SideLabel").Text =
            isCaravanEscortReport
                ? attackerRulerName
                : localization.Format(
                    "ui.campaign.battle_report_side_ruler",
                    localization.T("ui.campaign.battle_report_attacker"),
                    attackerRulerName);
        _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/ForceRow/AttackerPanel/Margin/Content/ForceLabel").Text =
            localization.Format(
                "ui.campaign.battle_report_force",
                Math.Max(report.AttackerCommittedTroops, report.AttackerActiveTroops + report.AttackerWoundedTroops),
                report.AttackerLostTroops,
                report.AttackerWoundedTroops,
                report.AttackerReturnedTroops,
                report.AttackerActiveTroops);
        _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/ForceRow/DefenderPanel/Margin/Content/SideLabel").Text =
            isCaravanEscortReport
                ? localization.Format("ui.battle.caravan_report_transport_side", defenderRulerName)
                : localization.Format(
                    "ui.campaign.battle_report_side_ruler",
                    localization.T("ui.campaign.battle_report_defender"),
                    defenderRulerName);
        _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/ForceRow/DefenderPanel/Margin/Content/ForceLabel").Text =
            isCaravanEscortReport
                ? localization.Format(
                    "ui.battle.caravan_report_transport_force",
                    report.DefenderCommittedTroops,
                    report.DefenderLostTroops,
                    report.DefenderWoundedTroops,
                    report.CaravanArrivedTroops,
                    report.DefenderReturnedTroops)
                : localization.Format(
                    "ui.campaign.battle_report_force",
                    Math.Max(report.DefenderCommittedTroops, report.DefenderActiveTroops + report.DefenderWoundedTroops),
                    report.DefenderLostTroops,
                    report.DefenderWoundedTroops,
                    report.DefenderReturnedTroops,
                    report.DefenderActiveTroops);
        _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/ResourceLabel").Text =
            localization.Format(
                isCaravanEscortReport
                    ? "ui.battle.caravan_report_resources"
                    : "ui.campaign.battle_report_resources",
                report.AttackerGoldSpent,
                report.AttackerFoodSpent,
                report.AttackerGoldGained,
                report.AttackerFoodGained,
                report.DefenderGoldSpent,
                report.DefenderFoodSpent,
                report.DefenderGoldGained,
                report.DefenderFoodGained);
        _battleReportDialog.GetNode<Label>("Center/ReportPanel/Root/Body/Content/CapturedLabel").Text = capturedLine;

        var acknowledgeButton = _battleReportDialog.GetNode<Button>("Center/ReportPanel/Root/Body/Content/ButtonRow/AcknowledgeButton");
        acknowledgeButton.Text = localization.T("ui.campaign.battle_report_acknowledge");
        acknowledgeButton.Pressed += () => CloseBattleReport(report);
        var closeButton = _battleReportDialog.GetNodeOrNull<Button>("Center/ReportPanel/Root/Header/TitleRow/CloseButton");
        if (closeButton == null)
        {
            GD.PushWarning("Battle report close button is missing; the acknowledge button remains available.");
        }
        else
        {
            closeButton.Pressed += () => CloseBattleReport(report);
        }
        DismissCampaignReturnLoadingOverlay();
        _battleReportDialog.Visible = true;
    }

    private void DismissCampaignReturnLoadingOverlay()
    {
        GetTree().Root.GetNodeOrNull<CanvasLayer>(CampaignReturnLoadingOverlayName)?.QueueFree();
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
        GameAudioController.Instance?.StopBattleOutcomeBgm();
        GameAudioController.Instance?.PlayGameplayBgm();
        QueueCapturedRulerChangeOutcomes(
            report.CapturedOfficerIds,
            report.AttackerFactionId,
            report.AttackerRulerOfficerId,
            report.SourceCityId,
            report.TargetCityId,
            report.Year,
            report.Month);
        ShowNextFactionOutcomeIfPossible();
        if (_personnelUiController?.HasPendingPlayerSuccession() == true)
        {
            _personnelUiController.ShowSuccessionDialog();
            return;
        }
        if (_militaryUiController?.HasPendingPlayerCapturedOfficer() == true)
        {
            _militaryUiController.ShowCapturedOfficerDialog();
        }
    }
}
