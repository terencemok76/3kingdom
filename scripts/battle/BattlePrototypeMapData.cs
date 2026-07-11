using Godot;
using System;

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
    Tree,
    RockBig,
    RockSmall
}

public enum BattleDeploymentZone
{
    None,
    Attacker,
    Defender
}

public sealed class BattlePrototypeCellData
{
    public const int WallMaxHealth = 1200;
    public const int GateMaxHealth = 1800;

    public Vector2I Grid { get; init; }
    public BattleTerrainType Terrain { get; set; } = BattleTerrainType.Plain;
    public BattleStructureType Structure { get; set; } = BattleStructureType.None;
    public BattleDeploymentZone DeploymentZone { get; set; } = BattleDeploymentZone.None;
    public bool BlocksMovement { get; set; }
    public bool IsGateOpen { get; set; }
    public int HeightLevel { get; set; }
    public int StructureMaxHealth { get; set; }
    public int StructureHealth { get; set; }

    public bool HasStructureHealth => StructureMaxHealth > 0;
    public bool IsBroken => HasStructureHealth && StructureHealth <= 0;
    public bool IsBlockingStructure => BlocksMovement && !IsBroken && !(Structure == BattleStructureType.Gate && IsGateOpen);
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

    public static BattlePrototypeMapData CreateFromTileMapLayers(
        TileMapLayer? groundLayer,
        TileMapLayer? objectLayer,
        TileMapLayer? castleLayer,
        TileMapLayer? overlayLayer)
    {
        var map = new BattlePrototypeMapData();
        map.ReadGroundLayer(groundLayer);
        map.ReadObjectLayer(objectLayer);
        map.ReadCastleLayer(castleLayer);
        map.ReadOverlayLayer(overlayLayer);
        map.ApplyDerivedCellRules();
        return map;
    }

    public BattlePrototypeCellData GetCell(int x, int y) => Cells[x, y];

    public void ApplyStructureDamage(Vector2I grid, int damage)
    {
        var cell = GetCell(grid.X, grid.Y);
        if (!cell.HasStructureHealth || damage <= 0)
        {
            return;
        }

        cell.StructureHealth = Mathf.Max(0, cell.StructureHealth - damage);
    }

    public void SetStructureHealth(Vector2I grid, int health)
    {
        var cell = GetCell(grid.X, grid.Y);
        if (!cell.HasStructureHealth)
        {
            return;
        }

        cell.StructureHealth = Mathf.Clamp(health, 0, cell.StructureMaxHealth);
    }

    private void ReadGroundLayer(TileMapLayer? layer)
    {
        if (layer == null)
        {
            return;
        }

        ForEachGrid(grid =>
        {
            if (layer.GetCellSourceId(grid) < 0)
            {
                return;
            }

            var atlas = layer.GetCellAtlasCoords(grid);
            GetCell(grid.X, grid.Y).Terrain = atlas.X switch
            {
                1 => BattleTerrainType.Road,
                2 => BattleTerrainType.Courtyard,
                3 => BattleTerrainType.WallWalk,
                4 => BattleTerrainType.Forest,
                _ => BattleTerrainType.Grass
            };
        });
    }

    private void ReadObjectLayer(TileMapLayer? layer)
    {
        if (layer == null)
        {
            return;
        }

        ForEachGrid(grid =>
        {
            if (layer.GetCellSourceId(grid) < 0)
            {
                return;
            }

            var atlas = layer.GetCellAtlasCoords(grid);
            var cell = GetCell(grid.X, grid.Y);
            cell.Structure = atlas.X switch
            {
                0 => BattleStructureType.Tree,
                1 => BattleStructureType.RockBig,
                2 => BattleStructureType.RockSmall,
                _ => cell.Structure
            };
        });
    }

    private void ReadCastleLayer(TileMapLayer? layer)
    {
        if (layer == null)
        {
            return;
        }

        ForEachGrid(grid =>
        {
            if (layer.GetCellSourceId(grid) < 0)
            {
                return;
            }

            var atlas = layer.GetCellAtlasCoords(grid);
            var cell = GetCell(grid.X, grid.Y);
            if (atlas.X is 6 or 7)
            {
                cell.Structure = BattleStructureType.Gate;
                return;
            }

            cell.Structure = BattleStructureType.Wall;
        });
    }

