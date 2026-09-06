using Godot;
using System.Linq;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
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
        return BattleFormat(
            "ui.battle.team_hud",
            "{0}   Troops: {1:N0} / {2:N0} wounded   Generals: {3:N0}   Workers: {4:N0}   Siege: {5:N0}   Gold: {6:N0}   Food: {7:N0}",
            FormatTeamName(info.Name),
            info.TotalTroops,
            info.WoundedTroops,
            info.TotalGenerals,
            GetActiveWorkerCountForTeam(info.Name),
            info.TotalSiegeUnits,
            info.TotalGold,
            info.TotalFood);
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
