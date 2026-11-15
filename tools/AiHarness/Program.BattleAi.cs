using System.IO;
using System.Reflection;
using ThreeKingdom.Battle;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using ThreeKingdom.Map;
using ThreeKingdom.UI;

namespace AiHarness;

internal static partial class Program
{
    private static void RunBattleAiEngineeringPlannerTest()
    {
        var type = typeof(BattleSceneController).Assembly.GetType("ThreeKingdom.Battle.BattleAiEngineeringPlanner")!;
        (int Score, string Reason) Bridge(int hp, int max, int repair, bool heavy, int allies) =>
            InvokePrivateStatic<(int, string)>(type, "BridgeRepair", hp, max, repair, heavy, allies);
        int Gate(int hp, int max, int repair, int threat, bool worker) =>
            InvokePrivateStatic<int>(type, "GateRepairScore", hp, max, repair, threat, worker);
        var critical = Bridge(20, 100, 10, true, 0);
        var threshold = Bridge(40, 100, 20, true, 2);
        var normal = Bridge(60, 100, 10, false, 0);
        Assert(critical.Score == 1020 && critical.Reason == "prevent critically damaged bridge from collapsing" &&
               normal.Score == 420 && normal.Reason == "maintain damaged bridge route",
            "Bridge repair values critical and ordinary damage", "critical / ordinary");
        Assert(threshold.Score == 1140 && threshold.Reason == "restore bridge above heavy-damage threshold" &&
               Bridge(40, 100, 10, true, 0).Reason == "maintain damaged bridge route",
            "Bridge threshold bonus requires repair above half health", "crossing / exactly half");
        Assert(Bridge(60, 100, 10, false, 8).Score == Bridge(60, 100, 10, false, 3).Score,
            "Bridge repair friendly support bonus is capped", "three nearby units");
        Assert(Gate(50, 100, 20, 300, true) == 2690 && Gate(51, 100, 20, 300, false) == 1540 &&
               Gate(50, 100, 20, 0, false) < Gate(50, 100, 20, 300, false),
            "Gate repair prioritizes damage, workers and threat", "half health / worker / threat");

        var pathfinder = typeof(BattleSceneController).Assembly.GetType("ThreeKingdom.Battle.BattleAiStrategicPathfinder")!;
        var start = new Godot.Vector2I(0, 1);
        var end = new Godot.Vector2I(2, 1);
        var bridge = new Godot.Vector2I(1, 1);
        Func<Godot.Vector2I, IEnumerable<Godot.Vector2I>> neighbors = point => new[]
        {
            new Godot.Vector2I(point.X - 1, point.Y), new Godot.Vector2I(point.X + 1, point.Y),
            new Godot.Vector2I(point.X, point.Y - 1), new Godot.Vector2I(point.X, point.Y + 1)
        }.Where(cell => cell.X >= 0 && cell.X < 3 && cell.Y >= 0 && cell.Y < 3);
        int Path(Func<Godot.Vector2I, bool> passable) =>
            InvokePrivateStatic<int>(pathfinder, "FindLength", start, end, passable, neighbors);
        var before = Path(point => point.X != 1);
        var after = Path(point => point.X != 1 || point == bridge);
        var improvement = InvokePrivateStatic<int>(type, "RouteImprovement", before, after);
        bool Build(int oldLength, int newLength, int congestion) =>
            InvokePrivateStatic<bool>(type, "ShouldBuildBridge", oldLength, newLength, congestion);
        bool Blocks(int oldLength, int newLength) =>
            InvokePrivateStatic<bool>(type, "BlocksFriendlyRoute", oldLength, newLength);
        Assert(before == int.MaxValue && after == 2 && improvement == 6 && Build(before, after, 0),
            "Strategic bridge opens a previously unreachable route", "3x3 map / virtual bridge");
        Assert(!Build(8, int.MaxValue, 9) && !Build(8, 6, 0) && Build(8, 8, 2) && !Build(8, 9, 2),
            "Bridge candidate requires a reachable improvement or useful congestion relief", "unreachable / threshold / congestion");
        Assert(Blocks(after, int.MaxValue) && Blocks(4, 7) && !Blocks(4, 6) && !Blocks(int.MaxValue, int.MaxValue),
            "Fence planning rejects blocking or excessive detours for friendly routes", "blocked / detour / unreachable already");
        Assert(InvokePrivateStatic<int>(type, "RouteObstruction", after, int.MaxValue) == 6 &&
               InvokePrivateStatic<int>(type, "RouteImprovement", int.MaxValue, after) == 6 &&
               InvokePrivateStatic<int>(type, "RouteImprovement", 8, 5) == 3,
            "Fence route impact values obstruction and reopening", "blocked / reopened / shorter");
        var remove = InvokePrivateStatic<int>(type, "FenceRemovalScore", 3, 1, 5);
        var removeFar = InvokePrivateStatic<int>(type, "FenceRemovalScore", 3, 2, 5);
        var fence = InvokePrivateStatic<int>(type, "FenceConstructionScore", 3, true, 2, 60, 1, 5);
        var fenceWeaker = InvokePrivateStatic<int>(type, "FenceConstructionScore", 1, true, 2, 60, 1, 5);
        var bridgeScore = InvokePrivateStatic<int>(type, "BridgeConstructionScore", 3, 0, 1, 1, 60, 20);
        var bridgeFar = InvokePrivateStatic<int>(type, "BridgeConstructionScore", 3, 0, 1, 2, 60, 20);
        Assert(remove > removeFar && fence > fenceWeaker && bridgeScore > bridgeFar,
            "Engineering scoring preserves route impact and approach costs", "fence removal / construction / bridge");
    }

