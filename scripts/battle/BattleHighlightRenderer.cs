using Godot;

namespace ThreeKingdom.Battle;

internal enum BattleHighlightVisualKind
{
    Movable,
    WallTopMovable,
    MoveCanAttack,
    MoveCannotAttack,
    WallTopMoveCanAttack,
    WallTopMoveCannotAttack,
    Attackable,
    Workable,
    Selected
}

public partial class BattleHighlightRenderer : Node2D
{
    private const string OverlayAtlasPath = "res://assets/battle/overlay/overlay.png";
    private const int OverlayTileWidth = 128;
    private const int OverlayTileHeight = 64;
    private const int WallTopWaypointTileIndex = 2;

    private static readonly Texture2D? OverlayAtlasTexture = GD.Load<Texture2D>(OverlayAtlasPath);

    private BattleHighlightVisualKind _visualKind;
    private bool _hasVisual;

    internal void Configure(BattleHighlightVisualKind visualKind)
    {
        _visualKind = visualKind;
        _hasVisual = true;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_hasVisual)
        {
            return;
        }

        switch (_visualKind)
        {
            case BattleHighlightVisualKind.Movable:
                DrawDiamond(44.0f, 22.0f, new Color(0.42f, 0.78f, 0.96f, 0.24f), new Color(0.72f, 0.92f, 1.0f, 0.72f), 2.0f);
                break;
            case BattleHighlightVisualKind.WallTopMovable:
                if (!DrawOverlayAtlasTile(WallTopWaypointTileIndex))
                {
                    DrawDiamond(44.0f, 22.0f, new Color(0.98f, 0.70f, 0.22f, 0.26f), new Color(1.0f, 0.88f, 0.46f, 0.78f), 2.0f);
                }
                break;
            case BattleHighlightVisualKind.MoveCanAttack:
                DrawDiamond(44.0f, 22.0f, new Color(0.20f, 0.82f, 0.38f, 0.28f), new Color(0.56f, 1.0f, 0.68f, 0.90f), 2.0f);
                break;
            case BattleHighlightVisualKind.MoveCannotAttack:
                DrawDiamond(44.0f, 22.0f, new Color(0.98f, 0.76f, 0.16f, 0.30f), new Color(1.0f, 0.92f, 0.48f, 0.92f), 2.0f);
                break;
            case BattleHighlightVisualKind.WallTopMoveCanAttack:
                DrawWallTopMoveDiamond(new Color(0.20f, 0.82f, 0.38f, 0.20f), new Color(0.56f, 1.0f, 0.68f, 0.96f));
                break;
            case BattleHighlightVisualKind.WallTopMoveCannotAttack:
                DrawWallTopMoveDiamond(new Color(0.98f, 0.76f, 0.16f, 0.22f), new Color(1.0f, 0.92f, 0.48f, 0.98f));
                break;
            case BattleHighlightVisualKind.Attackable:
                DrawDiamond(44.0f, 22.0f, new Color(0.96f, 0.34f, 0.28f, 0.22f), new Color(1.0f, 0.70f, 0.62f, 0.78f), 2.0f);
                break;
            case BattleHighlightVisualKind.Workable:
                DrawDiamond(44.0f, 22.0f, new Color(0.34f, 0.82f, 0.44f, 0.24f), new Color(0.72f, 1.0f, 0.68f, 0.84f), 2.0f);
                break;
            case BattleHighlightVisualKind.Selected:
                DrawDiamond(54.0f, 27.0f, new Color(1.0f, 0.93f, 0.45f, 0.22f), new Color(1.0f, 0.98f, 0.72f, 0.95f), 3.0f);
                break;
        }
    }

    private void DrawDiamond(float halfWidth, float halfHeight, Color fillColor, Color borderColor, float borderWidth)
    {
        var points = new[]
        {
            new Vector2(0.0f, -halfHeight),
            new Vector2(halfWidth, 0.0f),
            new Vector2(0.0f, halfHeight),
            new Vector2(-halfWidth, 0.0f)
        };
        var colors = new[] { fillColor, fillColor, fillColor, fillColor };
        DrawPolygon(points, colors);
        var outlinePoints = new[]
        {
            points[0],
            points[1],
            points[2],
            points[3],
            points[0]
        };
        DrawPolyline(outlinePoints, borderColor, borderWidth, true);
    }

    private void DrawWallTopMoveDiamond(Color fillColor, Color borderColor)
    {
        DrawOverlayAtlasTile(WallTopWaypointTileIndex);
        DrawDiamond(44.0f, 22.0f, fillColor, borderColor, 2.5f);
    }

    private bool DrawOverlayAtlasTile(int tileIndex)
    {
        if (OverlayAtlasTexture == null)
        {
            return false;
        }

        var region = new Rect2(tileIndex * OverlayTileWidth, 0.0f, OverlayTileWidth, OverlayTileHeight);
        var destination = new Rect2(-OverlayTileWidth * 0.5f, -OverlayTileHeight * 0.5f, OverlayTileWidth, OverlayTileHeight);
        DrawTextureRectRegion(OverlayAtlasTexture, destination, region);
        return true;
    }
}
