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
    private static WorldState CreateBattleCampaignWorld()
    {
        var world = TestHelpers.World();
        world.InteractiveBattlesEnabled = true;
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 2000, 5000, 7000, new[] { 101 }, new[] { 2, 3 }));
        world.Cities.Add(TestHelpers.City(2, "TargetCity", 2, 1800, 5000, 6000, new[] { 201, 202 }, new[] { 1, 3 }));
        world.Cities.Add(TestHelpers.City(3, "ReserveCity", 1, 1200, 3000, 4000, new[] { 102 }, new[] { 1, 2 }));
        world.Officers.Add(TestHelpers.Officer(101, "P1", 1));
        world.Officers.Add(TestHelpers.Officer(102, "P2", 3));
        world.Officers.Add(TestHelpers.Officer(201, "D1", 2));
        world.Officers.Add(TestHelpers.Officer(202, "D2", 2));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102 }));
        world.Factions.Add(TestHelpers.Faction(2, "Defender", false, 201, new[] { 201, 202 }));
        return world;
    }

    private static PendingCommandData CreateBattleCampaignAttack() => new()
    {
        Type = CommandType.Attack,
        ActorFactionId = 1,
        SourceCityId = 1,
        TargetCityId = 2,
        GoldToSend = 500,
        FoodToSend = 2000,
        AttackOfficerDeployments = new List<AttackOfficerDeploymentData>
        {
            new() { OfficerId = 101, TroopType = TroopType.Infantry, TroopCount = 3000 },
            new() { OfficerId = 101, TroopType = TroopType.Siege, TroopCount = 500, SiegeEngineType = SiegeEngineType.Ram }
        }
    };

    private static void Assert(bool condition, string name, string detail)
    {
        if (condition)
        {
            Passes.Add($"PASS: {name} - {detail}");
        }
        else
        {
            Failures.Add($"FAIL: {name} - {detail}");
        }
    }

    private static (TurnManager Turn, CommandResolver Resolver, AiController Ai) CreateServices(WorldState world)
    {
        var turn = new TurnManager();
        turn.Initialize(world);
        var localization = new LocalizationService();
        localization.LoadFromFileSystem(GetLocalizationDirectoryPath());
        var resolver = new CommandResolver();
        resolver.Initialize(turn, new CombatResolver(), localization);
        var ai = new AiController();
        ai.Initialize(resolver, turn, localization);
        return (turn, resolver, ai);
    }

    private static string GetLocalizationDirectoryPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "data", "localization");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "data", "localization");
    }

    private static T InvokePrivateStatic<T>(Type targetType, string methodName, params object[] args)
    {
        var method = targetType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null)
        {
            throw new InvalidOperationException($"Method not found: {targetType.FullName}.{methodName}");
        }

        var result = method.Invoke(null, args);
        if (result is not T typedResult)
        {
            throw new InvalidOperationException($"Unexpected result type from {targetType.FullName}.{methodName}");
        }

        return typedResult;
    }

    private static T InvokePrivateInstance<T>(object target, string methodName, params object[] args)
    {
        var method = target.GetType()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == args.Length);
        if (method == null)
        {
            throw new InvalidOperationException($"Method not found: {target.GetType().FullName}.{methodName}");
        }

        var result = method.Invoke(target, args);
        if (result is not T typedResult)
        {
            throw new InvalidOperationException($"Unexpected result type from {target.GetType().FullName}.{methodName}");
        }

        return typedResult;
    }

    private static WorldState CloneWorld(WorldState source)
    {
        var clone = new WorldState
        {
            Year = source.Year,
            Month = source.Month,
            RandomSeed = source.RandomSeed,
            ViewAllInformationEnabled = source.ViewAllInformationEnabled,
            InteractiveBattlesEnabled = source.InteractiveBattlesEnabled
        };

        clone.Cities.AddRange(source.Cities.Select(city => new CityData
        {
            Id = city.Id,
            Name = city.Name,
            NameEn = city.NameEn,
            NameZhHant = city.NameZhHant,
            OwnerFactionId = city.OwnerFactionId,
            Gold = city.Gold,
            Food = city.Food,
            Horses = city.Horses,
            Troops = city.Troops,
            InfantryTroops = city.InfantryTroops,
            SpearmanTroops = city.SpearmanTroops,
            CavalryTroops = city.CavalryTroops,
            ArcherTroops = city.ArcherTroops,
            CrossbowTroops = city.CrossbowTroops,
            SiegeTroops = city.SiegeTroops,
            Farm = city.Farm,
            Commercial = city.Commercial,
            Defense = city.Defense,
            DisasterPrevention = city.DisasterPrevention,
            Loyalty = city.Loyalty,
            HasBowWorkshop = city.HasBowWorkshop,
            HasSiegeWorkshop = city.HasSiegeWorkshop,
            OfficerIds = new List<int>(city.OfficerIds),
            ConnectedCityIds = new List<int>(city.ConnectedCityIds),
            LastSearchYear = city.LastSearchYear,
            LastSearchMonth = city.LastSearchMonth
        }));

        clone.Officers.AddRange(source.Officers.Select(officer => new OfficerData
        {
            Id = officer.Id,
            Name = officer.Name,
            NameZhHant = officer.NameZhHant,
            Role = officer.Role,
            Belongs = officer.Belongs,
            BirthYear = officer.BirthYear,
            DeathYear = officer.DeathYear,
            Strength = officer.Strength,
            Intelligence = officer.Intelligence,
            Charm = officer.Charm,
            Leadership = officer.Leadership,
            Politics = officer.Politics,
            Loyalty = officer.Loyalty,
            Ambition = officer.Ambition,
            Combat = officer.Combat,
            RelationshipType = officer.RelationshipType,
            CityId = officer.CityId,
            LastAssignedYear = officer.LastAssignedYear,
            LastAssignedMonth = officer.LastAssignedMonth,
            LastAssignedCommand = officer.LastAssignedCommand,
            MilitaryRank = officer.MilitaryRank,
            StrategistRank = officer.StrategistRank,
            CivilRank = officer.CivilRank,
            SpyRank = officer.SpyRank,
            DiplomacyRank = officer.DiplomacyRank,
            FarmRank = officer.FarmRank,
            CommercialRank = officer.CommercialRank,
            DefendRank = officer.DefendRank,
            DisasterPreventionRank = officer.DisasterPreventionRank,
            ConstructionRank = officer.ConstructionRank,
            BattleExperience = officer.BattleExperience,
            StrategistExperience = officer.StrategistExperience,
            CivilExperience = officer.CivilExperience,
            SpyExperience = officer.SpyExperience,
            DiplomacyExperience = officer.DiplomacyExperience,
            FarmExperience = officer.FarmExperience,
            CommercialExperience = officer.CommercialExperience,
            DefendExperience = officer.DefendExperience,
            DisasterPreventionExperience = officer.DisasterPreventionExperience,
            ConstructionExperience = officer.ConstructionExperience,
            GeneralTitle = officer.GeneralTitle,
            StrategistTitle = officer.StrategistTitle,
            CivilTitle = officer.CivilTitle,
            SpyTitle = officer.SpyTitle,
            DiplomacyTitle = officer.DiplomacyTitle,
            FarmTitle = officer.FarmTitle,
            CommercialTitle = officer.CommercialTitle,
            DefendTitle = officer.DefendTitle,
            DisasterPreventionTitle = officer.DisasterPreventionTitle,
            ConstructionTitle = officer.ConstructionTitle,
            FreeOfficerStayMonths = officer.FreeOfficerStayMonths
        }));

        clone.Factions.AddRange(source.Factions.Select(faction => new FactionData
        {
            Id = faction.Id,
            NameEn = faction.NameEn,
            NameZhHant = faction.NameZhHant,
            IsPlayer = faction.IsPlayer,
            RulerOfficerId = faction.RulerOfficerId,
            ChancellorOfficerId = faction.ChancellorOfficerId,
            ChiefStrategistOfficerId = faction.ChiefStrategistOfficerId,
            OfficerIds = new List<int>(faction.OfficerIds)
        }));

        clone.Items.AddRange(source.Items.Select(item => new ItemData
        {
            Id = item.Id,
            NameEn = item.NameEn,
            NameZhHant = item.NameZhHant,
            ItemType = item.ItemType,
            StrengthBonus = item.StrengthBonus,
            IntelligenceBonus = item.IntelligenceBonus,
            CharmBonus = item.CharmBonus,
            LeadershipBonus = item.LeadershipBonus,
            PoliticsBonus = item.PoliticsBonus,
            CombatBonus = item.CombatBonus,
            LoyaltyBonus = item.LoyaltyBonus,
            OwnerFactionId = item.OwnerFactionId,
            OwnerCityId = item.OwnerCityId,
            EquippedOfficerId = item.EquippedOfficerId,
            Rarity = item.Rarity
        }));

        clone.PendingSuccessionRecords.AddRange(source.PendingSuccessionRecords.Select(record => new WorldState.PendingSuccessionData
        {
            FactionId = record.FactionId,
            CandidateOfficerIds = new List<int>(record.CandidateOfficerIds)
        }));

        return clone;
    }

    private static int FindSeedForMonthlyCityEvent(MonthlyCityEventType eventType)
    {
        for (var seed = 1; seed <= 5000; seed += 1)
        {
            var world = CreateMonthlyEventTestWorldForEventType(eventType, seed, disasterPrevention: 0);
            var services = CreateServices(world);
            var result = services.Turn.ApplyMonthlyEconomy();
            if (result.PlayerCityEvents.Any(item => item.EventType == eventType))
            {
                return seed;
            }
        }

        return -1;
    }

    private static int FindSeedForNoMonthlyCityEvent(int month, int disasterPrevention)
    {
        for (var seed = 1; seed <= 5000; seed += 1)
        {
            var world = CreateMonthlyEventTestWorld(month, seed, disasterPrevention);
            var result = CreateServices(world).Turn.ApplyMonthlyEconomy();
            if (result.AllCityEvents.Count == 0)
            {
                return seed;
            }
        }

        return 1;
    }

    private static int FindSeedForWorldWithoutMonthlyEvents(WorldState world)
    {
        for (var seed = 1; seed <= 5000; seed += 1)
        {
            var clone = CloneWorld(world);
            clone.RandomSeed = seed;
            var result = CreateServices(clone).Turn.ApplyMonthlyEconomy();
            if (result.AllCityEvents.Count == 0)
            {
                return seed;
            }
        }

        return 1;
    }

    private static int FindComparableMonthlyCityEventSeed(MonthlyCityEventType eventType, int month, int lowPrevention, int highPrevention)
    {
        for (var seed = 1; seed <= 5000; seed += 1)
        {
            var lowWorld = CreateMonthlyEventTestWorld(month, seed, lowPrevention);
            var highWorld = CreateMonthlyEventTestWorld(month, seed, highPrevention);
            var lowResult = CreateServices(lowWorld).Turn.ApplyMonthlyEconomy();
            var highResult = CreateServices(highWorld).Turn.ApplyMonthlyEconomy();
            var lowEvent = lowResult.PlayerCityEvents.SingleOrDefault();
            var highEvent = highResult.PlayerCityEvents.SingleOrDefault();
            if (lowEvent != null && highEvent != null && lowEvent.EventType == eventType && highEvent.EventType == eventType)
            {
                return seed;
            }
        }

        return -1;
    }

    private static int FindSeedForSpecificCityMonthlyEvent(MonthlyCityEventType eventType, int month, int cityId)
    {
        for (var seed = 1; seed <= 5000; seed += 1)
        {
            var world = TestHelpers.World(month: month);
            world.RandomSeed = seed;
            world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 0, new[] { 101 }, Array.Empty<int>()));
            world.Cities.Add(TestHelpers.City(2, "NeutralCity", 0, 1000, 1000, 0, Array.Empty<int>(), Array.Empty<int>()));
            world.GetCity(1)!.Farm = 80;
            world.GetCity(1)!.Defense = 70;
            world.GetCity(1)!.Loyalty = 85;
            world.GetCity(1)!.DisasterPrevention = 120;
            world.GetCity(2)!.Farm = 80;
            world.GetCity(2)!.Defense = 70;
            world.GetCity(2)!.Loyalty = 85;
            world.Officers.Add(TestHelpers.Officer(101, "P1", 1));
            world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
            var result = CreateServices(world).Turn.ApplyMonthlyEconomy();
            if (result.AllCityEvents.Any(item => item.CityId == cityId && item.EventType == eventType))
            {
                return seed;
            }
        }

        return -1;
    }

    private static bool CanMonthlyCityEventOccur(MonthlyCityEventType eventType, int month, int? loyalty = null, int? defense = null)
    {
        for (var seed = 1; seed <= 5000; seed += 1)
        {
            var world = CreateMonthlyEventTestWorld(month, seed, disasterPrevention: 0, loyalty: loyalty ?? 85, defense: defense ?? 70);
            var services = CreateServices(world);
            var result = services.Turn.ApplyMonthlyEconomy();
            if (result.PlayerCityEvents.Any(item => item.EventType == eventType))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetCityPrefectOfficerId(WorldState world, int cityId)
    {
        var city = world.GetCity(cityId);
        if (city == null)
        {
            return 0;
        }

        return city.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null && OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Governor))
            .Select(officer => officer!.Id)
            .FirstOrDefault();
    }

    private static WorldState CreateMonthlyEventTestWorld(int month, int seed, int disasterPrevention)
    {
        return CreateMonthlyEventTestWorld(month, seed, disasterPrevention, loyalty: 85, defense: 70);
    }

    private static WorldState CreateMonthlyEventTestWorld(int month, int seed, int disasterPrevention, int loyalty, int defense)
    {
        var world = TestHelpers.World(month: month);
        world.RandomSeed = seed;
        world.Cities.Add(TestHelpers.City(1, "PlayerCity", 1, 1000, 1000, 1200, new[] { 101 }, Array.Empty<int>()));
        world.GetCity(1)!.Farm = 80;
        world.GetCity(1)!.Defense = defense;
        world.GetCity(1)!.Loyalty = loyalty;
        world.GetCity(1)!.Population = 45000;
        world.GetCity(1)!.DisasterPrevention = disasterPrevention;
        world.Officers.Add(TestHelpers.Officer(101, "P1", 1));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101 }));
        return world;
    }

    private static WorldState CreateMonthlyEventTestWorldForEventType(MonthlyCityEventType eventType, int seed, int disasterPrevention)
    {
        var month = GetMonthlyCityEventTestMonth(eventType);
        return eventType switch
        {
            MonthlyCityEventType.Rebellion => CreateMonthlyEventTestWorld(month, seed, disasterPrevention, loyalty: 35, defense: 60),
            MonthlyCityEventType.Bandit => CreateMonthlyEventTestWorld(month, seed, disasterPrevention, loyalty: 40, defense: 20),
            _ => CreateMonthlyEventTestWorld(month, seed, disasterPrevention)
        };
    }

    private static int GetMonthlyCityEventTestMonth(MonthlyCityEventType eventType)
    {
        return eventType switch
        {
            MonthlyCityEventType.Flooding => 6,
            MonthlyCityEventType.Drought => 6,
            MonthlyCityEventType.Earthquake => 4,
            MonthlyCityEventType.InsectDisaster => 5,
            MonthlyCityEventType.Plague => 12,
            MonthlyCityEventType.Rebellion => 6,
            MonthlyCityEventType.Bandit => 6,
            MonthlyCityEventType.Snow => 12,
            MonthlyCityEventType.Typhoon => 10,
            MonthlyCityEventType.BumperHarvest => 9,
            MonthlyCityEventType.Fire => 11,
            _ => 6
        };
    }
}
