using Godot;
using System;
using System.Collections.Generic;

namespace ThreeKingdom.Battle;

public enum BattleTerrainType
{
    Plain,
    Road,
    Courtyard,
    Grass,
    Forest,
    WallWalk,
    Moat,
    River,
    Swamp,
    Coast,
    Bridge,
    Hill,
    Mountain,
    Farm
}

public enum BattleScenarioType
{
    SiegeAssault,
    FieldBattle,
    MoatSiegeBattle
}

public enum BattleStructureType
{
    None,
    Wall,
    Gate,
    Tower,
    Building,
    WoodenFence,
    Trap,
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

public enum BattleStructureFacing
{
    None,
    NorthEast,
    NorthWest
}

public enum BattleGateSegment
{
    None,
    Left,
    Right
}

public sealed class BattleCellData
{
    public const int GateMaxHealth = 1800;
    public const int BridgeMaxDurability = 900;
    public const int BridgeConstructionStep = BridgeMaxDurability / 2;
    public const int WoodenFenceMaxHealth = 600;

    public Vector2I Grid { get; init; }
    public BattleTerrainType Terrain { get; set; } = BattleTerrainType.Plain;
    public Vector2I GroundAtlasCoords { get; set; } = new(-1, -1);
    public BattleStructureType Structure { get; set; } = BattleStructureType.None;
    public BattleDeploymentZone DeploymentZone { get; set; } = BattleDeploymentZone.None;
    public bool BlocksMovement { get; set; }
    public bool IsGateOpen { get; set; }
    public int HeightLevel { get; set; }
    public int StructureMaxHealth { get; set; }
    public int StructureHealth { get; set; }
    public BattleStructureFacing StructureFacing { get; set; } = BattleStructureFacing.None;
    public BattleGateSegment GateSegment { get; set; } = BattleGateSegment.None;
    public int CastleSourceId { get; set; } = -1;
    public Vector2I CastleAtlasCoords { get; set; } = new(-1, -1);
    public int CastleAlternativeTile { get; set; }
    public bool HideGroundOccupantWithForeground { get; set; }
    public bool HideGroundOccupantWhenGateOpen { get; set; }
    public bool HasBridgeVisual { get; set; }
    public bool BridgeFlipHorizontally { get; set; }
    public int BridgeAtlasSourceId { get; set; } = -1;
    public Vector2I BridgeAtlasCoords { get; set; } = new(-1, -1);
    public bool WoodenFenceFlipHorizontally { get; set; }
    public Vector2I BuildingAtlasCoords { get; set; }
    public Vector2I ForestAtlasCoords { get; set; } = new(-1, -1);
    public int ForestAtlasSourceId { get; set; } = -1;
    public Vector2I SwampAtlasCoords { get; set; } = new(-1, -1);
    public Vector2I HillAtlasCoords { get; set; } = new(-1, -1);
    public Vector2I MountainAtlasCoords { get; set; } = new(-1, -1);
    public Vector2I FarmAtlasCoords { get; set; } = new(-1, -1);
    public int BridgeMaxHealth { get; set; }
    public int BridgeHealth { get; set; }

    public bool HasStructureHealth => StructureMaxHealth > 0;
    public bool IsBroken => HasStructureHealth && StructureHealth <= 0;
    public bool HasBridgeHealth => Terrain == BattleTerrainType.Bridge && BridgeMaxHealth > 0;
    public bool IsBridgeDamaged => HasBridgeHealth && BridgeHealth < BridgeMaxHealth;
    public bool ProvidesBuildingCover => Structure == BattleStructureType.Building;
    public bool IsBlockingStructure => BlocksMovement && !IsBroken && !(Structure == BattleStructureType.Gate && IsGateOpen);
}

public sealed class BattleMapData
{
    public const int Width = 25;
    public const int Height = 25;

    public BattleCellData[,] Cells { get; } = new BattleCellData[Width, Height];
    public BattleScenarioDefinition ScenarioDefinition { get; private set; } = BattleScenarioDefinition.CreateBuiltIn(BattleScenarioType.SiegeAssault);

