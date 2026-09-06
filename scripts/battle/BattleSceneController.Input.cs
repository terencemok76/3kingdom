using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static ThreeKingdom.Battle.BattlePresentationSettings;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.Pressed &&
            mouseButton.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown &&
            (IsPointInCommandMenu(mouseButton.GlobalPosition) ||
             IsPointInCameraZoomBlockedUi(mouseButton.GlobalPosition)))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (mouseButton.Pressed && TryAdjustBattleCameraZoom(mouseButton))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_isBattleFinished)
        {
            HideCommandMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
        {
            UpdateHoverGrid();
            _selectedGrid = _hoverGrid;
            _selectedGridKey = _hoverGridKey;
            if (_commandMode == BattleCommandMode.MoveSelect)
            {
                if (!TryExecuteSelectedBattleAction(BattleActionKind.Move))
                {
                    CancelCommandAction(clearSelection: true);
                }

                RefreshCoordinateLabel();
                RefreshInfoPanel();
                RefreshHighlights();
                return;
            }

            if (_commandMode == BattleCommandMode.AttackSelect)
            {
                if (!TryExecuteSelectedBattleAction(BattleActionKind.Attack))
                {
                    CancelCommandAction(clearSelection: true);
                }

                RefreshCoordinateLabel();
                RefreshInfoPanel();
                RefreshHighlights();
                return;
            }

            if (_commandMode == BattleCommandMode.StrategySelect)
            {
                var strategyKind = _selectedStrategyAction switch
                {
                    BattleStrategyAction.Extinguish => BattleActionKind.Extinguish,
                    BattleStrategyAction.Fire => BattleActionKind.FireStrategy,
                    _ => BattleActionKind.MentalStrategy
                };
                if (!TryExecuteSelectedBattleAction(strategyKind))
                {
                    CancelCommandAction(clearSelection: true);
                }

                RefreshCoordinateLabel();
                RefreshInfoPanel();
                RefreshHighlights();
                return;
            }

            if (_commandMode == BattleCommandMode.DuelSelect)
            {
                if (!TryExecuteSelectedBattleAction(BattleActionKind.Duel))
                {
                    CancelCommandAction(clearSelection: true);
                }

                RefreshCoordinateLabel();
                RefreshInfoPanel();
                RefreshHighlights();
                return;
            }

            if (_commandMode == BattleCommandMode.ChargeSelect)
            {
                if (!TryExecuteSelectedBattleAction(BattleActionKind.Charge))
                {
                    CancelCommandAction(clearSelection: true);
                }

                RefreshCoordinateLabel();
                RefreshInfoPanel();
                RefreshHighlights();
                return;
            }

            if (_commandMode == BattleCommandMode.HireOfficerSelect)
            {
                if (!TryExecuteSelectedBattleAction(BattleActionKind.HireOfficer))
                {
                    CancelCommandAction(clearSelection: true);
                }

                RefreshCoordinateLabel();
                RefreshInfoPanel();
                RefreshHighlights();
                return;
            }

            if (_commandMode == BattleCommandMode.WorkSelect)
            {
                if (!TryExecuteSelectedBattleAction(
                        BattleActionKind.Work,
                        useWoodFenceWork: _workerWorkAction == WorkerWorkAction.WoodFence))
                {
                    CancelCommandAction(clearSelection: true);
                }

                RefreshCoordinateLabel();
                RefreshInfoPanel();
                RefreshHighlights();
                return;
            }

            UpdateUnitSelection();
            if (_selectedUnit != null)
            {
                ShowCommandMenu(mouseButton.Position);
            }
            else
            {
                HideCommandMenu();
            }

            RefreshCoordinateLabel();
            RefreshInfoPanel();
            RefreshHighlights();
            return;
        }

        if (mouseButton.ButtonIndex != MouseButton.Right)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left && !mouseButton.Pressed)
            {
                _isDraggingBattleLog = false;
                _isResizingBattleLog = false;
            }

            return;
        }

        if (mouseButton.Pressed)
        {
            _isDraggingMap = true;
            _lastMousePosition = mouseButton.Position;
            GetViewport().SetInputAsHandled();
            return;
        }

        _isDraggingMap = false;
    }

    private bool IsPointInCommandMenu(Vector2 globalPosition)
    {
        return _commandMenu?.Visible == true && _commandMenu.GetGlobalRect().HasPoint(globalPosition);
    }

    private bool IsPointInCameraZoomBlockedUi(Vector2 globalPosition)
    {
        return (_topBar?.Visible == true && _topBar.GetGlobalRect().HasPoint(globalPosition)) ||
               (_tileInfoPanel?.Visible == true && _tileInfoPanel.GetGlobalRect().HasPoint(globalPosition)) ||
               (_battleLogPanel?.Visible == true && _battleLogPanel.GetGlobalRect().HasPoint(globalPosition));
    }

    private bool TryAdjustBattleCameraZoom(InputEventMouseButton mouseButton)
    {
        if (_camera == null)
        {
            return false;
        }

        var zoomDelta = mouseButton.ButtonIndex switch
        {
            MouseButton.WheelUp => -BattleCameraZoomStep,
            MouseButton.WheelDown => BattleCameraZoomStep,
            _ => 0.0f
        };
        if (Mathf.IsZeroApprox(zoomDelta))
        {
            return false;
        }

        var zoom = Mathf.Clamp(_camera.Zoom.X + zoomDelta, MinimumBattleCameraZoom, MaximumBattleCameraZoom);
        _camera.Zoom = new Vector2(zoom, zoom);
        if (_mapRoot != null)
        {
            _mapRoot.Position = GetClampedMapPosition(_mapRoot.Position);
        }

        return true;
    }

    private void HandleMouseMotion(InputEventMouseMotion mouseMotion)
    {
        if (_isDraggingCommandMenu && _commandMenu != null)
        {
            _commandMenu.Position = ClampCommandMenuPosition(mouseMotion.GlobalPosition - _commandMenuDragOffset);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!_isDraggingMap || _mapRoot == null)
        {
            return;
        }

        var delta = mouseMotion.Position - _lastMousePosition;
        _lastMousePosition = mouseMotion.Position;
        _mapRoot.Position = GetClampedMapPosition(_mapRoot.Position + delta);
        GetViewport().SetInputAsHandled();
    }

    private Vector2 GetClampedMapPosition(Vector2 position)
    {
        var mapBounds = GetMapBounds();
        var visibleRect = GetVisibleWorldRect();

        return new Vector2(
            ClampAxis(position.X, mapBounds.Position.X, mapBounds.End.X, visibleRect.Position.X, visibleRect.End.X),
            ClampAxis(position.Y, mapBounds.Position.Y, mapBounds.End.Y, visibleRect.Position.Y, visibleRect.End.Y));
    }

    private void UpdateHoverGrid()
    {
        if (_groundLayer == null || _mapData == null)
        {
            return;
        }

        var localMouse = _groundLayer.ToLocal(GetGlobalMousePosition());
        var newHoverGridKey = ResolvePointerGridKey(localMouse);
        var newHoverGrid = newHoverGridKey?.Grid;
        if (_hoverGrid == newHoverGrid && _hoverGridKey == newHoverGridKey)
        {
            return;
        }

        _hoverGrid = newHoverGrid;
        _hoverGridKey = newHoverGridKey;
        RefreshCoordinateLabel();
    }

    private BattleGridKey? ResolvePointerGridKey(Vector2 localMouse)
    {
        if (_groundLayer == null || _mapData == null)
        {
            return null;
        }

        var highlightedGrid = ResolvePointerHighlightedGrid(localMouse);
        if (highlightedGrid.HasValue)
        {
            return highlightedGrid.Value;
        }

        var occludedUnitGrid = ResolvePointerOccludedUnitSilhouetteGridKey(localMouse);
        if (occludedUnitGrid.HasValue)
        {
            return occludedUnitGrid.Value;
        }

        var groundCandidate = _groundLayer.LocalToMap(localMouse);
        var layeredGrid = ResolvePointerLayeredGridKey(localMouse, groundCandidate);
        if (layeredGrid.HasValue)
        {
            return layeredGrid.Value;
        }

        var markerGridKey = ResolvePointerMarkerGridKey(localMouse);
        if (markerGridKey.HasValue)
        {
            return markerGridKey.Value;
        }

        return IsWithinMap(groundCandidate) ? GetDefaultGridKey(groundCandidate) : null;
    }

    private BattleGridKey? ResolvePointerLayeredGridKey(Vector2 localMouse, Vector2I groundCandidate)
    {
        if (_groundLayer == null || !IsWithinMap(groundCandidate) || !IsGateGrid(groundCandidate))
        {
            return null;
        }

        var groundKey = ToGroundGridKey(groundCandidate);
        var wallTopKey = ToWallWalkGridKey(groundCandidate);
        var inGroundDiamond = PointInDiamond(localMouse, GetHighlightPosition(groundKey), 46.0f, 24.0f);
        var inWallTopDiamond = PointInDiamond(localMouse, GetHighlightPosition(wallTopKey), 46.0f, 24.0f);
        if (inGroundDiamond && !inWallTopDiamond)
        {
            return groundKey;
        }

        if (inWallTopDiamond && !inGroundDiamond)
        {
            return wallTopKey;
        }

        if (inGroundDiamond && inWallTopDiamond)
        {
            var groundDistance = localMouse.DistanceSquaredTo(GetHighlightPosition(groundKey));
            var wallTopDistance = localMouse.DistanceSquaredTo(GetHighlightPosition(wallTopKey));
            return groundDistance <= wallTopDistance ? groundKey : wallTopKey;
        }

        return null;
    }

    private BattleGridKey? ResolvePointerHighlightedGrid(Vector2 localMouse)
    {
        var activeGrids = _commandMode switch
        {
            BattleCommandMode.MoveSelect => _movableGrids,
            BattleCommandMode.AttackSelect => _attackableGrids,
            BattleCommandMode.WorkSelect => _workableGrids,
            BattleCommandMode.StrategySelect => _strategyTargetGrids,
            BattleCommandMode.DuelSelect => _duelTargetGrids,
            BattleCommandMode.ChargeSelect => _chargeTargetGrids,
            BattleCommandMode.HireOfficerSelect => _hireOfficerTargetGrids,
            _ => null
        };

        if (activeGrids == null)
        {
            return null;
        }

        var highlightedCandidates = activeGrids
            .Where(grid => PointInDiamond(localMouse, GetHighlightPosition(grid), 46.0f, 24.0f))
            .ToList();
        if (highlightedCandidates.Count == 0)
        {
            return null;
        }

        if (highlightedCandidates.Count == 1)
        {
            return highlightedCandidates[0];
        }

        var sharedGrid = highlightedCandidates[0].Grid;
        if (highlightedCandidates.All(grid => grid.Grid == sharedGrid))
        {
            var layeredGrid = ResolvePointerLayeredGridKey(localMouse, sharedGrid);
            if (layeredGrid.HasValue && highlightedCandidates.Contains(layeredGrid.Value))
            {
                return layeredGrid.Value;
            }
        }

        return highlightedCandidates
            .OrderBy(grid => localMouse.DistanceSquaredTo(GetHighlightPosition(grid)))
            .ThenByDescending(grid => grid.Level)
            .First();
    }

    private static bool PointInDiamond(Vector2 point, Vector2 center, float halfWidth, float halfHeight)
    {
        var dx = Mathf.Abs(point.X - center.X) / halfWidth;
        var dy = Mathf.Abs(point.Y - center.Y) / halfHeight;
        return dx + dy <= 1.0f;
    }

    private BattleGridKey? ResolvePointerMarkerGridKey(Vector2 localMouse)
    {
        foreach (var (grid, occupants) in _occupantsByGrid)
        {
            foreach (var occupant in occupants)
            {
                if (occupant.Marker == null || !IsBattlePiece(occupant))
                {
                    continue;
                }

                var markerRadius = Mathf.Max(20.0f, occupant.Marker.Radius + 8.0f);
                if (localMouse.DistanceTo(occupant.Marker.Position) <= markerRadius)
                {
                    return grid;
                }
            }
        }

        return null;
    }

    private BattleGridKey? ResolvePointerOccludedUnitSilhouetteGridKey(Vector2 localMouse)
    {
        if (_commandMode is BattleCommandMode.MoveSelect or BattleCommandMode.AttackSelect or BattleCommandMode.StrategySelect or BattleCommandMode.DuelSelect or BattleCommandMode.ChargeSelect or BattleCommandMode.HireOfficerSelect)
        {
            return null;
        }

        foreach (var (grid, silhouette) in _occludedUnitSilhouettesByGrid)
        {
            if (!GodotObject.IsInstanceValid(silhouette))
            {
                continue;
            }

            if (localMouse.DistanceTo(silhouette.Position) <= 32.0f)
            {
                return grid;
            }
        }

        return null;
    }

    private bool IsWithinMap(Vector2I grid)
    {
        return grid.X >= 0 &&
               grid.X < BattleMapData.Width &&
               grid.Y >= 0 &&
               grid.Y < BattleMapData.Height;
    }

    private BattleGridKey GetDefaultGridKey(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return ToGroundGridKey(grid);
        }

        return IsWallTopGrid(grid)
            ? ToWallWalkGridKey(grid)
            : ToGroundGridKey(grid);
    }

    private static BattleGridKey ToGroundGridKey(Vector2I grid)
    {
        return new BattleGridKey(grid.X, grid.Y, 0);
    }

    private static BattleGridKey ToWallWalkGridKey(Vector2I grid)
    {
        return new BattleGridKey(grid.X, grid.Y, 2);
    }

    private bool IsWallTopGrid(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        return cell.Structure is BattleStructureType.Wall or BattleStructureType.Gate or BattleStructureType.Tower;
    }

    private BattleGridKey? ResolveStepGridKey(BattleGridKey sourceGrid, Vector2I destinationGrid)
    {
        if (_mapData == null || !IsWithinMap(destinationGrid))
        {
            return null;
        }

        if (sourceGrid.Level == 2)
        {
            if (IsWallTopGrid(destinationGrid))
            {
                return ToWallWalkGridKey(destinationGrid);
            }

            return null;
        }

        if (IsWallTopGrid(destinationGrid))
        {
            if (sourceGrid.Level == 0 &&
                IsGateGrid(sourceGrid.Grid) &&
                IsGateGrid(destinationGrid))
            {
                return ToGroundGridKey(destinationGrid);
            }

            if (CanUseGateGroundPassage(destinationGrid) && sourceGrid.Level == 0)
            {
                return ToGroundGridKey(destinationGrid);
            }

            return IsInsideCityGroundGrid(sourceGrid.Grid)
                ? ToWallWalkGridKey(destinationGrid)
                : null;
        }

        return ToGroundGridKey(destinationGrid);
    }

    private bool IsGateGroundPassage(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        return cell.Structure == BattleStructureType.Gate && (cell.IsGateOpen || cell.IsBroken);
    }

    private bool CanUseGateGroundPassage(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        if (cell.Structure != BattleStructureType.Gate)
        {
            return false;
        }

        return cell.IsGateOpen || cell.IsBroken;
    }

    private bool IsGateGrid(Vector2I grid)
    {
        if (_mapData == null || !IsWithinMap(grid))
        {
            return false;
        }

        return _mapData.GetCell(grid.X, grid.Y).Structure == BattleStructureType.Gate;
    }
}
