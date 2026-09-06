using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattlePresentationSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;
using static ThreeKingdom.Battle.BattleUnitVisualCatalog;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private bool TryMoveSelectedUnit(bool markAiActed = true, Action? onMoveAnimationComplete = null)
    {
        if (!_selectedGrid.HasValue || !_selectedUnitGrid.HasValue || _selectedUnit == null || _groundLayer == null)
        {
            return false;
        }

        if (!IsCurrentTurnPiece(_selectedUnit))
        {
            return false;
        }

        var destinationGrid = _selectedGridKey ?? GetDefaultGridKey(_selectedGrid.Value);
        var sourceGrid = _selectedUnitGrid.Value;
        if (destinationGrid == sourceGrid || !_movableGrids.Contains(destinationGrid))
        {
            return false;
        }

        if (!_occupantsByGrid.TryGetValue(sourceGrid, out var sourceOccupants))
        {
            return false;
        }

        var movingOccupant = sourceOccupants.FirstOrDefault(static occupant => occupant.Marker != null && IsBattlePiece(occupant));
        if (movingOccupant == null || movingOccupant.Marker == null)
        {
            return false;
        }

        if (movingOccupant.HasAttackedThisTurn ||
            !TryBuildMovePath(sourceGrid, destinationGrid, movingOccupant.Energy, GetAvailableMoveRange(movingOccupant), out var movePath))
        {
            return false;
        }

        var moveEnergyCost = GetMovePathEnergyCost(movePath);
        if (moveEnergyCost > movingOccupant.Energy || movePath.Count > GetAvailableMoveRange(movingOccupant))
        {
            return false;
        }

        movePath = ExpandMovePathWithCarLadderWaypoints(sourceGrid, movePath, movingOccupant);
        var pathPositions = movePath.Select(GetMarkerPosition).ToArray();
        var pathDirections = BuildPathDirections(sourceGrid, movePath);
        var pathModulates = BuildMovePathModulates(sourceGrid, movePath, movingOccupant);
        var moveDirection = pathDirections.Length > 0
            ? pathDirections[^1]
            : GetInfantryDirection(sourceGrid.Grid, destinationGrid.Grid);
        var remainsHidden = movingOccupant.IsHidden && IsForestGrid(destinationGrid);
        var movedOccupant = movingOccupant with
        {
            Marker = movingOccupant.Marker,
            FacingDirection = moveDirection,
            IsHidden = remainsHidden,
            Energy = movingOccupant.Energy - moveEnergyCost,
            RemainingMoveRange = movingOccupant.RemainingMoveRange - movePath.Count
        };
        if (!_occupantsByGrid.Move(sourceGrid, movingOccupant, destinationGrid, movedOccupant))
        {
            return false;
        }
        UpdateMarkerStatusIndicator(movedOccupant);
        RegisterBattleDepthEntry(
            movedOccupant.Marker!,
            destinationGrid,
            movedOccupant.Category == CategorySiegeEngine ? BattleDepthRenderKind.SiegeEngine : BattleDepthRenderKind.Unit);
        RefreshBattleDepthLayerOrder();
        ClearOccludedUnitSilhouettes();
        _selectedUnitGrid = destinationGrid;
        _selectedUnit = movedOccupant;
        _selectedGrid = destinationGrid.Grid;
        _selectedGridKey = destinationGrid;
        _commandMode = BattleCommandMode.None;
        _selectedStrategyAction = BattleStrategyAction.None;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        HideCommandMenu();
        ApplyMoveAnimation(
            movedOccupant,
            moveDirection,
            GetMarkerPosition(destinationGrid),
            pathPositions,
            pathDirections,
            pathModulates,
            () =>
            {
                CaptureDefenseOutpost(destinationGrid, movedOccupant);
                ApplyBattleFireEntryDamage(destinationGrid, movedOccupant);
                RefreshOccludedUnitSilhouettes();
                TryShowTerrainSpeech(movedOccupant, destinationGrid);
                onMoveAnimationComplete?.Invoke();
            });
        AppendBattleLog(movedOccupant, "Move", $"{FormatLogUnit(movedOccupant)} {sourceGrid} -> {destinationGrid}");
        if (movingOccupant.IsHidden && !remainsHidden)
        {
            AppendBattleLog(movedOccupant, "Status", $"{FormatLogUnit(movedOccupant)} leaves forest and is no longer hidden");
        }
        RefreshHiddenUnitVisibility();
        if (markAiActed)
        {
            MarkUnitActedForAiOnly(movedOccupant);
        }

        return true;
    }

    private Color?[]? BuildMovePathModulates(BattleGridKey sourceGrid, IReadOnlyList<BattleGridKey> movePath, BattleOccupantInfo movingOccupant)
    {
        if (movePath.Count == 0)
        {
            return null;
        }

        var silhouetteColor = GetOccludedUnitSilhouetteColor(movingOccupant);
        var pathModulates = new Color?[movePath.Count];
        var hasSilhouetteSegment = false;
        var previousGrid = sourceGrid;
        for (var index = 0; index < movePath.Count; index++)
        {
            var nextGrid = movePath[index];
            if (ShouldUseMovingSegmentSilhouette(previousGrid, nextGrid))
            {
                pathModulates[index] = silhouetteColor;
                hasSilhouetteSegment = true;
            }

            previousGrid = nextGrid;
        }

        return hasSilhouetteSegment ? pathModulates : null;
    }

    private bool ShouldUseMovingSegmentSilhouette(BattleGridKey sourceGrid, BattleGridKey destinationGrid)
    {
        return IsGateVerticalLayerMove(sourceGrid, destinationGrid) ||
               IsUnitOccludedByCastleVisual(destinationGrid);
    }

    private bool IsGateVerticalLayerMove(BattleGridKey sourceGrid, BattleGridKey destinationGrid)
    {
        if (_mapData == null ||
            sourceGrid.Grid != destinationGrid.Grid ||
            !IsWithinMap(sourceGrid.Grid))
        {
            return false;
        }

        var isGateVerticalMove =
            (sourceGrid.Level == 0 && destinationGrid.Level == 2) ||
            (sourceGrid.Level == 2 && destinationGrid.Level == 0);
        if (!isGateVerticalMove)
        {
            return false;
        }

        var cell = _mapData.GetCell(sourceGrid.X, sourceGrid.Y);
        return cell.Structure == BattleStructureType.Gate;
    }


    private void UpdateUnitSelection()
    {
        _selectedUnit = null;
        _selectedUnitGrid = null;
        _movableGrids.Clear();
        _attackableGrids.Clear();
        _strategyTargetGrids.Clear();
        _duelTargetGrids.Clear();
        _chargeTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        _commandMode = BattleCommandMode.None;

        if (!_selectedGrid.HasValue || _mapData == null)
        {
            return;
        }

        var selectedGridKey = _selectedGridKey;
        if (!selectedGridKey.HasValue)
        {
            return;
        }

        if (!_occupantsByGrid.TryGetValue(selectedGridKey.Value, out var occupants))
        {
            return;
        }

        var selectedUnit = occupants.FirstOrDefault(occupant => IsBattlePiece(occupant) && IsVisibleToCurrentTurnSide(occupant));
        if (selectedUnit == null)
        {
            return;
        }

        _selectedUnit = selectedUnit;
        _selectedUnitGrid = selectedGridKey.Value;
        if (IsCurrentTurnPiece(selectedUnit))
        {
            _commandMode = BattleCommandMode.AwaitingCommand;
        }
    }

    private IEnumerable<BattleGridKey> CalculateReachableGrids(BattleGridKey startGrid, int energyBudget, int rangeBudget)
    {
        if (_mapData == null || energyBudget <= 0 || rangeBudget <= 0)
        {
            yield break;
        }

        var frontier = new Queue<(BattleGridKey Grid, int RemainingEnergy, int RemainingRange)>();
        var visitedStates = new HashSet<(BattleGridKey Grid, int RemainingEnergy, int RemainingRange)> { (startGrid, energyBudget, rangeBudget) };
        var reachableGrids = new HashSet<BattleGridKey>();
        frontier.Enqueue((startGrid, energyBudget, rangeBudget));

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var step in GetMovementNeighbors(current.Grid))
            {
                var neighbor = step.Grid;
                if (!IsWithinMap(neighbor.Grid))
                {
                    continue;
                }

                var cell = _mapData.GetCell(neighbor.X, neighbor.Y);
                if (!CanEnterCell(current.Grid, neighbor, cell, step.UsesLadderBridge))
                {
                    continue;
                }

                if (IsCellBlockingMovement(cell))
                {
                    if (!CanTraverseBlockedCell(neighbor, cell, step.UsesLadderBridge))
                    {
                        continue;
                    }
                }

                if (neighbor != startGrid && HasBlockingOccupant(neighbor))
                {
                    continue;
                }

                var moveCost = GetMoveEnergyCost(cell);
                var remainingEnergy = current.RemainingEnergy - moveCost;
                var remainingRange = current.RemainingRange - 1;
                if (remainingEnergy < 0 || remainingRange < 0)
                {
                    continue;
                }

                if (!visitedStates.Add((neighbor, remainingEnergy, remainingRange)))
                {
                    continue;
                }

                frontier.Enqueue((neighbor, remainingEnergy, remainingRange));

                if (neighbor != startGrid && reachableGrids.Add(neighbor))
                {
                    yield return neighbor;
                }
            }
        }
    }

    private bool TryBuildMovePath(BattleGridKey startGrid, BattleGridKey destinationGrid, int energyBudget, int rangeBudget, out List<BattleGridKey> path)
    {
        path = [];
        if (_mapData == null || energyBudget <= 0 || rangeBudget <= 0)
        {
            return false;
        }

        var frontier = new PriorityQueue<BattleGridKey, (int EstimatedTotalCost, int LayerChanges, int GateVerticalSteps, int Steps, int Sequence)>();
        var bestCost = new Dictionary<BattleGridKey, int> { [startGrid] = 0 };
        var pathStatsByGrid = new Dictionary<BattleGridKey, (int LayerChanges, int GateVerticalSteps, int Steps)>
        {
            [startGrid] = (0, 0, 0)
        };
        var previousByGrid = new Dictionary<BattleGridKey, BattleGridKey>();
        var sequence = 0;
        frontier.Enqueue(startGrid, (EstimateMoveCost(startGrid, destinationGrid), 0, 0, 0, sequence++));

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current == destinationGrid)
            {
                path = RebuildMovePath(startGrid, destinationGrid, previousByGrid);
                return path.Count > 0 && path.Count <= rangeBudget;
            }

            foreach (var step in GetMovementNeighbors(current))
            {
                var neighbor = step.Grid;
                if (!IsWithinMap(neighbor.Grid))
                {
                    continue;
                }

                var cell = _mapData.GetCell(neighbor.X, neighbor.Y);
                if (!CanEnterCell(current, neighbor, cell, step.UsesLadderBridge))
                {
                    continue;
                }

                if (IsCellBlockingMovement(cell) && !CanTraverseBlockedCell(neighbor, cell, step.UsesLadderBridge))
                {
                    continue;
                }

                if (neighbor != startGrid && HasBlockingOccupant(neighbor))
                {
                    continue;
                }

                var moveCost = GetMoveEnergyCost(cell);
                var newCost = bestCost[current] + moveCost;
                if (newCost > energyBudget)
                {
                    continue;
                }

                var currentStats = pathStatsByGrid[current];
                var newStats = (
                    LayerChanges: currentStats.LayerChanges + (current.Level == neighbor.Level ? 0 : 1),
                    GateVerticalSteps: currentStats.GateVerticalSteps + (IsGateVerticalLayerMove(current, neighbor) ? 1 : 0),
                    Steps: currentStats.Steps + 1);
                if (newStats.Steps > rangeBudget)
                {
                    continue;
                }
                if (bestCost.TryGetValue(neighbor, out var knownCost) &&
                    (knownCost < newCost ||
                     knownCost == newCost && !IsBetterPathTieBreak(newStats, pathStatsByGrid[neighbor])))
                {
                    continue;
                }

                bestCost[neighbor] = newCost;
                pathStatsByGrid[neighbor] = newStats;
                previousByGrid[neighbor] = current;
                var estimatedTotalCost = newCost + EstimateMoveCost(neighbor, destinationGrid);
                frontier.Enqueue(neighbor, (estimatedTotalCost, newStats.LayerChanges, newStats.GateVerticalSteps, newStats.Steps, sequence++));
            }
        }

        return false;
    }

    private bool TryGetAiPathEndpointToward(
        BattleGridKey startGrid,
        BattleOccupantInfo unit,
        BattleGridKey goalGrid,
        out BattleGridKey destinationGrid,
        out int fullPathEnergyCost,
        out int fullPathSteps)
    {
        destinationGrid = default;
        fullPathEnergyCost = 0;
        fullPathSteps = 0;
        if (_mapData == null || goalGrid == startGrid)
        {
            return false;
        }

        var fullPathBudget = BattleMapData.Width * BattleMapData.Height * 2;
        if (!TryBuildMovePath(startGrid, goalGrid, fullPathBudget, fullPathBudget, out var fullPath))
        {
            return false;
        }

        fullPathEnergyCost = GetMovePathEnergyCost(fullPath);
        fullPathSteps = fullPath.Count;
        var availableEnergy = GetAvailableMoveEnergy(unit);
        var availableRange = GetAvailableMoveRange(unit);
        var spentEnergy = 0;
        var steps = 0;
        foreach (var grid in fullPath)
        {
            spentEnergy += GetMoveEnergyCost(_mapData.GetCell(grid.X, grid.Y));
            steps++;
            if (spentEnergy > availableEnergy || steps > availableRange)
            {
                break;
            }

            if (IsAiSafeMovementDestination(grid))
            {
                destinationGrid = grid;
            }
        }

        return destinationGrid != default;
    }

    private bool TryGetAiLocalFortressAdvance(
        BattleGridKey startGrid,
        BattleOccupantInfo unit,
        BattleGridKey goalGrid,
        out BattleGridKey destinationGrid,
        out int pathEnergyCost,
        out int pathSteps)
    {
        destinationGrid = default;
        pathEnergyCost = 0;
        pathSteps = 0;
        if (_mapData == null)
        {
            return false;
        }

        var currentDistance = GetManhattanDistance(startGrid.Grid, goalGrid.Grid);
        foreach (var candidateGrid in CalculateReachableGrids(
                     startGrid,
                     GetAvailableMoveEnergy(unit),
                     GetAvailableMoveRange(unit))
                 .Where(IsAiSafeMovementDestination)
                 .OrderBy(grid => GetManhattanDistance(grid.Grid, goalGrid.Grid))
                 .ThenBy(grid => GetManhattanDistance(startGrid.Grid, grid.Grid)))
        {
            if (GetManhattanDistance(candidateGrid.Grid, goalGrid.Grid) >= currentDistance ||
                !TryBuildMovePath(
                    startGrid,
                    candidateGrid,
                    GetAvailableMoveEnergy(unit),
                    GetAvailableMoveRange(unit),
                    out var path))
            {
                continue;
            }

            destinationGrid = candidateGrid;
            pathEnergyCost = GetMovePathEnergyCost(path);
            pathSteps = path.Count;
            return true;
        }

        return false;
    }

    private static int EstimateMoveCost(BattleGridKey fromGrid, BattleGridKey toGrid)
    {
        return Mathf.Abs(fromGrid.X - toGrid.X) +
               Mathf.Abs(fromGrid.Y - toGrid.Y) +
               (fromGrid.Level == toGrid.Level ? 0 : 1);
    }

    private static bool IsBetterPathTieBreak(
        (int LayerChanges, int GateVerticalSteps, int Steps) candidate,
        (int LayerChanges, int GateVerticalSteps, int Steps) known)
    {
        return candidate.LayerChanges < known.LayerChanges ||
               candidate.LayerChanges == known.LayerChanges && candidate.GateVerticalSteps < known.GateVerticalSteps ||
               candidate.LayerChanges == known.LayerChanges && candidate.GateVerticalSteps == known.GateVerticalSteps && candidate.Steps < known.Steps;
    }

    private static List<BattleGridKey> RebuildMovePath(BattleGridKey startGrid, BattleGridKey destinationGrid, IReadOnlyDictionary<BattleGridKey, BattleGridKey> previousByGrid)
    {
        var path = new List<BattleGridKey>();
        var current = destinationGrid;
        while (current != startGrid)
        {
            path.Add(current);
            if (!previousByGrid.TryGetValue(current, out current))
            {
                return [];
            }
        }

        path.Reverse();
        return path;
    }

    private List<BattleGridKey> ExpandMovePathWithCarLadderWaypoints(BattleGridKey sourceGrid, IReadOnlyList<BattleGridKey> movePath, BattleOccupantInfo movingOccupant)
    {
        if (_mapData == null || movePath.Count == 0 || !CanUseCarLadderBridge(movingOccupant))
        {
            return movePath.ToList();
        }

        var expandedPath = new List<BattleGridKey>();
        var previousGrid = sourceGrid;
        foreach (var nextGrid in movePath)
        {
            if (TryGetCarLadderGridForTransition(previousGrid, nextGrid, out var ladderGrid))
            {
                AddPathGrid(expandedPath, ladderGrid, previousGrid);
            }

            AddPathGrid(expandedPath, nextGrid, previousGrid);
            previousGrid = nextGrid;
        }

        return expandedPath;
    }

    private bool TryGetCarLadderGridForTransition(BattleGridKey fromGrid, BattleGridKey toGrid, out BattleGridKey ladderGrid)
    {
        ladderGrid = default;
        if (_mapData == null || !IsWithinMap(fromGrid.Grid) || !IsWithinMap(toGrid.Grid))
        {
            return false;
        }

        if (!ShouldUseCarLadderBridgeForMove(fromGrid, toGrid))
        {
            return false;
        }

        foreach (var candidateLadderGrid in GetUsableCarLadderGrids())
        {
            var groundGrids = GetCarLadderGroundEndpoints(candidateLadderGrid.Grid).Select(ToGroundGridKey).ToList();
            var wallWalkGrids = GetCarLadderWallTopEndpoints(candidateLadderGrid.Grid).Select(ToWallWalkGridKey).ToList();
            var matchesGroundToWall = fromGrid.Level != 2 &&
                                      groundGrids.Contains(fromGrid) &&
                                      wallWalkGrids.Contains(toGrid);
            var matchesWallToGround = fromGrid.Level == 2 &&
                                      wallWalkGrids.Contains(fromGrid) &&
                                      groundGrids.Contains(toGrid);
            if (!matchesGroundToWall && !matchesWallToGround)
            {
                continue;
            }

            ladderGrid = candidateLadderGrid;
            return true;
        }

        return false;
    }

    private IEnumerable<(BattleGridKey Grid, bool UsesLadderBridge)> GetMovementNeighbors(BattleGridKey grid)
    {
        var verticalGateStep = ResolveGateVerticalStepGridKey(grid);
        if (verticalGateStep.HasValue)
        {
            yield return (verticalGateStep.Value, false);
        }

        foreach (var neighbor in GetOrthogonalNeighbors(grid.Grid))
        {
            var neighborKey = ResolveStepGridKey(grid, neighbor);
            if (neighborKey.HasValue)
            {
                yield return (neighborKey.Value, false);
            }
        }

        foreach (var bridgeNeighbor in GetCarLadderBridgeNeighbors(grid))
        {
            yield return (bridgeNeighbor, true);
        }
    }

    private BattleGridKey? ResolveGateVerticalStepGridKey(BattleGridKey grid)
    {
        if (_mapData == null || !IsWithinMap(grid.Grid) || !CanUseGateVerticalStep(grid))
        {
            return null;
        }

        return grid.Level switch
        {
            2 => ToGroundGridKey(grid.Grid),
            0 => ToWallWalkGridKey(grid.Grid),
            _ => null
        };
    }

    private bool CanUseGateVerticalStep(BattleGridKey grid)
    {
        if (_mapData == null || _selectedUnit == null || !IsWithinMap(grid.Grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        if (cell.Structure != BattleStructureType.Gate)
        {
            return false;
        }

        if (cell.IsGateOpen || cell.IsBroken)
        {
            return true;
        }

        if (_selectedUnit.Category != CategoryUnit)
        {
            return false;
        }

        return IsDefenderPiece(_selectedUnit) || grid.Level == 2;
    }

    private IEnumerable<BattleGridKey> GetCarLadderBridgeNeighbors(BattleGridKey grid)
    {
        if (_mapData == null || _selectedUnit == null || !CanUseCarLadderBridge(_selectedUnit) || !IsWithinMap(grid.Grid))
        {
            yield break;
        }

        var currentCell = _mapData.GetCell(grid.X, grid.Y);
        foreach (var ladderGrid in GetUsableCarLadderGrids())
        {
            var bridgeGroundGrids = GetCarLadderGroundEndpoints(ladderGrid.Grid).Select(ToGroundGridKey).ToList();
            var bridgeWallWalkGrids = GetCarLadderWallTopEndpoints(ladderGrid.Grid).Select(ToWallWalkGridKey).ToList();
            if (grid.Level == 2)
            {
                if (!bridgeWallWalkGrids.Contains(grid))
                {
                    continue;
                }

                foreach (var groundGrid in bridgeGroundGrids)
                {
                    yield return groundGrid;
                }

                continue;
            }

            if (!bridgeGroundGrids.Contains(grid))
            {
                continue;
            }

            foreach (var wallWalkGrid in bridgeWallWalkGrids)
            {
                yield return wallWalkGrid;
            }
        }
    }

    private bool TryGetCarLadderBridgePath(BattleGridKey sourceGrid, BattleGridKey destinationGrid, BattleOccupantInfo movingOccupant, out Vector2[] pathPositions, out BattleSpriteDirection[] pathDirections)
    {
        pathPositions = [];
        pathDirections = [];
        if (_mapData == null || !CanUseCarLadderBridge(movingOccupant) || !IsWithinMap(sourceGrid.Grid) || !IsWithinMap(destinationGrid.Grid))
        {
            return false;
        }

        if (!ShouldUseCarLadderBridgeForMove(sourceGrid, destinationGrid))
        {
            return false;
        }

        if (sourceGrid.Level == destinationGrid.Level ||
            (sourceGrid.Level != 2 && destinationGrid.Level != 2))
        {
            return false;
        }

        foreach (var ladderGrid in GetUsableCarLadderGrids())
        {
            var groundGrids = GetCarLadderGroundEndpoints(ladderGrid.Grid).Select(ToGroundGridKey).ToList();
            var wallWalkGrids = GetCarLadderWallTopEndpoints(ladderGrid.Grid).Select(ToWallWalkGridKey).ToList();
            var pathGrids = new List<BattleGridKey>();
            if (sourceGrid.Level != 2 && wallWalkGrids.Contains(destinationGrid))
            {
                var entryGrid = GetNearestGrid(sourceGrid, groundGrids);
                if (!entryGrid.HasValue)
                {
                    continue;
                }

                AddPathGrid(pathGrids, entryGrid.Value, sourceGrid);
                AddPathGrid(pathGrids, ladderGrid, sourceGrid);
                AddPathGrid(pathGrids, destinationGrid, sourceGrid);
            }
            else if (sourceGrid.Level == 2 && wallWalkGrids.Contains(sourceGrid))
            {
                var exitGrid = GetNearestGrid(destinationGrid, groundGrids);
                if (!exitGrid.HasValue)
                {
                    continue;
                }

                AddPathGrid(pathGrids, ladderGrid, sourceGrid);
                AddPathGrid(pathGrids, exitGrid.Value, sourceGrid);
                AddPathGrid(pathGrids, destinationGrid, sourceGrid);
            }
            else
            {
                continue;
            }

            if (pathGrids.Count == 0)
            {
                continue;
            }

            pathPositions = pathGrids.Select(GetMarkerPosition).ToArray();
            pathDirections = BuildPathDirections(sourceGrid, pathGrids);
            return true;
        }

        return false;
    }

    private static BattleGridKey? GetNearestGrid(BattleGridKey fromGrid, IEnumerable<BattleGridKey> candidates)
    {
        BattleGridKey? nearestGrid = null;
        var nearestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            var distance = Mathf.Abs(candidate.X - fromGrid.X) + Mathf.Abs(candidate.Y - fromGrid.Y);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestGrid = candidate;
            nearestDistance = distance;
        }

        return nearestGrid;
    }

    private static void AddPathGrid(List<BattleGridKey> pathGrids, BattleGridKey grid, BattleGridKey sourceGrid)
    {
        if (grid == sourceGrid || (pathGrids.Count > 0 && pathGrids[^1] == grid))
        {
            return;
        }

        pathGrids.Add(grid);
    }

    private static BattleSpriteDirection[] BuildPathDirections(BattleGridKey sourceGrid, IReadOnlyList<BattleGridKey> pathGrids)
    {
        var directions = new BattleSpriteDirection[pathGrids.Count];
        var previousGrid = sourceGrid;
        for (var index = 0; index < pathGrids.Count; index++)
        {
            directions[index] = GetInfantryDirection(previousGrid.Grid, pathGrids[index].Grid);
            previousGrid = pathGrids[index];
        }

        return directions;
    }

    private static bool ShouldUseCarLadderBridgeForMove(BattleGridKey sourceGrid, BattleGridKey destinationGrid)
    {
        return sourceGrid.Level != destinationGrid.Level &&
               (sourceGrid.Level == 2 || destinationGrid.Level == 2);
    }

    private IEnumerable<BattleGridKey> GetUsableCarLadderGrids()
    {
        foreach (var (grid, occupants) in _occupantsByGrid)
        {
            if (!IsWithinMap(grid.Grid))
            {
                continue;
            }

            if (occupants.Any(static occupant =>
                    occupant.Category == CategorySiegeEngine &&
                    occupant.TroopType == TroopLadder &&
                    occupant.Marker != null))
            {
                yield return grid;
            }
        }
    }

    private IEnumerable<Vector2I> GetCarLadderGroundEndpoints(Vector2I ladderGrid)
    {
        if (_mapData == null)
        {
            yield break;
        }

        foreach (var neighbor in GetOrthogonalNeighbors(ladderGrid))
        {
            if (!IsWithinMap(neighbor))
            {
                continue;
            }

            var cell = _mapData.GetCell(neighbor.X, neighbor.Y);
            if (!IsWallTopGrid(neighbor) && !cell.IsBlockingStructure)
            {
                yield return neighbor;
            }
        }
    }

    private IEnumerable<Vector2I> GetCarLadderWallTopEndpoints(Vector2I ladderGrid)
    {
        if (_mapData == null)
        {
            yield break;
        }

        foreach (var direction in GetOrthogonalDirections())
        {
            var adjacentGrid = ladderGrid + direction;
            if (!IsWithinMap(adjacentGrid))
            {
                continue;
            }

            if (!IsWallTopGrid(adjacentGrid))
            {
                continue;
            }

            yield return adjacentGrid;
        }
    }

    private static IEnumerable<Vector2I> GetOrthogonalDirections()
    {
        yield return new Vector2I(1, 0);
        yield return new Vector2I(-1, 0);
        yield return new Vector2I(0, 1);
        yield return new Vector2I(0, -1);
    }

    private IEnumerable<Vector2I> GetOrthogonalNeighbors(Vector2I grid)
    {
        yield return new Vector2I(grid.X + 1, grid.Y);
        yield return new Vector2I(grid.X - 1, grid.Y);
        yield return new Vector2I(grid.X, grid.Y + 1);
        yield return new Vector2I(grid.X, grid.Y - 1);
    }

    private IEnumerable<Vector2I> GetAdjacentEightNeighbors(Vector2I grid)
    {
        for (var y = -1; y <= 1; y++)
        {
            for (var x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                yield return new Vector2I(grid.X + x, grid.Y + y);
            }
        }
    }

    private bool HasBlockingOccupant(BattleGridKey grid)
    {
        if (!_occupantsByGrid.TryGetValue(grid, out var occupants))
        {
            return false;
        }

        return occupants.Any(static occupant => IsBattlePiece(occupant));
    }

    private static bool IsBattlePiece(BattleOccupantInfo occupant)
    {
        return BattleMovementService.IsBattlePiece(occupant);
    }

    private static bool CanUseCarLadderBridge(BattleOccupantInfo occupant)
    {
        return BattleMovementService.CanUseCarLadderBridge(occupant);
    }

    private static int GetMoveCost(BattleCellData cell)
    {
        return BattleMovementService.GetMoveCost(cell);
    }

    private static int GetAvailableMoveEnergy(BattleOccupantInfo unit)
    {
        return BattleMovementService.GetAvailableMoveEnergy(unit);
    }

    private int GetTeamMoveRangeCap(BattleOccupantInfo unit)
    {
        var moveRange = GetEffectiveMoveRange(unit);
        return IsSustainedZeroFood(unit.TeamName)
            ? Math.Min(moveRange, SustainedZeroFoodMoveRangeCap)
            : moveRange;
    }

    private int GetAvailableMoveRange(BattleOccupantInfo unit)
    {
        return unit.HasAttackedThisTurn
            ? 0
            : Math.Min(unit.RemainingMoveRange, GetTeamMoveRangeCap(unit));
    }

    private static int GetMoveEnergyCost(BattleCellData cell)
    {
        return BattleMovementService.GetMoveEnergyCost(cell);
    }

    private int GetMovePathEnergyCost(IEnumerable<BattleGridKey> path)
    {
        if (_mapData == null)
        {
            return int.MaxValue;
        }

        return path.Sum(grid => GetMoveEnergyCost(_mapData.GetCell(grid.X, grid.Y)));
    }

    private bool TryGetMovePreview(BattleGridKey? destinationGrid, out int energyCost, out int remainingEnergy, out int remainingMoveRange)
    {
        energyCost = 0;
        remainingEnergy = 0;
        remainingMoveRange = 0;
        if (_commandMode != BattleCommandMode.MoveSelect ||
            !destinationGrid.HasValue ||
            !_movableGrids.Contains(destinationGrid.Value) ||
            !_selectedUnitGrid.HasValue ||
            _selectedUnit == null ||
            !TryBuildMovePath(_selectedUnitGrid.Value, destinationGrid.Value, _selectedUnit.Energy, GetAvailableMoveRange(_selectedUnit), out var movePath))
        {
            return false;
        }

        energyCost = GetMovePathEnergyCost(movePath);
        remainingEnergy = _selectedUnit.Energy - energyCost;
        remainingMoveRange = _selectedUnit.RemainingMoveRange - movePath.Count;
        return remainingEnergy >= 0 && remainingMoveRange >= 0;
    }

    private BattleHighlightVisualKind GetMoveHighlightVisualKind(BattleGridKey grid)
    {
        var canAttackAfterMove = TryGetMovePreview(grid, out _, out var remainingEnergy, out _) &&
                                 remainingEnergy >= NormalAttackEnergyCost;
        if (grid.Level == 2)
        {
            return canAttackAfterMove
                ? BattleHighlightVisualKind.WallTopMoveCanAttack
                : BattleHighlightVisualKind.WallTopMoveCannotAttack;
        }

        return canAttackAfterMove
            ? BattleHighlightVisualKind.MoveCanAttack
            : BattleHighlightVisualKind.MoveCannotAttack;
    }

    private void RefreshHighlights()
    {
        ClearHighlightDepthVisuals();
        if (_battleDepthLayer == null || _groundLayer == null)
        {
            return;
        }

        foreach (var grid in _movableGrids)
        {
            AddHighlightDepthVisual(grid, GetMoveHighlightVisualKind(grid));
        }

        foreach (var grid in _attackableGrids)
        {
            AddHighlightDepthVisual(grid, BattleHighlightVisualKind.Attackable);
        }

        foreach (var grid in _workableGrids)
        {
            AddHighlightDepthVisual(grid, BattleHighlightVisualKind.Workable);
        }

        foreach (var grid in _strategyTargetGrids)
        {
            AddHighlightDepthVisual(grid, BattleHighlightVisualKind.Attackable);
        }

        foreach (var grid in _duelTargetGrids)
        {
            AddHighlightDepthVisual(grid, BattleHighlightVisualKind.Attackable);
        }

        foreach (var grid in _chargeTargetGrids)
        {
            AddHighlightDepthVisual(grid, BattleHighlightVisualKind.Attackable);
        }

        foreach (var grid in _hireOfficerTargetGrids)
        {
            AddHighlightDepthVisual(grid, BattleHighlightVisualKind.Attackable);
        }

        if (_selectedGridKey.HasValue && ShouldDisplaySelectedGridHighlight(_selectedGridKey.Value))
        {
            AddHighlightDepthVisual(_selectedGridKey.Value, BattleHighlightVisualKind.Selected);
        }
        else if (_selectedGrid.HasValue)
        {
            var defaultSelectedGridKey = GetDefaultGridKey(_selectedGrid.Value);
            if (ShouldDisplaySelectedGridHighlight(defaultSelectedGridKey))
            {
                AddHighlightDepthVisual(defaultSelectedGridKey, BattleHighlightVisualKind.Selected);
            }
        }

        RefreshBattleDepthLayerOrder();
    }

    private bool ShouldDisplaySelectedGridHighlight(BattleGridKey selectedGridKey)
    {
        return !_selectedUnitGrid.HasValue ||
               _selectedUnit == null ||
               selectedGridKey != _selectedUnitGrid.Value ||
               selectedGridKey.Level != 2 ||
               !IsBattlePiece(_selectedUnit);
    }

    private void AddHighlightDepthVisual(BattleGridKey grid, BattleHighlightVisualKind visualKind)
    {
        if (_battleDepthLayer == null)
        {
            return;
        }

        var visual = new BattleHighlightRenderer
        {
            Name = $"Highlight_{visualKind}_{grid.X}_{grid.Y}_L{grid.Level}",
            Position = GetHighlightPosition(grid),
            ZIndex = 0
        };
        visual.Configure(visualKind);
        _battleDepthLayer.AddChild(visual);
        _highlightDepthVisuals.Add(visual);
        RegisterBattleDepthEntry(visual, grid, ToBattleDepthRenderKind(visualKind));
    }

    private static BattleDepthRenderKind ToBattleDepthRenderKind(BattleHighlightVisualKind visualKind)
    {
        return visualKind switch
        {
            BattleHighlightVisualKind.Movable or
            BattleHighlightVisualKind.WallTopMovable or
            BattleHighlightVisualKind.MoveCanAttack or
            BattleHighlightVisualKind.MoveCannotAttack or
            BattleHighlightVisualKind.WallTopMoveCanAttack or
            BattleHighlightVisualKind.WallTopMoveCannotAttack => BattleDepthRenderKind.MoveHighlight,
            BattleHighlightVisualKind.Attackable => BattleDepthRenderKind.AttackHighlight,
            BattleHighlightVisualKind.Workable => BattleDepthRenderKind.MoveHighlight,
            BattleHighlightVisualKind.Selected => BattleDepthRenderKind.SelectedHighlight,
            _ => BattleDepthRenderKind.MoveHighlight
        };
    }


    private void OnMoveButtonPressed()
    {
        if (_selectedUnit == null ||
            !_selectedUnitGrid.HasValue ||
            !IsCurrentTurnPiece(_selectedUnit) ||
            HasUsedChargeThisTurn(_selectedUnit))
        {
            return;
        }

        _commandMode = BattleCommandMode.MoveSelect;
        _attackableGrids.Clear();
        _workableGrids.Clear();
        _movableGrids.Clear();
        _duelTargetGrids.Clear();
        _hireOfficerTargetGrids.Clear();
        foreach (var grid in CalculateReachableGrids(_selectedUnitGrid.Value, GetAvailableMoveEnergy(_selectedUnit), GetAvailableMoveRange(_selectedUnit)))
        {
            _movableGrids.Add(grid);
        }

        HideCommandMenu();
        RefreshInfoPanel();
        RefreshHighlights();
        RefreshOccludedUnitSilhouettes();
    }


    private bool CanTraverseBlockedCell(BattleGridKey grid, BattleCellData cell, bool usesLadderBridge = false)
    {
        _ = grid;

        if (grid.Level == 2 && IsWallTopGrid(grid.Grid))
        {
            return true;
        }

        if (!cell.IsBlockingStructure)
        {
            return true;
        }

        if (_selectedUnit == null)
        {
            return false;
        }

        if (cell.Structure == BattleStructureType.Gate && _selectedUnit.Category == CategoryUnit)
        {
            return IsDefenderPiece(_selectedUnit) || grid.Level == 0;
        }

        if (usesLadderBridge && grid.Level == 2 && IsWallTopGrid(grid.Grid) && CanUseCarLadderBridge(_selectedUnit))
        {
            return true;
        }

        return false;
    }

    private bool CanEnterCell(BattleGridKey sourceGrid, BattleGridKey destinationGrid, BattleCellData cell, bool usesLadderBridge = false)
    {
        _ = destinationGrid;

        if (_selectedUnit == null)
        {
            return false;
        }

        if (_selectedUnit.Category == CategorySiegeEngine)
        {
            return CanSiegeEngineEnterCell(sourceGrid, destinationGrid);
        }

        if (destinationGrid.Level == 2 && _selectedUnit.TroopType == TroopCavalry)
        {
            return false;
        }

        if (_selectedUnit.TroopType == TroopCavalry &&
            cell.Terrain == BattleTerrainType.Forest)
        {
            return false;
        }

        if (IsClosedGateGroundMoveBlocked(sourceGrid, destinationGrid))
        {
            return false;
        }

        var sourceCell = _mapData != null && IsWithinMap(sourceGrid.Grid)
            ? _mapData.GetCell(sourceGrid.X, sourceGrid.Y)
            : null;
        var sourceIsWallWalk = sourceGrid.Level == 2;
        var sourceIsCourtyard = sourceCell?.Terrain == BattleTerrainType.Courtyard &&
                                IsInsideCityGroundGrid(sourceGrid.Grid);
        if (destinationGrid.Level == 2 &&
            IsAttackerPiece(_selectedUnit) &&
            !sourceIsWallWalk &&
            !sourceIsCourtyard &&
            !(usesLadderBridge && CanUseCarLadderBridge(_selectedUnit)))
        {
            return false;
        }

        return true;
    }

    private bool IsClosedGateGroundMoveBlocked(BattleGridKey sourceGrid, BattleGridKey destinationGrid)
    {
        if (_mapData == null ||
            sourceGrid.Level != 0 ||
            destinationGrid.Level != 0 ||
            sourceGrid.Grid == destinationGrid.Grid)
        {
            return false;
        }

        var sourceIsGate = IsGateGrid(sourceGrid.Grid);
        var destinationIsGate = IsGateGrid(destinationGrid.Grid);
        if (sourceIsGate == destinationIsGate)
        {
            return false;
        }

        var gateGrid = sourceIsGate ? sourceGrid.Grid : destinationGrid.Grid;
        var otherGrid = sourceIsGate ? destinationGrid.Grid : sourceGrid.Grid;
        var gateCell = _mapData.GetCell(gateGrid.X, gateGrid.Y);
        if (gateCell.IsGateOpen || gateCell.IsBroken)
        {
            return false;
        }

        return !IsInsideCityGroundGrid(otherGrid);
    }

    private bool CanSiegeEngineEnterCell(BattleGridKey sourceGrid, BattleGridKey destinationGrid)
    {
        if (destinationGrid.Level != 0)
        {
            return false;
        }

        // Buildings are defensive positions for human units, not siege engines.
        if (_mapData != null &&
            IsWithinMap(destinationGrid.Grid) &&
            (_mapData.GetCell(destinationGrid.X, destinationGrid.Y).ProvidesBuildingCover ||
             _mapData.GetCell(destinationGrid.X, destinationGrid.Y).IsWoodenBridge))
        {
            return false;
        }

        if (!IsInsideCityGroundGrid(destinationGrid.Grid))
        {
            return true;
        }

        return IsGateGroundPassage(sourceGrid.Grid) || IsInsideCityGroundGrid(sourceGrid.Grid);
    }

    private bool IsInsideCityGroundGrid(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        return cell.Terrain == BattleTerrainType.Courtyard && !IsWallTopGrid(grid);
    }


}
