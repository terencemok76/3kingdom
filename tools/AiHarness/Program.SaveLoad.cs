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
    private static void RunExpandedSaveSlotsTest()
    {
        var slots = new[] { 1, 10, 11, 20 };
        var names = slots.Select(SaveSlotSettings.GetPath).ToArray();
        Assert(SaveSlotSettings.Count == 20 && names.SequenceEqual(new[]
        {
            "user://saves/slot01.json", "user://saves/slot10.json",
            "user://saves/slot11.json", "user://saves/slot20.json"
        }), "Save slot count and paths include twenty slots", "legacy and new slot names");

        var directory = Path.Combine(Path.GetTempPath(), "3kingdom-slots-" + Guid.NewGuid().ToString("N"));
        var repository = new WorldRepository(refreshOfficerReferenceDataOnLoad: false);
        try
        {
            var saved = true;
            foreach (var slot in slots)
            {
                var path = Path.Combine(directory, $"slot{slot:00}.json");
                saved &= repository.SaveGame(path, TestHelpers.World(year: 200 + slot), $"Slot {slot}", slot);
                var summary = repository.LoadSaveSlotSummary(path, slot);
                var loaded = repository.LoadSavedGame(path);
                saved &= summary.Exists && summary.SlotIndex == slot && summary.Description == $"Slot {slot}" &&
                         loaded?.Year == 200 + slot;
            }
            Assert(saved, "Legacy and new save slots round trip independently", "slots 1, 10, 11 and 20");
        }
        finally
        {
            foreach (var slot in slots)
            {
                var path = Path.Combine(directory, $"slot{slot:00}.json");
                if (File.Exists(path)) File.Delete(path);
            }
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    private static void RunSaveLoadRoundTripTest()
    {
        var world = TestHelpers.World(year: 207, month: 9);
        world.RandomSeed = 321;
        world.ViewAllInformationEnabled = true;
        world.StoryNameEn = "SaveLoad Story";
        world.StoryNameZhHant = "存讀檔劇本";

        var sourceCity = TestHelpers.City(1, "SaveCity", 1, 888, 1666, 1333, new[] { 101, 102 }, new[] { 2 });
        sourceCity.Population = 54321;
        sourceCity.Horses = 222;
        sourceCity.Farm = 77;
        sourceCity.Commercial = 66;
        sourceCity.Defense = 55;
        sourceCity.Loyalty = 44;
        sourceCity.DisasterPrevention = 33;
        sourceCity.InfantryTroops = 500;
        sourceCity.SpearmanTroops = 200;
        sourceCity.CavalryTroops = 150;
        sourceCity.ArcherTroops = 180;
        sourceCity.CrossbowTroops = 140;
        sourceCity.SiegeTroops = 163;
        sourceCity.Troops = 1333;
        sourceCity.LastSearchYear = 207;
        sourceCity.LastSearchMonth = 8;
        world.Cities.Add(sourceCity);
        world.Cities.Add(TestHelpers.City(2, "OtherCity", 2, 500, 700, 800, new[] { 201 }, new[] { 1 }));

        world.Officers.Add(TestHelpers.Officer(101, "Ruler", 1));
        world.Officers.Add(TestHelpers.Officer(102, "General", 1, strength: 88, intelligence: 72, charm: 65, combat: 90));
        world.Officers.Add(TestHelpers.Officer(201, "Enemy", 2));
        world.Factions.Add(TestHelpers.Faction(1, "Player", true, 101, new[] { 101, 102 }));
        world.Factions.Add(TestHelpers.Faction(2, "Enemy", false, 201, new[] { 201 }));
        world.Items.Add(new ItemData
        {
            Id = 1,
            NameEn = "Seal",
            NameZhHant = "玉璽",
            ItemType = ItemType.Treasure,
            OwnerFactionId = 1,
            OwnerCityId = 1,
            EquippedOfficerId = 101,
            Rarity = "Legendary"
        });
        world.DiplomacyRelations.Add(new DiplomacyRelationData
        {
            FactionAId = 1,
            FactionBId = 2,
            Status = DiplomacyStatusType.Truce,
            RemainingMonths = 4,
            RelationScore = 28
        });
        world.PendingCommands.Add(new PendingCommandData
        {
            Type = CommandType.Spy,
            ActorFactionId = 1,
            SourceCityId = 1,
            TargetCityId = 2,
            SpyActionType = SpyActionType.Assassination,
            TargetOfficerId = 201,
            OfficerIds = new List<int> { 102 }
        });
        world.InternalAffairsSchedules.Add(new InternalAffairsScheduleData
        {
            CityId = 1,
            OfficerId = 102,
            JobType = InternalAffairsJobType.Farm,
            RemainingMonths = 2,
            State = InternalAffairsScheduleState.Active
        });
        world.CityIntelRecords.Add(new WorldState.CityIntelData
        {
            ViewerFactionId = 1,
            TargetCityId = 2,
            RemainingMonths = 3
        });
        world.PendingSuccessionRecords.Add(new WorldState.PendingSuccessionData
        {
            FactionId = 1,
            CandidateOfficerIds = new List<int> { 102 }
        });
        world.BattleReports.Add(new WorldState.BattleReportData
        {
            Id = 1,
            AttackerFactionId = 1,
            DefenderFactionId = 2,
            ParticipantContributions = new List<WorldState.BattleParticipantContributionData>
            {
                new()
                {
                    FactionId = 2,
                    Side = CampaignBattleSide.Defender,
                    TroopsCommitted = 800,
                    TroopsLost = 120,
                    GoldContribution = 90,
                    FoodContribution = 180,
                    BattleDaysParticipated = 3,
                    ContributionScore = 76,
                    RewardGold = 152,
                    RewardFood = 304,
                    RelationDelta = 8,
                    TrustDelta = 12,
                    PromiseFulfilled = true
                }
            }
        });

        // The harness deliberately has no Godot engine instance.  This test
        // verifies persisted campaign state, while authored relationship data
        // is covered by the live-game load path.
        var repository = new WorldRepository(refreshOfficerReferenceDataOnLoad: false);
        var savePath = Path.Combine(Path.GetTempPath(), "3kingdom_save_load_test.json");
        var saved = repository.SaveGame(savePath, world, "Regression Save", 3);
        var loadedDocument = repository.LoadSaveDocument(savePath);
        var loaded = repository.LoadSavedGame(savePath);

        var loadedCity = loaded?.GetCity(1);
        var loadedPending = loaded?.PendingCommands.SingleOrDefault();
        var loadedRelation = loaded?.GetDiplomacyRelation(1, 2);
        var loadedContribution = loaded?.BattleReports.SingleOrDefault()?.ParticipantContributions.SingleOrDefault();
        var ok = saved &&
                 loaded != null &&
                 loaded.Year == 207 &&
                 loaded.Month == 9 &&
                 loaded.RandomSeed == 321 &&
                 loaded.ViewAllInformationEnabled &&
                 loaded.StoryNameZhHant == "存讀檔劇本" &&
                 loadedDocument != null &&
                 loadedDocument.Description == "Regression Save" &&
                 loadedDocument.SlotIndex == 3 &&
                 loadedCity != null &&
                 loadedCity.Population == 54321 &&
                 loadedCity.Horses == 222 &&
                 loadedCity.InfantryTroops == 500 &&
                 loadedCity.SpearmanTroops == 200 &&
                 loadedCity.CavalryTroops == 150 &&
                 loadedCity.ArcherTroops == 180 &&
                 loadedCity.CrossbowTroops == 140 &&
                 loadedCity.SiegeTroops == 163 &&
                 loadedCity.Troops == 1333 &&
                 loadedCity.OfficerIds.SequenceEqual(new[] { 101, 102 }) &&
                 loadedPending != null &&
                 loadedPending.TargetOfficerId == 201 &&
                 loadedPending.OfficerIds.SequenceEqual(new[] { 102 }) &&
                 loadedRelation != null &&
                 loadedRelation.Status == DiplomacyStatusType.Truce &&
                 loadedRelation.RemainingMonths == 4 &&
                 loadedContribution != null &&
                 loadedContribution.ContributionScore == 76 &&
                 loadedContribution.RewardGold == 152 &&
                 loadedContribution.RewardFood == 304 &&
                 loadedContribution.RelationDelta == 8 &&
                 loadedContribution.TrustDelta == 12 &&
                 loadedContribution.PromiseFulfilled &&
                 loaded.InternalAffairsSchedules.Count == 1 &&
                 loaded.CityIntelRecords.Count == 1 &&
                 loaded.PendingSuccessionRecords.Count == 1;

        Assert(ok, "Save/load round trip", loaded == null ? "loaded=null" : $"cityPop={loadedCity?.Population}, reward={loadedContribution?.RewardGold}/{loadedContribution?.RewardFood}");
    }

    private static void RunBattleSaveSlotSummaryTest()
    {
        var world = TestHelpers.World();
        var source = TestHelpers.City(1, "Wuling", 1, 0, 0, 0, Array.Empty<int>(), new[] { 2 });
        var target = TestHelpers.City(2, "Changsha", 2, 0, 0, 0, Array.Empty<int>(), new[] { 1 });
        source.NameZhHant = "武陵";
        target.NameZhHant = "長沙";
        world.Cities.AddRange(new[] { source, target });
        var playerRuler = TestHelpers.Officer(101, "Sun Jian", source.Id);
        playerRuler.NameZhHant = "孫堅";
        world.Officers.Add(playerRuler);
        world.Factions.Add(TestHelpers.Faction(1, "Sun Jian", true, playerRuler.Id, new[] { playerRuler.Id }));
        world.Factions.Add(TestHelpers.Faction(2, "Zhang Bao", false, 0, Array.Empty<int>()));
        world.Factions[0].NameZhHant = "孫堅";
        world.Factions[1].NameZhHant = "張寶";
        world.ActiveBattleCampaigns.Add(new ActiveBattleCampaignData
        {
            Id = 1,
            AttackerFactionId = 1,
            DefenderFactionId = 2,
            SourceCityId = 1,
            TargetCityId = 2,
            BattleSnapshotJson = "{}"
        });
        var summary = WorldRepository.BuildSaveSlotSummary(new SaveGameData
        {
            SlotIndex = 9,
            Description = "Battle Save",
            World = world
        }, 9);
        Assert(
            summary.IsCampaignBattleSave && summary.BattleAttackerNameZhHant == "孫堅" &&
            summary.BattleDefenderNameZhHant == "張寶" && summary.BattleLocationNameZhHant == "長沙" &&
            summary.PlayerRulerNameZhHant == "孫堅",
            "Battle save summary includes attacker, defender, location, and player ruler",
            $"attacker={summary.BattleAttackerNameZhHant}, defender={summary.BattleDefenderNameZhHant}, location={summary.BattleLocationNameZhHant}, ruler={summary.PlayerRulerNameZhHant}");
    }
}
