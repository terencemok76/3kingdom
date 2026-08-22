using System;
using System.Collections.Generic;
using Godot;

namespace ThreeKingdom.Battle;

public enum BattleTileLayerKind
{
    Ground,
    Moat,
    Object,
    Castle,
    DeploymentOverlay
}

internal enum BattleFloorTileVisual
{
    Grass = 0,
    Road = 1,
    Courtyard = 2,
    WallWalk = 3,
    ForestGround = 4,
    River = 5,
    River2 = 6,
    Swamp = 7,
    Coast = 8,
    WetGrass = 9,
    Mud = 10,
    Pebble = 11,
    ShallowWater = 12,
    WornRoad = 13,
    OfficialRoad = 14,
    DryRiverBed = 15
}

internal enum BattleObjectTileVisual
{
    Tree = 0,
    Rock_Big = 1,
    Rock_Small = 2,
    Bridge = 3,
    WoodenFence = 4
}

internal enum BattleCastleTileVisual
{
    Wall0 = 0,
    Wall1 = 1,
    Wall2 = 2,
    Wall3 = 3,
    Wall4 = 4,
    Wall5 = 5,
    GateLeft = 6,
    GateRight = 7,
    Wall8 = 8,
    Wall9 = 9
}

internal enum BattleOverlayTileVisual
{
    AttackerZone = 0,
    DefenderZone = 1
}

public static class BattleTileMapBuilder
{
    private const int TileWidth = 128;
    private const int BaseTileHeight = 64;
    private const int AtlasSourceId = 0;
    private const int CastleOpenGateAtlasSourceId = 1;
    private const int ObjectBuildingAtlasSourceId = 1;
    private const int ObjectForestAtlasSourceId = 2;
    private const int ObjectSwampAtlasSourceId = 3;
    private const int ObjectHillAtlasSourceId = 4;
    private const int ObjectMountainAtlasSourceId = 5;
    private const int ObjectWoodAtlasSourceId = 6;
    private const int ObjectFarmAtlasSourceId = 7;
    private const int ObjectBridgeAtlasSourceId = 8;
    private const int ObjectRiverDecorationAtlasSourceId = 9;
    private const int ObjectFarmDecorationAtlasSourceId = 10;
    private const int ObjectForestEdgeDecorationAtlasSourceId = 11;
    private const int ObjectDefenseOutpostAtlasSourceId = 12;
    private const int ObjectFarmDecorationRegionHeight = 96;
    private const int ObjectBuildingTileCount = 3;
    private const int ObjectDefenseOutpostTileCount = 2;
    private const string FloorAtlasPath = "res://assets/battle/floor/floor.png";
    private const string ObjectAtlasPath = "res://assets/battle/object/object_01.png";
    private const string ObjectBuildingAtlasPath = "res://assets/battle/object/building_01.png";
    private const string ObjectDefenseOutpostAtlasPath = "res://assets/battle/object/outpost_01.png";
    private const string ObjectForestAtlasPath = "res://assets/battle/object/forest_01.png";
    private const string ObjectSwampAtlasPath = "res://assets/battle/object/swamp_01.png";
    private const string ObjectHillAtlasPath = "res://assets/battle/object/hill_01.png";
    private const string ObjectMountainAtlasPath = "res://assets/battle/object/mountain_01.png";
    private const string ObjectWoodAtlasPath = "res://assets/battle/object/wood_01.png";
    private const string ObjectFarmAtlasPath = "res://assets/battle/object/farm_01.png";
    private const string ObjectBridgeAtlasPath = "res://assets/battle/object/bridge_01.png";
    private const string ObjectRiverDecorationAtlasPath = "res://assets/battle/object/river_object_01.png";
    private const string ObjectFarmDecorationAtlasPath = "res://assets/battle/object/farm_object_01.png";
    private const string ObjectForestEdgeDecorationAtlasPath = "res://assets/battle/object/forest_object_01.png";
    private const string CastleAtlasPath = "res://assets/battle/wall/castle.png";
    private const string CastleOpenGateAtlasPath = "res://assets/battle/wall/castle_gate_open.png";
    private const string OverlayAtlasPath = "res://assets/battle/overlay/overlay.png";

    private static readonly Dictionary<BattleTileLayerKind, TileSet> SharedTileSets = new();
    private static readonly Dictionary<BattleTileLayerKind, Texture2D> SharedAtlasTextures = new();
    private static Texture2D? SharedCastleOpenGateAtlasTexture;

    public readonly record struct BattleTileSpriteSpec(Texture2D Texture, Rect2 Region, Vector2 Pivot, bool FlipHorizontally = false);

    public static TileSet CreateSharedTileSet(BattleTileLayerKind layerKind)
    {
        if (!SharedTileSets.TryGetValue(layerKind, out var tileSet))
        {
            tileSet = BuildTileSet(layerKind);
            SharedTileSets[layerKind] = tileSet;
        }

        return tileSet;
    }

