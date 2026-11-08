using System;
using Godot;

namespace ThreeKingdom.Map;

public enum MapMovementIcon
{
    Logistics,
    Attack,
    Reinforcement,
    Spy,
    Diplomacy,
    RecruitOfficer,
    ContactOfficer
}

public partial class MapMovementMarker : Node2D
{
    private static readonly Font? CaptionFont = GD.Load<FontFile>("res://assets/fonts/LXGWWenKaiTC-Regular.ttf");
    private Vector2[] _points = Array.Empty<Vector2>();
    private float[] _distances = Array.Empty<float>();
    private float _length;
    private float _elapsed;
    private float _duration;
    private Action? _completed;
    private MapMovementIcon _icon;
    private string _caption = string.Empty;

    public void Start(Vector2[] points, MapMovementIcon icon, float duration, Action completed, string caption = "")
    {
        _points = points;
        _icon = icon;
        _caption = caption;
        _duration = Math.Max(0.1f, duration);
        _completed = completed;
        _distances = new float[points.Length];
        for (var index = 1; index < points.Length; index += 1)
        {
            _length += points[index].DistanceTo(points[index - 1]);
            _distances[index] = _length;
        }
        Position = points[0];
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        var progress = Mathf.Clamp(_elapsed / _duration, 0.0f, 1.0f);
        Position = GetPointAtDistance(_length * progress);
        if (progress < 1.0f)
        {
            return;
        }

        var completed = _completed;
        _completed = null;
        completed?.Invoke();
        QueueFree();
    }

    public override void _Draw()
    {
        var color = _icon switch
        {
            MapMovementIcon.Attack => new Color("d95245"),
            MapMovementIcon.Reinforcement => new Color("63b6d8"),
            MapMovementIcon.Spy => new Color("ac88d1"),
            MapMovementIcon.Diplomacy => new Color("72b98a"),
            MapMovementIcon.RecruitOfficer => new Color("db9f68"),
            MapMovementIcon.ContactOfficer => new Color("db9f68"),
            _ => new Color("e0b95b")
        };
        DrawCircle(Vector2.Zero, 11.0f, new Color("211a12", 0.9f));
        DrawCircle(Vector2.Zero, 8.5f, color);
        if (!string.IsNullOrWhiteSpace(_caption))
        {
            var font = CaptionFont ?? ThemeDB.FallbackFont;
            const int fontSize = 15;
            var width = font.GetStringSize(_caption, HorizontalAlignment.Left, -1, fontSize).X;
            var origin = new Vector2(-width / 2, -17);
            DrawStringOutline(font, origin, _caption, HorizontalAlignment.Left, -1, fontSize, 3, new Color("211a12"));
            DrawString(font, origin, _caption, HorizontalAlignment.Left, -1, fontSize, new Color("fff4d7"));
        }
        if (_icon == MapMovementIcon.Spy)
        {
            DrawPolyline(new[] { new Vector2(-6, 0), new Vector2(0, -4), new Vector2(6, 0), new Vector2(0, 4), new Vector2(-6, 0) }, Colors.White, 1.5f);
            DrawCircle(Vector2.Zero, 1.8f, Colors.White);
            return;
        }
        if (_icon == MapMovementIcon.Diplomacy)
        {
            DrawRect(new Rect2(-5.5f, -4, 11, 8), Colors.White, false, 1.5f);
            DrawPolyline(new[] { new Vector2(-5, -3), new Vector2(0, 1), new Vector2(5, -3) }, Colors.White, 1.5f);
            return;
        }
        if (_icon is MapMovementIcon.RecruitOfficer or MapMovementIcon.ContactOfficer)
        {
            DrawCircle(new Vector2(0, -3), 2.5f, Colors.White);
            DrawArc(new Vector2(0, 6), 5, Mathf.Pi, Mathf.Tau, 12, Colors.White, 1.5f);
            return;
        }
        if (_icon == MapMovementIcon.Logistics)
        {
            DrawRect(new Rect2(-5.5f, -2.5f, 11.0f, 6.0f), new Color("fff2bf"));
            DrawCircle(new Vector2(-3.8f, 4.5f), 1.8f, new Color("332619"));
            DrawCircle(new Vector2(3.8f, 4.5f), 1.8f, new Color("332619"));
            return;
        }

        if (_icon == MapMovementIcon.Attack)
        {
            DrawLine(new Vector2(-3.0f, 6.0f), new Vector2(-3.0f, -7.0f), Colors.White, 2.0f);
            DrawPolyline(new[] { new Vector2(-2.0f, -7.0f), new Vector2(6.0f, -3.5f), new Vector2(-2.0f, 0.0f), new Vector2(-2.0f, -7.0f) }, Colors.White, 2.0f);
            return;
        }

        DrawPolyline(new[] { new Vector2(0, -7), new Vector2(6, -3), new Vector2(4, 6), new Vector2(-4, 6), new Vector2(-6, -3), new Vector2(0, -7) }, Colors.White, 2.0f);
    }

    private Vector2 GetPointAtDistance(float distance)
    {
        for (var index = 1; index < _points.Length; index += 1)
        {
            if (distance > _distances[index])
            {
                continue;
            }
            var segmentStart = _distances[index - 1];
            var segmentLength = Math.Max(0.001f, _distances[index] - segmentStart);
            return _points[index - 1].Lerp(_points[index], (distance - segmentStart) / segmentLength);
        }
        return _points[^1];
    }
}
