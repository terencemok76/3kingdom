using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private bool IsForestGrid(BattleGridKey grid)
    {
        return _mapData != null &&
               grid.Level == 0 &&
               IsWithinMap(grid.Grid) &&
               _mapData.GetCell(grid.X, grid.Y).Terrain == BattleTerrainType.Forest;
    }

    private bool CanHideAtGrid(BattleGridKey grid, BattleOccupantInfo unit)
    {
        return IsBattlePiece(unit) &&
               !unit.IsHidden &&
               IsForestGrid(grid);
    }

    private bool CanHideSelectedUnit()
    {
        return _selectedUnit != null &&
               _selectedUnitGrid.HasValue &&
               CanHideAtGrid(_selectedUnitGrid.Value, _selectedUnit);
    }

    private bool IsHiddenFromSide(BattleOccupantInfo occupant, string viewerTeamName)
    {
        return occupant.IsHidden && occupant.TeamName != viewerTeamName;
    }

    private bool IsHiddenAmbushAttack(BattleGridKey sourceGrid, BattleOccupantInfo attacker)
    {
        return attacker.IsHidden &&
               attacker.Category == CategoryUnit &&
               IsForestGrid(sourceGrid);
    }

    private string FormatAmbushAttackLog(bool isHiddenAmbush)
    {
        return isHiddenAmbush
            ? $"; {BattleText("ui.battle.ambush_damage_bonus", "Ambush: damage +25%")}"
            : string.Empty;
    }

    private bool IsVisibleToCurrentTurnSide(BattleOccupantInfo occupant)
    {
        return !IsHiddenFromSide(occupant, GetCurrentTurnSideName());
    }

    private void RefreshHiddenUnitVisibility()
    {
        foreach (var occupants in _occupantsByGrid.Values)
        {
            foreach (var occupant in occupants)
            {
                ApplyHiddenMarkerVisibility(occupant);
            }
        }
    }

    private void ApplyHiddenMarkerVisibility(BattleOccupantInfo occupant)
    {
        if (occupant.Marker == null || !IsBattlePiece(occupant))
        {
            return;
        }

        var visible = IsVisibleToCurrentTurnSide(occupant);
        occupant.Marker.Visible = visible;
        occupant.Marker.SetHiddenBodyVisual(visible && occupant.IsHidden);
    }


}
