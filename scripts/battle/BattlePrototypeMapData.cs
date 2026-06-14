using Godot;

namespace ThreeKingdom.Battle;

public enum BattleTerrainType
{
    Plain,
    Road,
    Courtyard,
    Grass,
    Forest,
    WallWalk
}

public enum BattleStructureType
{
    None,
    Wall,
    Gate,
    Tower,
    Building,
    Tree
}

public enum BattleDeploymentZone
{
    None,
    Attacker,
    Defender
}

public sealed class BattlePrototypeCellData
{
    public Vector2I Grid { get; init; }
    public BattleTerrainType Terrain { get; set; } = BattleTerrainType.Plain;
    public BattleStructureType Structure { get; set; } = BattleStructureType.None;
    public BattleDeploymentZone DeploymentZone { get; set; } = BattleDeploymentZone.None;
    public bool BlocksMovement { get; set; }
    public int HeightLevel { get; set; }
}

public sealed class BattlePrototypeMapData
{
    public const int Width = 25;
    public const int Height = 25;

    public BattlePrototypeCellData[,] Cells { get; } = new BattlePrototypeCellData[Width, Height];

    private BattlePrototypeMapData()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                Cells[x, y] = new BattlePrototypeCellData
                {
                    Grid = new Vector2I(x, y),
                    Terrain = BattleTerrainType.Grass
                };
            }
        }
    }

    public static BattlePrototypeMapData CreateSiegeAssault()
    {
        var map = new BattlePrototypeMapData();
        map.BuildSiegeAssaultLayout();
        return map;
    }

    public BattlePrototypeCellData GetCell(int x, int y) => Cells[x, y];

    private void BuildSiegeAssaultLayout()
    {
        PaintCourtyard();
        PaintApproachRoad();
        PaintWallsAndGate();
        PaintStructures();
        PaintForests();
        PaintDeploymentZones();
    }

    private void PaintCourtyard()
    {
        for (var y = 0; y <= 5; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                GetCell(x, y).Terrain = BattleTerrainType.Courtyard;
            }
        }
    }

    private void PaintApproachRoad()
    {
        var roadCells = new[]
        {
            new Vector2I(12, 24), new Vector2I(12, 23), new Vector2I(12, 22), new Vector2I(12, 21),
            new Vector2I(12, 20), new Vector2I(12, 19), new Vector2I(12, 18), new Vector2I(12, 17),
            new Vector2I(12, 16), new Vector2I(12, 15), new Vector2I(12, 14), new Vector2I(12, 13),
            new Vector2I(12, 12), new Vector2I(12, 11), new Vector2I(12, 10), new Vector2I(12, 9),
            new Vector2I(11, 9), new Vector2I(13, 9), new Vector2I(10, 10), new Vector2I(14, 10),
            new Vector2I(11, 10), new Vector2I(13, 10), new Vector2I(11, 8), new Vector2I(12, 8),
            new Vector2I(13, 8), new Vector2I(11, 7), new Vector2I(12, 7), new Vector2I(13, 7)
        };

        foreach (var cell in roadCells)
        {
            GetCell(cell.X, cell.Y).Terrain = cell.Y <= 8 ? BattleTerrainType.Courtyard : BattleTerrainType.Road;
        }
    }

    private void PaintWallsAndGate()
    {
        for (var x = 2; x <= 22; x++)
        {
            var wallCell = GetCell(x, 7);
            wallCell.Structure = x is >= 11 and <= 13 ? BattleStructureType.Gate : BattleStructureType.Wall;
            wallCell.BlocksMovement = true;
            wallCell.HeightLevel = 2;

            var wallWalkCell = GetCell(x, 6);
            wallWalkCell.Terrain = BattleTerrainType.WallWalk;
            wallWalkCell.HeightLevel = 2;
        }

        GetCell(2, 6).Structure = BattleStructureType.Tower;
        GetCell(22, 6).Structure = BattleStructureType.Tower;
        GetCell(2, 6).HeightLevel = 3;
        GetCell(22, 6).HeightLevel = 3;
    }

    private void PaintStructures()
    {
        for (var x = 4; x <= 8; x++)
        {
            for (var y = 2; y <= 4; y++)
            {
                var buildingCell = GetCell(x, y);
                buildingCell.Structure = BattleStructureType.Building;
                buildingCell.BlocksMovement = true;
            }
        }

        for (var x = 2; x <= 4; x++)
        {
            GetCell(x, 14 + (x % 2)).Structure = BattleStructureType.Tree;
            GetCell(x, 14 + (x % 2)).BlocksMovement = true;
        }

        for (var x = 18; x <= 21; x++)
        {
            GetCell(x, 16 + (x % 2)).Structure = BattleStructureType.Tree;
            GetCell(x, 16 + (x % 2)).BlocksMovement = true;
        }

        for (var x = 7; x <= 9; x++)
        {
            GetCell(x, 20 + (x % 2)).Structure = BattleStructureType.Tree;
            GetCell(x, 20 + (x % 2)).BlocksMovement = true;
        }
    }

    private void PaintForests()
    {
        var forestRects = new[]
        {
            new Rect2I(0, 11, 4, 6),
            new Rect2I(18, 13, 5, 6),
            new Rect2I(6, 18, 4, 4)
        };

        foreach (var rect in forestRects)
        {
            for (var y = rect.Position.Y; y < rect.End.Y; y++)
            {
                for (var x = rect.Position.X; x < rect.End.X; x++)
                {
                    var cell = GetCell(x, y);
                    if (cell.Structure == BattleStructureType.None && cell.Terrain == BattleTerrainType.Grass)
                    {
                        cell.Terrain = BattleTerrainType.Forest;
                    }
                }
            }
        }
    }

    private void PaintDeploymentZones()
    {
        for (var y = 2; y <= 5; y++)
        {
            for (var x = 7; x <= 17; x++)
            {
                GetCell(x, y).DeploymentZone = BattleDeploymentZone.Defender;
            }
        }

        for (var y = 17; y <= 22; y++)
        {
            for (var x = 5; x <= 19; x++)
            {
                GetCell(x, y).DeploymentZone = BattleDeploymentZone.Attacker;
            }
        }
    }
}
