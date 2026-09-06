using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static ThreeKingdom.Battle.BattlePresentationSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private void BuildCastleDepthVisuals()
    {
        ClearCastleDepthVisuals();
        if (_battleDepthLayer == null || _groundLayer == null || _mapData == null)
        {
            return;
        }

        if (_castleLayer != null)
        {
            _castleLayer.Visible = false;
        }

        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var cell = _mapData.GetCell(x, y);
                if (!BattleTileMapBuilder.TryGetCastleSpriteSpec(cell, out var spec))
                {
                    continue;
                }

                var sprite = CreateCastleDepthSprite(cell.Grid, spec);
                _battleDepthLayer.AddChild(sprite);
                _castleDepthSpritesByGrid[cell.Grid] = sprite;
                RegisterBattleDepthEntry(sprite, ToGroundGridKey(cell.Grid), BattleDepthRenderKind.CastleVisual);
            }
        }
    }

    private void ClearCastleDepthVisuals()
    {
        foreach (var sprite in _castleDepthSpritesByGrid.Values)
        {
            _battleDepthEntries.Remove(sprite);
            sprite.QueueFree();
        }

        _castleDepthSpritesByGrid.Clear();
    }

    private void BuildBuildingDepthVisuals()
    {
        ClearBuildingDepthVisuals();
        if (_battleDepthLayer == null || _objectLayer == null || _mapData == null)
        {
            return;
        }

        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var cell = _mapData.GetCell(x, y);
                if (!BattleTileMapBuilder.TryGetBuildingSpriteSpec(cell, out var spec))
                {
                    continue;
                }

                if (ShouldKeepFieldOutpostInObjectLayer(cell))
                {
                    // Keep the authored fortress tile with the forest tiles in ObjectLayer.
                    // A separate runtime host carries only the owner flag, so it can still update on capture.
                    var flagHost = new Node2D
                    {
                        Name = $"OutpostFlagHost_{cell.Grid.X}_{cell.Grid.Y}",
                        Position = GetCastleDepthSpritePosition(cell.Grid)
                    };
                    _battleDepthLayer.AddChild(flagHost);
                    _staticOutpostFlagHostsByGrid[cell.Grid] = flagHost;
                    RefreshDefenseOutpostFlag(cell.Grid);
                    RegisterBattleDepthEntry(flagHost, ToGroundGridKey(cell.Grid), BattleDepthRenderKind.BuildingVisual);
                    continue;
                }

                // Building tiles are rendered in the shared depth layer, not the fixed ObjectLayer.
                _objectLayer.EraseCell(cell.Grid);
                var sprite = CreateCastleDepthSprite(cell.Grid, spec);
                sprite.Name = $"Building_{cell.Grid.X}_{cell.Grid.Y}";
                _battleDepthLayer.AddChild(sprite);
                _buildingDepthSpritesByGrid[cell.Grid] = sprite;
                RefreshDefenseOutpostFlag(cell.Grid);
                RegisterBattleDepthEntry(sprite, ToGroundGridKey(cell.Grid), BattleDepthRenderKind.BuildingVisual);
            }
        }

        _objectLayer.UpdateInternals();
    }

    private void ClearBuildingDepthVisuals()
    {
        foreach (var sprite in _buildingDepthSpritesByGrid.Values)
        {
            _battleDepthEntries.Remove(sprite);
            sprite.QueueFree();
        }

        foreach (var flagHost in _staticOutpostFlagHostsByGrid.Values)
        {
            _battleDepthEntries.Remove(flagHost);
            flagHost.QueueFree();
        }

        _buildingDepthSpritesByGrid.Clear();
        _staticOutpostFlagHostsByGrid.Clear();
        _outpostOwnerFlagsByGrid.Clear();
    }

    private void ClearHighlightDepthVisuals()
    {
        foreach (var visual in _highlightDepthVisuals)
        {
            _battleDepthEntries.Remove(visual);
            visual.QueueFree();
        }

        _highlightDepthVisuals.Clear();
    }

    private Sprite2D CreateCastleDepthSprite(Vector2I grid, BattleTileMapBuilder.BattleTileSpriteSpec spec)
    {
        var sprite = new Sprite2D
        {
            Name = $"Castle_{grid.X}_{grid.Y}",
            Texture = spec.Texture,
            RegionEnabled = true,
            RegionRect = spec.Region,
            FlipH = spec.FlipHorizontally,
            Centered = false,
            Offset = -spec.Pivot,
            Position = GetCastleDepthSpritePosition(grid),
            ZIndex = 0
        };
        return sprite;
    }

    private Vector2 GetCastleDepthSpritePosition(Vector2I grid)
    {
        return _groundLayer?.MapToLocal(grid) ?? BattleMapRenderer.GridToWorld(grid);
    }

    private void RefreshCastleDepthVisual(Vector2I grid)
    {
        if (_battleDepthLayer == null || _mapData == null || !IsWithinMap(grid))
        {
            return;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        if (!BattleTileMapBuilder.TryGetCastleSpriteSpec(cell, out var spec))
        {
            if (_castleDepthSpritesByGrid.Remove(grid, out var removedSprite))
            {
                _battleDepthEntries.Remove(removedSprite);
                removedSprite.QueueFree();
            }

            RefreshBattleDepthLayerOrder();
            return;
        }

        if (!_castleDepthSpritesByGrid.TryGetValue(grid, out var sprite))
        {
            sprite = CreateCastleDepthSprite(grid, spec);
            _battleDepthLayer.AddChild(sprite);
            _castleDepthSpritesByGrid[grid] = sprite;
        }
        else
        {
            sprite.Texture = spec.Texture;
            sprite.RegionRect = spec.Region;
            sprite.FlipH = spec.FlipHorizontally;
            sprite.Offset = -spec.Pivot;
            sprite.Position = GetCastleDepthSpritePosition(grid);
        }

        RegisterBattleDepthEntry(sprite, ToGroundGridKey(grid), BattleDepthRenderKind.CastleVisual);
        RefreshBattleDepthLayerOrder();
    }

    private void RegisterBattleDepthEntry(Node2D node, BattleGridKey grid, BattleDepthRenderKind kind)
    {
        if (_battleDepthLayer != null && node.GetParent() != _battleDepthLayer)
        {
            node.Reparent(_battleDepthLayer);
        }

        node.ZIndex = 0;
        _battleDepthEntries[node] = new BattleDepthEntry(node, grid, kind, GetBattleDepthLocalOrder(kind));
    }

    private void RefreshBattleDepthLayerOrder()
    {
        if (_battleDepthLayer == null)
        {
            return;
        }

        var sortedEntries = _battleDepthEntries.Values
            .Where(entry => GodotObject.IsInstanceValid(entry.Node) && entry.Node.GetParent() == _battleDepthLayer)
            .OrderBy(entry => entry, Comparer<BattleDepthEntry>.Create(CompareBattleDepthEntries))
            .ToList();

        for (var index = 0; index < sortedEntries.Count; index++)
        {
            _battleDepthLayer.MoveChild(sortedEntries[index].Node, index);
        }
    }

    private static int CompareBattleDepthEntries(BattleDepthEntry left, BattleDepthEntry right)
    {
        var leftBand = GetBattleDepthRenderBand(left.Kind);
        var rightBand = GetBattleDepthRenderBand(right.Kind);
        if (leftBand != rightBand)
        {
            return leftBand.CompareTo(rightBand);
        }

        var leftDepth = GetBattleDepth(left.Grid);
        var rightDepth = GetBattleDepth(right.Grid);
        if (leftDepth != rightDepth)
        {
            return leftDepth.CompareTo(rightDepth);
        }

        if (left.LocalOrder != right.LocalOrder)
        {
            return left.LocalOrder.CompareTo(right.LocalOrder);
        }

        if (left.Grid.Y != right.Grid.Y)
        {
            return left.Grid.Y.CompareTo(right.Grid.Y);
        }

        return left.Grid.X.CompareTo(right.Grid.X);
    }

    private static int GetBattleDepthRenderBand(BattleDepthRenderKind kind)
    {
        return kind switch
        {
            BattleDepthRenderKind.CastleVisual => 0,
            BattleDepthRenderKind.BuildingVisual or
            BattleDepthRenderKind.MoveHighlight or
            BattleDepthRenderKind.SelectedHighlight => 3,
            BattleDepthRenderKind.AttackHighlight => 2,
            BattleDepthRenderKind.SiegeEngine or
            BattleDepthRenderKind.Unit => 3,
            BattleDepthRenderKind.FireEffect => 4,
            _ => 0
        };
    }

    private static int GetBattleDepth(BattleGridKey grid)
    {
        return grid.X + grid.Y + GetBattleLevelDepthOffset(grid.Level);
    }

    private static int GetBattleLevelDepthOffset(int level)
    {
        return level * WallTopLevelDepthOffset;
    }

    private static int GetBattleDepthLocalOrder(BattleDepthRenderKind kind)
    {
        return kind switch
        {
            BattleDepthRenderKind.CastleVisual => 0,
            BattleDepthRenderKind.BuildingVisual => 0,
            BattleDepthRenderKind.MoveHighlight => 4,
            BattleDepthRenderKind.AttackHighlight => 5,
            BattleDepthRenderKind.SelectedHighlight => 6,
            BattleDepthRenderKind.SiegeEngine => 10,
            BattleDepthRenderKind.Unit => 20,
            BattleDepthRenderKind.FireEffect => 30,
            _ => 0
        };
    }

    private void RefreshOccludedUnitSilhouettes()
    {
        ClearOccludedUnitSilhouettes();
        if (_occludedUnitSilhouetteLayer == null || _mapData == null)
        {
            return;
        }

        foreach (var (grid, occupants) in _occupantsByGrid)
        {
            if (!IsUnitOccludedByCastleVisual(grid))
            {
                continue;
            }

            var occupant = occupants.FirstOrDefault(candidate =>
                candidate.Marker != null &&
                IsBattlePiece(candidate) &&
                IsVisibleToCurrentTurnSide(candidate));
            if (occupant?.Marker == null)
            {
                continue;
            }

            occupant.Marker.Visible = false;
            var silhouette = occupant.Marker.CreateSilhouetteVisual(GetOccludedUnitSilhouetteColor(occupant));
            if (silhouette == null)
            {
                ApplyHiddenMarkerVisibility(occupant);
                continue;
            }

            silhouette.Name = $"Occluded_{occupant.ShortLabel}_{grid.X}_{grid.Y}_L{grid.Level}";
            silhouette.Position = GetMarkerPosition(grid);
            _occludedUnitSilhouetteLayer.AddChild(silhouette);
            _occludedUnitSilhouettesByGrid[grid] = silhouette;
        }
    }

    private void ClearOccludedUnitSilhouettes()
    {
        RestoreOccludedMarkerVisibility();
        foreach (var silhouette in _occludedUnitSilhouettesByGrid.Values)
        {
            silhouette.QueueFree();
        }

        _occludedUnitSilhouettesByGrid.Clear();
    }

    private void RestoreOccludedMarkerVisibility()
    {
        foreach (var occupants in _occupantsByGrid.Values)
        {
            foreach (var occupant in occupants)
            {
                if (occupant.Marker != null && IsBattlePiece(occupant))
                {
                    ApplyHiddenMarkerVisibility(occupant);
                }
            }
        }
    }

    private bool IsUnitOccludedByCastleVisual(BattleGridKey grid)
    {
        if (_mapData == null || !IsWithinMap(grid.Grid))
        {
            return false;
        }

        if (grid.Level != 0)
        {
            return false;
        }

        var cell = _mapData.GetCell(grid.X, grid.Y);
        if (cell.Structure == BattleStructureType.Gate)
        {
            if (cell.IsBroken)
            {
                return false;
            }

            return cell.IsGateOpen
                ? cell.HideGroundOccupantWhenGateOpen
                : cell.HideGroundOccupantWithForeground;
        }

        return cell.HideGroundOccupantWithForeground;
    }

    private static Color GetOccludedUnitSilhouetteColor(BattleOccupantInfo occupant)
    {
        return IsAttackerPiece(occupant)
            ? new Color(1.0f, 0.18f, 0.10f, 0.42f)
            : new Color(0.25f, 0.62f, 1.0f, 0.42f);
    }
}
