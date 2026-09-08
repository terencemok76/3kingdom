using Godot;
using Godot.Collections;

namespace ThreeKingdom.Battle;

public enum BattleGateForegroundSide
{
    None,
    Left,
    Right
}

public enum BattleWeatherType
{
    Sunny,
    Cloudy,
    Rain
}

public enum BattleWindDirection
{
    NorthEast,
    NorthWest,
    SouthEast,
    SouthWest
}

public enum BattleWindPower
{
    Calm,
    Breeze,
    Strong
}

public enum BattleTimeOfDay
{
    Dawn,
    Morning,
    Afternoon,
    Night
}

[GlobalClass]
public partial class BattleScenarioDefinition : Resource
{
    [Export]
    public string DisplayName { get; set; } = "Battle Scenario";

    [Export]
    public BattleScenarioType ScenarioType { get; set; } = BattleScenarioType.SiegeAssault;

    [Export]
    public BattleStructureFacing DefaultStructureFacing { get; set; } = BattleStructureFacing.NorthEast;

    [Export(PropertyHint.Range, "0,4,1")]
    public int ForegroundOcclusionDepth { get; set; } = 2;

    [Export]
    public BattleGateForegroundSide OpenGateForegroundSide { get; set; } = BattleGateForegroundSide.Right;

    [Export]
    public BattleWeatherType Weather { get; set; } = BattleWeatherType.Sunny;

    [Export]
    public BattleWindDirection WindDirection { get; set; } = BattleWindDirection.SouthEast;

    [Export]
    public BattleWindPower WindPower { get; set; } = BattleWindPower.Breeze;

    [Export]
    public BattleTimeOfDay TimeOfDay { get; set; } = BattleTimeOfDay.Morning;

    [Export]
    public Array<Vector2I> NorthWestStructureGrids { get; set; } = new();

    [Export]
    public Dictionary<string, Vector2I> UnitSpawnGrids { get; set; } = new();

    // Optional scenario-authored side entrances for campaign reinforcements.  When empty,
    // field battles use the standard NW (attacker) and SE (defender) two-row entrances.
    [Export]
    public Array<Vector2I> AttackerReinforcementEntranceGrids { get; set; } = new();

    [Export]
    public Array<Vector2I> DefenderReinforcementEntranceGrids { get; set; } = new();

    // Optional scenario-authored exits for voluntary retreats.  When empty,
    // attackers withdraw through SW and defenders withdraw through NE.
    [Export]
    public Array<Vector2I> AttackerRetreatExitGrids { get; set; } = new();

    [Export]
    public Array<Vector2I> DefenderRetreatExitGrids { get; set; } = new();

    public static BattleScenarioDefinition CreateBuiltIn(BattleScenarioType scenarioType)
    {
        return new BattleScenarioDefinition
        {
            DisplayName = scenarioType switch
            {
                BattleScenarioType.FieldBattle => "Field Battle",
                BattleScenarioType.MoatSiegeBattle => "Moat Siege Battle",
                _ => "Siege Assault"
            },
            ScenarioType = scenarioType,
            DefaultStructureFacing = BattleStructureFacing.NorthEast,
            ForegroundOcclusionDepth = 2,
            OpenGateForegroundSide = BattleGateForegroundSide.Right,
            Weather = BattleWeatherType.Sunny,
            WindDirection = BattleWindDirection.SouthEast,
            WindPower = BattleWindPower.Breeze,
            TimeOfDay = BattleTimeOfDay.Morning,
            UnitSpawnGrids = new Dictionary<string, Vector2I>
            {
                { "AttackerA", new Vector2I(10, 20) },
                { "Spearman", new Vector2I(8, 18) },
                { "AttackerB", new Vector2I(12, 18) },
                { "AttackerC", new Vector2I(14, 20) },
                { "Ram", new Vector2I(12, 16) },
                { "Ladder", new Vector2I(10, 15) },
                { "Catapult", new Vector2I(14, 15) },
                { "SupplyCart", new Vector2I(16, 19) },
                { "DefenderA", new Vector2I(10, 7) },
                { "DefenderB", new Vector2I(14, 7) },
                { "DefenderC", new Vector2I(12, 7) }
            }
        };
    }
}
