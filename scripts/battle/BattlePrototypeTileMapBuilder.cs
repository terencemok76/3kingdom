using System;
using Godot;

namespace ThreeKingdom.Battle;

public enum BattlePrototypeTileLayerKind
{
    Ground,
    TerrainDetail,
    DeploymentOverlay
}

internal enum BattlePrototypeTileVisual
{
    Grass = 0,
    Road = 1,
    Courtyard = 2,
    WallWalk = 3,
    ForestGround = 4,
    ForestDetail = 5,
    AttackerZone = 6,
    DefenderZone = 7
}

public static class BattlePrototypeTileMapBuilder
{
    private const int TileWidth = 128;
    private const int TileHeight = 64;
    private const int AtlasSourceId = 0;
    private const int TileCount = 8;
    private const string AtlasTexturePath = "res://assets/battle/battle_tileset_atlas_128x64.png";

    private static TileSet? _sharedTileSet;

    public static TileSet CreateSharedTileSet()
    {
        _sharedTileSet ??= BuildTileSet();
        return _sharedTileSet;
    }

    public static void ConfigureLayer(TileMapLayer layer, BattlePrototypeMapData mapData, BattlePrototypeTileLayerKind layerKind)
    {
        layer.TileSet = CreateSharedTileSet();
        layer.RenderingQuadrantSize = 8;

        foreach (var coords in layer.GetUsedCells())
        {
            layer.EraseCell(coords);
        }

        for (var y = 0; y < BattlePrototypeMapData.Height; y++)
        {
            for (var x = 0; x < BattlePrototypeMapData.Width; x++)
            {
                var cell = mapData.GetCell(x, y);
                var visual = ResolveVisual(cell, layerKind);
                if (!visual.HasValue)
                {
                    continue;
                }

                layer.SetCell(cell.Grid, AtlasSourceId, new Vector2I((int)visual.Value, 0));
            }
        }

        layer.UpdateInternals();
    }

    private static BattlePrototypeTileVisual? ResolveVisual(BattlePrototypeCellData cell, BattlePrototypeTileLayerKind layerKind)
    {
        return layerKind switch
        {
            BattlePrototypeTileLayerKind.Ground => cell.Terrain switch
            {
                BattleTerrainType.Road => BattlePrototypeTileVisual.Road,
                BattleTerrainType.Courtyard => BattlePrototypeTileVisual.Courtyard,
                BattleTerrainType.WallWalk => BattlePrototypeTileVisual.WallWalk,
                BattleTerrainType.Forest => BattlePrototypeTileVisual.ForestGround,
                _ => BattlePrototypeTileVisual.Grass
            },
            BattlePrototypeTileLayerKind.TerrainDetail => cell.Terrain == BattleTerrainType.Forest
                ? BattlePrototypeTileVisual.ForestDetail
                : null,
            BattlePrototypeTileLayerKind.DeploymentOverlay => cell.DeploymentZone switch
            {
                BattleDeploymentZone.Attacker => BattlePrototypeTileVisual.AttackerZone,
                BattleDeploymentZone.Defender => BattlePrototypeTileVisual.DefenderZone,
                _ => null
            },
            _ => null
        };
    }

    private static TileSet BuildTileSet()
    {
        var texture = GD.Load<Texture2D>(AtlasTexturePath);
        if (texture == null)
        {
            throw new InvalidOperationException($"Battle tileset atlas not found: {AtlasTexturePath}");
        }

        var atlasSource = new TileSetAtlasSource
        {
            Texture = texture,
            TextureRegionSize = new Vector2I(TileWidth, TileHeight),
            UseTexturePadding = false
        };

        for (var tileIndex = 0; tileIndex < TileCount; tileIndex++)
        {
            atlasSource.CreateTile(new Vector2I(tileIndex, 0));
        }

        var tileSet = new TileSet
        {
            TileShape = TileSet.TileShapeEnum.Isometric,
            TileLayout = TileSet.TileLayoutEnum.DiamondDown,
            TileOffsetAxis = TileSet.TileOffsetAxisEnum.Horizontal,
            TileSize = new Vector2I(TileWidth, TileHeight)
        };

        tileSet.AddSource(atlasSource, AtlasSourceId);
        return tileSet;
    }
}