    private static void RunBattlePeriodSupplyTest()
    {
        const int activeTroops = 4200;
        var gold = 1000;
        var food = 1000;
        var goldRemainder = 0;
        var foodRemainder = 0;
        var totalGoldNeed = 0;
        var totalFoodNeed = 0;
        for (var period = 0; period < BattleBalanceSettings.BattleTimePeriodsPerDay; period++)
        {
            var result = BattleSupplySystem.ResolvePeriodUpkeep(
                gold,
                food,
                activeTroops,
                goldRemainder,
                foodRemainder);
            gold = result.Gold;
            food = result.Food;
            goldRemainder = result.GoldRemainder;
            foodRemainder = result.FoodRemainder;
            totalGoldNeed += result.GoldNeed;
            totalFoodNeed += result.FoodNeed;
        }

        var expectedGoldNeed = BattleSupplySystem.CalculateScaledResourceNeed(
            activeTroops,
            BattleBalanceSettings.DailyGoldPer100ActiveTroops);
        var expectedFoodNeed = BattleSupplySystem.CalculateScaledResourceNeed(
            activeTroops,
            BattleBalanceSettings.DailyFoodPer100ActiveTroops);
        Assert(
            totalGoldNeed == expectedGoldNeed && totalFoodNeed == expectedFoodNeed &&
            goldRemainder == 0 && foodRemainder == 0,
            "Battle four-period upkeep equals one day",
            $"gold={totalGoldNeed}/{expectedGoldNeed}, food={totalFoodNeed}/{expectedFoodNeed}, remainder={goldRemainder}/{foodRemainder}");
    }

    private static void RunBattleEnvironmentForecastTest()
    {
        const uint seed = 0x1234ABCDu;
        var first = BattleEnvironmentSystem.CreateForecast(
            seed,
            7,
            BattleTimeOfDay.Afternoon,
            BattleWeatherType.Cloudy,
            BattleWindDirection.NorthEast,
            BattleWindPower.Breeze);
        var repeated = BattleEnvironmentSystem.CreateForecast(
            seed,
            7,
            BattleTimeOfDay.Afternoon,
            BattleWeatherType.Cloudy,
            BattleWindDirection.NorthEast,
            BattleWindPower.Breeze);
        var nextPowerDistance = Math.Abs((int)first.WindPower - (int)BattleWindPower.Breeze);
        var directionIsAdjacentOrSame = first.WindDirection is
            BattleWindDirection.NorthEast or
            BattleWindDirection.NorthWest or
            BattleWindDirection.SouthEast;
        Assert(
            first == repeated &&
            first.TimeOfDay == BattleTimeOfDay.Night &&
            first.Weather == BattleWeatherType.Cloudy &&
            !first.StartsNewDay &&
            directionIsAdjacentOrSame &&
            nextPowerDistance <= 1,
            "Battle environment forecast is deterministic and bounded",
            $"forecast={first}");
    }