    public static void ConfigureLayer(TileMapLayer layer, BattleMapData mapData, BattleTileLayerKind layerKind)
    {
        AssignLayerTileSet(layer, layerKind);

        foreach (var coords in layer.GetUsedCells())
        {
            layer.EraseCell(coords);
        }

        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var cell = mapData.GetCell(x, y);
                var atlasCoords = ResolveAtlasCoords(cell, layerKind);
                if (!atlasCoords.HasValue)
                {
                    continue;
                }

                layer.SetCell(cell.Grid, ResolveAtlasSourceId(cell, layerKind), atlasCoords.Value, ResolveAlternativeTile(cell, layerKind));
            }
        }

        layer.UpdateInternals();
    }

    public static void AssignLayerTileSet(TileMapLayer layer, BattleTileLayerKind layerKind)
    {
        layer.TileSet = CreateSharedTileSet(layerKind);
        layer.RenderingQuadrantSize = 8;
        layer.UpdateInternals();
    }

    public static void SetCastleGateVisual(TileMapLayer layer, Vector2I grid, bool isOpen)
    {
        var alternativeTile = layer.GetCellAlternativeTile(grid);
        var gateSegment = ResolveGateSegment(grid, layer);
        var baseGateSegment = IsHorizontallyFlipped(alternativeTile)
            ? MirrorGateSegment(gateSegment)
            : gateSegment;
        if (isOpen)
        {
            layer.SetCell(grid, CastleOpenGateAtlasSourceId, new Vector2I(baseGateSegment == BattleGateSegment.Right ? 1 : 0, 0), alternativeTile);
        }
        else
        {
            var visual = baseGateSegment == BattleGateSegment.Right
                ? BattleCastleTileVisual.GateRight
                : BattleCastleTileVisual.GateLeft;
            layer.SetCell(grid, AtlasSourceId, new Vector2I((int)visual, 0), alternativeTile);
        }

        layer.UpdateInternals();
    }

    public static bool TryGetCastleSpriteSpec(BattleCellData cell, out BattleTileSpriteSpec spec)
    {
        var metrics = GetAtlasMetrics(BattleTileLayerKind.Castle);
        var pivot = metrics.GetSpriteFootPivot();
        var usesAuthoredCastleVisual = cell.CastleSourceId >= 0 && cell.CastleAtlasCoords.X >= 0;
        var flipHorizontally = usesAuthoredCastleVisual
            ? IsHorizontallyFlipped(cell.CastleAlternativeTile)
            : cell.StructureFacing == BattleStructureFacing.NorthWest;
        if (cell.Structure == BattleStructureType.Gate && cell.IsGateOpen)
        {
            var openTexture = GetCastleOpenGateAtlasTexture(metrics);
            if (openTexture != null)
            {
                var gateSegment = usesAuthoredCastleVisual
                    ? ResolveBaseGateSegment(cell.CastleSourceId, cell.CastleAtlasCoords)
                    : cell.GateSegment;
                var openAtlasCoords = new Vector2I(gateSegment == BattleGateSegment.Right ? 1 : 0, 0);
                spec = CreateSpriteSpec(openTexture, metrics, openAtlasCoords, pivot, flipHorizontally);
                return true;
            }
        }

        if (usesAuthoredCastleVisual)
        {
            var authoredTexture = cell.CastleSourceId == CastleOpenGateAtlasSourceId
                ? GetCastleOpenGateAtlasTexture(metrics)
                : GetAtlasTexture(BattleTileLayerKind.Castle);
            if (authoredTexture != null)
            {
                spec = CreateSpriteSpec(authoredTexture, metrics, cell.CastleAtlasCoords, pivot, flipHorizontally);
                return true;
            }
        }

        var atlasCoords = ResolveCastleVisual(cell);
        if (!atlasCoords.HasValue)
        {
            spec = default;
            return false;
        }

        var texture = GetAtlasTexture(BattleTileLayerKind.Castle);
        spec = CreateSpriteSpec(texture, metrics, atlasCoords.Value, pivot, flipHorizontally);
        return true;
    }

    public static bool TryGetBuildingSpriteSpec(BattleCellData cell, out BattleTileSpriteSpec spec)
    {
        if (cell.Structure != BattleStructureType.Building)
        {
            spec = default;
            return false;
        }

        var atlasPath = cell.IsDefenseOutpost ? ObjectDefenseOutpostAtlasPath : ObjectBuildingAtlasPath;
        if (!ResourceLoader.Exists(atlasPath))
        {
            spec = default;
            return false;
        }

        var texture = GD.Load<Texture2D>(atlasPath);
        if (texture == null)
        {
            spec = default;
            return false;
        }

        var metrics = GetAtlasMetrics(BattleTileLayerKind.Object);
        var atlasCoords = new Vector2I(
            cell.IsDefenseOutpost
                ? Mathf.Clamp(cell.DefenseOutpostAtlasIndex, 0, ObjectDefenseOutpostTileCount - 1)
                : Mathf.Clamp(cell.BuildingAtlasCoords.X, 0, ObjectBuildingTileCount - 1),
            0);
        spec = CreateSpriteSpec(texture, metrics, atlasCoords, metrics.GetSpriteFootPivot());
        return true;
    }

    private static Vector2I? ResolveAtlasCoords(BattleCellData cell, BattleTileLayerKind layerKind)
    {
        return layerKind switch
        {
            BattleTileLayerKind.Ground => ResolveFloorVisual(cell),
            BattleTileLayerKind.Moat => ResolveMoatVisual(cell),
            BattleTileLayerKind.Object => ResolveObjectVisual(cell),
            BattleTileLayerKind.Castle => ResolveCastleVisual(cell),
            BattleTileLayerKind.DeploymentOverlay => ResolveOverlayVisual(cell),
            _ => null
        };
    }

    private static Vector2I ResolveFloorVisual(BattleCellData cell)
    {
        if (cell.GroundAtlasCoords.X >= 0)
        {
            return cell.GroundAtlasCoords;
        }

        var visual = cell.Terrain switch
        {
            BattleTerrainType.Road => BattleFloorTileVisual.Road,
            BattleTerrainType.Courtyard => BattleFloorTileVisual.Courtyard,
            BattleTerrainType.WallWalk => BattleFloorTileVisual.WallWalk,
            BattleTerrainType.Forest => BattleFloorTileVisual.ForestGround,
            BattleTerrainType.River => BattleFloorTileVisual.River2,
            BattleTerrainType.Swamp => BattleFloorTileVisual.Swamp,
            BattleTerrainType.Coast => BattleFloorTileVisual.Coast,
            _ => BattleFloorTileVisual.Grass
        };
        return GetAtlasCoords(BattleTileLayerKind.Ground, (int)visual);
    }

    private static Vector2I? ResolveMoatVisual(BattleCellData cell)
    {
        return cell.Terrain is BattleTerrainType.Moat or BattleTerrainType.Bridge
            ? GetAtlasCoords(BattleTileLayerKind.Moat, (int)BattleFloorTileVisual.River)
            : null;
    }

    private static Vector2I? ResolveObjectVisual(BattleCellData cell)
    {
        if (cell.HasBridgeVisual)
        {
            return cell.BridgeAtlasCoords.X >= 0
                ? cell.BridgeAtlasCoords
                : new Vector2I((int)BattleObjectTileVisual.Bridge, 0);
        }

        if (cell.Structure == BattleStructureType.Building)
        {
            return new Vector2I(cell.IsDefenseOutpost
                ? Mathf.Clamp(cell.DefenseOutpostAtlasIndex, 0, ObjectDefenseOutpostTileCount - 1)
                : Mathf.Clamp(cell.BuildingAtlasCoords.X, 0, ObjectBuildingTileCount - 1), 0);
        }

        if (cell.Terrain == BattleTerrainType.Forest && cell.ForestAtlasCoords.X >= 0)
        {
            return cell.ForestAtlasCoords;
        }

        if (cell.Terrain == BattleTerrainType.Swamp && cell.SwampAtlasCoords.X >= 0)
        {
            return cell.SwampAtlasCoords;
        }

        if (cell.Terrain == BattleTerrainType.Hill && cell.HillAtlasCoords.X >= 0)
        {
            return cell.HillAtlasCoords;
        }

        if (cell.Terrain == BattleTerrainType.Mountain && cell.MountainAtlasCoords.X >= 0)
        {
            return cell.MountainAtlasCoords;
        }

        if (cell.Terrain == BattleTerrainType.Farm && cell.FarmAtlasCoords.X >= 0)
        {
            return cell.FarmAtlasCoords;
        }

        var visual = cell.Structure switch
        {
            BattleStructureType.Tree => BattleObjectTileVisual.Tree,
            BattleStructureType.RockBig => BattleObjectTileVisual.Rock_Big,
            BattleStructureType.RockSmall => BattleObjectTileVisual.Rock_Small,
            BattleStructureType.WoodenFence => BattleObjectTileVisual.WoodenFence,
            _ => (BattleObjectTileVisual?)null
        };

        return visual.HasValue ? new Vector2I((int)visual.Value, 0) : null;
    }

    private static int ResolveAtlasSourceId(BattleCellData cell, BattleTileLayerKind layerKind)
    {
        return layerKind == BattleTileLayerKind.Object && cell.HasBridgeVisual && cell.BridgeAtlasSourceId == ObjectBridgeAtlasSourceId
            ? ObjectBridgeAtlasSourceId
            : layerKind == BattleTileLayerKind.Object && cell.Structure == BattleStructureType.Building
            ? cell.IsDefenseOutpost ? ObjectDefenseOutpostAtlasSourceId : ObjectBuildingAtlasSourceId
            : layerKind == BattleTileLayerKind.Object && cell.Terrain == BattleTerrainType.Forest && cell.ForestAtlasCoords.X >= 0
                ? cell.ForestAtlasSourceId == ObjectWoodAtlasSourceId
                    ? ObjectWoodAtlasSourceId
                    : ObjectForestAtlasSourceId
            : layerKind == BattleTileLayerKind.Object && cell.Terrain == BattleTerrainType.Swamp && cell.SwampAtlasCoords.X >= 0
                ? ObjectSwampAtlasSourceId
            : layerKind == BattleTileLayerKind.Object && cell.Terrain == BattleTerrainType.Hill && cell.HillAtlasCoords.X >= 0
                ? ObjectHillAtlasSourceId
            : layerKind == BattleTileLayerKind.Object && cell.Terrain == BattleTerrainType.Mountain && cell.MountainAtlasCoords.X >= 0
                ? ObjectMountainAtlasSourceId
            : layerKind == BattleTileLayerKind.Object && cell.Terrain == BattleTerrainType.Farm && cell.FarmAtlasCoords.X >= 0
                ? ObjectFarmAtlasSourceId
            : AtlasSourceId;
    }

    private static Vector2I? ResolveCastleVisual(BattleCellData cell)
    {
        BattleCastleTileVisual? visual = cell.Structure switch
        {
            BattleStructureType.Gate => cell.GateSegment == BattleGateSegment.Right
                ? BattleCastleTileVisual.GateRight
                : BattleCastleTileVisual.GateLeft,
            BattleStructureType.Wall => ResolveWallVisual(cell.Grid.X),
            
            BattleStructureType.Tower => BattleCastleTileVisual.Wall0,
            // Buildings are logical blockers until dedicated interior building art is available.
            // Rendering them as Wall0 created repeated interior castle walls.
            BattleStructureType.Building => null,
            _ => null
        };

        return visual.HasValue ? new Vector2I((int)visual.Value, 0) : null;
    }

    private static BattleGateSegment ResolveGateSegment(Vector2I grid, TileMapLayer layer)
    {
        var alternativeTile = layer.GetCellAlternativeTile(grid);
        var isFlippedHorizontally = IsHorizontallyFlipped(alternativeTile);
        var baseSegment = ResolveBaseGateSegment(layer.GetCellSourceId(grid), layer.GetCellAtlasCoords(grid));

        if (!isFlippedHorizontally)
        {
            return baseSegment;
        }

        return MirrorGateSegment(baseSegment);
    }

    private static BattleCastleTileVisual ResolveWallVisual(int x)
    {
        if (x < 6)
            return BattleCastleTileVisual.Wall0;
        else if (x == 6)
            return BattleCastleTileVisual.Wall1;
        else if (x == 7)
            return BattleCastleTileVisual.Wall2;
        else if (x == 8)
            return BattleCastleTileVisual.Wall3;
        else if (x == 9)
            return BattleCastleTileVisual.Wall4;
        else if (x == 10)
            return BattleCastleTileVisual.Wall5;
        else if (x == 13)
            return BattleCastleTileVisual.Wall8;
        else if (x == 14)
            return BattleCastleTileVisual.Wall0;
        else if (x == 15)
            return BattleCastleTileVisual.Wall1;
        else if (x == 16)
            return BattleCastleTileVisual.Wall2;
        else if (x == 17)
            return BattleCastleTileVisual.Wall3;
        else if (x >= 18)
            return BattleCastleTileVisual.Wall4;
        else
            return BattleCastleTileVisual.Wall0;
    }

    private static Vector2I? ResolveOverlayVisual(BattleCellData cell)
    {
        BattleOverlayTileVisual? visual = cell.DeploymentZone switch
        {
            BattleDeploymentZone.Attacker => BattleOverlayTileVisual.AttackerZone,
            BattleDeploymentZone.Defender => BattleOverlayTileVisual.DefenderZone,
            _ => null
        };

        return visual.HasValue ? new Vector2I((int)visual.Value, 0) : null;
    }

    private static BattleGateSegment ResolveBaseGateSegment(int sourceId, Vector2I atlasCoords)
    {
        return sourceId == CastleOpenGateAtlasSourceId
            ? atlasCoords.X == 1 ? BattleGateSegment.Right : BattleGateSegment.Left
            : atlasCoords.X == (int)BattleCastleTileVisual.GateRight
                ? BattleGateSegment.Right
                : BattleGateSegment.Left;
    }

    private static int ResolveAlternativeTile(BattleCellData cell, BattleTileLayerKind layerKind)
    {
        return layerKind switch
        {
            BattleTileLayerKind.Castle => cell.StructureFacing == BattleStructureFacing.NorthWest
                ? (int)TileSetAtlasSource.TransformFlipH
                : 0,
            BattleTileLayerKind.Object => ShouldFlipObjectTileHorizontally(cell)
                ? (int)TileSetAtlasSource.TransformFlipH
                : 0,
            _ => 0
        };
    }

    private static bool ShouldFlipObjectTileHorizontally(BattleCellData cell)
    {
        return cell.HasBridgeVisual && cell.BridgeFlipHorizontally ||
               cell.Structure == BattleStructureType.WoodenFence && cell.WoodenFenceFlipHorizontally;
    }

    private static bool IsHorizontallyFlipped(int alternativeTile)
    {
        return (alternativeTile & TileSetAtlasSource.TransformFlipH) != 0;
    }

    private static BattleGateSegment MirrorGateSegment(BattleGateSegment gateSegment)
    {
        return gateSegment switch
        {
            BattleGateSegment.Right => BattleGateSegment.Left,
            BattleGateSegment.Left => BattleGateSegment.Right,
            _ => BattleGateSegment.None
        };
    }

    private static TileSet BuildTileSet(BattleTileLayerKind layerKind)
    {
        var metrics = GetAtlasMetrics(layerKind);
        var tileCount = GetTileCount(layerKind);
        var atlasTexture = LoadExternalAtlasTexture(layerKind, tileCount, metrics) ?? BuildGeneratedAtlasTexture(layerKind, tileCount, metrics);
        var atlasSource = new TileSetAtlasSource
        {
            Texture = atlasTexture,
            TextureRegionSize = new Vector2I(metrics.RegionWidth, metrics.RegionHeight),
            UseTexturePadding = false
        };
        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            var atlasCoords = GetAtlasCoords(layerKind, tileIndex);
            atlasSource.CreateTile(atlasCoords);
            var textureOrigin = metrics.GetTextureOrigin();
            if (textureOrigin != Vector2I.Zero)
            {
                atlasSource.GetTileData(atlasCoords, 0).TextureOrigin = textureOrigin;
            }
        }

        var tileSet = new TileSet
        {
            TileShape = TileSet.TileShapeEnum.Isometric,
            TileLayout = TileSet.TileLayoutEnum.DiamondDown,
            TileOffsetAxis = TileSet.TileOffsetAxisEnum.Horizontal,
            TileSize = new Vector2I(TileWidth, BaseTileHeight)
        };

        tileSet.AddSource(atlasSource, AtlasSourceId);
        if (layerKind == BattleTileLayerKind.Castle)
        {
            AddCastleOpenGateSource(tileSet, metrics);
        }
        else if (layerKind == BattleTileLayerKind.Object)
        {
            AddObjectBuildingSource(tileSet, metrics);
            AddObjectForestSource(tileSet, metrics);
            AddObjectSwampSource(tileSet, metrics);
            AddObjectHillSource(tileSet, metrics);
            AddObjectMountainSource(tileSet, metrics);
            AddObjectWoodSource(tileSet, metrics);
            AddObjectFarmSource(tileSet, metrics);
            AddObjectBridgeSource(tileSet, metrics);
            AddObjectRiverDecorationSource(tileSet, metrics);
            AddObjectFarmDecorationSource(tileSet, metrics);
            AddObjectForestEdgeDecorationSource(tileSet, metrics);
            AddObjectDefenseOutpostSource(tileSet, metrics);
        }

        return tileSet;
    }

    private static void AddObjectBuildingSource(TileSet tileSet, BattleAtlasMetrics metrics)
    {
        if (!ResourceLoader.Exists(ObjectBuildingAtlasPath))
        {
            return;
        }

        var atlasTexture = GD.Load<Texture2D>(ObjectBuildingAtlasPath);
        if (atlasTexture == null)
        {
            GD.PushWarning($"Battle building atlas could not be loaded: {ObjectBuildingAtlasPath}");
            return;
        }

        var requiredWidth = ObjectBuildingTileCount * metrics.RegionWidth;
        if (atlasTexture.GetWidth() < requiredWidth || atlasTexture.GetHeight() < metrics.RegionHeight)
        {
            GD.PushWarning(
                $"Battle building atlas too small: {ObjectBuildingAtlasPath}. " +
                $"Expected at least {requiredWidth}x{metrics.RegionHeight}, got {atlasTexture.GetWidth()}x{atlasTexture.GetHeight()}.");
            return;
        }

        var atlasSource = new TileSetAtlasSource
        {
            Texture = atlasTexture,
            TextureRegionSize = new Vector2I(metrics.RegionWidth, metrics.RegionHeight),
            UseTexturePadding = false
        };

        for (var tileIndex = 0; tileIndex < ObjectBuildingTileCount; tileIndex++)
        {
            var atlasCoords = new Vector2I(tileIndex, 0);
            atlasSource.CreateTile(atlasCoords);
            var textureOrigin = metrics.GetTextureOrigin();
            if (textureOrigin != Vector2I.Zero)
            {
                atlasSource.GetTileData(atlasCoords, 0).TextureOrigin = textureOrigin;
            }
        }

        tileSet.AddSource(atlasSource, ObjectBuildingAtlasSourceId);
    }

    private static void AddObjectDefenseOutpostSource(TileSet tileSet, BattleAtlasMetrics metrics)
    {
        if (!ResourceLoader.Exists(ObjectDefenseOutpostAtlasPath))
        {
            return;
        }

        var atlasTexture = GD.Load<Texture2D>(ObjectDefenseOutpostAtlasPath);
        if (atlasTexture == null || atlasTexture.GetWidth() < ObjectDefenseOutpostTileCount * metrics.RegionWidth || atlasTexture.GetHeight() < metrics.RegionHeight)
        {
            GD.PushWarning($"Battle defense outpost atlas could not be loaded: {ObjectDefenseOutpostAtlasPath}");
            return;
        }

        var atlasSource = new TileSetAtlasSource
        {
            Texture = atlasTexture,
            TextureRegionSize = new Vector2I(metrics.RegionWidth, metrics.RegionHeight),
            UseTexturePadding = false
        };
        for (var tileIndex = 0; tileIndex < ObjectDefenseOutpostTileCount; tileIndex++)
        {
            var atlasCoords = new Vector2I(tileIndex, 0);
            atlasSource.CreateTile(atlasCoords);
            atlasSource.GetTileData(atlasCoords, 0).TextureOrigin = metrics.GetTextureOrigin();
        }

        tileSet.AddSource(atlasSource, ObjectDefenseOutpostAtlasSourceId);
    }

    private static void AddObjectForestSource(TileSet tileSet, BattleAtlasMetrics metrics)
    {
        if (!ResourceLoader.Exists(ObjectForestAtlasPath))
        {
            return;
        }

        var atlasTexture = GD.Load<Texture2D>(ObjectForestAtlasPath);
        if (atlasTexture == null || atlasTexture.GetWidth() < metrics.RegionWidth * 4 || atlasTexture.GetHeight() < metrics.RegionHeight * 2)
        {
            GD.PushWarning($"Battle forest atlas could not be loaded: {ObjectForestAtlasPath}");
            return;
        }

        var atlasSource = new TileSetAtlasSource
        {
            Texture = atlasTexture,
            TextureRegionSize = new Vector2I(metrics.RegionWidth, metrics.RegionHeight),
            UseTexturePadding = false
        };
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var atlasCoords = new Vector2I(x, y);
                atlasSource.CreateTile(atlasCoords);
                atlasSource.GetTileData(atlasCoords, 0).TextureOrigin = metrics.GetTextureOrigin();
            }
        }

        tileSet.AddSource(atlasSource, ObjectForestAtlasSourceId);
    }

    private static void AddObjectSwampSource(TileSet tileSet, BattleAtlasMetrics metrics)
    {
        if (!ResourceLoader.Exists(ObjectSwampAtlasPath))
        {
            return;
        }

        var atlasTexture = GD.Load<Texture2D>(ObjectSwampAtlasPath);
        if (atlasTexture == null || atlasTexture.GetWidth() < metrics.RegionWidth * 4 || atlasTexture.GetHeight() < metrics.RegionHeight * 2)
        {
            GD.PushWarning($"Battle swamp atlas could not be loaded: {ObjectSwampAtlasPath}");
            return;
        }

        var atlasSource = new TileSetAtlasSource
        {
            Texture = atlasTexture,
            TextureRegionSize = new Vector2I(metrics.RegionWidth, metrics.RegionHeight),
            UseTexturePadding = false
        };
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var atlasCoords = new Vector2I(x, y);
                atlasSource.CreateTile(atlasCoords);
                atlasSource.GetTileData(atlasCoords, 0).TextureOrigin = metrics.GetTextureOrigin();
            }
        }

        tileSet.AddSource(atlasSource, ObjectSwampAtlasSourceId);
    }

    private static void AddObjectHillSource(TileSet tileSet, BattleAtlasMetrics metrics)
    {
        AddObjectAtlasSource(tileSet, metrics, ObjectHillAtlasSourceId, ObjectHillAtlasPath, "hill");
    }

    private static void AddObjectMountainSource(TileSet tileSet, BattleAtlasMetrics metrics)
    {
        AddObjectAtlasSource(tileSet, metrics, ObjectMountainAtlasSourceId, ObjectMountainAtlasPath, "mountain");
    }

    private static void AddObjectWoodSource(TileSet tileSet, BattleAtlasMetrics metrics)
    {
        AddObjectAtlasSource(tileSet, metrics, ObjectWoodAtlasSourceId, ObjectWoodAtlasPath, "wood");
    }

    private static void AddObjectFarmSource(TileSet tileSet, BattleAtlasMetrics metrics)
    {
        AddObjectAtlasSource(tileSet, metrics, ObjectFarmAtlasSourceId, ObjectFarmAtlasPath, "farm", rows: 3);
    }

    private static void AddObjectBridgeSource(TileSet tileSet, BattleAtlasMetrics metrics)
    {
        if (!ResourceLoader.Exists(ObjectBridgeAtlasPath))
        {
            return;
        }

        var atlasTexture = GD.Load<Texture2D>(ObjectBridgeAtlasPath);
        if (atlasTexture == null || atlasTexture.GetWidth() < metrics.RegionWidth * 4 || atlasTexture.GetHeight() < metrics.RegionHeight * 2)
        {
            GD.PushWarning($"Battle bridge atlas could not be loaded: {ObjectBridgeAtlasPath}");
            return;
        }

        var atlasSource = new TileSetAtlasSource
        {
            Texture = atlasTexture,
            TextureRegionSize = new Vector2I(metrics.RegionWidth, metrics.RegionHeight),
            UseTexturePadding = false
        };
        foreach (var atlasCoords in new[] { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(2, 0), new Vector2I(3, 0), new Vector2I(0, 1) })
        {
            atlasSource.CreateTile(atlasCoords);
            atlasSource.GetTileData(atlasCoords, 0).TextureOrigin = metrics.GetTextureOrigin();
        }

        tileSet.AddSource(atlasSource, ObjectBridgeAtlasSourceId);
    }

    private static void AddObjectRiverDecorationSource(TileSet tileSet, BattleAtlasMetrics metrics)
    {
        AddObjectAtlasSource(tileSet, metrics, ObjectRiverDecorationAtlasSourceId, ObjectRiverDecorationAtlasPath, "river decoration");
    }

    private static void AddObjectFarmDecorationSource(TileSet tileSet, BattleAtlasMetrics metrics)
    {
        if (!ResourceLoader.Exists(ObjectFarmDecorationAtlasPath))
        {
            return;
        }

        var atlasTexture = GD.Load<Texture2D>(ObjectFarmDecorationAtlasPath);
        if (atlasTexture == null || atlasTexture.GetWidth() < metrics.RegionWidth * 4 || atlasTexture.GetHeight() < ObjectFarmDecorationRegionHeight * 2)
        {
            GD.PushWarning($"Battle farm decoration atlas could not be loaded: {ObjectFarmDecorationAtlasPath}");
            return;
        }

        var atlasSource = new TileSetAtlasSource
        {
            Texture = atlasTexture,
            TextureRegionSize = new Vector2I(metrics.RegionWidth, ObjectFarmDecorationRegionHeight),
            UseTexturePadding = false
        };
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var atlasCoords = new Vector2I(x, y);
                atlasSource.CreateTile(atlasCoords);
                atlasSource.GetTileData(atlasCoords, 0).TextureOrigin = new Vector2I(0, 16);
            }
        }

        tileSet.AddSource(atlasSource, ObjectFarmDecorationAtlasSourceId);
    }

    private static void AddObjectForestEdgeDecorationSource(TileSet tileSet, BattleAtlasMetrics metrics)
    {
        if (!ResourceLoader.Exists(ObjectForestEdgeDecorationAtlasPath))
        {
            return;
        }

        var atlasTexture = GD.Load<Texture2D>(ObjectForestEdgeDecorationAtlasPath);
        if (atlasTexture == null || atlasTexture.GetWidth() < metrics.RegionWidth * 4 || atlasTexture.GetHeight() < ObjectFarmDecorationRegionHeight * 2)
        {
            GD.PushWarning($"Battle forest edge decoration atlas could not be loaded: {ObjectForestEdgeDecorationAtlasPath}");
            return;
        }

        var atlasSource = new TileSetAtlasSource
        {
            Texture = atlasTexture,
            TextureRegionSize = new Vector2I(metrics.RegionWidth, ObjectFarmDecorationRegionHeight),
            UseTexturePadding = false
        };
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var atlasCoords = new Vector2I(x, y);
                atlasSource.CreateTile(atlasCoords);
                atlasSource.GetTileData(atlasCoords, 0).TextureOrigin = new Vector2I(0, 16);
            }
        }

        tileSet.AddSource(atlasSource, ObjectForestEdgeDecorationAtlasSourceId);
    }

    private static void AddObjectAtlasSource(
        TileSet tileSet,
        BattleAtlasMetrics metrics,
        int sourceId,
        string atlasPath,
        string atlasName,
        int rows = 2)
    {
        if (!ResourceLoader.Exists(atlasPath))
        {
            return;
        }

        var atlasTexture = GD.Load<Texture2D>(atlasPath);
        if (atlasTexture == null || atlasTexture.GetWidth() < metrics.RegionWidth * 4 || atlasTexture.GetHeight() < metrics.RegionHeight * rows)
        {
            GD.PushWarning($"Battle {atlasName} atlas could not be loaded: {atlasPath}");
            return;
        }

        var atlasSource = new TileSetAtlasSource
        {
            Texture = atlasTexture,
            TextureRegionSize = new Vector2I(metrics.RegionWidth, metrics.RegionHeight),
            UseTexturePadding = false
        };
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var atlasCoords = new Vector2I(x, y);
                atlasSource.CreateTile(atlasCoords);
                atlasSource.GetTileData(atlasCoords, 0).TextureOrigin = metrics.GetTextureOrigin();
            }
        }

        tileSet.AddSource(atlasSource, sourceId);
    }

    private static void AddCastleOpenGateSource(TileSet tileSet, BattleAtlasMetrics metrics)
    {
        if (!ResourceLoader.Exists(CastleOpenGateAtlasPath))
        {
            return;
        }

        var atlasTexture = GD.Load<Texture2D>(CastleOpenGateAtlasPath);
        if (atlasTexture == null)
        {
            GD.PushWarning($"Battle open gate atlas could not be loaded: {CastleOpenGateAtlasPath}");
            return;
        }

        var tileCount = 2;
        var requiredWidth = tileCount * metrics.RegionWidth;
        if (atlasTexture.GetWidth() < requiredWidth || atlasTexture.GetHeight() < metrics.RegionHeight)
        {
            GD.PushWarning(
                $"Battle open gate atlas too small: {CastleOpenGateAtlasPath}. " +
                $"Expected at least {requiredWidth}x{metrics.RegionHeight}, got {atlasTexture.GetWidth()}x{atlasTexture.GetHeight()}.");
            return;
        }

        var atlasSource = new TileSetAtlasSource
        {
            Texture = atlasTexture,
            TextureRegionSize = new Vector2I(metrics.RegionWidth, metrics.RegionHeight),
            UseTexturePadding = false
        };

        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            var atlasCoords = new Vector2I(tileIndex, 0);
            atlasSource.CreateTile(atlasCoords);
            var textureOrigin = metrics.GetTextureOrigin();
            if (textureOrigin != Vector2I.Zero)
            {
                atlasSource.GetTileData(atlasCoords, 0).TextureOrigin = textureOrigin;
            }
        }

        tileSet.AddSource(atlasSource, CastleOpenGateAtlasSourceId);
    }

    private static Texture2D? LoadExternalAtlasTexture(BattleTileLayerKind layerKind, int tileCount, BattleAtlasMetrics metrics)
    {
        var atlasPath = GetAtlasPath(layerKind);
        if (string.IsNullOrWhiteSpace(atlasPath) || !ResourceLoader.Exists(atlasPath))
        {
            return null;
        }

        var texture = GD.Load<Texture2D>(atlasPath);
        if (texture == null)
        {
            GD.PushWarning($"Battle tileset atlas could not be loaded: {atlasPath}");
            return null;
        }

        var requiredAtlasSize = GetAtlasSizeInTiles(layerKind, tileCount);
        var requiredWidth = requiredAtlasSize.X * metrics.RegionWidth;
        var requiredHeight = requiredAtlasSize.Y * metrics.RegionHeight;
        if (texture.GetWidth() < requiredWidth || texture.GetHeight() < requiredHeight)
        {
            GD.PushWarning(
                $"Battle tileset atlas too small for {layerKind}: {atlasPath}. " +
                $"Expected at least {requiredWidth}x{requiredHeight}, got {texture.GetWidth()}x{texture.GetHeight()}. Using generated fallback.");
            return null;
        }

        return texture;
    }

    private static Texture2D GetAtlasTexture(BattleTileLayerKind layerKind)
    {
        if (SharedAtlasTextures.TryGetValue(layerKind, out var texture))
        {
            return texture;
        }

        var metrics = GetAtlasMetrics(layerKind);
        var tileCount = GetTileCount(layerKind);
        texture = LoadExternalAtlasTexture(layerKind, tileCount, metrics) ?? BuildGeneratedAtlasTexture(layerKind, tileCount, metrics);
        SharedAtlasTextures[layerKind] = texture;
        return texture;
    }

    private static Texture2D? GetCastleOpenGateAtlasTexture(BattleAtlasMetrics metrics)
    {
        if (SharedCastleOpenGateAtlasTexture != null)
        {
            return SharedCastleOpenGateAtlasTexture;
        }

        if (!ResourceLoader.Exists(CastleOpenGateAtlasPath))
        {
            return null;
        }

        var atlasTexture = GD.Load<Texture2D>(CastleOpenGateAtlasPath);
        if (atlasTexture == null)
        {
            GD.PushWarning($"Battle open gate atlas could not be loaded: {CastleOpenGateAtlasPath}");
            return null;
        }

        var tileCount = 2;
        var requiredWidth = tileCount * metrics.RegionWidth;
        if (atlasTexture.GetWidth() < requiredWidth || atlasTexture.GetHeight() < metrics.RegionHeight)
        {
            GD.PushWarning(
                $"Battle open gate atlas too small: {CastleOpenGateAtlasPath}. " +
                $"Expected at least {requiredWidth}x{metrics.RegionHeight}, got {atlasTexture.GetWidth()}x{atlasTexture.GetHeight()}.");
            return null;
        }

        SharedCastleOpenGateAtlasTexture = atlasTexture;
        return SharedCastleOpenGateAtlasTexture;
    }

    private static BattleTileSpriteSpec CreateSpriteSpec(Texture2D texture, BattleAtlasMetrics metrics, Vector2I atlasCoords, Vector2 pivot, bool flipHorizontally = false)
    {
        var region = new Rect2(
            atlasCoords.X * metrics.RegionWidth,
            atlasCoords.Y * metrics.RegionHeight,
            metrics.RegionWidth,
            metrics.RegionHeight);
        return new BattleTileSpriteSpec(texture, region, pivot, flipHorizontally);
    }

    private static Texture2D BuildGeneratedAtlasTexture(BattleTileLayerKind layerKind, int tileCount, BattleAtlasMetrics metrics)
    {
        var atlasImage = BuildAtlasImage(layerKind, tileCount, metrics);
        return ImageTexture.CreateFromImage(atlasImage);
    }

    private static string GetAtlasPath(BattleTileLayerKind layerKind)
    {
        return layerKind switch
        {
            BattleTileLayerKind.Ground => FloorAtlasPath,
            BattleTileLayerKind.Moat => FloorAtlasPath,
            BattleTileLayerKind.Object => ObjectAtlasPath,
            BattleTileLayerKind.Castle => CastleAtlasPath,
            BattleTileLayerKind.DeploymentOverlay => OverlayAtlasPath,
            _ => string.Empty
        };
    }

    private static int GetTileCount(BattleTileLayerKind layerKind)
    {
        return layerKind switch
        {
            BattleTileLayerKind.Ground => Enum.GetValues<BattleFloorTileVisual>().Length,
            BattleTileLayerKind.Moat => Enum.GetValues<BattleFloorTileVisual>().Length,
            BattleTileLayerKind.Object => Enum.GetValues<BattleObjectTileVisual>().Length,
            BattleTileLayerKind.Castle => Enum.GetValues<BattleCastleTileVisual>().Length,
            BattleTileLayerKind.DeploymentOverlay => Enum.GetValues<BattleOverlayTileVisual>().Length,
            _ => 0
        };
    }

    private static Vector2I GetAtlasSizeInTiles(BattleTileLayerKind layerKind, int tileCount)
    {
        return layerKind is BattleTileLayerKind.Ground or BattleTileLayerKind.Moat
            ? new Vector2I(8, 2)
            : new Vector2I(tileCount, 1);
    }

    private static Vector2I GetAtlasCoords(BattleTileLayerKind layerKind, int tileIndex)
    {
        if (layerKind is not (BattleTileLayerKind.Ground or BattleTileLayerKind.Moat))
        {
            return new Vector2I(tileIndex, 0);
        }

        return tileIndex switch
        {
            (int)BattleFloorTileVisual.Coast => new Vector2I(0, 1),
            (int)BattleFloorTileVisual.WetGrass => new Vector2I(1, 1),
            (int)BattleFloorTileVisual.Mud => new Vector2I(2, 1),
            (int)BattleFloorTileVisual.Pebble => new Vector2I(3, 1),
            (int)BattleFloorTileVisual.ShallowWater => new Vector2I(4, 1),
            (int)BattleFloorTileVisual.WornRoad => new Vector2I(5, 1),
            (int)BattleFloorTileVisual.OfficialRoad => new Vector2I(6, 1),
            (int)BattleFloorTileVisual.DryRiverBed => new Vector2I(7, 1),
            _ => new Vector2I(tileIndex, 0)
        };
    }

    private static Image BuildAtlasImage(BattleTileLayerKind layerKind, int tileCount, BattleAtlasMetrics metrics)
    {
        var atlasSize = GetAtlasSizeInTiles(layerKind, tileCount);
        var image = Image.CreateEmpty(
            atlasSize.X * metrics.RegionWidth,
            atlasSize.Y * metrics.RegionHeight,
            false,
            Image.Format.Rgba8);
        image.Fill(Colors.Transparent);

        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            var atlasCoords = GetAtlasCoords(layerKind, tileIndex);
            var tileOffsetX = atlasCoords.X * metrics.RegionWidth;
            var tileOffsetY = atlasCoords.Y * metrics.RegionHeight;
            switch (layerKind)
            {
                case BattleTileLayerKind.Ground:
                case BattleTileLayerKind.Moat:
                    DrawFloorTile(image, tileOffsetX, tileOffsetY, metrics, (BattleFloorTileVisual)tileIndex);
                    break;
                case BattleTileLayerKind.Object:
                    DrawObjectTile(image, tileOffsetX, metrics, (BattleObjectTileVisual)tileIndex);
                    break;
                case BattleTileLayerKind.Castle:
                    DrawCastleTile(image, tileOffsetX, metrics, (BattleCastleTileVisual)tileIndex);
                    break;
                case BattleTileLayerKind.DeploymentOverlay:
                    DrawOverlayTile(image, tileOffsetX, metrics, (BattleOverlayTileVisual)tileIndex);
                    break;
            }
        }

        return image;
    }

    private static void DrawFloorTile(Image image, int tileOffsetX, int tileOffsetY, BattleAtlasMetrics metrics, BattleFloorTileVisual visual)
    {
        var fillColor = visual switch
        {
            BattleFloorTileVisual.Grass => new Color("738c50"),
            BattleFloorTileVisual.Road => new Color("b89263"),
            BattleFloorTileVisual.Courtyard => new Color("c3ad78"),
            BattleFloorTileVisual.WallWalk => new Color("99846d"),
            BattleFloorTileVisual.ForestGround => new Color("5f7745"),
            BattleFloorTileVisual.River => new Color("2b7198"),
            BattleFloorTileVisual.River2 => new Color("2184b4"),
            BattleFloorTileVisual.Swamp => new Color("61734d"),
            BattleFloorTileVisual.Coast => new Color("9cb3a3"),
            _ => new Color("738c50")
        };

        var shadeColor = visual switch
        {
            BattleFloorTileVisual.Grass => new Color("5f7640"),
            BattleFloorTileVisual.Road => new Color("9f7b54"),
            BattleFloorTileVisual.Courtyard => new Color("ad9763"),
            BattleFloorTileVisual.WallWalk => new Color("7d6a56"),
            BattleFloorTileVisual.ForestGround => new Color("476037"),
            BattleFloorTileVisual.River => new Color("1c5278"),
            BattleFloorTileVisual.River2 => new Color("176285"),
            BattleFloorTileVisual.Swamp => new Color("46573b"),
            BattleFloorTileVisual.Coast => new Color("718d8c"),
            _ => new Color("5f7640")
        };

        DrawDiamond(image, tileOffsetX, metrics, fillColor, new Color("3d3528", 0.55f), (localX, localY, edge) =>
        {
            var noise = SampleNoise(localX, localY, tileOffsetX);
            var color = fillColor.Lerp(shadeColor, 0.16f + (noise * 0.18f));

            if (visual == BattleFloorTileVisual.Road)
            {
                var stripe = Mathf.Abs(localX - 64.0f) < 14.0f || Mathf.Abs(localY - 32.0f) < 8.0f;
                if (stripe)
                {
                    color = color.Lerp(new Color("d5bb8a"), 0.16f);
                }
            }
            else if (visual == BattleFloorTileVisual.Courtyard)
            {
                var pattern = ((int)localX / 8 + (int)localY / 6) % 2 == 0;
                color = color.Lerp(pattern ? new Color("d6c18d") : new Color("b39b65"), 0.12f);
            }
            else if (visual == BattleFloorTileVisual.WallWalk)
            {
                var masonry = ((int)localX / 10 + (int)localY / 6) % 2 == 0;
                color = color.Lerp(masonry ? new Color("b59f87") : new Color("85715b"), 0.18f);
            }
            else if (visual == BattleFloorTileVisual.ForestGround)
            {
                color = color.Lerp(new Color("2e4927"), noise * 0.22f);
            }
            else if (visual == BattleFloorTileVisual.River)
            {
                var wave = ((int)localX / 18 + (int)localY / 6) % 3 == 0;
                color = color.Lerp(wave ? new Color("87bcd0") : new Color("1f5f87"), 0.22f);
            }

            if (edge > 0.955f)
            {
                color = color.Lerp(new Color("e3d2a3"), 0.14f);
            }

            return color;
        }, tileOffsetY: tileOffsetY);
    }

    private static void DrawObjectTile(Image image, int tileOffsetX, BattleAtlasMetrics metrics, BattleObjectTileVisual visual)
    {
        var baselineOffset = metrics.RegionHeight - BaseTileHeight;
        switch (visual)
        {
            case BattleObjectTileVisual.Tree:
                DrawFilledEllipse(image, tileOffsetX, new Vector2(66.0f, 46.0f + baselineOffset), 24.0f, 10.0f, new Color(0.0f, 0.0f, 0.0f, 0.18f));
                DrawFilledRect(image, tileOffsetX, new Rect2(60.0f, 26.0f + baselineOffset - 48.0f, 6.0f, 18.0f), new Color("65462d"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(49.0f, 28.0f + baselineOffset - 48.0f), 18.0f, 15.0f, new Color("35572e"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(70.0f, 24.0f + baselineOffset - 48.0f), 20.0f, 17.0f, new Color("436b38"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(83.0f, 33.0f + baselineOffset - 48.0f), 14.0f, 12.0f, new Color("31522a"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(60.0f, 38.0f + baselineOffset - 48.0f), 17.0f, 12.0f, new Color("4d7a40"));
                break;
            case BattleObjectTileVisual.Rock_Big:
                DrawFilledEllipse(image, tileOffsetX, new Vector2(63.0f, 54.0f + baselineOffset - 34.0f), 27.0f, 11.0f, new Color(0.0f, 0.0f, 0.0f, 0.16f));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(49.0f, 34.0f + baselineOffset - 34.0f), 14.0f, 10.0f, new Color("b8a88f"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(68.0f, 28.0f + baselineOffset - 34.0f), 16.0f, 10.0f, new Color("c7b89f"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(82.0f, 36.0f + baselineOffset - 34.0f), 12.0f, 9.0f, new Color("a3937b"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(62.0f, 41.0f + baselineOffset - 34.0f), 26.0f, 12.0f, new Color("97866f"));
                break;
            case BattleObjectTileVisual.Rock_Small:
                DrawFilledEllipse(image, tileOffsetX, new Vector2(63.0f, 52.0f + baselineOffset - 34.0f), 20.0f, 8.0f, new Color(0.0f, 0.0f, 0.0f, 0.13f));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(56.0f, 35.0f + baselineOffset - 34.0f), 10.0f, 8.0f, new Color("baaa92"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(69.0f, 38.0f + baselineOffset - 34.0f), 11.0f, 7.0f, new Color("97876f"));
                break;
            case BattleObjectTileVisual.WoodenFence:
                DrawFilledEllipse(image, tileOffsetX, new Vector2(64.0f, 48.0f + baselineOffset - 16.0f), 30.0f, 8.0f, new Color(0.0f, 0.0f, 0.0f, 0.18f));
                DrawFilledRect(image, tileOffsetX, new Rect2(37.0f, 28.0f + baselineOffset - 38.0f, 55.0f, 6.0f), new Color("76512f"));
                DrawFilledRect(image, tileOffsetX, new Rect2(42.0f, 23.0f + baselineOffset - 38.0f, 5.0f, 22.0f), new Color("8e6338"));
                DrawFilledRect(image, tileOffsetX, new Rect2(82.0f, 23.0f + baselineOffset - 38.0f, 5.0f, 22.0f), new Color("8e6338"));
                break;
        }
    }

    private static void DrawCastleTile(Image image, int tileOffsetX, BattleAtlasMetrics metrics, BattleCastleTileVisual visual)
    {
        _ = metrics;
        switch (visual)
        {
            case BattleCastleTileVisual.Wall0:
            case BattleCastleTileVisual.Wall1:
            case BattleCastleTileVisual.Wall2:
            case BattleCastleTileVisual.Wall3:
            case BattleCastleTileVisual.Wall4:
            case BattleCastleTileVisual.Wall5:
            case BattleCastleTileVisual.Wall8:
            case BattleCastleTileVisual.Wall9:
                DrawWallBlock(image, tileOffsetX, new Color("90755a"), new Color("725a43"));
                break;
            case BattleCastleTileVisual.GateLeft:
            case BattleCastleTileVisual.GateRight:
                DrawWallBlock(image, tileOffsetX, new Color("927659"), new Color("725941"));
                DrawFilledRect(image, tileOffsetX, new Rect2(50.0f, 30.0f, 28.0f, 22.0f), new Color("573722"));
                break;
        }
    }

    private static void DrawOverlayTile(Image image, int tileOffsetX, BattleAtlasMetrics metrics, BattleOverlayTileVisual visual)
    {
        var fillColor = visual == BattleOverlayTileVisual.AttackerZone
            ? new Color(0.88f, 0.56f, 0.20f, 0.20f)
            : new Color(0.28f, 0.76f, 0.88f, 0.20f);
        var borderColor = visual == BattleOverlayTileVisual.AttackerZone
            ? new Color(0.98f, 0.77f, 0.50f, 0.42f)
            : new Color(0.70f, 0.94f, 1.0f, 0.42f);

        DrawDiamond(image, tileOffsetX, metrics, fillColor, borderColor, (_, _, _) => fillColor, 0.86f);
    }

    private static void DrawWallBlock(Image image, int tileOffsetX, Color topColor, Color sideColor)
    {
        var top = new[]
        {
            new Vector2(64.0f, 8.0f),
            new Vector2(116.0f, 28.0f),
            new Vector2(64.0f, 42.0f),
            new Vector2(12.0f, 28.0f)
        };
        DrawFilledQuad(image, tileOffsetX, top[0], top[1], top[2], top[3], topColor);

        var leftFace = new[]
        {
            top[3],
            top[2],
            top[2] + new Vector2(0.0f, 18.0f),
            top[3] + new Vector2(0.0f, 18.0f)
        };
        var rightFace = new[]
        {
            top[1],
            top[2],
            top[2] + new Vector2(0.0f, 18.0f),
            top[1] + new Vector2(0.0f, 18.0f)
        };
        DrawFilledQuad(image, tileOffsetX, leftFace[0], leftFace[1], leftFace[2], leftFace[3], sideColor.Lerp(new Color("3a2c20"), 0.08f));
        DrawFilledQuad(image, tileOffsetX, rightFace[0], rightFace[1], rightFace[2], rightFace[3], sideColor);
    }

    private static void DrawBuildingTile(Image image, int tileOffsetX)
    {
        DrawFilledQuad(
            image,
            tileOffsetX,
            new Vector2(20.0f, 36.0f),
            new Vector2(64.0f, 22.0f),
            new Vector2(108.0f, 36.0f),
            new Vector2(64.0f, 52.0f),
            new Color("7d664a"));
        DrawFilledQuad(
            image,
            tileOffsetX,
            new Vector2(64.0f, 12.0f),
            new Vector2(102.0f, 28.0f),
            new Vector2(64.0f, 42.0f),
            new Vector2(26.0f, 28.0f),
            new Color("41495a"));
    }

    private static void DrawWallCorner(Image image, int tileOffsetX)
    {
        DrawWallBlock(image, tileOffsetX, new Color("92785e"), new Color("725942"));
        DrawFilledQuad(
            image,
            tileOffsetX,
            new Vector2(64.0f, 8.0f),
            new Vector2(96.0f, 20.0f),
            new Vector2(64.0f, 28.0f),
            new Vector2(32.0f, 20.0f),
            new Color("a88c6f"));
    }

    private static void DrawBrokenWallTile(Image image, int tileOffsetX)
    {
        DrawFilledQuad(
            image,
            tileOffsetX,
            new Vector2(24.0f, 36.0f),
            new Vector2(64.0f, 24.0f),
            new Vector2(104.0f, 36.0f),
            new Vector2(64.0f, 48.0f),
            new Color("7a6248", 0.88f));
        DrawFilledRect(image, tileOffsetX, new Rect2(36.0f, 28.0f, 14.0f, 8.0f), new Color("6c523b"));
        DrawFilledRect(image, tileOffsetX, new Rect2(55.0f, 24.0f, 16.0f, 10.0f), new Color("7a6146"));
        DrawFilledRect(image, tileOffsetX, new Rect2(76.0f, 30.0f, 12.0f, 7.0f), new Color("69523f"));
    }

    private static void DrawDiamond(
        Image image,
        int tileOffsetX,
        BattleAtlasMetrics metrics,
        Color fillColor,
        Color borderColor,
        Func<float, float, float, Color> colorProvider,
        float halfScale = 1.0f,
        int tileOffsetY = 0)
    {
        var halfWidth = (TileWidth * 0.5f) * halfScale;
        var halfHeight = (BaseTileHeight * 0.5f) * halfScale;
        var centerX = TileWidth * 0.5f;
        var centerY = metrics.RegionHeight - (BaseTileHeight * 0.5f);

        for (var localY = 0; localY < metrics.RegionHeight; localY++)
        {
            for (var localX = 0; localX < TileWidth; localX++)
            {
                var dx = Mathf.Abs((localX + 0.5f) - centerX) / halfWidth;
                var dy = Mathf.Abs((localY + 0.5f) - centerY) / halfHeight;
                var edge = dx + dy;
                if (edge > 1.0f)
                {
                    continue;
                }

                var color = colorProvider(localX, localY, edge);
                if (edge > 0.96f && borderColor.A > 0.0f)
                {
                    color = borderColor;
                }
                else if (color == default)
                {
                    color = fillColor;
                }

                image.SetPixel(tileOffsetX + localX, tileOffsetY + localY, color);
            }
        }
    }

    private static void DrawFilledEllipse(Image image, int tileOffsetX, Vector2 center, float radiusX, float radiusY, Color color)
    {
        var minX = Mathf.FloorToInt(center.X - radiusX);
        var maxX = Mathf.CeilToInt(center.X + radiusX);
        var minY = Mathf.FloorToInt(center.Y - radiusY);
        var maxY = Mathf.CeilToInt(center.Y + radiusY);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var dx = (x - center.X) / radiusX;
                var dy = (y - center.Y) / radiusY;
                if ((dx * dx) + (dy * dy) <= 1.0f)
                {
                    SetPixelSafe(image, tileOffsetX + x, y, color);
                }
            }
        }
    }

    private static void DrawFilledRect(Image image, int tileOffsetX, Rect2 rect, Color color)
    {
        var minX = Mathf.FloorToInt(rect.Position.X);
        var minY = Mathf.FloorToInt(rect.Position.Y);
        var maxX = Mathf.CeilToInt(rect.End.X);
        var maxY = Mathf.CeilToInt(rect.End.Y);

        for (var y = minY; y < maxY; y++)
        {
            for (var x = minX; x < maxX; x++)
            {
                SetPixelSafe(image, tileOffsetX + x, y, color);
            }
        }
    }

    private static void DrawFilledQuad(Image image, int tileOffsetX, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        var minX = Mathf.FloorToInt(Mathf.Min(Mathf.Min(a.X, b.X), Mathf.Min(c.X, d.X)));
        var maxX = Mathf.CeilToInt(Mathf.Max(Mathf.Max(a.X, b.X), Mathf.Max(c.X, d.X)));
        var minY = Mathf.FloorToInt(Mathf.Min(Mathf.Min(a.Y, b.Y), Mathf.Min(c.Y, d.Y)));
        var maxY = Mathf.CeilToInt(Mathf.Max(Mathf.Max(a.Y, b.Y), Mathf.Max(c.Y, d.Y)));

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);
                if (PointInConvexQuad(point, a, b, c, d))
                {
                    SetPixelSafe(image, tileOffsetX + x, y, color);
                }
            }
        }
    }

    private static bool PointInConvexQuad(Vector2 point, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        return SameSide(point, a, b, c) &&
               SameSide(point, b, c, d) &&
               SameSide(point, c, d, a) &&
               SameSide(point, d, a, b);
    }

    private static bool SameSide(Vector2 point, Vector2 edgeStart, Vector2 edgeEnd, Vector2 insideReference)
    {
        var edge = edgeEnd - edgeStart;
        var pointCross = Cross(edge, point - edgeStart);
        var referenceCross = Cross(edge, insideReference - edgeStart);
        return referenceCross >= 0.0f ? pointCross >= 0.0f : pointCross <= 0.0f;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return (a.X * b.Y) - (a.Y * b.X);
    }

    private static float SampleNoise(float x, float y, int seed)
    {
        var value = Mathf.Sin((x * 0.173f) + (y * 0.287f) + (seed * 0.011f)) * 43758.5453f;
        return Mathf.Abs(value - Mathf.Floor(value));
    }

    private static void SetPixelSafe(Image image, int x, int y, Color color)
    {
        if (x < 0 || x >= image.GetWidth() || y < 0 || y >= image.GetHeight())
        {
            return;
        }

        image.SetPixel(x, y, color);
    }

    private static BattleAtlasMetrics GetAtlasMetrics(BattleTileLayerKind layerKind)
    {
        return layerKind switch
        {
            BattleTileLayerKind.Ground => new BattleAtlasMetrics(TileWidth, BaseTileHeight),
            BattleTileLayerKind.Moat => new BattleAtlasMetrics(TileWidth, BaseTileHeight),
            // object_01.png uses 128x128 tiles; tune the footprint origin against the 128x64 map cell.
            BattleTileLayerKind.Object => new BattleAtlasMetrics(TileWidth, 128, FootprintTopY: 32),
            BattleTileLayerKind.Castle => new BattleAtlasMetrics(TileWidth, 320, FootprintTopY: 128),
            BattleTileLayerKind.DeploymentOverlay => new BattleAtlasMetrics(TileWidth, BaseTileHeight),
            _ => new BattleAtlasMetrics(TileWidth, BaseTileHeight)
        };
    }

    private readonly record struct BattleAtlasMetrics(int RegionWidth, int RegionHeight, int? FootprintTopY = null)
    {
        public Vector2I GetTextureOrigin()
        {
            return FootprintTopY.HasValue
                ? new Vector2I(0, FootprintTopY.Value)
                : Vector2I.Zero;
        }

        public Vector2 GetSpriteFootPivot()
        {
            return new Vector2(RegionWidth * 0.5f, RegionHeight - (BaseTileHeight * 0.5f));
        }
    }
}
