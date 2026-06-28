using System;
using System.Collections.Generic;
using Godot;

namespace ThreeKingdom.Battle;

public enum BattlePrototypeTileLayerKind
{
    Ground,
    Object,
    Castle,
    DeploymentOverlay
}

internal enum BattlePrototypeFloorTileVisual
{
    Grass = 0,
    Road = 1,
    Courtyard = 2,
    WallWalk = 3,
    ForestGround = 4
}

internal enum BattlePrototypeObjectTileVisual
{
    Tree = 0,
    Rock_Big = 1,
    Rock_Small = 2
}

internal enum BattlePrototypeCastleTileVisual
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

internal enum BattlePrototypeOverlayTileVisual
{
    AttackerZone = 0,
    DefenderZone = 1
}

public static class BattlePrototypeTileMapBuilder
{
    private const int TileWidth = 128;
    private const int BaseTileHeight = 64;
    private const int AtlasSourceId = 0;
    private const string FloorAtlasPath = "res://assets/battle/floor/floor.png";
    private const string ObjectAtlasPath = "res://assets/battle/object/object_01.png";
    private const string CastleAtlasPath = "res://assets/battle/wall/castle.png";
    private const string OverlayAtlasPath = "res://assets/battle/overlay/overlay.png";

    private static readonly Dictionary<BattlePrototypeTileLayerKind, TileSet> SharedTileSets = new();

    public static TileSet CreateSharedTileSet(BattlePrototypeTileLayerKind layerKind)
    {
        if (!SharedTileSets.TryGetValue(layerKind, out var tileSet))
        {
            tileSet = BuildTileSet(layerKind);
            SharedTileSets[layerKind] = tileSet;
        }

        return tileSet;
    }

    public static void ConfigureLayer(TileMapLayer layer, BattlePrototypeMapData mapData, BattlePrototypeTileLayerKind layerKind)
    {
        AssignLayerTileSet(layer, layerKind);

        foreach (var coords in layer.GetUsedCells())
        {
            layer.EraseCell(coords);
        }

        for (var y = 0; y < BattlePrototypeMapData.Height; y++)
        {
            for (var x = 0; x < BattlePrototypeMapData.Width; x++)
            {
                var cell = mapData.GetCell(x, y);
                var atlasCoords = ResolveAtlasCoords(cell, layerKind);
                if (!atlasCoords.HasValue)
                {
                    continue;
                }

                layer.SetCell(cell.Grid, AtlasSourceId, atlasCoords.Value);
            }
        }

        layer.UpdateInternals();
    }

    public static void AssignLayerTileSet(TileMapLayer layer, BattlePrototypeTileLayerKind layerKind)
    {
        layer.TileSet = CreateSharedTileSet(layerKind);
        layer.RenderingQuadrantSize = 8;
        layer.UpdateInternals();
    }

    private static Vector2I? ResolveAtlasCoords(BattlePrototypeCellData cell, BattlePrototypeTileLayerKind layerKind)
    {
        return layerKind switch
        {
            BattlePrototypeTileLayerKind.Ground => ResolveFloorVisual(cell),
            BattlePrototypeTileLayerKind.Object => ResolveObjectVisual(cell),
            BattlePrototypeTileLayerKind.Castle => ResolveCastleVisual(cell),
            BattlePrototypeTileLayerKind.DeploymentOverlay => ResolveOverlayVisual(cell),
            _ => null
        };
    }

    private static Vector2I ResolveFloorVisual(BattlePrototypeCellData cell)
    {
        var visual = cell.Terrain switch
        {
            BattleTerrainType.Road => BattlePrototypeFloorTileVisual.Road,
            BattleTerrainType.Courtyard => BattlePrototypeFloorTileVisual.Courtyard,
            BattleTerrainType.WallWalk => BattlePrototypeFloorTileVisual.WallWalk,
            BattleTerrainType.Forest => BattlePrototypeFloorTileVisual.ForestGround,
            _ => BattlePrototypeFloorTileVisual.Grass
        };
        return new Vector2I((int)visual, 0);
    }

