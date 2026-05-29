using System.Collections.Generic;
using System.Linq;
using File = System.IO.File;
using Directory = System.IO.Directory;
using Path = System.IO.Path;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class WorldRepository
{
    private const int MinimumOfficerJoinAge = 18;
    private const string MapLocationsPath = "res://data/scenarios/map_locations_40.json";
    private const string OfficerDataDirectoryPath = "res://data/person";
    private const string DefaultOfficerDataPath = "res://data/person/officer_story01.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
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

        LoadOfficerData(world, path);
        ApplyMapLocations(world);
        return world;
    }

    public bool SaveGame(string path, WorldState world, string description = "", int slotIndex = 0)
    {
        var resolvedPath = ResolveWritablePath(path);
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new SaveGameData
        {
            SlotIndex = slotIndex,
            Description = description.Trim(),
            SavedAtUtc = DateTime.UtcNow.ToString("O"),
            World = world
        };
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(resolvedPath, json);
        return true;
    }

    public WorldState? LoadSavedGame(string path)
    {
        return LoadSaveDocument(path)?.World;
    }

    public SaveGameData? LoadSaveDocument(string path)
    {
        var resolvedPath = ResolveWritablePath(path);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            return null;
        }

        var json = File.ReadAllText(resolvedPath);
        var document = JsonSerializer.Deserialize<SaveGameData>(json, JsonOptions);
        if (document?.World != null)
        {
            NormalizeLoadedWorld(document.World);
            return document;
        }

        return null;
    }

    public SaveSlotSummary LoadSaveSlotSummary(string path, int slotIndex)
    {
        var document = LoadSaveDocument(path);
        if (document?.World == null)
        {
            return new SaveSlotSummary
            {
                SlotIndex = slotIndex,
                Exists = false
            };
        }

        return new SaveSlotSummary
        {
            SlotIndex = slotIndex,
            Exists = true,
            Description = document.Description,
            SavedAtUtc = document.SavedAtUtc,
            StoryNameEn = document.World.StoryNameEn,
            StoryNameZhHant = document.World.StoryNameZhHant,
            Year = document.World.Year,
            Month = document.World.Month
        };
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

        var borderOffsetX = mapDoc.MapMetadata?.MapBorderX ?? 0.0f;
        var borderOffsetY = mapDoc.MapMetadata?.MapBorderY ?? 0.0f;
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

            var cityId = entry.Id > 0 ? entry.Id : nextCityId;

            var city = new CityData
            {
                Id = cityId,
                Name = !string.IsNullOrWhiteSpace(nameZh) ? nameZh : nameEn,
                NameEn = nameEn,
                NameZhHant = nameZh,
                OwnerFactionId = 0,
                Gold = 700,
                Food = 1300,
                Horses = 120,
                Population = 45000,
                Troops = 1400,
                HasBowWorkshop = false,
                HasSiegeWorkshop = false,
                Farm = 60,
                Commercial = 60,
                Defense = 55,
                Loyalty = 70,
                MapX = entry.X + borderOffsetX,
                MapY = entry.Y + borderOffsetY
            };

            cities.Add(city);
            if (cityId >= nextCityId)
            {
                nextCityId = cityId + 1;
            }
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
        foreach (var city in world.Cities)
        {
            city.OwnerFactionId = 0;
            city.OfficerIds.Clear();
        }

        foreach (var officer in world.Officers)
        {
            officer.CityId = 0;
        }

        ApplyCityStarts(world, world.CityStarts);
        foreach (var city in world.Cities)
        {
            city.EnsureTroopTypesInitialized();
        }

        foreach (var faction in world.Factions)
        {
            faction.OfficerIds.Clear();
        }

        ApplyFactionStarts(world, world.FactionStarts);
        EnsureFactionRulersAssigned(world);
        FreeOfficerMovement.InitializeLocations(world);
    }

    private static void NormalizeLoadedWorld(WorldState world)
    {
        world.Cities ??= new List<CityData>();
        world.Officers ??= new List<OfficerData>();
        world.Items ??= new List<ItemData>();
        world.Factions ??= new List<FactionData>();
        world.DiplomacyRelations ??= new List<DiplomacyRelationData>();
        world.CityStarts ??= new List<CityStartData>();
        world.FactionStarts ??= new List<FactionStartData>();
        world.PendingCommands ??= new List<PendingCommandData>();
        world.InternalAffairsSchedules ??= new List<InternalAffairsScheduleData>();
        world.CityIntelRecords ??= new List<WorldState.CityIntelData>();
        world.PendingSuccessionRecords ??= new List<WorldState.PendingSuccessionData>();

        foreach (var city in world.Cities)
        {
            city.OfficerIds ??= new List<int>();
            city.ConnectedCityIds ??= new List<int>();
            city.EnsureTroopTypesInitialized();
        }

        foreach (var officer in world.Officers)
        {
            OfficerAppointmentRules.NormalizeOfficer(officer);
        }

        foreach (var faction in world.Factions)
        {
            faction.OfficerIds ??= new List<int>();
        }

        EnsureFactionRulersAssigned(world);
    }

    private static string ResolveWritablePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.StartsWith("user://", System.StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("res://", System.StringComparison.OrdinalIgnoreCase)
            ? ProjectSettings.GlobalizePath(path)
            : Path.GetFullPath(path);
    }

    private static void LoadOfficerData(WorldState world, string scenarioPath)
    {
        var officerDataPath = ResolveOfficerDataPath(world, scenarioPath);
        if (string.IsNullOrWhiteSpace(officerDataPath) || !FileAccess.FileExists(officerDataPath))
        {
            GD.PushWarning($"Officer dataset missing for story: {world.StoryId}");
            return;
        }

        using var file = FileAccess.Open(officerDataPath, FileAccess.ModeFlags.Read);
        var raw = file.GetAsText();
        var document = JsonSerializer.Deserialize<OfficerDatasetDocument>(raw, JsonOptions);
        if (document?.Characters == null || document.Characters.Count == 0)
        {
            GD.PushWarning($"Officer dataset could not be parsed: {officerDataPath}");
            return;
        }

        world.Officers = document.Characters;
        foreach (var officer in world.Officers)
        {
            OfficerAppointmentRules.NormalizeOfficer(officer);
        }
    }

    private static string ResolveOfficerDataPath(WorldState world, string scenarioPath)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(world.StoryId))
        {
            candidates.Add($"{OfficerDataDirectoryPath}/officer_{world.StoryId}.json");
        }

        var scenarioFileName = Path.GetFileNameWithoutExtension(scenarioPath);
        if (!string.IsNullOrWhiteSpace(scenarioFileName))
        {
            candidates.Add($"{OfficerDataDirectoryPath}/officer_{scenarioFileName}.json");
        }

        candidates.Add(DefaultOfficerDataPath);

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (FileAccess.FileExists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static void ApplyCityStarts(WorldState world, List<CityStartData> cityStarts)
    {
        foreach (var cityStart in cityStarts)
        {
            var city = world.GetCity(cityStart.CityId);
            if (city == null)
            {
                continue;
            }

            city.OwnerFactionId = cityStart.OwnerFactionId;
            city.Gold = cityStart.Gold;
            city.Food = cityStart.Food;
            city.Horses = cityStart.Horses;
            city.Population = cityStart.Population > 0 ? cityStart.Population : city.Population;
            city.Troops = cityStart.Troops;
            city.InfantryTroops = cityStart.InfantryTroops;
            city.SpearmanTroops = cityStart.SpearmanTroops;
            city.CavalryTroops = cityStart.CavalryTroops;
            city.ArcherTroops = cityStart.ArcherTroops;
            city.CrossbowTroops = cityStart.CrossbowTroops;
            city.SiegeTroops = cityStart.SiegeTroops;
            city.BowWorkshopLevel = cityStart.BowWorkshopLevel;
            city.BowWorkshopProgress = cityStart.BowWorkshopProgress;
            city.SiegeWorkshopLevel = cityStart.SiegeWorkshopLevel;
            city.SiegeWorkshopProgress = cityStart.SiegeWorkshopProgress;
            city.HorsePastureLevel = cityStart.HorsePastureLevel;
            city.HorsePastureProgress = cityStart.HorsePastureProgress;
            city.RamCount = cityStart.RamCount;
            city.RamProgress = cityStart.RamProgress;
            city.CatapultCount = cityStart.CatapultCount;
            city.CatapultProgress = cityStart.CatapultProgress;
            city.LadderCount = cityStart.LadderCount;
            city.LadderProgress = cityStart.LadderProgress;
            city.HasBowWorkshop = cityStart.HasBowWorkshop;
            city.HasSiegeWorkshop = cityStart.HasSiegeWorkshop;
            city.EnsureTroopTypesInitialized();

            foreach (var officerId in cityStart.OfficerIds)
            {
                if (!IsOfficerOldEnoughToJoin(world, officerId))
                {
                    continue;
                }

                AssignOfficerToCity(world, officerId, city.Id);
            }
        }
    }

    private static void ApplyFactionStarts(WorldState world, List<FactionStartData> factionStarts)
    {
        foreach (var factionStart in factionStarts)
        {
            var faction = world.GetFaction(factionStart.FactionId);
            if (faction == null)
            {
                continue;
            }

            foreach (var cityId in factionStart.CityIds)
            {
                var city = world.GetCity(cityId);
                if (city != null)
                {
                    city.OwnerFactionId = factionStart.FactionId;
                }
            }

            var primaryCityId = factionStart.CityIds.Count > 0 ? factionStart.CityIds[0] : 0;
            var officerIds = new List<int>(factionStart.OfficerIds);
            if (faction.RulerOfficerId > 0 && !officerIds.Contains(faction.RulerOfficerId))
            {
                officerIds.Insert(0, faction.RulerOfficerId);
            }

            foreach (var officerId in officerIds)
            {
                if (!IsOfficerOldEnoughToJoin(world, officerId))
                {
                    continue;
                }

                if (!faction.OfficerIds.Contains(officerId))
                {
                    faction.OfficerIds.Add(officerId);
                }

                var officer = world.GetOfficer(officerId);
                if (officer == null)
                {
                    continue;
                }

                if (primaryCityId > 0)
                {
                    var officerCity = world.GetCity(officer.CityId);
                    var isInFactionCity = officerCity != null && factionStart.CityIds.Contains(officerCity.Id);
                    if (!isInFactionCity)
                    {
                        AssignOfficerToCity(world, officerId, primaryCityId);
                    }
                }
            }
        }
    }

    private static void AssignOfficerToCity(WorldState world, int officerId, int cityId)
    {
        var officer = world.GetOfficer(officerId);
        var targetCity = world.GetCity(cityId);
        if (officer == null || targetCity == null)
        {
            return;
        }

        if (officer.CityId > 0)
        {
            var oldCity = world.GetCity(officer.CityId);
            if (oldCity != null)
            {
                oldCity.OfficerIds.Remove(officerId);
            }
        }

        officer.CityId = cityId;
        if (!targetCity.OfficerIds.Contains(officerId))
        {
            targetCity.OfficerIds.Add(officerId);
        }
    }

    private static bool IsOfficerOldEnoughToJoin(WorldState world, int officerId)
    {
        var officer = world.GetOfficer(officerId);
        if (officer == null)
        {
            return false;
        }

        if (officer.DeathYear > 0 && world.Year > officer.DeathYear)
        {
            return false;
        }

        if (officer.BirthYear <= 0)
        {
            return true;
        }

        return world.Year - officer.BirthYear >= MinimumOfficerJoinAge;
    }

    private static void EnsureFactionRulersAssigned(WorldState world)
    {
        foreach (var faction in world.Factions)
        {
            if (faction.RulerOfficerId <= 0)
            {
                var fallbackRuler = FindFactionRulerOfficer(world, faction);
                if (fallbackRuler != null)
                {
                    faction.RulerOfficerId = fallbackRuler.Id;
                }
            }

            if (faction.RulerOfficerId <= 0)
            {
                continue;
            }

            if (!faction.OfficerIds.Contains(faction.RulerOfficerId))
            {
                faction.OfficerIds.Add(faction.RulerOfficerId);
            }

            if (faction.ChancellorOfficerId > 0 && !faction.OfficerIds.Contains(faction.ChancellorOfficerId))
            {
                faction.ChancellorOfficerId = 0;
            }

            if (faction.ChiefStrategistOfficerId > 0 && !faction.OfficerIds.Contains(faction.ChiefStrategistOfficerId))
            {
                faction.ChiefStrategistOfficerId = 0;
            }

            SyncFactionLordAppointment(world, faction);
        }

        foreach (var officer in world.Officers)
        {
            if (world.Factions.Any(faction => faction.RulerOfficerId == officer.Id))
            {
                continue;
            }

            OfficerAppointmentRules.RemoveAppointment(officer, OfficerAppointmentRules.Lord);
        }
    }

    private static OfficerData? FindFactionRulerOfficer(WorldState world, FactionData faction)
    {
        foreach (var officerId in faction.OfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer != null && OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Lord))
            {
                return officer;
            }
        }

        foreach (var officerId in faction.OfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer != null && IsLegacyRulerRole(officer.Role))
            {
                return officer;
            }
        }

        return null;
    }

    private static void SyncFactionLordAppointment(WorldState world, FactionData faction)
    {
        foreach (var officerId in faction.OfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (officer.Id == faction.RulerOfficerId)
            {
                OfficerAppointmentRules.AddAppointment(officer, OfficerAppointmentRules.Lord);
                continue;
            }

            OfficerAppointmentRules.RemoveAppointment(officer, OfficerAppointmentRules.Lord);
        }
    }

    private static bool IsLegacyRulerRole(string role)
    {
        return role.Equals("Lord", System.StringComparison.OrdinalIgnoreCase) ||
               role.Equals("Ruler", System.StringComparison.OrdinalIgnoreCase);
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
            "Taiwan" => "臺灣",
            _ => nameEn
        };
    }

    private sealed class MapLocationDocument
    {
        [JsonPropertyName("map_metadata")]
        public MapMetadata? MapMetadata { get; set; }

        [JsonPropertyName("locations")]
        public List<MapLocationEntry> Locations { get; set; } = new();

        [JsonPropertyName("cities")]
        public List<MapLocationEntry> Cities { get; set; } = new();
    }

    private sealed class MapMetadata
    {
        [JsonPropertyName("map_border_x")]
        public float MapBorderX { get; set; }

        [JsonPropertyName("map_border_y")]
        public float MapBorderY { get; set; }
    }

    private sealed class OfficerDatasetDocument
    {
        public List<OfficerData> Characters { get; set; } = new();
    }

    private sealed class MapLocationEntry
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string NameChi { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
    }
}
