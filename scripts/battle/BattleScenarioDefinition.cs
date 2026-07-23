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
    public Array<Vector2I> NorthWestStructureGrids { get; set; } = new();

    [Export]
    public Dictionary<string, Vector2I> UnitSpawnGrids { get; set; } = new();

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
            UnitSpawnGrids = new Dictionary<string, Vector2I>
            {
                { "AttackerA", new Vector2I(10, 20) },
                { "Spearman", new Vector2I(8, 18) },
                { "AttackerB", new Vector2I(12, 18) },
                { "AttackerC", new Vector2I(14, 20) },
                { "Ram", new Vector2I(12, 16) },
                { "Ladder", new Vector2I(10, 15) },
                { "Catapult", new Vector2I(14, 15) },
                { "DefenderA", new Vector2I(10, 7) },
                { "DefenderB", new Vector2I(14, 7) },
                { "DefenderC", new Vector2I(12, 7) }
            }
        };
    }
}