    private BattleMapData()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                Cells[x, y] = new BattleCellData
                {
                    Grid = new Vector2I(x, y),
                    Terrain = BattleTerrainType.Grass
                };
            }
        }
    }

    public static BattleMapData CreateSiegeAssault()
    {
        return CreateSiegeAssault(BattleScenarioDefinition.CreateBuiltIn(BattleScenarioType.SiegeAssault));
    }

    public static BattleMapData CreateFieldBattle()
    {
        return CreateFieldBattle(
            BattleScenarioDefinition.CreateBuiltIn(BattleScenarioType.FieldBattle),
            fieldObjectLayer: null);
    }

    public static BattleMapData CreateMoatSiegeAssault()
    {
        return CreateMoatSiegeAssault(BattleScenarioDefinition.CreateBuiltIn(BattleScenarioType.MoatSiegeBattle));
    }

    public static BattleMapData Create(BattleScenarioType scenarioType)
    {
        return Create(BattleScenarioDefinition.CreateBuiltIn(scenarioType), fieldObjectLayer: null);
    }

    public static BattleMapData Create(BattleScenarioDefinition? scenarioDefinition, TileMapLayer? fieldObjectLayer = null)
    {
        var definition = scenarioDefinition ?? BattleScenarioDefinition.CreateBuiltIn(BattleScenarioType.SiegeAssault);
        return definition.ScenarioType switch
        {
            BattleScenarioType.FieldBattle => CreateFieldBattle(definition, fieldObjectLayer),
            BattleScenarioType.MoatSiegeBattle => CreateMoatSiegeAssault(definition),
            _ => CreateSiegeAssault(definition)
        };
    }

    private static BattleMapData CreateSiegeAssault(BattleScenarioDefinition scenarioDefinition)
    {
        var map = new BattleMapData();
        map.ApplyScenarioDefinition(scenarioDefinition);
        map.BuildSiegeAssaultLayout();
        map.ApplyScenarioStructureFacingOverrides();
        map.ApplyDerivedCellRules();
        map.ApplyScenarioForegroundOcclusionMasks();
        return map;
    }

    private static BattleMapData CreateFieldBattle(BattleScenarioDefinition scenarioDefinition, TileMapLayer? fieldObjectLayer)
    {
        var map = new BattleMapData();
        map.ApplyScenarioDefinition(scenarioDefinition);
        map.BuildFieldBattleLayout();
        map.ReadFieldBattleBuildingLayer(fieldObjectLayer);
        map.ApplyScenarioStructureFacingOverrides();
        map.ApplyDerivedCellRules();
        map.ApplyScenarioForegroundOcclusionMasks();
        return map;
    }

    private static BattleMapData CreateMoatSiegeAssault(BattleScenarioDefinition scenarioDefinition)
    {
        var map = new BattleMapData();
        map.ApplyScenarioDefinition(scenarioDefinition);
        map.BuildMoatSiegeAssaultLayout();
        map.ApplyScenarioStructureFacingOverrides();
        map.ApplyDerivedCellRules();
        map.ApplyScenarioForegroundOcclusionMasks();
        return map;
    }

    public static BattleMapData CreateFromTileMapLayers(
        TileMapLayer? groundLayer,
        TileMapLayer? moatLayer,
        TileMapLayer? objectLayer,
        TileMapLayer? castleLayer,
        TileMapLayer? overlayLayer,
        BattleScenarioDefinition? scenarioDefinition)
    {
        var map = new BattleMapData();
        map.ApplyScenarioDefinition(scenarioDefinition ?? BattleScenarioDefinition.CreateBuiltIn(BattleScenarioType.SiegeAssault));
        map.ReadGroundLayer(groundLayer);
        if (map.ScenarioDefinition.ScenarioType == BattleScenarioType.MoatSiegeBattle)
        {
            map.ReadMoatLayer(moatLayer);
        }
        map.ReadObjectLayer(objectLayer);
        map.ReadCastleLayer(castleLayer);
        map.ReadOverlayLayer(overlayLayer);
        map.ApplyScenarioStructureFacingOverrides();
        map.ApplyDerivedCellRules();
        map.ApplyDefaultForegroundOcclusionMasksFromStructures();
        return map;
    }

    private void ApplyScenarioDefinition(BattleScenarioDefinition scenarioDefinition)
    {
        ScenarioDefinition = scenarioDefinition;
    }

    private void ApplyScenarioStructureFacingOverrides()
    {
        foreach (var grid in ScenarioDefinition.NorthWestStructureGrids)
        {
            if (grid.X < 0 || grid.X >= Width || grid.Y < 0 || grid.Y >= Height)
            {
                continue;
            }

            var cell = GetCell(grid.X, grid.Y);
            if (cell.Structure is BattleStructureType.Wall or BattleStructureType.Gate or BattleStructureType.Tower)
            {
                cell.StructureFacing = BattleStructureFacing.NorthWest;
            }
        }
    }

    public static BattleMapData CreateFromTileMapLayers(
        TileMapLayer? groundLayer,
        TileMapLayer? moatLayer,
        TileMapLayer? objectLayer,
        TileMapLayer? castleLayer,
        TileMapLayer? overlayLayer)
    {
        return CreateFromTileMapLayers(groundLayer, moatLayer, objectLayer, castleLayer, overlayLayer, scenarioDefinition: null);
    }

    public BattleCellData GetCell(int x, int y) => Cells[x, y];

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

    public int ApplyWoodenFenceDamage(Vector2I grid, int damage)
    {
        var cell = GetCell(grid.X, grid.Y);
        if (cell.Structure != BattleStructureType.WoodenFence || !cell.HasStructureHealth || damage <= 0)
        {
            return 0;
        }

        var actualDamage = Mathf.Min(cell.StructureHealth, damage);
        cell.StructureHealth -= actualDamage;
        if (cell.StructureHealth > 0)
        {
            return actualDamage;
        }

        cell.Structure = BattleStructureType.None;
        cell.StructureMaxHealth = 0;
        cell.StructureHealth = 0;
        cell.BlocksMovement = false;
        return actualDamage;
    }

    public int ApplyBridgeDamage(Vector2I grid, int damage)
    {
        var cell = GetCell(grid.X, grid.Y);
        if (!cell.HasBridgeHealth || damage <= 0)
        {
            return 0;
        }

        var actualDamage = Mathf.Min(cell.BridgeHealth, damage);
        cell.BridgeHealth -= actualDamage;
        if (cell.BridgeHealth > 0)
        {
            cell.BlocksMovement = cell.BridgeHealth < cell.BridgeMaxHealth;
            return actualDamage;
        }

        cell.Terrain = BattleTerrainType.Moat;
        cell.HasBridgeVisual = false;
        cell.BridgeFlipHorizontally = false;
        cell.BridgeAtlasSourceId = -1;
        cell.BridgeAtlasCoords = new Vector2I(-1, -1);
        cell.BridgeMaxHealth = 0;
        cell.BlocksMovement = true;
        return actualDamage;
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
            var cell = GetCell(grid.X, grid.Y);
            cell.GroundAtlasCoords = atlas;
            cell.Terrain = atlas switch
            {
                { X: 1, Y: 0 } => BattleTerrainType.Road,
                { X: 2, Y: 0 } => BattleTerrainType.Courtyard,
                { X: 3, Y: 0 } => BattleTerrainType.WallWalk,
                { X: 4, Y: 0 } => BattleTerrainType.Forest,
                { X: 5, Y: 0 } => BattleTerrainType.Moat,
                { X: 6, Y: 0 } => BattleTerrainType.River,
                { X: 7, Y: 0 } => BattleTerrainType.Swamp,
                { X: 0, Y: 1 } => BattleTerrainType.Coast,
                { X: 1, Y: 1 } => BattleTerrainType.Grass,
                { X: 2, Y: 1 } => BattleTerrainType.Swamp,
                { X: 3, Y: 1 } => BattleTerrainType.Road,
                { X: 4, Y: 1 } => BattleTerrainType.Coast,
                { X: 5, Y: 1 } => BattleTerrainType.Road,
                { X: 6, Y: 1 } => BattleTerrainType.Road,
                { X: 7, Y: 1 } => BattleTerrainType.Road,
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
            var sourceId = layer.GetCellSourceId(grid);
            var cell = GetCell(grid.X, grid.Y);
            if (sourceId == 8)
            {
                cell.Terrain = BattleTerrainType.Bridge;
                cell.HasBridgeVisual = true;
                cell.BridgeFlipHorizontally = (layer.GetCellAlternativeTile(grid) & TileSetAtlasSource.TransformFlipH) != 0;
                cell.BridgeAtlasSourceId = sourceId;
                cell.BridgeAtlasCoords = atlas;
                cell.Structure = BattleStructureType.None;
                return;
            }

            if (sourceId is 2 or 6)
            {
                cell.Terrain = BattleTerrainType.Forest;
                cell.Structure = BattleStructureType.None;
                cell.ForestAtlasCoords = new Vector2I(Mathf.Clamp(atlas.X, 0, 3), Mathf.Clamp(atlas.Y, 0, 1));
                cell.ForestAtlasSourceId = sourceId;
                return;
            }

            if (sourceId == 3)
            {
                cell.Terrain = BattleTerrainType.Swamp;
                cell.Structure = BattleStructureType.None;
                cell.SwampAtlasCoords = new Vector2I(Mathf.Clamp(atlas.X, 0, 3), Mathf.Clamp(atlas.Y, 0, 1));
                return;
            }

            if (sourceId == 4)
            {
                cell.Terrain = BattleTerrainType.Hill;
                cell.Structure = BattleStructureType.None;
                cell.HillAtlasCoords = new Vector2I(Mathf.Clamp(atlas.X, 0, 3), Mathf.Clamp(atlas.Y, 0, 1));
                return;
            }

            if (sourceId == 5)
            {
                cell.Terrain = BattleTerrainType.Mountain;
                cell.Structure = BattleStructureType.None;
                cell.MountainAtlasCoords = new Vector2I(Mathf.Clamp(atlas.X, 0, 3), Mathf.Clamp(atlas.Y, 0, 1));
                return;
            }

            if (sourceId == 7)
            {
                cell.Terrain = BattleTerrainType.Farm;
                cell.Structure = BattleStructureType.None;
                cell.FarmAtlasCoords = new Vector2I(Mathf.Clamp(atlas.X, 0, 3), Mathf.Clamp(atlas.Y, 0, 2));
                return;
            }

            if (sourceId == 1)
            {
                cell.Structure = BattleStructureType.Building;
                cell.BuildingAtlasCoords = new Vector2I(Mathf.Clamp(atlas.X, 0, 2), 0);
                return;
            }

            switch (atlas.X)
            {
                case 0:
                    cell.Structure = BattleStructureType.Tree;
                    break;
                case 1:
                    cell.Structure = BattleStructureType.RockBig;
                    break;
                case 2:
                    cell.Structure = BattleStructureType.RockSmall;
                    break;
                case 3:
                    if (ScenarioDefinition.ScenarioType == BattleScenarioType.MoatSiegeBattle)
                    {
                        cell.Terrain = BattleTerrainType.Bridge;
                        cell.HasBridgeVisual = true;
                        cell.BridgeFlipHorizontally = (layer.GetCellAlternativeTile(grid) & TileSetAtlasSource.TransformFlipH) != 0;
                        cell.BridgeAtlasSourceId = sourceId;
                        cell.BridgeAtlasCoords = atlas;
                        cell.Structure = BattleStructureType.None;
                        break;
                    }

                    cell.Terrain = BattleTerrainType.Road;
                    cell.HasBridgeVisual = false;
                    cell.BridgeFlipHorizontally = false;
                    cell.BridgeAtlasSourceId = -1;
                    cell.BridgeAtlasCoords = new Vector2I(-1, -1);
                    cell.Structure = BattleStructureType.None;
                    break;
                case 4:
                    cell.Structure = BattleStructureType.WoodenFence;
                    cell.WoodenFenceFlipHorizontally = (layer.GetCellAlternativeTile(grid) & TileSetAtlasSource.TransformFlipH) != 0;
                    break;
                case 5:
                    cell.Structure = BattleStructureType.Trap;
                    break;
            }
        });
    }

    private void ReadFieldBattleBuildingLayer(TileMapLayer? layer)
    {
        if (layer == null)
        {
            return;
        }

        ForEachGrid(grid =>
        {
            if (layer.GetCellSourceId(grid) != 1)
            {
                return;
            }

            var atlas = layer.GetCellAtlasCoords(grid);
            var cell = GetCell(grid.X, grid.Y);
            cell.Structure = BattleStructureType.Building;
            cell.BuildingAtlasCoords = new Vector2I(Mathf.Clamp(atlas.X, 0, 2), 0);
        });
    }

    private void ReadMoatLayer(TileMapLayer? layer)
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

            var cell = GetCell(grid.X, grid.Y);
            cell.Terrain = BattleTerrainType.Moat;
            cell.Structure = BattleStructureType.None;
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
            var sourceId = layer.GetCellSourceId(grid);
            if (sourceId < 0)
            {
                return;
            }

            var atlas = layer.GetCellAtlasCoords(grid);
            var alternativeTile = layer.GetCellAlternativeTile(grid);
            var cell = GetCell(grid.X, grid.Y);
            cell.CastleSourceId = sourceId;
            cell.CastleAtlasCoords = atlas;
            cell.CastleAlternativeTile = alternativeTile;
            cell.StructureFacing = ResolveCastleFacing(sourceId, atlas, alternativeTile);
            if (IsGateAtlas(sourceId, atlas))
            {
                cell.Structure = BattleStructureType.Gate;
                cell.GateSegment = ResolveGateSegment(sourceId, atlas, alternativeTile);
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
            var isBrokenWoodenFence = cell.Structure == BattleStructureType.WoodenFence && cell.StructureMaxHealth > 0 && cell.StructureHealth == 0;
            cell.BlocksMovement = cell.Terrain is BattleTerrainType.Moat or BattleTerrainType.River or BattleTerrainType.Mountain;
            cell.HeightLevel = cell.Terrain == BattleTerrainType.WallWalk ? 2 : 0;
            cell.HasBridgeVisual = cell.HasBridgeVisual && cell.Terrain == BattleTerrainType.Bridge;
            cell.BridgeFlipHorizontally = cell.HasBridgeVisual && cell.BridgeFlipHorizontally;
            if (!cell.HasBridgeVisual)
            {
                cell.BridgeAtlasSourceId = -1;
                cell.BridgeAtlasCoords = new Vector2I(-1, -1);
            }
            cell.WoodenFenceFlipHorizontally = cell.Structure == BattleStructureType.WoodenFence && cell.WoodenFenceFlipHorizontally;
            if (cell.Structure != BattleStructureType.Building)
            {
                cell.BuildingAtlasCoords = Vector2I.Zero;
            }
            if (cell.Terrain != BattleTerrainType.Forest)
            {
                cell.ForestAtlasCoords = new Vector2I(-1, -1);
                cell.ForestAtlasSourceId = -1;
            }
            if (cell.Terrain != BattleTerrainType.Swamp)
            {
                cell.SwampAtlasCoords = new Vector2I(-1, -1);
            }
            if (cell.Terrain != BattleTerrainType.Hill)
            {
                cell.HillAtlasCoords = new Vector2I(-1, -1);
            }
            if (cell.Terrain != BattleTerrainType.Mountain)
            {
                cell.MountainAtlasCoords = new Vector2I(-1, -1);
            }
            if (cell.Terrain != BattleTerrainType.Farm)
            {
                cell.FarmAtlasCoords = new Vector2I(-1, -1);
            }

            if (cell.Terrain == BattleTerrainType.Bridge)
            {
                cell.BridgeMaxHealth = BattleCellData.BridgeMaxDurability;
                cell.BridgeHealth = Mathf.Clamp(cell.BridgeHealth <= 0 ? cell.BridgeMaxHealth : cell.BridgeHealth, 0, cell.BridgeMaxHealth);
                cell.BlocksMovement = cell.BridgeHealth < cell.BridgeMaxHealth;
            }
            else
            {
                cell.BridgeMaxHealth = 0;
                cell.BridgeHealth = 0;
            }
            if (cell.HasBridgeVisual && ScenarioDefinition.DefaultStructureFacing == BattleStructureFacing.NorthWest)
            {
                cell.BridgeFlipHorizontally = true;
            }

            switch (cell.Structure)
            {
                case BattleStructureType.Wall:
                    cell.BlocksMovement = true;
                    cell.HeightLevel = 2;
                    cell.StructureMaxHealth = 0;
                    cell.StructureHealth = 0;
                    break;
                case BattleStructureType.Gate:
                    cell.BlocksMovement = true;
                    cell.HeightLevel = 2;
                    cell.StructureMaxHealth = BattleCellData.GateMaxHealth;
                    cell.StructureHealth = isBrokenGate ? 0 : BattleCellData.GateMaxHealth;
                    break;
                case BattleStructureType.Tower:
                    cell.BlocksMovement = true;
                    cell.HeightLevel = 3;
                    break;
                case BattleStructureType.WoodenFence:
                    cell.BlocksMovement = !isBrokenWoodenFence;
                    cell.StructureMaxHealth = BattleCellData.WoodenFenceMaxHealth;
                    cell.StructureHealth = isBrokenWoodenFence ? 0 : BattleCellData.WoodenFenceMaxHealth;
                    break;
                case BattleStructureType.Building:
                    // Building art represents a defensible position; one battle team may occupy it.
                    cell.BlocksMovement = false;
                    break;
                case BattleStructureType.Trap:
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

    private void BuildFieldBattleLayout()
    {
        PaintFieldRoad();
        PaintFieldForests();
        PaintFieldObstacles();
        PaintDeploymentZones();
    }

    private void BuildMoatSiegeAssaultLayout()
    {
        BuildSiegeAssaultLayout();
        PaintMoatAndBridge();
    }

    private void ApplyScenarioForegroundOcclusionMasks()
    {
        ClearForegroundOcclusionMasks();

        var hasCastleFacade = false;
        ForEachGrid(grid =>
        {
            var structure = GetCell(grid.X, grid.Y).Structure;
            if (structure is BattleStructureType.Wall or BattleStructureType.Gate or BattleStructureType.Tower)
            {
                hasCastleFacade = true;
            }
        });

        if (!hasCastleFacade)
        {
            return;
        }

        ForEachGrid(grid =>
        {
            var cell = GetCell(grid.X, grid.Y);
            if (cell.Structure is BattleStructureType.Wall or BattleStructureType.Gate or BattleStructureType.Tower)
            {
                if (cell.StructureFacing == BattleStructureFacing.None)
                {
                    cell.StructureFacing = ScenarioDefinition.DefaultStructureFacing;
                }

                MarkStructureForegroundOcclusion(grid, cell.StructureFacing, ScenarioDefinition.ForegroundOcclusionDepth);
            }
        });

        MarkGateOpenForegroundPieces();
    }

    private void ApplyDefaultForegroundOcclusionMasksFromStructures()
    {
        ClearForegroundOcclusionMasks();

        ForEachGrid(grid =>
        {
            var cell = GetCell(grid.X, grid.Y);
            if (cell.Structure is not (BattleStructureType.Wall or BattleStructureType.Gate or BattleStructureType.Tower))
            {
                return;
            }

            if (cell.StructureFacing == BattleStructureFacing.None)
            {
                cell.StructureFacing = ScenarioDefinition.DefaultStructureFacing;
            }

            if (cell.Structure == BattleStructureType.Gate && cell.GateSegment == BattleGateSegment.None)
            {
                cell.GateSegment = ResolveDefaultGateSegment(grid);
            }

            MarkStructureForegroundOcclusion(grid, cell.StructureFacing, ScenarioDefinition.ForegroundOcclusionDepth);
        });

        MarkGateOpenForegroundPieces();
    }

    private void ClearForegroundOcclusionMasks()
    {
        ForEachGrid(grid =>
        {
            var cell = GetCell(grid.X, grid.Y);
            cell.HideGroundOccupantWithForeground = false;
            cell.HideGroundOccupantWhenGateOpen = false;
        });
    }

    private void MarkStructureForegroundOcclusion(Vector2I grid, BattleStructureFacing facing, int depthBehindWall)
    {
        GetCell(grid.X, grid.Y).HideGroundOccupantWithForeground = true;

        foreach (var offset in GetForegroundOcclusionOffsets(facing, depthBehindWall))
        {
            var target = grid + offset;
            if (target.X < 0 || target.X >= Width || target.Y < 0 || target.Y >= Height)
            {
                continue;
            }

            GetCell(target.X, target.Y).HideGroundOccupantWithForeground = true;
        }
    }

    private void MarkGateOpenForegroundPieces()
    {
        ForEachGrid(grid =>
        {
            var cell = GetCell(grid.X, grid.Y);
            if (cell.Structure != BattleStructureType.Gate)
            {
                return;
            }

            var gateGroup = GetConnectedGateGroup(grid);
            var targetGrid = ResolveGateForegroundGrid(gateGroup);
            if (targetGrid.HasValue)
            {
                GetCell(targetGrid.Value.X, targetGrid.Value.Y).HideGroundOccupantWhenGateOpen = true;
            }
        });
    }

    private Vector2I? ResolveGateForegroundGrid(Vector2I[] gateGroup)
    {
        if (gateGroup.Length == 0)
        {
            return null;
        }

        var targetSegment = ScenarioDefinition.OpenGateForegroundSide switch
        {
            BattleGateForegroundSide.Left => BattleGateSegment.Left,
            BattleGateForegroundSide.Right => BattleGateSegment.Right,
            _ => BattleGateSegment.None
        };

        if (targetSegment == BattleGateSegment.None)
        {
            return null;
        }

        foreach (var gateGrid in gateGroup)
        {
            if (GetCell(gateGrid.X, gateGrid.Y).GateSegment == targetSegment)
            {
                return gateGrid;
            }
        }

        return targetSegment == BattleGateSegment.Left ? gateGroup[0] : gateGroup[gateGroup.Length - 1];
    }

    private static Vector2I[] GetForegroundOcclusionOffsets(BattleStructureFacing facing, int depthBehindWall)
    {
        if (depthBehindWall <= 0)
        {
            return Array.Empty<Vector2I>();
        }

        var offsets = new Vector2I[depthBehindWall];
        for (var index = 0; index < depthBehindWall; index++)
        {
            offsets[index] = facing switch
            {
                // The NW scene's facade is aligned on the X axis, so its hidden city side is X-.
                BattleStructureFacing.NorthWest => new Vector2I(-(index + 1), 0),
                BattleStructureFacing.NorthEast => new Vector2I(0, -(index + 1)),
                _ => new Vector2I(0, -(index + 1))
            };
        }

        return offsets;
    }

    private static bool IsGateAtlas(int sourceId, Vector2I atlas)
    {
        return sourceId == 1 || atlas.X is 6 or 7;
    }

    private static BattleGateSegment ResolveGateSegment(int sourceId, Vector2I atlas, int alternativeTile)
    {
        var isFlippedHorizontally = (alternativeTile & TileSetAtlasSource.TransformFlipH) != 0;
        var baseSegment = sourceId == 1
            ? atlas.X == 1 ? BattleGateSegment.Right : BattleGateSegment.Left
            : atlas.X == 7 ? BattleGateSegment.Right : BattleGateSegment.Left;

        if (!isFlippedHorizontally)
        {
            return baseSegment;
        }

        return baseSegment == BattleGateSegment.Right
            ? BattleGateSegment.Left
            : BattleGateSegment.Right;
    }

    private BattleStructureFacing ResolveCastleFacing(int sourceId, Vector2I atlas, int alternativeTile)
    {
        if (sourceId == 1)
        {
            return (alternativeTile & TileSetAtlasSource.TransformFlipH) != 0
                ? BattleStructureFacing.NorthWest
                : ScenarioDefinition.DefaultStructureFacing;
        }

        _ = atlas;
        return (alternativeTile & TileSetAtlasSource.TransformFlipH) != 0
            ? BattleStructureFacing.NorthWest
            : BattleStructureFacing.NorthEast;
    }

    private static BattleGateSegment ResolveDefaultGateSegment(Vector2I grid)
    {
        return grid.X % 2 == 0 ? BattleGateSegment.Right : BattleGateSegment.Left;
    }

    private Vector2I[] GetConnectedGateGroup(Vector2I startGrid)
    {
        if (GetCell(startGrid.X, startGrid.Y).Structure != BattleStructureType.Gate)
        {
            return Array.Empty<Vector2I>();
        }

        var minX = startGrid.X;
        while (minX - 1 >= 0 && GetCell(minX - 1, startGrid.Y).Structure == BattleStructureType.Gate)
        {
            minX--;
        }

        var maxX = startGrid.X;
        while (maxX + 1 < Width && GetCell(maxX + 1, startGrid.Y).Structure == BattleStructureType.Gate)
        {
            maxX++;
        }

        var gates = new Vector2I[maxX - minX + 1];
        for (var index = 0; index < gates.Length; index++)
        {
            gates[index] = new Vector2I(minX + index, startGrid.Y);
        }

        return gates;
    }

    private void PaintFieldRoad()
    {
        for (var y = 0; y < Height; y++)
        {
            var x = 10 + y / 5;
            GetCell(x, y).Terrain = BattleTerrainType.Road;
            if (x + 1 < Width)
            {
                GetCell(x + 1, y).Terrain = BattleTerrainType.Road;
            }
        }
    }

    private void PaintFieldForests()
    {
        var forestRects = new[]
        {
            new Rect2I(2, 6, 5, 5),
            new Rect2I(17, 5, 5, 6),
            new Rect2I(7, 15, 4, 4),
            new Rect2I(18, 17, 4, 4)
        };

        foreach (var rect in forestRects)
        {
            for (var y = rect.Position.Y; y < rect.End.Y; y++)
            {
                for (var x = rect.Position.X; x < rect.End.X; x++)
                {
                    GetCell(x, y).Terrain = BattleTerrainType.Forest;
                }
            }
        }
    }

    private void PaintFieldObstacles()
    {
        PlaceTree(4, 8);
        PlaceTree(6, 9);
        PlaceTree(18, 7);
        PlaceTree(20, 9);
        PlaceRockBig(9, 13);
        PlaceRockBig(16, 14);
        PlaceRockSmall(12, 16);
    }

    private void PaintMoatAndBridge()
    {
        var moatRows = new[] { 10, 11 };
        foreach (var moatY in moatRows)
        {
            for (var x = 0; x < Width; x++)
            {
                var cell = GetCell(x, moatY);
                cell.Terrain = BattleTerrainType.Moat;
                cell.Structure = BattleStructureType.None;
            }
        }

        foreach (var moatY in moatRows)
        {
            for (var x = 11; x <= 12; x++)
            {
                var cell = GetCell(x, moatY);
                cell.Terrain = BattleTerrainType.Bridge;
                cell.HasBridgeVisual = true;
            }
        }

        GetCell(10, 8).Terrain = BattleTerrainType.Courtyard;
        GetCell(10, 9).Terrain = BattleTerrainType.Courtyard;
        for (var y = 12; y <= 24; y++)
        {
            GetCell(11, y).Terrain = BattleTerrainType.Road;
        }
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
        for (var x = 0; x <= 24; x++)
        {
            var wallCell = GetCell(x, 7);
            wallCell.Structure = x is >= 11 and <= 12 ? BattleStructureType.Gate : BattleStructureType.Wall;
            wallCell.StructureFacing = ScenarioDefinition.DefaultStructureFacing;
            wallCell.GateSegment = wallCell.Structure == BattleStructureType.Gate
                ? ResolveDefaultGateSegment(wallCell.Grid)
                : BattleGateSegment.None;
            wallCell.BlocksMovement = true;
            wallCell.HeightLevel = 2;
            wallCell.StructureMaxHealth = wallCell.Structure == BattleStructureType.Gate
                ? BattleCellData.GateMaxHealth
                : 0;
            wallCell.StructureHealth = wallCell.StructureMaxHealth;

            var innerCourtyardCell = GetCell(x, 6);
            innerCourtyardCell.Terrain = BattleTerrainType.Courtyard;
            innerCourtyardCell.HeightLevel = 0;
        }

        // GetCell(2, 6).Structure = BattleStructureType.Tower;
        // GetCell(22, 6).Structure = BattleStructureType.Tower;
        // GetCell(2, 6).HeightLevel = 3;
        // GetCell(22, 6).HeightLevel = 3;
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
