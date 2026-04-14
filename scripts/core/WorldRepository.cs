using System.Collections.Generic;
using System.Text.Json;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class WorldRepository
{
    private const string MapLocationsPath = "res://data/scenarios/map_locations_40.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WorldState? LoadScenario(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            GD.PushError($"Scenario file missing: {path}");
            return null;
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        var json = file.GetAsText();
        var world = JsonSerializer.Deserialize<WorldState>(json, JsonOptions);
        if (world == null)
        {
            return null;
        }

        ApplyMapLocations(world);
        return world;
    }

    private static void ApplyMapLocations(WorldState world)
    {
        if (!FileAccess.FileExists(MapLocationsPath))
        {
            return;
        }

        using var file = FileAccess.Open(MapLocationsPath, FileAccess.ModeFlags.Read);
        var json = file.GetAsText();
        var mapDoc = JsonSerializer.Deserialize<MapLocationDocument>(json, JsonOptions);
        if (mapDoc == null)
        {
            return;
        }

        var sourceLocations = mapDoc.Locations;
        if (sourceLocations.Count == 0)
        {
            sourceLocations = mapDoc.Cities;
        }

        if (sourceLocations.Count == 0)
        {
            return;
        }

        var nextCityId = 1;
        var cities = new List<CityData>();
        foreach (var entry in sourceLocations)
        {
            var nameEn = !string.IsNullOrWhiteSpace(entry.NameEn)
                ? entry.NameEn.Trim()
                : ExtractMapCityName(entry.Name);
            var nameZh = !string.IsNullOrWhiteSpace(entry.NameChi)
                ? entry.NameChi.Trim()
                : ToZhHantName(nameEn);

            if (string.IsNullOrWhiteSpace(nameEn) && string.IsNullOrWhiteSpace(nameZh))
            {
                continue;
            }

            var city = new CityData
            {
                Id = nextCityId,
                Name = !string.IsNullOrWhiteSpace(nameZh) ? nameZh : nameEn,
                NameEn = nameEn,
                NameZhHant = nameZh,
                OwnerFactionId = 0,
                Gold = 700,
                Food = 1300,
                Troops = 1400,
                Farm = 60,
                Commercial = 60,
                Defense = 55,
                Loyalty = 70,
                MapX = entry.X,
                MapY = entry.Y
            };

            cities.Add(city);
            nextCityId += 1;
        }

        BuildAutoConnections(cities);
        world.Cities = cities;
        SetupInitialOwnershipAndOfficers(world);
    }

    private static void BuildAutoConnections(List<CityData> cities)
    {
        const int neighborCount = 3;
        const float maxDistance = 260.0f;

        foreach (var city in cities)
        {
            city.ConnectedCityIds.Clear();
        }

        for (var i = 0; i < cities.Count; i++)
        {
            var source = cities[i];
            var distances = new List<(CityData City, float Distance)>();
            for (var j = 0; j < cities.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var target = cities[j];
                var dx = source.MapX - target.MapX;
                var dy = source.MapY - target.MapY;
                var distance = Mathf.Sqrt(dx * dx + dy * dy);
                distances.Add((target, distance));
            }

            distances.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            var added = 0;
            foreach (var candidate in distances)
            {
                if (added >= neighborCount)
                {
                    break;
                }

                if (candidate.Distance > maxDistance && added > 0)
                {
                    continue;
                }

                AddBidirectionalConnection(source, candidate.City);
                added += 1;
            }

            if (source.ConnectedCityIds.Count == 0 && distances.Count > 0)
            {
                AddBidirectionalConnection(source, distances[0].City);
            }
        }
    }

    private static void AddBidirectionalConnection(CityData a, CityData b)
    {
        if (!a.ConnectedCityIds.Contains(b.Id))
        {
            a.ConnectedCityIds.Add(b.Id);
        }

        if (!b.ConnectedCityIds.Contains(a.Id))
        {
            b.ConnectedCityIds.Add(a.Id);
        }
    }

    private static void SetupInitialOwnershipAndOfficers(WorldState world)
    {
        var chengdu = FindCityByName(world.Cities, "Chengdu");
        var ye = FindCityByName(world.Cities, "Ye");
        var jianye = FindCityByName(world.Cities, "Jianye");

        foreach (var city in world.Cities)
        {
            city.OwnerFactionId = 0;
            city.OfficerIds.Clear();
        }

        if (chengdu != null)
        {
            chengdu.OwnerFactionId = 1;
            chengdu.Gold = 1200;
            chengdu.Food = 1800;
            chengdu.Troops = 2500;
        }

        if (ye != null)
        {
            ye.OwnerFactionId = 2;
            ye.Gold = 1000;
            ye.Food = 1700;
            ye.Troops = 2300;
        }

        if (jianye != null)
        {
            jianye.OwnerFactionId = 3;
            jianye.Gold = 950;
            jianye.Food = 1750;
            jianye.Troops = 2200;
        }

        foreach (var officer in world.Officers)
        {
            CityData? assignedCity = null;
            if (IsOfficerName(officer.Name, "Liu Bei") || IsOfficerName(officer.Name, "Guan Yu"))
            {
                assignedCity = chengdu;
            }
            else if (IsOfficerName(officer.Name, "Cao Cao"))
            {
                assignedCity = ye;
            }
            else if (IsOfficerName(officer.Name, "Sun Quan") || IsOfficerName(officer.Name, "Zhou Yu"))
            {
                assignedCity = jianye;
            }
            else
            {
                assignedCity = chengdu ?? ye ?? jianye;
            }

            if (assignedCity == null)
            {
                continue;
            }

            officer.CityId = assignedCity.Id;
            if (!assignedCity.OfficerIds.Contains(officer.Id))
            {
                assignedCity.OfficerIds.Add(officer.Id);
            }
        }
    }

    private static CityData? FindCityByName(List<CityData> cities, string nameEn)
    {
        foreach (var city in cities)
        {
            if (city.NameEn.Equals(nameEn, System.StringComparison.OrdinalIgnoreCase) ||
                city.Name.Equals(nameEn, System.StringComparison.OrdinalIgnoreCase))
            {
                return city;
            }
        }

        return null;
    }

    private static string ExtractMapCityName(string rawName)
    {
        var index = rawName.IndexOf(" (", System.StringComparison.Ordinal);
        if (index <= 0)
        {
            return rawName.Trim();
        }

        return rawName[..index].Trim();
    }

    private static bool IsOfficerName(string officerName, string targetEn)
    {
        return officerName.Equals(targetEn, System.StringComparison.OrdinalIgnoreCase);
    }

    private static string ToZhHantName(string nameEn)
    {
        return nameEn switch
        {
            "Ji" => "薊",
            "Ye" => "鄴",
            "Linzi" => "臨淄",
            "Beihai" => "北海",
            "Luoyang" => "洛陽",
            "Changan" => "長安",
            "Tianshui" => "天水",
            "Xiapi" => "下邳",
            "Shouchun" => "壽春",
            "Jianye" => "建業",
            "Xiangyang" => "襄陽",
            "Jiangling" => "江陵",
            "Wuchang" => "武昌",
            "Chengdu" => "成都",
            "Chongqing" => "重慶",
            "Changsha" => "長沙",
            "Kuaiji" => "會稽",
            "Nanhai" => "南海",
            "Jiaozhou" => "交州",
            "Taiwan" => "台灣",
            _ => nameEn
        };
    }

    private sealed class MapLocationDocument
    {
        public List<MapLocationEntry> Locations { get; set; } = new();
        public List<MapLocationEntry> Cities { get; set; } = new();
    }

    private sealed class MapLocationEntry
    {
        public string Name { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string NameChi { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
    }
}
