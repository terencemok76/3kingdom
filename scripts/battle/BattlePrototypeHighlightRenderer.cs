using Godot;
using System.Collections.Generic;

namespace ThreeKingdom.Battle;

public partial class BattlePrototypeHighlightRenderer : Node2D
{
    private readonly List<Vector2> _movableCenters = new();
    private readonly List<Vector2> _attackCenters = new();
    private Vector2? _selectedCenter;

    public void SetHighlights(Vector2? selectedCenter, IEnumerable<Vector2> movableCenters, IEnumerable<Vector2> attackCenters)
    {
        _selectedCenter = selectedCenter;
        _movableCenters.Clear();
        _movableCenters.AddRange(movableCenters);
        _attackCenters.Clear();
        _attackCenters.AddRange(attackCenters);
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var center in _movableCenters)
        {
            DrawDiamond(center, 44.0f, 22.0f, new Color(0.42f, 0.78f, 0.96f, 0.24f), new Color(0.72f, 0.92f, 1.0f, 0.72f), 2.0f);
        }

        foreach (var center in _attackCenters)
        {
            DrawDiamond(center, 44.0f, 22.0f, new Color(0.96f, 0.34f, 0.28f, 0.22f), new Color(1.0f, 0.70f, 0.62f, 0.78f), 2.0f);
        }

        if (_selectedCenter.HasValue)
        {
            DrawDiamond(_selectedCenter.Value, 54.0f, 27.0f, new Color(1.0f, 0.93f, 0.45f, 0.22f), new Color(1.0f, 0.98f, 0.72f, 0.95f), 3.0f);
        }
    }

    private void DrawDiamond(Vector2 center, float halfWidth, float halfHeight, Color fillColor, Color borderColor, float borderWidth)
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
        DrawPolyline(points, borderColor, borderWidth, true);
    }
}