    private static void RunBattleAiSupplyPlannerTest()
    {
        var type = typeof(BattleSceneController).Assembly.GetType("ThreeKingdom.Battle.BattleAiSupplyPlanner")!;
        bool Needs(bool self, bool siege, int hp, int maxHp, int wounded, int? morale, int? ammo, int? maxAmmo) =>
            InvokePrivateStatic<bool>(type, "NeedsSupport", self, siege, hp, maxHp, wounded, morale!, ammo!, maxAmmo!);
        int Weapon(int ammo, int maxAmmo, int attack, bool catapult, int intelligence) =>
            InvokePrivateStatic<int>(type, "WeaponScore", ammo, maxAmmo, attack, catapult, intelligence);
        Assert(!Needs(false, false, 100, 100, 0, null, null, null) &&
               Needs(false, false, 100, 100, 1, null, null, null) &&
               Needs(false, false, 100, 100, 0, 1, null, null) &&
               Needs(false, true, 99, 100, 0, null, null, null) &&
               Needs(false, false, 100, 100, 0, null, 0, 10),
            "Supply support detects wounded, morale, siege damage and missing ammo", "including nullable attributes");
        Assert(!Needs(true, false, 1, 100, 10, 1, 0, 10) &&
               Needs(true, true, 1, 100, 0, null, null, null),
            "Supply cart self-support remains limited to damaged siege engines", "self / other");
        Assert(Weapon(0, 10, 100, true, 50) > Weapon(0, 10, 100, false, 50) &&
               Weapon(0, 10, 100, false, 50) > Weapon(5, 10, 100, false, 50) &&
               Weapon(10, 10, 100, true, 50) == 0 && Weapon(0, 0, 100, true, 50) == 0,
            "Depleted catapults receive ammo priority", "empty / partial / full / no capacity");
        var morale = InvokePrivateStatic<int>(type, "MoraleScore", 80, 1f);
        var wounded = InvokePrivateStatic<int>(type, "RecoveryScore", 10, 1f);
        var repair = InvokePrivateStatic<int>(type, "RepairScore", 10, 1f);
        Assert(morale == 200 && wounded == 20 && repair == 20 &&
               InvokePrivateStatic<int>(type, "MoraleScore", 120, 1f) == 0 &&
               InvokePrivateStatic<int>(type, "RecoveryScore", 10, 1.5f) == 30,
            "Recovery and morale scoring preserve support multiplier", "morale / wounded / repair");
        var positions = new (int, int, int)[] { (200, 300, 0), (600, 50, 2), (0, 0, 1) };
        var plans = InvokePrivateStatic<List<(int Index, bool Weapon, int Score)>>(type, "BuildPlans", (object)positions);
        Assert(plans.SequenceEqual(new[] { (0, false, 200), (0, true, 300), (1, false, 400), (1, true, -150) }),
            "Direct and reachable move-then-supply plans preserve order and distance cost", "including positive raw score with negative net score");
        var approaches = new (int, int, int)[] { (20, 3, 1), (20, 2, 3), (20, 2, 2), (20, 2, 2), (19, 1, 1) };
        Assert(InvokePrivateStatic<int>(type, "SelectApproachIndex", (object)approaches) == 2 &&
               InvokePrivateStatic<int>(type, "SelectApproachIndex", (object)Array.Empty<(int, int, int)>()) == -1,
            "Reachable supply routes use score, energy, steps and stable ties", "no reachable route returns none");
    }

    private static void RunBattleAiSiegePlannerTest()
    {
        var type = typeof(BattleSceneController).Assembly.GetType("ThreeKingdom.Battle.BattleAiSiegePlanner")!;
        bool Needs(bool allied, bool passable) => InvokePrivateStatic<bool>(type, "NeedsAlliedBreach", allied, passable);
        bool Attack(bool ram, bool unit, bool outside, bool needs, int turns, int damage, int repair) =>
            InvokePrivateStatic<bool>(type, "CanAttackGate", ram, unit, outside, needs, turns, damage, repair);
        bool Move(bool ram, bool unit, bool outside, bool needs, bool canAttack, int damage) =>
            InvokePrivateStatic<bool>(type, "CanApproachGate", ram, unit, outside, needs, canAttack, damage);
        Assert(Needs(true, false) && !Needs(true, true) && !Needs(false, false),
            "Allied breach applies only during allied turns with no open or broken gate", "closed / open or broken / other turn");
        Assert(Attack(false, true, true, true, 8, 100, 100) && !Attack(false, true, false, true, 8, 100, 100),
            "Allied units outside attack sealed gates even against repairs", "outside / inside");
        Assert(!Attack(false, true, true, false, 3, 100, 0) && Attack(false, true, true, false, 2, 100, 0) &&
               !Attack(false, true, true, false, 2, 100, 100) && Attack(false, true, true, false, 1, 100, 100),
            "Ordinary gate attacks preserve finishing and repair rules", "long / finish / repair / final blow");
        Assert(Attack(true, false, true, false, 4, 100, 0) && !Attack(true, false, true, false, 4, 100, 100),
            "Ram attacks preserve repair stalemate rule", "unrepaired / repaired");
        Assert(Move(true, false, false, false, false, 0) && Move(false, true, true, true, true, 10) &&
               !Move(false, true, false, true, true, 10) && !Move(false, true, true, true, false, 10) &&
               !Move(false, true, true, true, true, 0) && !Move(false, true, true, false, true, 10),
            "Only eligible allies and rams approach gates", "position / attack / damage / gate state");
        var attackCandidates = new (int, int, int, int)[] { (10, 5, 2, 2), (10, 6, 3, 1), (10, 6, 1, 3), (10, 6, 1, 2), (10, 6, 1, 2) };
        var moveCandidates = new (int, int, int, int, int)[] { (20, 3, 1, 1, 1), (20, 2, 3, 1, 1), (20, 2, 2, 2, 1), (20, 2, 2, 1, 2), (20, 2, 2, 1, 1), (20, 2, 2, 1, 1) };
        Assert(InvokePrivateStatic<int>(type, "SelectAttackIndex", (object)attackCandidates) == 3 &&
               InvokePrivateStatic<int>(type, "SelectAttackIndex", (object)Array.Empty<(int, int, int, int)>()) == -1,
            "Breach attack ranking preserves tie order", "damage, gate coordinates, stable ties, empty");
        Assert(InvokePrivateStatic<int>(type, "SelectMoveIndex", (object)moveCandidates) == 4 &&
               InvokePrivateStatic<int>(type, "SelectMoveIndex", (object)Array.Empty<(int, int, int, int, int)>()) == -1,
            "Breach movement ranking preserves tie order", "energy, steps, gate coordinates, stable ties, empty");
        var ordinary = InvokePrivateStatic<int>(type, "AttackScore", 3, 100, false, 0, 50);
        var ramScore = InvokePrivateStatic<int>(type, "AttackScore", 3, 100, true, 0, 50);
        var move = InvokePrivateStatic<int>(type, "MoveScore", false, 2, 1, 3, 4, 50, 60);
        Assert(ramScore - ordinary == 4000 && move == 0 + 1400 - 110 - 900 - 160 - 27,
            "Breach scoring retains ram priority and movement costs", "attack and movement scores");
    }