    private void ReadOverlayLayer(TileMapLayer? layer)
    {
        if (layer == null)
        {
            return;
        }

        ForEachGrid(grid =>
        {
            if (layer.GetCellSourceId(grid) < 0)
            {
                return;
            }

            var atlas = layer.GetCellAtlasCoords(grid);
            GetCell(grid.X, grid.Y).DeploymentZone = atlas.X switch
            {
                0 => BattleDeploymentZone.Attacker,
                1 => BattleDeploymentZone.Defender,
                _ => BattleDeploymentZone.None
            };
        });
    }

    private void ApplyDerivedCellRules()
    {
        ForEachGrid(grid =>
        {
            var cell = GetCell(grid.X, grid.Y);
            var isBrokenWall = cell.Structure == BattleStructureType.Wall && cell.StructureMaxHealth > 0 && cell.StructureHealth == 0;
            var isBrokenGate = cell.Structure == BattleStructureType.Gate && cell.StructureMaxHealth > 0 && cell.StructureHealth == 0;
            cell.BlocksMovement = false;
            cell.HeightLevel = cell.Terrain == BattleTerrainType.WallWalk ? 2 : 0;

            switch (cell.Structure)
            {
                case BattleStructureType.Wall:
                    cell.BlocksMovement = true;
                    cell.HeightLevel = 2;
                    cell.StructureMaxHealth = BattlePrototypeCellData.WallMaxHealth;
                    cell.StructureHealth = isBrokenWall ? 0 : BattlePrototypeCellData.WallMaxHealth;
                    break;
                case BattleStructureType.Gate:
                    cell.BlocksMovement = true;
                    cell.HeightLevel = 2;
                    cell.StructureMaxHealth = BattlePrototypeCellData.GateMaxHealth;
                    cell.StructureHealth = isBrokenGate ? 0 : BattlePrototypeCellData.GateMaxHealth;
                    break;
                case BattleStructureType.Tower:
                    cell.BlocksMovement = true;
                    cell.HeightLevel = 3;
                    break;
                case BattleStructureType.Building:
                case BattleStructureType.Tree:
                case BattleStructureType.RockBig:
                case BattleStructureType.RockSmall:
                    cell.BlocksMovement = true;
                    break;
            }

        });
    }

    private void ForEachGrid(Action<Vector2I> action)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                action(new Vector2I(x, y));
            }
        }
    }

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
            wallCell.StructureMaxHealth = wallCell.Structure == BattleStructureType.Gate
                ? BattlePrototypeCellData.GateMaxHealth
                : BattlePrototypeCellData.WallMaxHealth;
            wallCell.StructureHealth = wallCell.StructureMaxHealth;

            var innerCourtyardCell = GetCell(x, 6);
            innerCourtyardCell.Terrain = BattleTerrainType.Courtyard;
            innerCourtyardCell.HeightLevel = 0;
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
            PlaceTree(x, 14 + (x % 2));
        }

        for (var x = 18; x <= 21; x++)
        {
            PlaceTree(x, 16 + (x % 2));
        }

        for (var x = 7; x <= 9; x++)
        {
            PlaceTree(x, 20 + (x % 2));
        }

        PlaceRockBig(5, 18);
        PlaceRockBig(16, 19);
        PlaceRockSmall(10, 22);
        PlaceRockSmall(22, 15);
    }

    private void PlaceTree(int x, int y)
    {
        var cell = GetCell(x, y);
        cell.Structure = BattleStructureType.Tree;
        cell.BlocksMovement = true;
    }

    private void PlaceRockBig(int x, int y)
    {
        var cell = GetCell(x, y);
        cell.Structure = BattleStructureType.RockBig;
        cell.BlocksMovement = true;
    }

    private void PlaceRockSmall(int x, int y)
    {
        var cell = GetCell(x, y);
        cell.Structure = BattleStructureType.RockSmall;
        cell.BlocksMovement = true;
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
