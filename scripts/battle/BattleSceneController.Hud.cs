using Godot;
using System.Linq;
using ThreeKingdom.Core;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private void RefreshTeamHudCards(BattleHudTeamInfo attackerInfo, BattleHudTeamInfo defenderInfo)
    {
        RefreshTeamHudCard("Attacker", attackerInfo);
        RefreshTeamHudCard("Defender", defenderInfo);
    }

    private void RefreshTeamHudCard(string cardPrefix, BattleHudTeamInfo info)
    {
        var isCaravanEscort = _activeCampaign?.IsCaravanEscortBattle == true;
        var isTransport = isCaravanEscort && !BattleTeamIdentity.IsAttacker(info.Name);
        var sideLabel = isCaravanEscort
            ? BattleTeamIdentity.IsAttacker(info.Name)
                ? BattleText("ui.battle.caravan_ambush", "Ambush")
                : BattleText("ui.battle.caravan_transport", "Transport")
            : BattleTeamIdentity.IsAttacker(info.Name)
                ? BattleText("ui.battle.log_attacker", "Attacker")
                : BattleText("ui.battle.log_defender", "Defender");
        var displayedGold = isTransport ? _activeCampaign!.CaravanGold : info.TotalGold;
        var displayedFood = isTransport ? _activeCampaign!.CaravanFood : info.TotalFood;

        SetTeamHudCardLabel(cardPrefix, "FactionLabel", BattleFormat("ui.battle.team_card_faction", "{0} · {1}", sideLabel, FormatTeamName(info.Name)));
        // Each icon already expresses its category. Keep the card compact by
        // rendering only the live numeric value beside it.
        SetTeamHudCardLabel(cardPrefix, "TroopsLabel", info.TotalTroops.ToString("N0"));
        SetTeamHudCardLabel(cardPrefix, "OfficersLabel", info.TotalGenerals.ToString("N0"));
        SetTeamHudCardLabel(cardPrefix, "EngineersLabel", GetActiveWorkerCountForTeam(info.Name).ToString("N0"));
        SetTeamHudCardLabel(cardPrefix, "SiegeLabel", info.TotalSiegeUnits.ToString("N0"));
        SetTeamHudCardLabel(cardPrefix, "GoldLabel", displayedGold.ToString("N0"));
        SetTeamHudCardLabel(cardPrefix, "FoodLabel", displayedFood.ToString("N0"));
    }

    private void SetTeamHudCardLabel(string cardPrefix, string labelName, string text)
    {
        var label = GetNodeOrNull<Label>($"UiLayer/TopBar/{cardPrefix}Card/{cardPrefix}{labelName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void RefreshTeamHudIdentity()
    {
        RefreshTeamHudPortrait(TeamAInfo.Name, "UiLayer/TopBar/AttackerCard/AttackerPortraitFrame/AttackerPortrait");
        RefreshTeamHudPortrait(TeamBInfo.Name, "UiLayer/TopBar/DefenderCard/DefenderPortraitFrame/DefenderPortrait");
        RefreshTeamHudRulerSurname(TeamAInfo.Name, "UiLayer/TopBar/AttackerCard/AttackerFlag/AttackerRulerSurnameLabel");
        RefreshTeamHudRulerSurname(TeamBInfo.Name, "UiLayer/TopBar/DefenderCard/DefenderFlag/DefenderRulerSurnameLabel");
    }

    private void RefreshTeamHudRulerSurname(string teamName, string nodePath)
    {
        var label = GetNodeOrNull<Label>(nodePath);
        if (label == null)
        {
            return;
        }

        var rulerName = GetTeamHudRulerName(teamName).Trim();
        label.Text = string.IsNullOrEmpty(rulerName) ? string.Empty : rulerName[..1];
    }

    private string GetTeamHudRulerName(string teamName)
    {
        if (_activeCampaign != null && CampaignRuntimeContext.World is { } world)
        {
            var factionId = BattleTeamIdentity.IsAttacker(teamName)
                ? _activeCampaign.AttackerFactionId
                : _activeCampaign.DefenderFactionId;
            var ruler = world.GetFaction(factionId) is { } faction
                ? world.GetOfficer(faction.RulerOfficerId)
                : null;
            if (ruler != null)
            {
                return _localization.GetOfficerName(ruler);
            }
        }

        // Standalone test battles do not have a campaign faction. Their
        // localized team display name still supplies the intended ruler name.
        return FormatTeamName(teamName);
    }

    private void RefreshTeamHudPortrait(string teamName, string nodePath)
    {
        var portrait = GetNodeOrNull<TextureRect>(nodePath);
        if (portrait == null)
        {
            return;
        }

        var commandingOfficer = _occupantsByGrid.Values
            .SelectMany(occupants => occupants)
            .Where(occupant => occupant.TeamName == teamName &&
                               IsGeneralCountedPiece(occupant.Category, occupant.OfficerName))
            .OrderByDescending(occupant => GetOfficerBattleAttribute(occupant.OfficerName))
            .FirstOrDefault();
        portrait.Texture = commandingOfficer == null ? null : GetOfficerPortraitTexture(commandingOfficer);
        portrait.Visible = portrait.Texture != null;
    }

    private void RefreshCoordinateLabel()
    {
        var coordinateLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/CoordinateLabel");
        if (coordinateLabel != null)
        {
            coordinateLabel.Text = BuildCoordinateText();
        }
    }

    private void RefreshInfoPanel()
    {
        var infoLabel = GetNodeOrNull<Label>("UiLayer/SidePanel/Margin/PanelContent/InfoScroll/InfoPadding/InfoLabel") ??
                        GetNodeOrNull<Label>("UiLayer/SidePanel/Margin/PanelContent/InfoScroll/InfoLabel") ??
                        GetNodeOrNull<Label>("UiLayer/SidePanel/Margin/PanelContent/InfoLabel");
        if (infoLabel == null)
        {
            return;
        }

        infoLabel.Text = BuildInfoText();
    }

    private string BuildCoordinateText()
    {
        var coordinateText = BattleFormat(
            "ui.battle.coordinate_hover_click",
            "Hover: {0}    Click: {1}",
            FormatGrid(_hoverGridKey, _hoverGrid),
            FormatGrid(_selectedGridKey, _selectedGrid));
        if (!TryGetMovePreview(_hoverGridKey, out var energyCost, out var remainingEnergy, out var remainingMoveRange))
        {
            return coordinateText;
        }

        return BattleFormat(
            "ui.battle.coordinate_move_preview",
            "{0}    Move: -{1}, Remaining Energy {2}, Move Range {3}/{4}; {5}",
            coordinateText,
            energyCost,
            remainingEnergy,
            remainingMoveRange,
            _selectedUnit!.MoveRange,
            remainingEnergy >= NormalAttackEnergyCost
                ? BattleText("ui.battle.can_attack_after_move", "can attack")
                : BattleText("ui.battle.cannot_attack_after_move", "cannot attack"));
    }

    private string BuildTeamHudText(BattleHudTeamInfo info)
    {
        var isCaravanEscort = _activeCampaign?.IsCaravanEscortBattle == true;
        var isTransport = isCaravanEscort && !BattleTeamIdentity.IsAttacker(info.Name);
        var sideLabel = isCaravanEscort
            ? BattleTeamIdentity.IsAttacker(info.Name)
                ? BattleText("ui.battle.caravan_ambush", "Ambush")
                : BattleText("ui.battle.caravan_transport", "Transport")
            : BattleTeamIdentity.IsAttacker(info.Name)
                ? BattleText("ui.battle.log_attacker", "Attacker")
                : BattleText("ui.battle.log_defender", "Defender");
        // Caravan gold and food are cargo, not battle upkeep. Display the
        // locked shipment values instead of the generic campaign supply pool,
        // which is intentionally zero for an escort battle.
        var displayedGold = isTransport ? _activeCampaign!.CaravanGold : info.TotalGold;
        var displayedFood = isTransport ? _activeCampaign!.CaravanFood : info.TotalFood;
        return BattleFormat(
            "ui.battle.team_hud_compact",
            "{0} · {1}\nTroops {2:N0}   Wounded {3:N0}\n  Generals {4:N0}    Workers {5:N0}    Siege {6:N0}\n  Gold {7:N0}      Food {8:N0}",
            sideLabel,
            FormatTeamName(info.Name),
            info.TotalTroops,
            info.WoundedTroops,
            info.TotalGenerals,
            GetActiveWorkerCountForTeam(info.Name),
            info.TotalSiegeUnits,
            displayedGold,
            displayedFood);
    }

    private int GetActiveWorkerCountForTeam(string teamName)
    {
        return _occupantsByGrid.Values
            .SelectMany(occupants => occupants)
            .Count(occupant => occupant.Category == CategoryUnit &&
                               occupant.TroopType == TroopWorker &&
                               occupant.TeamName == teamName);
    }

    private int GetTotalWoundedTroopsForTeam(string teamName)
    {
        var total = 0;
        foreach (var occupants in _occupantsByGrid.Values)
        {
            foreach (var occupant in occupants)
            {
                if (occupant.Category == CategoryUnit && occupant.TeamName == teamName)
                {
                    total += occupant.WoundedTroops;
                }
            }
        }

        return total;
    }
}