    private static void RunBattleAiStrategyScoringTest()
    {
        var type = typeof(BattleSceneController).Assembly.GetType("ThreeKingdom.Battle.BattleAiScoring")!;
        int Expected(int value, float chance) => InvokePrivateStatic<int>(type, "GetExpectedStrategyScore", value, chance);
        int ConfusionValue(int troops, bool officer) => InvokePrivateStatic<int>(type, "GetConfusionValue", troops, officer);
        var targetValue = ConfusionValue(1200, true);
        Assert(Expected(targetValue, 63f) > Expected(targetValue, 60f), "Chief support increases expected strategy value", "63% vs 60%");
        Assert(Expected(targetValue, 0f) == 0 && Expected(targetValue, 120f) == targetValue,
            "Strategy scoring respects probability bounds", "0%-100%");
        Assert(ConfusionValue(1500, true) > ConfusionValue(1500, false) &&
               ConfusionValue(1500, true) > ConfusionValue(500, true) &&
               ConfusionValue(5000, true) == ConfusionValue(3000, true),
            "Confusion values meaningful threats with bounded troop influence", "officer and troop strength");
        Assert(Expected(3000, 73f) > Expected(3000, 70f), "Extinguishing score considers higher success", "73% vs 70%");

        var planner = typeof(BattleSceneController).Assembly.GetType("ThreeKingdom.Battle.BattleAiPlanner")!;
        var random = new Random(741);
        var matchesOriginal = true;
        for (var trial = 0; trial < 200; trial++)
        {
            var choices = Enumerable.Range(0, trial % 13)
                .Select(_ => (Final: random.Next(-5, 6), Base: random.Next(-5, 6), Noise: random.Next(-3, 4)))
                .ToArray();
            var expected = choices.Select((choice, index) => (choice, index))
                .OrderByDescending(entry => entry.choice.Final + entry.choice.Noise)
                .ThenByDescending(entry => entry.choice.Final)
                .ThenByDescending(entry => entry.choice.Base)
                .ThenByDescending(entry => entry.choice.Noise)
                .Select(entry => entry.index).DefaultIfEmpty(-1).First();
            if (InvokePrivateStatic<int>(planner, "SelectOffensiveActionIndex", (object)choices) != expected)
            {
                matchesOriginal = false;
                break;
            }
        }
        var tie = new[] { (Final: 10, Base: 5, Noise: 0), (Final: 10, Base: 5, Noise: 0) };
        Assert(matchesOriginal && InvokePrivateStatic<int>(planner, "SelectOffensiveActionIndex", (object)tie) == 0,
            "Offensive action ranking matches original priority and stable ties", "200 candidate sets and equal scores");

        var visited = new List<int>();
        Func<bool>[] stages =
        {
            () => { visited.Add(1); return false; },
            () => { visited.Add(2); return true; },
            () => { visited.Add(3); return true; }
        };
        var acted = InvokePrivateStatic<bool>(planner, "TryExecuteFirstAction", (object)stages);
        var none = InvokePrivateStatic<bool>(planner, "TryExecuteFirstAction", (object)new Func<bool>[] { () => false, () => false });
        Assert(acted && visited.SequenceEqual(new[] { 1, 2 }) && !none,
            "AI turn stages stop after the first successful action", "ordered execution / no action");
    }
}
