using System;
using System.Collections.Generic;
using Godot;

namespace ThreeKingdom.Battle;

internal static class BattleAiStrategicPathfinder
{
    internal static int FindLength(Vector2I source, Vector2I objective,
        Func<Vector2I, bool> passable, Func<Vector2I, IEnumerable<Vector2I>> neighbors)
    {
        if (!passable(source) || !passable(objective))
            return int.MaxValue;

        var distances = new Dictionary<Vector2I, int> { [source] = 0 };
        var frontier = new Queue<Vector2I>();
        frontier.Enqueue(source);
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current == objective)
                return distances[current];
            foreach (var neighbor in neighbors(current))
            {
                if (!distances.ContainsKey(neighbor) && passable(neighbor))
                {
                    distances[neighbor] = distances[current] + 1;
                    frontier.Enqueue(neighbor);
                }
            }
        }
        return int.MaxValue;
    }
}
