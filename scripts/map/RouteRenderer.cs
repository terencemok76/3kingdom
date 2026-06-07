using System;
using System.Collections.Generic;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Map;

public partial class RouteRenderer : Node2D
{
    private static readonly HashSet<(int A, int B)> SeaRoutePairs =
    [
        (41, 47)
    ];

    private readonly List<RouteVisual> _routes = new();

    private readonly record struct RouteVisual(Vector2[] Points, bool IsSeaRoute);

    public void Bind(WorldState world)
    {
        _routes.Clear();

        foreach (var city in world.Cities)
        {
            var from = new Vector2(city.MapX, city.MapY);
            foreach (var connectedCityId in city.ConnectedCityIds)
            {
                var target = world.GetCity(connectedCityId);
                if (target == null || city.Id > target.Id)
                {
                    continue;
                }

                var to = new Vector2(target.MapX, target.MapY);
                var points = BuildRoutePoints(city.Id, target.Id, from, to);
                if (points.Length < 2)
                {
                    continue;
                }

                var isSeaRoute = IsSeaRoute(city.Id, target.Id);
                _routes.Add(new RouteVisual(points, isSeaRoute));
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var route in _routes)
        {
            if (route.IsSeaRoute)
            {
                DrawSeaRoute(route);
                continue;
            }

            var shadowColor = new Color("2a2016", 0.36f);
            var baseColor = new Color("725638", 0.94f);
            var topColor = new Color("d7b57d", 0.98f);
            var highlightColor = new Color("f7e5ba", 0.78f);
            const float shadowWidth = 6.6f;
            const float baseWidth = 4.9f;
            const float topWidth = 2.8f;
            const float highlightWidth = 1.15f;

            DrawPolyline(route.Points, shadowColor, shadowWidth, false);
            DrawPolyline(route.Points, baseColor, baseWidth, false);
            DrawPolyline(route.Points, topColor, topWidth, false);
            DrawPolyline(route.Points, highlightColor, highlightWidth, false);
        }
    }

    private static Vector2[] BuildRoutePoints(int fromCityId, int toCityId, Vector2 from, Vector2 to)
    {
        if (IsSeaRoute(fromCityId, toCityId))
        {
            return BuildSeaRoutePoints(fromCityId, toCityId, from, to);
        }

        if (TryBuildSpecialLandRoutePoints(fromCityId, toCityId, from, to, out var specialLandRoutePoints))
        {
            return specialLandRoutePoints;
        }

        var direction = to - from;
        var distance = direction.Length();
        if (distance <= 1.0f)
        {
            return Array.Empty<Vector2>();
        }

        var normalized = direction / distance;
        var perpendicular = new Vector2(-normalized.Y, normalized.X);
        var bendAmount = Mathf.Clamp(distance * 0.10f, 10.0f, 28.0f);
        var bendSign = GetDeterministicBendSign(fromCityId, toCityId);
        var offset = perpendicular * bendAmount * bendSign;

        var p0 = from;
        var p1 = from.Lerp(to, 0.32f) + (offset * 0.55f);
        var p2 = from.Lerp(to, 0.68f) + offset;
        var p3 = to;

        var curve = new Curve2D();
        curve.AddPoint(p0);
        curve.AddPoint(p1);
        curve.AddPoint(p2);
        curve.AddPoint(p3);

        return curve.GetBakedPoints();
    }

    private static Vector2[] BuildSeaRoutePoints(int fromCityId, int toCityId, Vector2 from, Vector2 to)
    {
        var curve = new Curve2D();
        curve.AddPoint(from);

        curve.AddPoint(from.Lerp(to, 0.30f) + new Vector2(22.0f, 18.0f));
        curve.AddPoint(from.Lerp(to, 0.72f) + new Vector2(34.0f, 8.0f));

        curve.AddPoint(to);
        return curve.GetBakedPoints();
    }

    private static bool TryBuildSpecialLandRoutePoints(int fromCityId, int toCityId, Vector2 from, Vector2 to, out Vector2[] points)
    {
        var curve = new Curve2D();
        curve.AddPoint(from);

        if (MatchesRoute(fromCityId, toCityId, 2, 3))
        {
            curve.AddPoint(from.Lerp(to, 0.20f) + new Vector2(-18.0f, -30.0f));
            curve.AddPoint(from.Lerp(to, 0.48f) + new Vector2(-10.0f, -42.0f));
            curve.AddPoint(from.Lerp(to, 0.74f) + new Vector2(-10.0f, -24.0f));
            curve.AddPoint(to);
            points = curve.GetBakedPoints();
            return true;
        }

        if (MatchesRoute(fromCityId, toCityId, 8, 17))
        {
            curve.AddPoint(from.Lerp(to, 0.24f) + new Vector2(-28.0f, 10.0f));
            curve.AddPoint(from.Lerp(to, 0.52f) + new Vector2(-30.0f, 20.0f));
            curve.AddPoint(from.Lerp(to, 0.80f) + new Vector2(-18.0f, 12.0f));
            curve.AddPoint(to);
            points = curve.GetBakedPoints();
            return true;
        }

        if (MatchesRoute(fromCityId, toCityId, 9, 11))
        {
            curve.AddPoint(from.Lerp(to, 0.28f) + new Vector2(4.0f, -12.0f));
            curve.AddPoint(from.Lerp(to, 0.56f) + new Vector2(10.0f, -20.0f));
            curve.AddPoint(from.Lerp(to, 0.82f) + new Vector2(8.0f, -10.0f));
            curve.AddPoint(to);
            points = curve.GetBakedPoints();
            return true;
        }

        points = Array.Empty<Vector2>();
        return false;
    }

    private static float GetDeterministicBendSign(int fromCityId, int toCityId)
    {
        return ((fromCityId * 31) + (toCityId * 17)) % 2 == 0 ? 1.0f : -1.0f;
    }

    private static bool IsSeaRoute(int fromCityId, int toCityId)
    {
        var pair = fromCityId < toCityId ? (fromCityId, toCityId) : (toCityId, fromCityId);
        return SeaRoutePairs.Contains(pair);
    }

    private static bool MatchesRoute(int fromCityId, int toCityId, int a, int b)
    {
        return (fromCityId == a && toCityId == b) || (fromCityId == b && toCityId == a);
    }

    private void DrawSeaRoute(RouteVisual route)
    {
        var shadowColor = new Color("233245", 0.26f);
        var wakeColor = new Color("7f8f9b", 0.80f);
        var foamColor = new Color("e8e1cb", 0.92f);
        DrawDashedPolyline(route.Points, shadowColor, 5.8f, 13.0f, 8.0f);
        DrawDashedPolyline(route.Points, wakeColor, 3.8f, 13.0f, 8.0f);
        DrawDashedPolyline(route.Points, foamColor, 1.8f, 13.0f, 8.0f);
    }

    private void DrawDashedPolyline(Vector2[] points, Color color, float width, float dashLength, float gapLength)
    {
        if (points.Length < 2)
        {
            return;
        }

        var remainingDash = dashLength;
        var remainingGap = 0.0f;

        for (var i = 0; i < points.Length - 1; i++)
        {
            var start = points[i];
            var end = points[i + 1];
            var segment = end - start;
            var segmentLength = segment.Length();
            if (segmentLength <= 0.001f)
            {
                continue;
            }

            var direction = segment / segmentLength;
            var travelled = 0.0f;
            while (travelled < segmentLength)
            {
                if (remainingGap > 0.0f)
                {
                    var gapStep = MathF.Min(remainingGap, segmentLength - travelled);
                    travelled += gapStep;
                    remainingGap -= gapStep;
                    continue;
                }

                var dashStep = MathF.Min(remainingDash, segmentLength - travelled);
                var dashStart = start + (direction * travelled);
                var dashEnd = start + (direction * (travelled + dashStep));
                DrawLine(dashStart, dashEnd, color, width, false);
                travelled += dashStep;
                remainingDash -= dashStep;

                if (remainingDash <= 0.001f)
                {
                    remainingDash = dashLength;
                    remainingGap = gapLength;
                }
            }
        }
    }
}
