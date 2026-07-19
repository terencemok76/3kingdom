using Godot;

namespace ThreeKingdom.Battle;

internal enum BattleHighlightVisualKind
{
    Movable,
    WallTopMovable,
    Attackable,
    Selected
}

public partial class BattleHighlightRenderer : Node2D
{
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
                DrawDiamond(44.0f, 22.0f, new Color(0.98f, 0.70f, 0.22f, 0.26f), new Color(1.0f, 0.88f, 0.46f, 0.78f), 2.0f);
                break;
            case BattleHighlightVisualKind.Attackable:
                DrawDiamond(44.0f, 22.0f, new Color(0.96f, 0.34f, 0.28f, 0.22f), new Color(1.0f, 0.70f, 0.62f, 0.78f), 2.0f);
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
        DrawPolyline(points, borderColor, borderWidth, true);
    }
}
