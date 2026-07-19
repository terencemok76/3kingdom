using Godot;

namespace ThreeKingdom.Battle;

public enum BattleRenderLayer
{
    Ground,
    Terrain,
    Structure,
    Overlay
}

public partial class BattleMapRenderer : Node2D
{
    private const float TileWidth = 128.0f;
    private const float TileHeight = 64.0f;

    private BattleMapData? _mapData;
    private BattleRenderLayer _renderLayer;

    public void Configure(BattleMapData mapData, BattleRenderLayer renderLayer)
    {
        _mapData = mapData;
        _renderLayer = renderLayer;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_mapData == null)
        {
            return;
        }

        for (var y = 0; y < BattleMapData.Height; y++)
        {
            for (var x = 0; x < BattleMapData.Width; x++)
            {
                var cell = _mapData.GetCell(x, y);
                var center = GridToWorld(cell.Grid);
                switch (_renderLayer)
                {
                    case BattleRenderLayer.Ground:
                        DrawGroundCell(center, cell);
                        break;
                    case BattleRenderLayer.Terrain:
                        DrawTerrainOverlay(center, cell);
                        break;
                    case BattleRenderLayer.Structure:
                        DrawStructure(center, cell);
                        break;
                    case BattleRenderLayer.Overlay:
                        DrawOverlay(center, cell);
                        break;
                }
            }
        }
    }

    public static Vector2 GridToWorld(Vector2I grid)
    {
        return new Vector2(
            (grid.X - grid.Y) * (TileWidth * 0.5f),
            (grid.X + grid.Y) * (TileHeight * 0.5f));
    }

    public static Vector2I WorldToGrid(Vector2 world)
    {
        var halfWidth = TileWidth * 0.5f;
        var halfHeight = TileHeight * 0.5f;
        var gridX = (world.X / halfWidth + world.Y / halfHeight) * 0.5f;
        var gridY = (world.Y / halfHeight - world.X / halfWidth) * 0.5f;
        return new Vector2I(Mathf.RoundToInt(gridX), Mathf.RoundToInt(gridY));
    }

    private void DrawGroundCell(Vector2 center, BattleCellData cell)
    {
        var color = cell.Terrain switch
        {
            BattleTerrainType.Courtyard => new Color("bfa66d"),
            BattleTerrainType.Road => new Color("a9865e"),
            BattleTerrainType.Forest => new Color("537143"),
            BattleTerrainType.WallWalk => new Color("918068"),
            BattleTerrainType.Moat => new Color("286694"),
            BattleTerrainType.Bridge => new Color("725134"),
            _ => new Color("6f8650")
        };

        DrawDiamond(center, TileWidth * 0.5f, TileHeight * 0.5f, color, new Color("3d3a2d", 0.35f));
    }

    private void DrawTerrainOverlay(Vector2 center, BattleCellData cell)
    {
        if (cell.Terrain == BattleTerrainType.Forest)
        {
            DrawCircle(center + new Vector2(-16.0f, -14.0f), 18.0f, new Color("31502a", 0.85f));
            DrawCircle(center + new Vector2(14.0f, -15.0f), 19.0f, new Color("3d5e34", 0.88f));
            DrawCircle(center + new Vector2(0.0f, -4.0f), 20.0f, new Color("2d4927", 0.86f));
        }
    }

    private void DrawStructure(Vector2 center, BattleCellData cell)
    {
        switch (cell.Structure)
        {
            case BattleStructureType.Wall:
                if (cell.IsBroken)
                {
                    DrawBrokenWall(center);
                    break;
                }

                DrawWallBlock(center, new Color("8d7458"), new Color("705940"));
                break;
            case BattleStructureType.Gate:
                if (cell.IsBroken)
                {
                    DrawBrokenGate(center);
                    break;
                }

                DrawWallBlock(center, new Color("8f7252"), new Color("6f553c"));
                DrawRect(new Rect2(center + new Vector2(-18.0f, -4.0f), new Vector2(36.0f, 36.0f)), new Color("5a3822"));
                break;
            case BattleStructureType.Tower:
                DrawWallBlock(center, new Color("9b8366"), new Color("776049"));
                DrawRect(new Rect2(center + new Vector2(-22.0f, -38.0f), new Vector2(44.0f, 32.0f)), new Color("6e5038"));
                break;
            case BattleStructureType.Building:
                DrawBuilding(center);
                break;
            case BattleStructureType.Tree:
                DrawCircle(center + new Vector2(0.0f, -22.0f), 20.0f, new Color("34572d"));
                DrawCircle(center + new Vector2(-10.0f, -15.0f), 15.0f, new Color("40643a"));
                break;
        }
    }

    private void DrawOverlay(Vector2 center, BattleCellData cell)
    {
        var color = cell.DeploymentZone switch
        {
            BattleDeploymentZone.Attacker => new Color(0.84f, 0.56f, 0.18f, 0.17f),
            BattleDeploymentZone.Defender => new Color(0.26f, 0.72f, 0.82f, 0.16f),
            _ => Colors.Transparent
        };

        if (color.A > 0.0f)
        {
            DrawDiamond(center, TileWidth * 0.44f, TileHeight * 0.44f, color, Colors.Transparent);
        }
    }

    private void DrawDiamond(Vector2 center, float halfWidth, float halfHeight, Color fillColor, Color borderColor)
    {
        var points = new[]
        {
            center + new Vector2(0.0f, -halfHeight),
            center + new Vector2(halfWidth, 0.0f),
            center + new Vector2(0.0f, halfHeight),
            center + new Vector2(-halfWidth, 0.0f)
        };
        var colors = new[] { fillColor, fillColor, fillColor, fillColor };
        DrawPolygon(points, colors);

        if (borderColor.A > 0.0f)
        {
            DrawPolyline(points, borderColor, 1.0f, true);
        }
    }

    private void DrawWallBlock(Vector2 center, Color topColor, Color sideColor)
    {
        var top = new[]
        {
            center + new Vector2(0.0f, -38.0f),
            center + new Vector2(64.0f, -8.0f),
            center + new Vector2(0.0f, 22.0f),
            center + new Vector2(-64.0f, -8.0f)
        };
        var topColors = new[] { topColor, topColor, topColor, topColor };
        DrawPolygon(top, topColors);

        var leftFace = new[]
        {
            top[3],
            top[2],
            top[2] + new Vector2(0.0f, 30.0f),
            top[3] + new Vector2(0.0f, 30.0f)
        };
        var rightFace = new[]
        {
            top[1],
            top[2],
            top[2] + new Vector2(0.0f, 30.0f),
            top[1] + new Vector2(0.0f, 30.0f)
        };
        var sideColors = new[] { sideColor, sideColor, sideColor, sideColor };
        DrawPolygon(leftFace, sideColors);
        DrawPolygon(rightFace, sideColors);
    }

    private void DrawBrokenWall(Vector2 center)
    {
        DrawDiamond(center + new Vector2(0.0f, 6.0f), 44.0f, 20.0f, new Color("7a6248", 0.85f), new Color("4d3a29", 0.5f));
        DrawRect(new Rect2(center + new Vector2(-28.0f, -6.0f), new Vector2(16.0f, 10.0f)), new Color("6b523b"));
        DrawRect(new Rect2(center + new Vector2(-6.0f, -10.0f), new Vector2(18.0f, 12.0f)), new Color("755c43"));
        DrawRect(new Rect2(center + new Vector2(18.0f, -4.0f), new Vector2(12.0f, 8.0f)), new Color("6e5640"));
    }

    private void DrawBrokenGate(Vector2 center)
    {
        DrawBrokenWall(center);
        DrawRect(new Rect2(center + new Vector2(-14.0f, -2.0f), new Vector2(10.0f, 24.0f)), new Color("5a3822", 0.7f));
        DrawRect(new Rect2(center + new Vector2(2.0f, 4.0f), new Vector2(12.0f, 16.0f)), new Color("5a3822", 0.55f));
    }

    private void DrawBuilding(Vector2 center)
    {
        DrawDiamond(center + new Vector2(0.0f, 4.0f), 46.0f, 23.0f, new Color("7a6247"), Colors.Transparent);
        DrawPolygon(
            new[]
            {
                center + new Vector2(0.0f, -38.0f),
                center + new Vector2(42.0f, -12.0f),
                center + new Vector2(0.0f, 14.0f),
                center + new Vector2(-42.0f, -12.0f)
            },
            new[] { new Color("3f4555"), new Color("3f4555"), new Color("3f4555"), new Color("3f4555") });
    }
}
