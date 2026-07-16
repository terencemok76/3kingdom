using Godot;

namespace ThreeKingdom.Battle;

public partial class BattlePrototypeMoatRenderer : Node2D
{
    private const float TileHalfWidth = 64.0f;
    private const float TileHalfHeight = 32.0f;

    private BattlePrototypeMapData? _mapData;

    public void Configure(BattlePrototypeMapData? mapData)
    {
        _mapData = mapData;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_mapData == null)
        {
            return;
        }

        for (var y = 0; y < BattlePrototypeMapData.Height; y++)
        {
            for (var x = 0; x < BattlePrototypeMapData.Width; x++)
            {
                var cell = _mapData.GetCell(x, y);
                if (cell.Terrain is not (BattleTerrainType.Moat or BattleTerrainType.Bridge))
                {
                    continue;
                }

                var center = BattlePrototypeMapRenderer.GridToWorld(cell.Grid);
                DrawColoredPolygon(BuildDiamond(center), cell.Terrain == BattleTerrainType.Moat
                    ? new Color(0.10f, 0.40f, 0.58f, 0.82f)
                    : new Color(0.43f, 0.27f, 0.12f, 0.92f));

                if (cell.Terrain == BattleTerrainType.Moat)
                {
                    DrawLine(center + new Vector2(-38.0f, -4.0f), center + new Vector2(32.0f, -4.0f), new Color(0.67f, 0.88f, 0.96f, 0.72f), 2.0f);
                    DrawLine(center + new Vector2(-26.0f, 8.0f), center + new Vector2(42.0f, 8.0f), new Color(0.67f, 0.88f, 0.96f, 0.52f), 2.0f);
                }
                else
                {
                    DrawLine(center + new Vector2(-30.0f, -15.0f), center + new Vector2(30.0f, 15.0f), new Color(0.72f, 0.52f, 0.28f, 0.95f), 4.0f);
                    DrawLine(center + new Vector2(-30.0f, 15.0f), center + new Vector2(30.0f, -15.0f), new Color(0.72f, 0.52f, 0.28f, 0.95f), 4.0f);
                }
            }
        }
    }

    private static Vector2[] BuildDiamond(Vector2 center)
    {
        return new[]
        {
            center + new Vector2(0.0f, -TileHalfHeight),
            center + new Vector2(TileHalfWidth, 0.0f),
            center + new Vector2(0.0f, TileHalfHeight),
            center + new Vector2(-TileHalfWidth, 0.0f)
        };
    }
}
