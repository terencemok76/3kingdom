using System.IO;
using System.Reflection;
using ThreeKingdom.Battle;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using ThreeKingdom.Map;
using ThreeKingdom.UI;

namespace AiHarness;


internal static class TestHelpers
{
    public static OfficerData Officer(int id, string name, int cityId, int strength = 70, int intelligence = 60, int charm = 60, int combat = 70)
    {
        return new OfficerData
        {
            Id = id,
            Name = name,
            NameZhHant = name,
            Role = "General",
            CityId = cityId,
            Strength = strength,
            Intelligence = intelligence,
            Charm = charm,
            Leadership = 70,
            Politics = 50,
            Loyalty = 80,
            Ambition = 50,
            Combat = combat
        };
    }

    public static CityData City(int id, string name, int ownerFactionId, int gold, int food, int troops, IEnumerable<int> officers, IEnumerable<int> connected)
    {
        return new CityData
        {
            Id = id,
            Name = name,
            NameEn = name,
            NameZhHant = name,
            OwnerFactionId = ownerFactionId,
            Gold = gold,
            Food = food,
            Population = 45000,
            InfantryTroops = troops,
            Troops = troops,
            Farm = 50,
            Commercial = 50,
            Defense = 40,
            Loyalty = 80,
            OfficerIds = new List<int>(officers),
            ConnectedCityIds = new List<int>(connected)
        };
    }

    public static FactionData Faction(int id, string name, bool isPlayer, int rulerOfficerId, IEnumerable<int> officers)
    {
        return new FactionData
        {
            Id = id,
            NameEn = name,
            NameZhHant = name,
            IsPlayer = isPlayer,
            RulerOfficerId = rulerOfficerId,
            OfficerIds = new List<int>(officers)
        };
    }

    public static WorldState World(int year = 200, int month = 1)
    {
        return new WorldState
        {
            Year = year,
            Month = month,
            RandomSeed = 1,
            InteractiveBattlesEnabled = false
        };
    }
}
