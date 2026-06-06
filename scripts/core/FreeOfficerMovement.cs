using System;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class FreeOfficerMovement
{
    private const int MinimumJoinAge = 14;
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

            if (TryAssignRelationshipCity(world, officer, random))
            {
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

            if (officer.CityId <= 0 &&
                officer.FreeOfficerStayMonths <= 0 &&
                GetOfficerAge(world, officer) == MinimumJoinAge)
            {
                InitializeNewlyEligibleOfficer(world, officer, random);
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
        if (officer.DeathYear > 0 && world.Year > officer.DeathYear)
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

    private static bool TryAssignRelationshipCity(WorldState world, OfficerData officer, Random random)
    {
        var relatedCities = world.Officers
            .Where(candidate => candidate.Id != officer.Id &&
                                IsOfficerAlive(world, candidate) &&
                                candidate.CityId > 0 &&
                                world.GetCity(candidate.CityId) != null &&
                                HasRelationshipWith(officer, candidate))
            .Select(candidate => candidate.CityId)
            .Distinct()
            .ToList();
        if (relatedCities.Count == 0)
        {
            return false;
        }

        officer.CityId = relatedCities[random.Next(relatedCities.Count)];
        officer.FreeOfficerStayMonths = RollStayMonths(random);
        return true;
    }

    private static void InitializeNewlyEligibleOfficer(WorldState world, OfficerData officer, Random random)
    {
        if (random.Next(100) < InitialVisiblePercent)
        {
            if (!TryAssignRelationshipCity(world, officer, random))
            {
                AssignRandomVisibleCity(world, officer, random);
            }

            return;
        }

        HideOfficer(officer, random);
    }

    private static bool HasRelationshipWith(OfficerData officer, OfficerData candidate)
    {
        if (officer.RelationshipType == null || officer.RelationshipType.Count == 0)
        {
            return false;
        }

        foreach (var relatedName in officer.RelationshipType.Keys)
        {
            if (string.IsNullOrWhiteSpace(relatedName))
            {
                continue;
            }

            if ((!string.IsNullOrWhiteSpace(candidate.NameZhHant) &&
                 relatedName.Equals(candidate.NameZhHant, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(candidate.Name) &&
                 relatedName.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountVisibleFreeOfficers(WorldState world, int cityId)
    {
        return world.Officers.Count(officer =>
            officer.CityId == cityId &&
            IsFreeOfficer(world, officer));
    }

    private static bool IsOfficerAlive(WorldState world, OfficerData officer)
    {
        return officer.DeathYear <= 0 || world.Year <= officer.DeathYear;
    }

    private static int GetOfficerAge(WorldState world, OfficerData officer)
    {
        return officer.BirthYear <= 0 ? MinimumJoinAge : world.Year - officer.BirthYear;
    }

    private static Random CreateRandom(WorldState world, int salt)
    {
        return new Random(HashCode.Combine(world.RandomSeed, world.Year, world.Month, salt));
    }
}
