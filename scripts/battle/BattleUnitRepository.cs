using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ThreeKingdom.Battle;

internal sealed class BattleUnitRepository : IEnumerable<KeyValuePair<BattleGridKey, IReadOnlyList<BattleOccupantInfo>>>
{
    private readonly Dictionary<BattleGridKey, List<BattleOccupantInfo>> _byGrid = new();

    internal IEnumerable<IReadOnlyList<BattleOccupantInfo>> Values =>
        _byGrid.Values.Select(static occupants => (IReadOnlyList<BattleOccupantInfo>)occupants);

    internal IReadOnlyList<BattleOccupantInfo> this[BattleGridKey grid]
    {
        get => _byGrid[grid];
    }

    internal bool TryGetValue(BattleGridKey grid, out IReadOnlyList<BattleOccupantInfo> occupants)
    {
        if (_byGrid.TryGetValue(grid, out var mutableOccupants))
        {
            occupants = mutableOccupants;
            return true;
        }

        occupants = Array.Empty<BattleOccupantInfo>();
        return false;
    }

    internal void Clear() => _byGrid.Clear();

    public IEnumerator<KeyValuePair<BattleGridKey, IReadOnlyList<BattleOccupantInfo>>> GetEnumerator()
    {
        foreach (var (grid, occupants) in _byGrid)
        {
            yield return new KeyValuePair<BattleGridKey, IReadOnlyList<BattleOccupantInfo>>(grid, occupants);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal void Add(BattleGridKey grid, BattleOccupantInfo occupant)
    {
        if (!_byGrid.TryGetValue(grid, out var occupants))
        {
            occupants = new List<BattleOccupantInfo>();
            _byGrid[grid] = occupants;
        }

        occupants.Add(occupant);
    }

    internal bool Move(
        BattleGridKey sourceGrid,
        BattleOccupantInfo sourceOccupant,
        BattleGridKey destinationGrid,
        BattleOccupantInfo destinationOccupant)
    {
        if (!_byGrid.TryGetValue(sourceGrid, out var sourceOccupants) ||
            !sourceOccupants.Remove(sourceOccupant))
        {
            return false;
        }

        if (sourceOccupants.Count == 0)
        {
            _byGrid.Remove(sourceGrid);
        }

        Add(destinationGrid, destinationOccupant);
        return true;
    }

    internal IEnumerable<(BattleGridKey Grid, BattleOccupantInfo Occupant)> GetAtGrid(Vector2I grid)
    {
        return _byGrid
            .Where(entry => entry.Key.Grid == grid)
            .SelectMany(entry => entry.Value.Select(occupant => (entry.Key, occupant)));
    }

    internal bool TryGetCurrent(BattleGridKey grid, BattleOccupantInfo occupant, out BattleOccupantInfo current)
    {
        current = occupant;
        if (!_byGrid.TryGetValue(grid, out var occupants))
        {
            return false;
        }

        if (occupants.Contains(occupant))
        {
            return true;
        }

        if (occupant.Marker == null)
        {
            return false;
        }

        var match = occupants.FirstOrDefault(candidate => candidate.Marker == occupant.Marker);
        if (match == null)
        {
            return false;
        }

        current = match;
        return true;
    }

    internal bool Replace(BattleGridKey grid, BattleOccupantInfo oldOccupant, BattleOccupantInfo newOccupant)
    {
        if (!_byGrid.TryGetValue(grid, out var occupants))
        {
            return false;
        }

        var index = occupants.IndexOf(oldOccupant);
        if (index < 0)
        {
            return false;
        }

        occupants[index] = newOccupant;
        return true;
    }

    internal void UpdateAll(Func<BattleGridKey, BattleOccupantInfo, BattleOccupantInfo> update)
    {
        foreach (var (grid, occupants) in _byGrid)
        {
            for (var index = 0; index < occupants.Count; index++)
            {
                occupants[index] = update(grid, occupants[index]);
            }
        }
    }

    internal bool Contains(BattleGridKey grid, BattleOccupantInfo occupant)
    {
        return _byGrid.TryGetValue(grid, out var occupants) && occupants.Contains(occupant);
    }

    internal bool Remove(BattleGridKey grid, BattleOccupantInfo occupant)
    {
        if (!_byGrid.TryGetValue(grid, out var occupants) || !occupants.Remove(occupant))
        {
            return false;
        }

        if (occupants.Count == 0)
        {
            _byGrid.Remove(grid);
        }

        return true;
    }
}