    private static Vector2I? ResolveObjectVisual(BattlePrototypeCellData cell)
    {
        var visual = cell.Structure switch
        {
            BattleStructureType.Tree => BattlePrototypeObjectTileVisual.Tree,
            BattleStructureType.RockBig => BattlePrototypeObjectTileVisual.Rock_Big,
            BattleStructureType.RockSmall => BattlePrototypeObjectTileVisual.Rock_Small,
            _ => (BattlePrototypeObjectTileVisual?)null
        };

        return visual.HasValue ? new Vector2I((int)visual.Value, 0) : null;
    }

    private static Vector2I? ResolveCastleVisual(BattlePrototypeCellData cell)
    {
        BattlePrototypeCastleTileVisual? visual = cell.Structure switch
        {
            BattleStructureType.Gate => cell.Grid.X % 2 == 0
                ? BattlePrototypeCastleTileVisual.GateRight
                : BattlePrototypeCastleTileVisual.GateLeft,
            BattleStructureType.Wall or BattleStructureType.Tower or BattleStructureType.Building => BattlePrototypeCastleTileVisual.Wall0,
            _ => null
        };

        return visual.HasValue ? new Vector2I((int)visual.Value, 0) : null;
    }

    private static Vector2I? ResolveOverlayVisual(BattlePrototypeCellData cell)
    {
        BattlePrototypeOverlayTileVisual? visual = cell.DeploymentZone switch
        {
            BattleDeploymentZone.Attacker => BattlePrototypeOverlayTileVisual.AttackerZone,
            BattleDeploymentZone.Defender => BattlePrototypeOverlayTileVisual.DefenderZone,
            _ => null
        };

        return visual.HasValue ? new Vector2I((int)visual.Value, 0) : null;
    }

