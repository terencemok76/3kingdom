using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using static ThreeKingdom.Battle.BattleAiSettings;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattlePresentationSettings;
using static ThreeKingdom.Battle.BattleResourcePaths;
using static ThreeKingdom.Battle.BattleUnitTypes;
using static ThreeKingdom.Battle.BattleUnitVisualCatalog;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private bool IgniteBattleFire(BattleGridKey grid)
    {
        if (_mapData == null || grid.Level != 0 || !IsWithinMap(grid.Grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        if (!CanCellIgnite(cell))
        {
            return false;
        }

        var duration = GetInitialFireDuration(GetCurrentBattleWeather(), cell);
        var burnTurns = 0;
        if (_activeFireByGrid.TryGetValue(grid, out var existingFire))
        {
            duration = Math.Max(duration, existingFire.RemainingTurns);
            burnTurns = existingFire.BurnTurns;
        }

        _activeFireByGrid[grid] = new BattleFireState(duration, burnTurns);
        RefreshFireVisual(grid);
        RefreshBattleDepthLayerOrder();
        return true;
    }

    private void ResolveBattleFireAtTimePeriodEnd()
    {
        if (_mapData == null || _activeFireByGrid.Count == 0)
        {
            return;
        }

        var weather = GetCurrentBattleWeather();
        var activeFires = _activeFireByGrid.ToArray();
        var pendingNewFires = new HashSet<BattleGridKey>();
        var expiredFires = new List<BattleGridKey>();

        foreach (var (grid, state) in activeFires)
        {
            ApplyBattleFireDamage(grid, weather);

            if (CanFireSpread(weather, grid, state))
            {
                foreach (var spreadGrid in GetFireSpreadTargets(grid))
                {
                    if (!_activeFireByGrid.ContainsKey(spreadGrid))
                    {
                        pendingNewFires.Add(spreadGrid);
                    }
                }
            }

            var remainingTurns = state.RemainingTurns - 1;
            if (remainingTurns <= 0)
            {
                expiredFires.Add(grid);
            }
            else
            {
                _activeFireByGrid[grid] = state with
                {
                    RemainingTurns = remainingTurns,
                    BurnTurns = state.BurnTurns + 1
                };
                RefreshFireVisual(grid);
            }
        }

        foreach (var grid in expiredFires)
        {
            _activeFireByGrid.Remove(grid);
            RemoveFireVisual(grid);
        }

        foreach (var spreadGrid in pendingNewFires)
        {
            IgniteBattleFire(spreadGrid);
        }

        RefreshInfoPanel();
    }

    private void ApplyBattleFireDamage(BattleGridKey targetGrid, BattleWeatherType weather)
    {
        ApplyBattleFireDamageToOccupants(targetGrid, GetFireDamagePerTurn(weather));
        ApplyBattleFireDamageToStructure(targetGrid);
    }

    private void ApplyBattleFireEntryDamage(BattleGridKey grid, BattleOccupantInfo enteringOccupant)
    {
        if (grid.Level != 0 ||
            !_activeFireByGrid.ContainsKey(grid) ||
            !TryGetCurrentOccupantAtGrid(grid, enteringOccupant, out var currentOccupant))
        {
            return;
        }

        ApplyBattleFireDamageToOccupants(grid, GetFireDamagePerTurn(GetCurrentBattleWeather()), currentOccupant);
    }

    private bool IsAiSafeMovementDestination(BattleGridKey grid)
    {
        return grid.Level != 0 || !_activeFireByGrid.ContainsKey(ToGroundGridKey(grid.Grid));
    }

    private void ApplyBattleFireDamageToOccupants(BattleGridKey targetGrid, int damage, BattleOccupantInfo? targetOverride = null)
    {
        if (damage <= 0 || !_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            return;
        }

        var target = targetOverride == null
            ? GetFireDamageTarget(targetOccupants)
            : TryGetCurrentOccupantAtGrid(targetGrid, targetOverride, out var currentTarget)
                ? currentTarget
                : null;
        if (target == null)
        {
            return;
        }

        if (IsClosedGateGroundFireProtected(targetGrid, target))
        {
            return;
        }

        var casualtyResult = ApplyUnitCasualties(targetGrid, target, damage, FireDamageKilledRatio, ignoresBuildingCover: true);
        var updatedTarget = casualtyResult.UpdatedTarget;

        ShowDamagePopup(targetGrid, casualtyResult.ActualDamage);
        AppendBattleLog(target, "Hurt", $"Fire burns {FormatLogUnit(target)} for {casualtyResult.ActualDamage:N0} at {targetGrid} ({FormatCasualtyResult(casualtyResult)})");
        ConfigureHud();
        if (updatedTarget.HitPoints <= 0)
        {
            DestroyOccupantAfterDelay(targetGrid, updatedTarget, 0.0, captureOfficer: true);
        }
    }

    private bool IsClosedGateGroundFireProtected(BattleGridKey grid, BattleOccupantInfo occupant)
    {
        if (_mapData == null ||
            grid.Level != 0 ||
            !IsDefenderPiece(occupant) ||
            !IsWithinMap(grid.Grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        return cell.Structure == BattleStructureType.Gate &&
               !cell.IsGateOpen &&
               !cell.IsBroken;
    }

    private void ApplyBattleFireDamageToStructure(BattleGridKey targetGrid)
    {
        if (_mapData == null || targetGrid.Level != 0 || !IsWithinMap(targetGrid.Grid))
        {
            return;
        }

        var cell = _mapData.GetCell(targetGrid.X, targetGrid.Y);
        if (cell.HasBridgeHealth)
        {
            var wasWoodenBridge = cell.IsWoodenBridge;
            var actualBridgeDamage = _mapData.ApplyBridgeDamage(targetGrid.Grid, FireDamageToBridge);
            if (actualBridgeDamage > 0)
            {
                ShowDamagePopup(targetGrid, actualBridgeDamage);
                if (!cell.HasBridgeHealth)
                {
                    RefreshWorkerObjectLayers();
                    if (wasWoodenBridge)
                    {
                        AppendBattleLog(GetCurrentTurnSideName(), "Destroy", $"Wooden bridge destroyed by fire at {targetGrid}.");
                        ShowWoodenBridgeDestroyedNotice(targetGrid);
                    }
                }
            }

            return;
        }

        if (cell.Structure == BattleStructureType.WoodenFence && cell.HasStructureHealth)
        {
            var actualFenceDamage = _mapData.ApplyWoodenFenceDamage(targetGrid.Grid, FireDamageToWoodenFence);
            if (actualFenceDamage > 0)
            {
                ShowDamagePopup(targetGrid, actualFenceDamage);
                if (cell.Structure != BattleStructureType.WoodenFence)
                {
                    RefreshWorkerObjectLayers();
                }
            }

            return;
        }

        if (cell.Structure == BattleStructureType.Gate && cell.HasStructureHealth && !cell.IsBroken)
        {
            var actualDamage = ApplyGateGroupDamage(targetGrid.Grid, FireDamageToGate);
            if (actualDamage > 0)
            {
                AppendGateDamageLog(null, targetGrid, actualDamage, causedByFire: true);
                ShowDamagePopup(targetGrid, actualDamage);
            }
        }
    }

    private IEnumerable<BattleGridKey> GetFireSpreadTargets(BattleGridKey sourceGrid)
    {
        if (_mapData == null)
        {
            yield break;
        }

        var sourceCell = _mapData.GetCell(sourceGrid.X, sourceGrid.Y);
        if (IsClosedIntactGate(sourceCell))
        {
            // Fire may burn and damage a closed gate, but the gatehouse is a sealed
            // barrier: it cannot carry that fire into (or out of) the inner city.
            yield break;
        }

        var maxSpreadTargets = GetFireSpreadTargetCount(sourceCell, GetCurrentBattleWindPower());
        if (maxSpreadTargets <= 0)
        {
            yield break;
        }

        var windOffset = GetWindGridOffset(GetCurrentBattleWindDirection());
        var candidates = GetFireSpreadOffsets()
            .Select(offset => new BattleGridKey(sourceGrid.X + offset.X, sourceGrid.Y + offset.Y, 0))
            .Where(candidate => IsWithinMap(candidate.Grid))
            .Select(candidate => (Grid: candidate, Cell: _mapData.GetCell(candidate.X, candidate.Y)))
            .Where(candidate => CanCellIgnite(candidate.Cell))
            .OrderByDescending(candidate => GetFireSpreadScore(candidate.Grid, candidate.Cell, sourceGrid, windOffset))
            .ThenBy(candidate => candidate.Grid.Y)
            .ThenBy(candidate => candidate.Grid.X)
            .Take(maxSpreadTargets)
            .ToList();

        foreach (var (grid, _) in candidates)
        {
            yield return grid;
        }
    }

    private void RefreshFireVisual(BattleGridKey grid)
    {
        if (_battleDepthLayer == null || _mapData == null)
        {
            return;
        }

        if (!_fireVisualsByGrid.TryGetValue(grid, out var fireRoot))
        {
            fireRoot = CreateFireVisual(grid);
            _battleDepthLayer.AddChild(fireRoot);
            _fireVisualsByGrid[grid] = fireRoot;
        }
        else
        {
            fireRoot.Position = GetFireVisualPosition(grid);
        }

        RegisterBattleDepthEntry(fireRoot, grid, BattleDepthRenderKind.FireEffect);
    }

    private void RemoveFireVisual(BattleGridKey grid)
    {
        if (!_fireVisualsByGrid.TryGetValue(grid, out var fireRoot))
        {
            return;
        }

        _fireVisualsByGrid.Remove(grid);
        _battleDepthEntries.Remove(fireRoot);
        fireRoot.QueueFree();
    }

    private Node2D CreateFireVisual(BattleGridKey grid)
    {
        var fireRoot = new Node2D
        {
            Name = $"Fire_{grid.X}_{grid.Y}",
            Position = GetFireVisualPosition(grid),
            ZIndex = 0
        };

        fireRoot.AddChild(CreateFireFlamePolygon(new Vector2(-12.0f, -6.0f), 9.0f, 16.0f, new Color(1.0f, 0.45f, 0.08f, 0.95f)));
        fireRoot.AddChild(CreateFireFlamePolygon(new Vector2(0.0f, -12.0f), 11.0f, 20.0f, new Color(1.0f, 0.76f, 0.14f, 0.96f)));
        fireRoot.AddChild(CreateFireFlamePolygon(new Vector2(12.0f, -5.0f), 8.0f, 15.0f, new Color(0.98f, 0.30f, 0.04f, 0.92f)));
        return fireRoot;
    }

    private static Polygon2D CreateFireFlamePolygon(Vector2 offset, float halfWidth, float height, Color color)
    {
        return new Polygon2D
        {
            Position = offset,
            Color = color,
            Polygon = new[]
            {
                new Vector2(0.0f, -height),
                new Vector2(halfWidth, 0.0f),
                new Vector2(0.0f, 6.0f),
                new Vector2(-halfWidth, 0.0f)
            }
        };
    }

    private Vector2 GetFireVisualPosition(BattleGridKey grid)
    {
        var center = _groundLayer?.MapToLocal(grid.Grid) ?? BattleMapRenderer.GridToWorld(grid.Grid);
        return center + new Vector2(0.0f, -6.0f);
    }

    private static bool CanCellIgnite(BattleCellData cell)
    {
        if (cell.Terrain is BattleTerrainType.Moat or BattleTerrainType.River or BattleTerrainType.Swamp or BattleTerrainType.Coast or BattleTerrainType.WallWalk or BattleTerrainType.Mountain)
        {
            return false;
        }

        if (cell.Structure is BattleStructureType.Wall or BattleStructureType.Tower or BattleStructureType.RockBig or BattleStructureType.RockSmall)
        {
            return false;
        }

        return true;
    }

    private static bool IsClosedIntactGate(BattleCellData cell)
    {
        return cell.Structure == BattleStructureType.Gate &&
               !cell.IsGateOpen &&
               !cell.IsBroken;
    }

    private static int GetInitialFireDuration(BattleWeatherType weather, BattleCellData cell)
    {
        var baseDuration = weather switch
        {
            BattleWeatherType.Rain => FireStrategyBaseDurationRain,
            BattleWeatherType.Cloudy => FireStrategyBaseDurationCloudy,
            _ => FireStrategyBaseDurationSunny
        };

        return Math.Max(1, baseDuration + GetFireDurationBonus(cell));
    }

    private static int GetFireDamagePerTurn(BattleWeatherType weather)
    {
        return weather switch
        {
            BattleWeatherType.Rain => FireDamagePerTurnRain,
            BattleWeatherType.Cloudy => FireDamagePerTurnCloudy,
            _ => FireDamagePerTurnSunny
        };
    }

    private bool CanFireSpread(BattleWeatherType weather, BattleGridKey grid, BattleFireState state)
    {
        if (_mapData == null || weather == BattleWeatherType.Rain || state.RemainingTurns <= 1)
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        var interval = GetFireSpreadInterval(cell, GetCurrentBattleWindPower());
        return interval > 0 && state.BurnTurns % interval == 0;
    }

    private static IEnumerable<Vector2I> GetFireSpreadOffsets()
    {
        yield return new Vector2I(1, 0);
        yield return new Vector2I(-1, 0);
        yield return new Vector2I(0, 1);
        yield return new Vector2I(0, -1);
        yield return new Vector2I(1, 1);
        yield return new Vector2I(1, -1);
        yield return new Vector2I(-1, 1);
        yield return new Vector2I(-1, -1);
    }

    private static int GetFireSpreadTargetCount(BattleCellData sourceCell, BattleWindPower windPower)
    {
        if (windPower == BattleWindPower.Calm)
        {
            return CanTerrainCarrySlowFire(sourceCell) ? 1 : 0;
        }

        var baseCount = sourceCell.Terrain switch
        {
            BattleTerrainType.Forest => 3,
            BattleTerrainType.Grass => 2,
            BattleTerrainType.Plain or BattleTerrainType.Courtyard => 1,
            BattleTerrainType.Road or BattleTerrainType.Bridge => 1,
            _ => 0
        };

        if (sourceCell.Structure == BattleStructureType.WoodenFence)
        {
            baseCount = Math.Max(baseCount, 2);
        }

        var windAdjustment = windPower == BattleWindPower.Strong ? 1 : 0;

        return Math.Clamp(baseCount + windAdjustment, 0, FireMaxSpreadCandidates);
    }

    private static int GetFireSpreadInterval(BattleCellData sourceCell, BattleWindPower windPower)
    {
        if (windPower == BattleWindPower.Calm)
        {
            return CanTerrainCarrySlowFire(sourceCell) ? 3 : 0;
        }

        var baseInterval = sourceCell.Terrain switch
        {
            BattleTerrainType.Forest => 1,
            BattleTerrainType.Grass => 1,
            BattleTerrainType.Plain or BattleTerrainType.Courtyard => 2,
            BattleTerrainType.Road or BattleTerrainType.Bridge => 3,
            _ => 0
        };

        if (sourceCell.Structure == BattleStructureType.WoodenFence)
        {
            baseInterval = 1;
        }

        if (baseInterval <= 0)
        {
            return 0;
        }

        var windAdjustment = windPower == BattleWindPower.Strong ? -1 : 0;

        return Math.Max(1, baseInterval + windAdjustment);
    }

    private static bool CanTerrainCarrySlowFire(BattleCellData cell)
    {
        return cell.Structure == BattleStructureType.WoodenFence ||
               cell.Terrain is BattleTerrainType.Forest or BattleTerrainType.Grass;
    }

    private static int GetFireDurationBonus(BattleCellData cell)
    {
        if (cell.Structure == BattleStructureType.WoodenFence)
        {
            return 1;
        }

        return cell.Terrain switch
        {
            BattleTerrainType.Forest => 2,
            BattleTerrainType.Grass => 1,
            BattleTerrainType.Road or BattleTerrainType.Bridge => -1,
            _ => 0
        };
    }

    private static int GetFireSpreadScore(BattleGridKey candidate, BattleCellData candidateCell, BattleGridKey sourceGrid, Vector2I windOffset)
    {
        var offset = new Vector2I(candidate.X - sourceGrid.X, candidate.Y - sourceGrid.Y);
        return GetFireTerrainSpreadScore(candidateCell) + GetFireWindSpreadScore(offset, windOffset);
    }

    private static int GetFireTerrainSpreadScore(BattleCellData cell)
    {
        if (cell.Structure == BattleStructureType.WoodenFence)
        {
            return 7;
        }

        return cell.Terrain switch
        {
            BattleTerrainType.Forest => 8,
            BattleTerrainType.Grass => 6,
            BattleTerrainType.Plain or BattleTerrainType.Courtyard => 4,
            BattleTerrainType.Road or BattleTerrainType.Bridge => 2,
            _ => 0
        };
    }

    private static int GetFireWindSpreadScore(Vector2I offset, Vector2I windOffset)
    {
        if (offset == windOffset)
        {
            return 6;
        }

        var dot = (offset.X * windOffset.X) + (offset.Y * windOffset.Y);
        if (dot > 0)
        {
            return 3;
        }

        return dot == 0 ? 1 : -2;
    }
}
