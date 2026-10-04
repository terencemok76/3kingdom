using System;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public enum CaravanRoadRiskLevel
{
    Safe,
    Low,
    Medium,
    High
}

public sealed class CaravanRoadRiskAssessment
{
    public CaravanRoadRiskLevel Level { get; init; }
    public int Score { get; init; }
    public int EncounterChancePercent { get; init; }
}

public sealed class CaravanAmbushResolution
{
    public CaravanRoadRiskAssessment Risk { get; init; } = new();
    public bool EncounteredBandits { get; init; }
    public bool HasEscort { get; init; }
    // The first road-risk slice resolves this small field engagement at month end.
    // Keeping it explicit lets a later standalone battle scene consume the same data.
    public bool FieldBattleResolved { get; init; }
    public bool EscortWon { get; init; }
    public int TroopLossPercent { get; init; }
    public int CargoLossPercent { get; init; }
}

public static class CaravanRoadRiskRules
{
    public const int DefenseSafetyThreshold = 45;
    public const int LoyaltySafetyThreshold = 65;
    public const int MinimumEscortTroops = 100;

    public static CaravanRoadRiskAssessment Assess(CityData sourceCity, CityData targetCity)
    {
        var sourceWeakness = GetCityWeakness(sourceCity);
        var targetWeakness = GetCityWeakness(targetCity);
        var score = (sourceWeakness + targetWeakness) / 2;
        var level = score switch
        {
            <= 0 => CaravanRoadRiskLevel.Safe,
            <= 15 => CaravanRoadRiskLevel.Low,
            <= 35 => CaravanRoadRiskLevel.Medium,
            _ => CaravanRoadRiskLevel.High
        };
        var encounterChance = level switch
        {
            CaravanRoadRiskLevel.Safe => 0,
            CaravanRoadRiskLevel.Low => 10,
            CaravanRoadRiskLevel.Medium => 30,
            _ => 60
        };
        return new CaravanRoadRiskAssessment
        {
            Level = level,
            Score = score,
            EncounterChancePercent = encounterChance
        };
    }

    public static CaravanAmbushResolution Resolve(
        WorldState world,
        CityData sourceCity,
        CityData targetCity,
        TroopAllocationData escort,
        int cargoValue,
        bool forceEncounterForDebug = false)
    {
        var risk = Assess(sourceCity, targetCity);
        var hasEscort = escort.Total >= MinimumEscortTroops;
        if (cargoValue <= 0 || (risk.EncounterChancePercent <= 0 && !forceEncounterForDebug))
        {
            return new CaravanAmbushResolution { Risk = risk, HasEscort = hasEscort };
        }

        var random = new Random(HashCode.Combine(
            world.RandomSeed,
            world.Year,
            world.Month,
            sourceCity.Id,
            targetCity.Id,
            cargoValue,
            escort.Total,
            451));
        if (!forceEncounterForDebug && random.Next(100) >= risk.EncounterChancePercent)
        {
            return new CaravanAmbushResolution { Risk = risk, HasEscort = hasEscort };
        }

        if (!hasEscort)
        {
            return new CaravanAmbushResolution
            {
                Risk = risk,
                EncounteredBandits = true,
                HasEscort = false,
                CargoLossPercent = risk.Level == CaravanRoadRiskLevel.High ? 50 : 35
            };
        }

        var banditStrength = 120 + risk.Score * 12 + Math.Min(280, cargoValue / 30);
        var escortStrength = escort.Total + random.Next(0, 81);
        var escortWon = escortStrength >= banditStrength;
        return new CaravanAmbushResolution
        {
            Risk = risk,
            EncounteredBandits = true,
            HasEscort = true,
            FieldBattleResolved = true,
            EscortWon = escortWon,
            TroopLossPercent = escortWon ? 8 : 25,
            CargoLossPercent = escortWon ? 0 : 40
        };
    }

    public static int ApplyLoss(int amount, int lossPercent) =>
        Math.Max(0, amount - (int)Math.Ceiling(Math.Max(0, amount) * Math.Clamp(lossPercent, 0, 100) / 100.0));

    public static TroopAllocationData ApplyTroopLoss(TroopAllocationData allocation, int lossPercent) => new()
    {
        Infantry = ApplyLoss(allocation.Infantry, lossPercent),
        Spearman = ApplyLoss(allocation.Spearman, lossPercent),
        Cavalry = ApplyLoss(allocation.Cavalry, lossPercent),
        Archer = ApplyLoss(allocation.Archer, lossPercent),
        Crossbow = ApplyLoss(allocation.Crossbow, lossPercent),
        Siege = ApplyLoss(allocation.Siege, lossPercent)
    };

    private static int GetCityWeakness(CityData city) =>
        Math.Max(0, DefenseSafetyThreshold - city.Defense) +
        Math.Max(0, LoyaltySafetyThreshold - city.Loyalty);
}
