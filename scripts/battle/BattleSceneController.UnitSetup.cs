using Godot;
using System;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattlePresentationSettings;
using static ThreeKingdom.Battle.BattleResourcePaths;
using static ThreeKingdom.Battle.BattleUnitTypes;
using static ThreeKingdom.Battle.BattleUnitVisualCatalog;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private void PopulateMarkers()
    {
        var isFieldBattle = ResolveScenarioDefinition().ScenarioType == BattleScenarioType.FieldBattle;
        CreateMarker("MapRoot/UnitLayer/AttackerA", ResolveUnitSpawnGrid("AttackerA", new Vector2I(10, 20)), "I", "Attacker Infantry A", CategoryUnit, BattleTeamIdentity.AttackerName, "Xiahou Yuan", TroopInfantry, 6200, new Color("ad4832"), new Color("f0d6a8"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Spearman", ResolveUnitSpawnGrid("Spearman", new Vector2I(8, 18)), "S", "Attacker Spearman", CategoryUnit, BattleTeamIdentity.AttackerName, "Cao Hong", TroopSpearman, 4200, new Color("9b5931"), new Color("f0d6a8"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/AttackerB", ResolveUnitSpawnGrid("AttackerB", new Vector2I(12, 18)), "A", "Attacker Archer B", CategoryUnit, BattleTeamIdentity.AttackerName, "Zhang He", TroopArcher, 5400, new Color("b96d2c"), new Color("f0d6a8"), moveRange: 4, attackRange: 3);
        CreateMarker("MapRoot/UnitLayer/AttackerC", ResolveUnitSpawnGrid("AttackerC", new Vector2I(14, 20)), "C", "Attacker Cavalry C", CategoryUnit, BattleTeamIdentity.AttackerName, "Cao Chun", TroopCavalry, 4800, new Color("8f3f31"), new Color("f0d6a8"), moveRange: 6, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/AttackerWorker", ResolveUnitSpawnGrid("AttackerWorker", new Vector2I(16, 20)), "W", "Attacker Worker", CategoryUnit, BattleTeamIdentity.AttackerName, "Worker", TroopWorker, 1800, new Color("715137"), new Color("f0d6a8"), moveRange: 3, attackRange: 1);
        if (!isFieldBattle)
        {
            CreateMarker("MapRoot/UnitLayer/Ram", ResolveUnitSpawnGrid("Ram", new Vector2I(12, 16)), "R", "Battering Ram", CategorySiegeEngine, BattleTeamIdentity.AttackerName, string.Empty, TroopRam, RamMaxHitPoints, new Color("7a4a20"), new Color("ead7aa"), 21.0f, moveRange: 3, attackRange: 1);
            CreateMarker("MapRoot/UnitLayer/Ladder", ResolveUnitSpawnGrid("Ladder", new Vector2I(10, 15)), "L", "Siege Ladder", CategorySiegeEngine, BattleTeamIdentity.AttackerName, string.Empty, TroopLadder, LadderMaxHitPoints, new Color("8c7b44"), new Color("ead7aa"), 21.0f, moveRange: 3, attackRange: 1);
        }
        CreateMarker("MapRoot/UnitLayer/Catapult", ResolveUnitSpawnGrid("Catapult", new Vector2I(14, 15)), "T", "Catapult", CategorySiegeEngine, BattleTeamIdentity.AttackerName, string.Empty, TroopCatapult, CatapultMaxHitPoints, new Color("6e5131"), new Color("ead7aa"), 21.0f, moveRange: 2, attackRange: 4);
        CreateMarker("MapRoot/UnitLayer/SupplyCart", ResolveUnitSpawnGrid("SupplyCart", new Vector2I(16, 19)), "糧", "Supply Cart", CategorySiegeEngine, BattleTeamIdentity.AttackerName, string.Empty, TroopSupplyCart, SupplyCartMaxHitPoints, new Color("6d5a2d"), new Color("f1df9b"), 21.0f, moveRange: 3, attackRange: 0);

        CreateMarker("MapRoot/UnitLayer/DefenderA", ResolveUnitSpawnGrid("DefenderA", new Vector2I(10, 7)), "D", "Defender Infantry A", CategoryUnit, BattleTeamIdentity.DefenderName, "Dong Zhuo", TroopInfantry, 5100, new Color("326b8d"), new Color("e0f0ff"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/DefenderB", ResolveUnitSpawnGrid("DefenderB", new Vector2I(14, 7)), "X", "Defender Crossbow B", CategoryUnit, BattleTeamIdentity.DefenderName, "Li Jue", TroopArcher, 4300, new Color("245f76"), new Color("e0f0ff"), moveRange: 4, attackRange: 3);
        CreateMarker("MapRoot/UnitLayer/DefenderC", ResolveUnitSpawnGrid("DefenderC", new Vector2I(12, 7)), "G", "Defender Commander", CategoryUnit, BattleTeamIdentity.DefenderName, "Guo Si", TroopSpearman, 3100, new Color("274e8a"), new Color("e0f0ff"), moveRange: 4, attackRange: 1);
        CreateMarker("MapRoot/UnitLayer/Worker", ResolveUnitSpawnGrid("Worker", new Vector2I(16, 5)), "W", "Defender Worker", CategoryUnit, BattleTeamIdentity.DefenderName, "Worker", TroopWorker, 1800, new Color("5f583e"), new Color("e8ddbc"), moveRange: 3, attackRange: 1);
        if (isFieldBattle)
        {
            CreateMarker("MapRoot/UnitLayer/DefenderSupplyCart", ResolveUnitSpawnGrid("DefenderSupplyCart", new Vector2I(18, 4)), "糧", "Defender Supply Cart", CategorySiegeEngine, BattleTeamIdentity.DefenderName, string.Empty, TroopSupplyCart, SupplyCartMaxHitPoints, new Color("485f72"), new Color("d8ecff"), 21.0f, moveRange: 3, attackRange: 0);
        }
    }

    private Vector2I ResolveUnitSpawnGrid(string unitKey, Vector2I fallbackGrid)
    {
        var scenarioDefinition = ResolveScenarioDefinition();
        if (scenarioDefinition.UnitSpawnGrids.TryGetValue(unitKey, out var configuredGrid))
        {
            return configuredGrid;
        }

        return fallbackGrid;
    }

    private void CreateMarker(string path, Vector2I grid, string label, string displayName, string category, string teamName, string officerName, string troopType, int troopCount, Color fillColor, Color borderColor, float radius = 19.0f, int moveRange = 0, int attackRange = 1)
    {
        var marker = GetNodeOrNull<BattlePieceMarker>(path);
        if (marker == null)
        {
            return;
        }

        var gridKey = GetDefaultGridKey(grid);
        marker.Position = GetMarkerPosition(gridKey);
        marker.Setup(label, fillColor, borderColor, radius);
        marker.SetupNamePlate(FormatMarkerName(officerName, displayName, troopType));
        marker.SetupTeamArrow(GetTeamArrowColor(teamName));
        if (category == CategoryUnit)
        {
            marker.SetupTroopSegmentBar(troopCount, 0, troopCount);
        }
        else
        {
            marker.SetupHealthBar(troopCount, troopCount);
        }
        if (category == CategoryUnit && troopType == TroopInfantry)
        {
            marker.SetupSpriteAnimationScene(GetInitialInfantryDirectionScene(teamName));
        }
        else if (category == CategoryUnit && troopType == TroopSpearman)
        {
            marker.SetupSpriteAnimationScene(SpearmanIdleSouthEastScenePath);
        }
        else if (category == CategoryUnit && troopType == TroopArcher)
        {
            marker.SetupSpriteAnimationScene(ArcherIdleSouthEastScenePath);
        }
        else if (category == CategoryUnit && troopType == TroopCavalry)
        {
            marker.SetupSpriteAnimationScene(CavalryIdleSouthEastScenePath);
        }
        else if (category == CategoryUnit && troopType == TroopWorker)
        {
            marker.SetupSpriteAnimationScene(WorkerIdleSouthEastScenePath);
        }
        else if (category == CategorySiegeEngine && troopType == TroopRam)
        {
            marker.SetupSpriteAnimationScene(CarIdleSouthEastScenePath);
        }
        else if (category == CategorySiegeEngine && troopType == TroopLadder)
        {
            marker.SetupSpriteAnimationScene(CarLadderIdleSouthEastScenePath);
        }
        else if (category == CategorySiegeEngine && troopType == TroopCatapult)
        {
            marker.SetupSpriteAnimationScene(CatapultIdleSouthEastScenePath);
        }
        else if (category == CategorySiegeEngine && troopType == TroopSupplyCart)
        {
            marker.SetupSpriteAnimationScene(SupplyCarIdleSouthEastScenePath);
        }

        RegisterOccupant(gridKey, displayName, category, label, teamName, officerName, troopType, troopCount, moveRange, attackRange, marker);
        ApplyTeamTroopDelta(category, teamName, troopCount);
        ApplyTeamSiegeUnitDelta(category, teamName, 1);
        ApplyTeamGeneralDelta(category, teamName, officerName, 1);
        RegisterBattleDepthEntry(marker, gridKey, category == CategorySiegeEngine ? BattleDepthRenderKind.SiegeEngine : BattleDepthRenderKind.Unit);
    }

    private static Color GetTeamArrowColor(string teamName)
    {
        return BattleTeamIdentity.IsAttacker(teamName)
            ? new Color(1.0f, 0.18f, 0.12f, 0.96f)
            : new Color(0.18f, 0.58f, 1.0f, 0.96f);
    }

    private Vector2 GetMarkerPosition(Vector2I grid)
    {
        return GetMarkerPosition(GetDefaultGridKey(grid));
    }

    private Vector2 GetMarkerPosition(BattleGridKey gridKey)
    {
        if (gridKey.Level == 2)
        {
            return GetWallTopAnchorPosition(gridKey);
        }

        var grid = gridKey.Grid;
        var gridCenter = _groundLayer?.MapToLocal(grid) ?? BattleMapRenderer.GridToWorld(grid);
        return gridCenter + new Vector2(0.0f, GetUnitVisualLift(gridKey));
    }

    private float GetUnitVisualLift(Vector2I grid)
    {
        return GetUnitVisualLift(GetDefaultGridKey(grid));
    }

    private float GetUnitVisualLift(BattleGridKey gridKey)
    {
        if (gridKey.Level == 2)
        {
            return WallWalkUnitVisualLift;
        }

        return DefaultUnitVisualLift;
    }

    private Vector2 GetHighlightPosition(Vector2I grid)
    {
        return GetHighlightPosition(GetDefaultGridKey(grid));
    }

    private Vector2 GetHighlightPosition(BattleGridKey gridKey)
    {
        if (gridKey.Level == 2)
        {
            return GetWallTopAnchorPosition(gridKey);
        }

        var grid = gridKey.Grid;
        var gridCenter = _groundLayer?.MapToLocal(grid) ?? BattleMapRenderer.GridToWorld(grid);
        return gridCenter;
    }

    private Vector2 GetWallTopAnchorPosition(BattleGridKey gridKey)
    {
        var grid = gridKey.Grid;
        var gridCenter = _groundLayer?.MapToLocal(grid) ?? BattleMapRenderer.GridToWorld(grid);
        return gridCenter + GetWallTopHighlightOffset() + new Vector2(0.0f, WallWalkHighlightVisualLift);
    }

    private Vector2 GetWallTopHighlightOffset()
    {
        if (IsNorthWestSceneVariant())
        {
            return NorthWestWallTopHighlightOffset;
        }

        return NorthEastWallTopHighlightOffset;
    }


}
