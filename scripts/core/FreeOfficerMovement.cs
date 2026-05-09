using System;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class FreeOfficerMovement
{
    private const int MinimumJoinAge = 18;
    private const int MinimumStayMonths = 2;
    private const int MaximumStayMonths = 4;
    private const int MinimumHideMonths = 2;
    private const int MaximumHideMonths = 5;
    private const int InitialVisiblePercent = 30;
    private const int HideAfterStayPercent = 40;
    private const int ReappearAfterHidePercent = 65;
    private const int MaxVisibleFreeOfficersPerCity = 2;

    public static void InitializeLocations(WorldState world)
    {
        if (world.Cities.Count == 0)
        {
            return;
        }

        var random = CreateRandom(world, 17);
        foreach (var officer in world.Officers)
        {
            if (IsEmployed(world, officer) || !IsOldEnough(world, officer))
            {
                continue;
            }

            if (officer.CityId > 0 && world.GetCity(officer.CityId) != null)
            {
                if (officer.FreeOfficerStayMonths <= 0)
                {
                    officer.FreeOfficerStayMonths = RollStayMonths(random);
                }

                continue;
            }

            if (random.Next(100) < InitialVisiblePercent)
            {
                AssignRandomVisibleCity(world, officer, random);
            }
            else
            {
                HideOfficer(officer, random);
            }
        }
    }

    public static void Advance(WorldState world)
    {
        if (world.Cities.Count == 0)
        {
            return;
        }

        var random = CreateRandom(world, world.Year * 100 + world.Month);
        foreach (var officer in world.Officers)
        {
            if (IsEmployed(world, officer) || !IsOldEnough(world, officer))
            {
                continue;
            }

            if (officer.CityId <= 0 || world.GetCity(officer.CityId) == null)
            {
                officer.CityId = 0;
                officer.FreeOfficerStayMonths -= 1;
                if (officer.FreeOfficerStayMonths <= 0)
                {
                    if (random.Next(100) < ReappearAfterHidePercent)
                    {
                        AssignRandomVisibleCity(world, officer, random);
                    }
                    else
                    {
                        HideOfficer(officer, random);
                    }
                }

                continue;
            }

            officer.FreeOfficerStayMonths -= 1;
            if (officer.FreeOfficerStayMonths > 0)
            {
                continue;
            }

            var city = world.GetCity(officer.CityId);
            var connectedCityIds = city?.ConnectedCityIds
                .Where(cityId => world.GetCity(cityId) != null)
                .ToList();

            if (random.Next(100) < HideAfterStayPercent || connectedCityIds == null || connectedCityIds.Count == 0)
            {
                HideOfficer(officer, random);
                continue;
            }

            officer.CityId = connectedCityIds[random.Next(connectedCityIds.Count)];
            officer.FreeOfficerStayMonths = RollStayMonths(random);
        }
    }

    public static bool IsFreeOfficer(WorldState world, OfficerData officer)
    {
        return !IsEmployed(world, officer) && IsOldEnough(world, officer);
    }

    public static bool IsVisibleFreeOfficer(WorldState world, OfficerData officer)
    {
        return IsFreeOfficer(world, officer) && officer.CityId > 0 && world.GetCity(officer.CityId) != null;
    }

    public static bool IsHiddenFreeOfficer(WorldState world, OfficerData officer)
    {
        return IsFreeOfficer(world, officer) && (officer.CityId <= 0 || world.GetCity(officer.CityId) == null);
    }

    public static bool IsEmployed(WorldState world, OfficerData officer)
    {
        return world.Cities.Any(city => city.OfficerIds.Contains(officer.Id)) ||
               world.Factions.Any(faction => faction.OfficerIds.Contains(officer.Id) || faction.RulerOfficerId == officer.Id);
    }

    private static bool IsOldEnough(WorldState world, OfficerData officer)
    {
        if (officer.DeathYear > 0 && world.Year >= officer.DeathYear)
        {
            return false;
        }

        return officer.BirthYear <= 0 || world.Year - officer.BirthYear >= MinimumJoinAge;
    }

    private static int RollStayMonths(Random random)
    {
        return random.Next(MinimumStayMonths, MaximumStayMonths + 1);
    }

    private static int RollHideMonths(Random random)
    {
        return random.Next(MinimumHideMonths, MaximumHideMonths + 1);
    }

    private static void HideOfficer(OfficerData officer, Random random)
    {
        officer.CityId = 0;
        officer.FreeOfficerStayMonths = RollHideMonths(random);
    }

    private static void AssignRandomVisibleCity(WorldState world, OfficerData officer, Random random)
    {
        var candidateCities = world.Cities
            .Where(city => CountVisibleFreeOfficers(world, city.Id) < MaxVisibleFreeOfficersPerCity)
            .ToList();
        var pool = candidateCities.Count > 0 ? candidateCities : world.Cities;
        officer.CityId = pool[random.Next(pool.Count)].Id;
        officer.FreeOfficerStayMonths = RollStayMonths(random);
    }

    private static int CountVisibleFreeOfficers(WorldState world, int cityId)
    {
        return world.Officers.Count(officer =>
            officer.CityId == cityId &&
            IsFreeOfficer(world, officer));
    }

    private static Random CreateRandom(WorldState world, int salt)
    {
        return new Random(HashCode.Combine(world.RandomSeed, world.Year, world.Month, salt));
    }
}
