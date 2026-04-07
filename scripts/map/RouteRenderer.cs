using System.Collections.Generic;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Map;

public partial class RouteRenderer : Node2D
{
    private readonly List<(Vector2 From, Vector2 To)> _segments = new();

    public void Bind(WorldState world, Vector2 offset)
    {
        _segments.Clear();

        foreach (var city in world.Cities)
        {
            var from = new Vector2(city.MapX, city.MapY) + offset;
            foreach (var connectedCityId in city.ConnectedCityIds)
            {
                var target = world.GetCity(connectedCityId);
                if (target == null || city.Id > target.Id)
                {
                    continue;
                }

                _segments.Add((from, new Vector2(target.MapX, target.MapY) + offset));
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var segment in _segments)
        {
            DrawLine(segment.From, segment.To, new Color("8e8065"), 3.0f);
        }
    }
}