    private static TileSet BuildTileSet(BattlePrototypeTileLayerKind layerKind)
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
            var atlasCoords = new Vector2I(tileIndex, 0);
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
        return tileSet;
    }

    private static Texture2D? LoadExternalAtlasTexture(BattlePrototypeTileLayerKind layerKind, int tileCount, BattleAtlasMetrics metrics)
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

        var requiredWidth = tileCount * metrics.RegionWidth;
        if (texture.GetWidth() < requiredWidth || texture.GetHeight() < metrics.RegionHeight)
        {
            GD.PushWarning(
                $"Battle tileset atlas too small for {layerKind}: {atlasPath}. " +
                $"Expected at least {requiredWidth}x{metrics.RegionHeight}, got {texture.GetWidth()}x{texture.GetHeight()}. Using generated fallback.");
            return null;
        }

        return texture;
    }

    private static Texture2D BuildGeneratedAtlasTexture(BattlePrototypeTileLayerKind layerKind, int tileCount, BattleAtlasMetrics metrics)
    {
        var atlasImage = BuildAtlasImage(layerKind, tileCount, metrics);
        return ImageTexture.CreateFromImage(atlasImage);
    }

    private static string GetAtlasPath(BattlePrototypeTileLayerKind layerKind)
    {
        return layerKind switch
        {
            BattlePrototypeTileLayerKind.Ground => FloorAtlasPath,
            BattlePrototypeTileLayerKind.Object => ObjectAtlasPath,
            BattlePrototypeTileLayerKind.Castle => CastleAtlasPath,
            BattlePrototypeTileLayerKind.DeploymentOverlay => OverlayAtlasPath,
            _ => string.Empty
        };
    }

    private static int GetTileCount(BattlePrototypeTileLayerKind layerKind)
    {
        return layerKind switch
        {
            BattlePrototypeTileLayerKind.Ground => Enum.GetValues<BattlePrototypeFloorTileVisual>().Length,
            BattlePrototypeTileLayerKind.Object => Enum.GetValues<BattlePrototypeObjectTileVisual>().Length,
            BattlePrototypeTileLayerKind.Castle => Enum.GetValues<BattlePrototypeCastleTileVisual>().Length,
            BattlePrototypeTileLayerKind.DeploymentOverlay => Enum.GetValues<BattlePrototypeOverlayTileVisual>().Length,
            _ => 0
        };
    }

    private static Image BuildAtlasImage(BattlePrototypeTileLayerKind layerKind, int tileCount, BattleAtlasMetrics metrics)
    {
        var image = Image.CreateEmpty(tileCount * metrics.RegionWidth, metrics.RegionHeight, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);

        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            var tileOffsetX = tileIndex * metrics.RegionWidth;
            switch (layerKind)
            {
                case BattlePrototypeTileLayerKind.Ground:
                    DrawFloorTile(image, tileOffsetX, metrics, (BattlePrototypeFloorTileVisual)tileIndex);
                    break;
                case BattlePrototypeTileLayerKind.Object:
                    DrawObjectTile(image, tileOffsetX, metrics, (BattlePrototypeObjectTileVisual)tileIndex);
                    break;
                case BattlePrototypeTileLayerKind.Castle:
                    DrawCastleTile(image, tileOffsetX, metrics, (BattlePrototypeCastleTileVisual)tileIndex);
                    break;
                case BattlePrototypeTileLayerKind.DeploymentOverlay:
                    DrawOverlayTile(image, tileOffsetX, metrics, (BattlePrototypeOverlayTileVisual)tileIndex);
                    break;
            }
        }

        return image;
    }

    private static void DrawFloorTile(Image image, int tileOffsetX, BattleAtlasMetrics metrics, BattlePrototypeFloorTileVisual visual)
    {
        var fillColor = visual switch
        {
            BattlePrototypeFloorTileVisual.Grass => new Color("738c50"),
            BattlePrototypeFloorTileVisual.Road => new Color("b89263"),
            BattlePrototypeFloorTileVisual.Courtyard => new Color("c3ad78"),
            BattlePrototypeFloorTileVisual.WallWalk => new Color("99846d"),
            BattlePrototypeFloorTileVisual.ForestGround => new Color("5f7745"),
            _ => new Color("738c50")
        };

        var shadeColor = visual switch
        {
            BattlePrototypeFloorTileVisual.Grass => new Color("5f7640"),
            BattlePrototypeFloorTileVisual.Road => new Color("9f7b54"),
            BattlePrototypeFloorTileVisual.Courtyard => new Color("ad9763"),
            BattlePrototypeFloorTileVisual.WallWalk => new Color("7d6a56"),
            BattlePrototypeFloorTileVisual.ForestGround => new Color("476037"),
            _ => new Color("5f7640")
        };

        DrawDiamond(image, tileOffsetX, metrics, fillColor, new Color("3d3528", 0.55f), (localX, localY, edge) =>
        {
            var noise = SampleNoise(localX, localY, tileOffsetX);
            var color = fillColor.Lerp(shadeColor, 0.16f + (noise * 0.18f));

            if (visual == BattlePrototypeFloorTileVisual.Road)
            {
                var stripe = Mathf.Abs(localX - 64.0f) < 14.0f || Mathf.Abs(localY - 32.0f) < 8.0f;
                if (stripe)
                {
                    color = color.Lerp(new Color("d5bb8a"), 0.16f);
                }
            }
            else if (visual == BattlePrototypeFloorTileVisual.Courtyard)
            {
                var pattern = ((int)localX / 8 + (int)localY / 6) % 2 == 0;
                color = color.Lerp(pattern ? new Color("d6c18d") : new Color("b39b65"), 0.12f);
            }
            else if (visual == BattlePrototypeFloorTileVisual.WallWalk)
            {
                var masonry = ((int)localX / 10 + (int)localY / 6) % 2 == 0;
                color = color.Lerp(masonry ? new Color("b59f87") : new Color("85715b"), 0.18f);
            }
            else if (visual == BattlePrototypeFloorTileVisual.ForestGround)
            {
                color = color.Lerp(new Color("2e4927"), noise * 0.22f);
            }

            if (edge > 0.955f)
            {
                color = color.Lerp(new Color("e3d2a3"), 0.14f);
            }

            return color;
        });
    }

    private static void DrawObjectTile(Image image, int tileOffsetX, BattleAtlasMetrics metrics, BattlePrototypeObjectTileVisual visual)
    {
        var baselineOffset = metrics.RegionHeight - BaseTileHeight;
        switch (visual)
        {
            case BattlePrototypeObjectTileVisual.Tree:
                DrawFilledEllipse(image, tileOffsetX, new Vector2(66.0f, 46.0f + baselineOffset), 24.0f, 10.0f, new Color(0.0f, 0.0f, 0.0f, 0.18f));
                DrawFilledRect(image, tileOffsetX, new Rect2(60.0f, 26.0f + baselineOffset - 48.0f, 6.0f, 18.0f), new Color("65462d"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(49.0f, 28.0f + baselineOffset - 48.0f), 18.0f, 15.0f, new Color("35572e"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(70.0f, 24.0f + baselineOffset - 48.0f), 20.0f, 17.0f, new Color("436b38"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(83.0f, 33.0f + baselineOffset - 48.0f), 14.0f, 12.0f, new Color("31522a"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(60.0f, 38.0f + baselineOffset - 48.0f), 17.0f, 12.0f, new Color("4d7a40"));
                break;
            case BattlePrototypeObjectTileVisual.Rock_Big:
                DrawFilledEllipse(image, tileOffsetX, new Vector2(63.0f, 54.0f + baselineOffset - 34.0f), 27.0f, 11.0f, new Color(0.0f, 0.0f, 0.0f, 0.16f));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(49.0f, 34.0f + baselineOffset - 34.0f), 14.0f, 10.0f, new Color("b8a88f"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(68.0f, 28.0f + baselineOffset - 34.0f), 16.0f, 10.0f, new Color("c7b89f"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(82.0f, 36.0f + baselineOffset - 34.0f), 12.0f, 9.0f, new Color("a3937b"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(62.0f, 41.0f + baselineOffset - 34.0f), 26.0f, 12.0f, new Color("97866f"));
                break;
            case BattlePrototypeObjectTileVisual.Rock_Small:
                DrawFilledEllipse(image, tileOffsetX, new Vector2(63.0f, 52.0f + baselineOffset - 34.0f), 20.0f, 8.0f, new Color(0.0f, 0.0f, 0.0f, 0.13f));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(56.0f, 35.0f + baselineOffset - 34.0f), 10.0f, 8.0f, new Color("baaa92"));
                DrawFilledEllipse(image, tileOffsetX, new Vector2(69.0f, 38.0f + baselineOffset - 34.0f), 11.0f, 7.0f, new Color("97876f"));
                break;
        }
    }

    private static void DrawCastleTile(Image image, int tileOffsetX, BattleAtlasMetrics metrics, BattlePrototypeCastleTileVisual visual)
    {
        _ = metrics;
        switch (visual)
        {
            case BattlePrototypeCastleTileVisual.Wall0:
            case BattlePrototypeCastleTileVisual.Wall1:
            case BattlePrototypeCastleTileVisual.Wall2:
            case BattlePrototypeCastleTileVisual.Wall3:
            case BattlePrototypeCastleTileVisual.Wall4:
            case BattlePrototypeCastleTileVisual.Wall5:
            case BattlePrototypeCastleTileVisual.Wall8:
            case BattlePrototypeCastleTileVisual.Wall9:
                DrawWallBlock(image, tileOffsetX, new Color("90755a"), new Color("725a43"));
                break;
            case BattlePrototypeCastleTileVisual.GateLeft:
            case BattlePrototypeCastleTileVisual.GateRight:
                DrawWallBlock(image, tileOffsetX, new Color("927659"), new Color("725941"));
                DrawFilledRect(image, tileOffsetX, new Rect2(50.0f, 30.0f, 28.0f, 22.0f), new Color("573722"));
                break;
        }
    }

    private static void DrawOverlayTile(Image image, int tileOffsetX, BattleAtlasMetrics metrics, BattlePrototypeOverlayTileVisual visual)
    {
        var fillColor = visual == BattlePrototypeOverlayTileVisual.AttackerZone
            ? new Color(0.88f, 0.56f, 0.20f, 0.20f)
            : new Color(0.28f, 0.76f, 0.88f, 0.20f);
        var borderColor = visual == BattlePrototypeOverlayTileVisual.AttackerZone
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
        float halfScale = 1.0f)
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

                image.SetPixel(tileOffsetX + localX, localY, color);
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

    private static BattleAtlasMetrics GetAtlasMetrics(BattlePrototypeTileLayerKind layerKind)
    {
        return layerKind switch
        {
            BattlePrototypeTileLayerKind.Ground => new BattleAtlasMetrics(TileWidth, BaseTileHeight),
            // object_01.png uses 128x128 tiles; tune the footprint origin against the 128x64 map cell.
            BattlePrototypeTileLayerKind.Object => new BattleAtlasMetrics(TileWidth, 128, FootprintTopY: 32),
            BattlePrototypeTileLayerKind.Castle => new BattleAtlasMetrics(TileWidth, 320, FootprintTopY: 128),
            BattlePrototypeTileLayerKind.DeploymentOverlay => new BattleAtlasMetrics(TileWidth, BaseTileHeight),
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
    }
}
