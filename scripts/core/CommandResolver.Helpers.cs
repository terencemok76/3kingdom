using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public partial class CommandResolver
{
    private CommandResult LocalizedResult(bool success, string key, object[]? args = null)
    {
        return LocalizedResult(success, key, args, args);
    }

    private CommandResult LocalizedResult(bool success, string key, object[]? zhArgs, object[]? enArgs)
    {
        var traditionalArgs = zhArgs ?? Array.Empty<object>();
        var englishArgs = enArgs ?? Array.Empty<object>();
        var zh = _localization?.FormatForLanguage(GameLanguage.TraditionalChinese, key, traditionalArgs) ?? key;
        var en = _localization?.FormatForLanguage(GameLanguage.English, key, englishArgs) ?? key;

        return new CommandResult
        {
            Success = success,
            Message = en,
            MessageZhHant = zh,
            MessageEn = en
        };
    }

    private object[] GetCityArgs(CityData city, GameLanguage language)
    {
        return new object[]
        {
            GetCityName(city, language)
        };
    }

    private object[] GetOfficerArgs(OfficerData officer, GameLanguage language)
    {
        return new object[]
        {
            GetOfficerDisplayName(officer, language)
        };
    }

    private object[] GetFactionArgs(FactionData faction, GameLanguage language)
    {
        return new object[]
        {
            GetFactionName(faction, language)
        };
    }

    private static int GetAverageStat(WorldState world, CityData city, Func<OfficerData, int> selector)
    {
        var count = 0;
        var total = 0;
        foreach (var officerId in city.OfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            total += selector(officer);
            count += 1;
        }

        return count == 0 ? 50 : total / count;
    }

    private static int GetAverageEffectiveStat(WorldState world, CityData city, Func<OfficerData, int> selector, Func<ItemData, int> bonusSelector, OfficerProgressionStat progressionStat)
    {
        var count = 0;
        var total = 0;
        foreach (var officerId in city.OfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            total += GetEffectiveStat(world, officer, selector, bonusSelector, progressionStat);
            count += 1;
        }

        return count == 0 ? 50 : total / count;
    }

    private static int GetTransferAmount(int requestedAmount, int availableAmount)
    {
        var transferAmount = requestedAmount > 0 ? requestedAmount : availableAmount / 2;
        if (transferAmount > availableAmount)
        {
            transferAmount = availableAmount;
        }

        return transferAmount < 0 ? 0 : transferAmount;
    }

    private static TroopAllocationData CreateTroopAllocationFromTotal(CityData city, int requestedTroops)
    {
        var allocation = new TroopAllocationData();
        var remaining = GetTransferAmount(requestedTroops, city.Troops);
        if (remaining <= 0)
        {
            return allocation;
        }

        foreach (var troopType in new[]
                 {
                     TroopType.Infantry,
                     TroopType.Spearman,
                     TroopType.Archer,
                     TroopType.Cavalry,
                     TroopType.Crossbow,
                     TroopType.Siege
                 })
        {
            if (remaining <= 0)
            {
                break;
            }

            var available = city.GetTroops(troopType);
            if (available <= 0)
            {
                continue;
            }

            var toTake = Math.Min(available, remaining);
            SetTroopAllocationValue(allocation, troopType, toTake);
            remaining -= toTake;
        }

        return allocation;
    }

    private static TroopAllocationData CreateTroopAllocationFromAttackDeployments(IEnumerable<AttackOfficerDeploymentData> deployments)
    {
        var allocation = new TroopAllocationData();
        foreach (var deployment in deployments)
        {
            switch (deployment.TroopType)
            {
                case TroopType.Infantry:
                    allocation.Infantry += deployment.TroopCount;
                    break;
                case TroopType.Spearman:
                    allocation.Spearman += deployment.TroopCount;
                    break;
                case TroopType.Cavalry:
                    allocation.Cavalry += deployment.TroopCount;
                    break;
                case TroopType.Archer:
                    allocation.Archer += deployment.TroopCount;
                    break;
                case TroopType.Crossbow:
                    allocation.Crossbow += deployment.TroopCount;
                    break;
                case TroopType.Siege:
                    allocation.Siege += deployment.TroopCount;
                    break;
            }
        }

        return allocation;
    }

    private static void SetTroopAllocationValue(TroopAllocationData allocation, TroopType troopType, int value)
    {
        switch (troopType)
        {
            case TroopType.Infantry:
                allocation.Infantry = value;
                break;
            case TroopType.Spearman:
                allocation.Spearman = value;
                break;
            case TroopType.Cavalry:
                allocation.Cavalry = value;
                break;
            case TroopType.Archer:
                allocation.Archer = value;
                break;
            case TroopType.Crossbow:
                allocation.Crossbow = value;
                break;
            case TroopType.Siege:
                allocation.Siege = value;
                break;
        }
    }

    private static int GetTroopAllocationValue(TroopAllocationData allocation, TroopType troopType)
    {
        return troopType switch
        {
            TroopType.Infantry => allocation.Infantry,
            TroopType.Spearman => allocation.Spearman,
            TroopType.Cavalry => allocation.Cavalry,
            TroopType.Archer => allocation.Archer,
            TroopType.Crossbow => allocation.Crossbow,
            TroopType.Siege => allocation.Siege,
            _ => 0
        };
    }

    private static TroopAllocationData ScaleTroopAllocationToTotal(TroopAllocationData source, int targetTotal)
    {
        var allocation = new TroopAllocationData();
        var sourceTotal = source.Total;
        if (sourceTotal <= 0 || targetTotal <= 0)
        {
            return allocation;
        }

        if (targetTotal >= sourceTotal)
        {
            allocation.Infantry = source.Infantry;
            allocation.Spearman = source.Spearman;
            allocation.Cavalry = source.Cavalry;
            allocation.Archer = source.Archer;
            allocation.Crossbow = source.Crossbow;
            allocation.Siege = source.Siege;
            return allocation;
        }

        var troopTypes = new[]
        {
            TroopType.Infantry,
            TroopType.Spearman,
            TroopType.Cavalry,
            TroopType.Archer,
            TroopType.Crossbow,
            TroopType.Siege
        };
        var remainders = new List<(TroopType TroopType, double Fraction)>();
        var allocated = 0;
        foreach (var troopType in troopTypes)
        {
            var current = GetTroopAllocationValue(source, troopType);
            if (current <= 0)
            {
                SetTroopAllocationValue(allocation, troopType, 0);
                continue;
            }

            var scaled = current * targetTotal / (double)sourceTotal;
            var whole = (int)Math.Floor(scaled);
            SetTroopAllocationValue(allocation, troopType, whole);
            allocated += whole;
            remainders.Add((troopType, scaled - whole));
        }

        var remaining = targetTotal - allocated;
        foreach (var (troopType, _) in remainders
                     .OrderByDescending(item => item.Fraction)
                     .ThenByDescending(item => GetTroopAllocationValue(source, item.TroopType)))
        {
            if (remaining <= 0)
            {
                break;
            }

            var current = GetTroopAllocationValue(allocation, troopType);
            var sourceValue = GetTroopAllocationValue(source, troopType);
            if (current >= sourceValue)
            {
                continue;
            }

            SetTroopAllocationValue(allocation, troopType, current + 1);
            remaining -= 1;
        }

        return allocation;
    }

    private static TroopAllocationData CreateTroopAllocationFromCityProportion(CityData city, int targetTotal)
    {
        return ScaleTroopAllocationToTotal(new TroopAllocationData
        {
            Infantry = city.InfantryTroops,
            Spearman = city.SpearmanTroops,
            Cavalry = city.CavalryTroops,
            Archer = city.ArcherTroops,
            Crossbow = city.CrossbowTroops,
            Siege = city.SiegeTroops
        }, targetTotal);
    }

    private static int GetTroopTypeRecruitGoldCost(TroopType troopType)
    {
        return troopType switch
        {
            TroopType.Cavalry => RecruitGoldCost + 40,
            TroopType.Crossbow => RecruitGoldCost + 20,
            TroopType.Siege => RecruitGoldCost + 80,
            _ => RecruitGoldCost
        };
    }

    private static int GetTroopTypeRecruitFoodCost(TroopType troopType)
    {
        return troopType switch
        {
            TroopType.Cavalry => RecruitFoodCost + 40,
            TroopType.Siege => RecruitFoodCost + 60,
            _ => RecruitFoodCost
        };
    }

    private static string GetTroopTypeLocaleKey(TroopType troopType)
    {
        return troopType switch
        {
            TroopType.Infantry => "troop_type.infantry",
            TroopType.Spearman => "troop_type.spearman",
            TroopType.Cavalry => "troop_type.cavalry",
            TroopType.Archer => "troop_type.archer",
            TroopType.Crossbow => "troop_type.crossbow",
            TroopType.Siege => "troop_type.siege",
            _ => "troop_type.infantry"
        };
    }

    private string GetTroopTypeName(TroopType troopType, GameLanguage language)
    {
        var key = GetTroopTypeLocaleKey(troopType);
        return _localization?.TForLanguage(language, key) ?? troopType.ToString();
    }

    private static bool CanRecruitTroopType(CityData city, TroopType troopType)
    {
        return troopType switch
        {
            TroopType.Cavalry => city.Horses > 0,
            TroopType.Crossbow => city.HasBowWorkshop,
            TroopType.Siege => city.HasSiegeWorkshop,
            _ => true
        };
    }

    private static List<int> GetMovableOfficerIds(CityData sourceCity, List<int> requestedOfficerIds)
    {
        var result = new List<int>();
        foreach (var officerId in requestedOfficerIds)
        {
            if (!sourceCity.OfficerIds.Contains(officerId) || result.Contains(officerId))
            {
                continue;
            }

            result.Add(officerId);
        }

        return result;
    }

    private static bool AreOfficerIdsAvailableForPendingOrder(
        WorldState world,
        List<int> requestedOfficerIds)
    {
        if (requestedOfficerIds.Count == 0)
        {
            return true;
        }

        foreach (var officerId in requestedOfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null ||
                IsOfficerAssignedThisMonth(world, officer) ||
                HasActiveInternalAffairsSchedule(world, officerId))
            {
                return false;
            }
        }

        return true;
    }

    private static OfficerData? GetSingleAvailableOfficer(WorldState world, CityData city, List<int> requestedOfficerIds)
    {
        if (requestedOfficerIds.Count != 1)
        {
            return null;
        }

        var officerId = requestedOfficerIds[0];
        if (!city.OfficerIds.Contains(officerId))
        {
            return null;
        }

        var officer = world.GetOfficer(officerId);
        if (officer == null ||
            IsOfficerAssignedThisMonth(world, officer) ||
            HasActiveInternalAffairsSchedule(world, officerId))
        {
            return null;
        }

        return officer;
    }

    private static bool IsOfficerAssignedThisMonth(WorldState world, OfficerData officer)
    {
        return officer.LastAssignedYear == world.Year && officer.LastAssignedMonth == world.Month;
    }

    private static bool HasActiveInternalAffairsSchedule(WorldState world, int officerId)
    {
        return world.InternalAffairsSchedules.Any(schedule =>
            schedule.State == InternalAffairsScheduleState.Active &&
            schedule.OfficerId == officerId);
    }

    private static bool HasActiveInternalAffairsJob(WorldState world, int cityId, InternalAffairsJobType jobType)
    {
        return world.InternalAffairsSchedules.Any(schedule =>
            schedule.State == InternalAffairsScheduleState.Active &&
            schedule.CityId == cityId &&
            schedule.JobType == jobType);
    }

    private static bool IsFactionRuler(WorldState world, int officerId)
    {
        return world.Factions.Any(faction => faction.RulerOfficerId == officerId);
    }

    private static bool IsFactionAlive(WorldState world, int factionId)
    {
        return world.Cities.Any(city => city.OwnerFactionId == factionId);
    }

    private static DiplomacyRelationData GetOrCreateDiplomacyRelation(WorldState world, int factionAId, int factionBId)
    {
        var low = Math.Min(factionAId, factionBId);
        var high = Math.Max(factionAId, factionBId);
        var existing = world.DiplomacyRelations.FirstOrDefault(relation =>
            relation.FactionAId == low &&
            relation.FactionBId == high);
        if (existing != null)
        {
            return existing;
        }

        var relation = new DiplomacyRelationData
        {
            FactionAId = low,
            FactionBId = high
        };
        world.DiplomacyRelations.Add(relation);
        return relation;
    }

    private static DiplomacyRelationData? FindDiplomacyRelation(WorldState world, int factionAId, int factionBId)
    {
        var low = Math.Min(factionAId, factionBId);
        var high = Math.Max(factionAId, factionBId);
        return world.DiplomacyRelations.FirstOrDefault(relation =>
            relation.FactionAId == low &&
            relation.FactionBId == high);
    }

    private static bool HasActiveDiplomacyBlock(WorldState world, int factionAId, int factionBId)
    {
        var relation = FindDiplomacyRelation(world, factionAId, factionBId);
        return relation != null &&
               relation.Status is DiplomacyStatusType.Truce or DiplomacyStatusType.Alliance &&
               relation.RemainingMonths > 0;
    }

    private static bool IsAssignableRole(string role)
    {
        return role.Equals("General", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("Strategist", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("Advisor", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("Governor", StringComparison.OrdinalIgnoreCase);
    }

    private string GetOfficerRoleName(string role, GameLanguage language)
    {
        var key = role.ToLowerInvariant() switch
        {
            "general" => "role.general",
            "strategist" => "role.strategist",
            "advisor" => "role.advisor",
            "governor" => "role.governor",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(key)
            ? role
            : _localization?.FormatForLanguage(language, key) ?? role;
    }

    private static int GetNextInternalAffairsScheduleId(WorldState world)
    {
        return world.InternalAffairsSchedules.Count == 0
            ? 1
            : world.InternalAffairsSchedules.Max(schedule => schedule.Id) + 1;
    }

    private static (int Farm, int Commercial, int Defense, int DisasterPrevention, int Loyalty) ApplyInternalAffairsJob(
        WorldState world,
        CityData city,
        OfficerData officer,
        InternalAffairsJobType jobType)
    {
        var intelligence = GetEffectiveStat(world, officer, data => data.Intelligence, item => item.IntelligenceBonus, OfficerProgressionStat.Intelligence);
        var politics = GetEffectiveStat(world, officer, data => data.Politics, item => item.PoliticsBonus, OfficerProgressionStat.Politics);
        var charm = GetEffectiveStat(world, officer, data => data.Charm, item => item.CharmBonus, OfficerProgressionStat.Charm);
        var officerBonus = Math.Max(0, (intelligence + politics + charm) / 90);
        var progressionBonus = OfficerProgressionRules.GetInternalAffairsOutputBonus(officer, jobType);
        var primaryGain = 2 + officerBonus + progressionBonus;
        var secondaryGain = 1 + Math.Max(0, progressionBonus / 2);
        (int Farm, int Commercial, int Defense, int DisasterPrevention, int Loyalty) gains = jobType switch
        {
            InternalAffairsJobType.Farm => (primaryGain, 0, 0, 0, 0),
            InternalAffairsJobType.Commercial => (0, primaryGain, 0, 0, 0),
            InternalAffairsJobType.Defend => (0, 0, primaryGain, 0, 0),
            InternalAffairsJobType.WaterControl => (0, 0, 0, primaryGain, secondaryGain),
            InternalAffairsJobType.Construction => (0, secondaryGain, secondaryGain, secondaryGain, 0),
            _ => (0, 0, 0, 0, 0)
        };

        city.Farm = ClampStat(city.Farm + gains.Farm);
        city.Commercial = ClampStat(city.Commercial + gains.Commercial);
        city.Defense = ClampStat(city.Defense + gains.Defense);
        city.DisasterPrevention = ClampStat(city.DisasterPrevention + gains.DisasterPrevention);
        city.Loyalty = ClampStat(city.Loyalty + gains.Loyalty);
        OfficerProgressionRules.AwardInternalAffairsExperience(officer, jobType, 40);
        OfficerProgressionRules.AwardCivilExperience(officer, 12);
        return gains;
    }

    private string GetInternalAffairsJobName(InternalAffairsJobType jobType, GameLanguage language)
    {
        var key = jobType switch
        {
            InternalAffairsJobType.Farm => "command.internal_affairs.farm",
            InternalAffairsJobType.Commercial => "command.internal_affairs.commercial",
            InternalAffairsJobType.Defend => "command.internal_affairs.defend",
            InternalAffairsJobType.WaterControl => "command.internal_affairs.disaster_prevention",
            InternalAffairsJobType.Construction => "command.internal_affairs.construction",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(key)
            ? jobType.ToString()
            : _localization?.FormatForLanguage(language, key) ?? jobType.ToString();
    }

    private static void MarkOfficerAssigned(WorldState world, OfficerData officer, CommandType commandType)
    {
        officer.LastAssignedYear = world.Year;
        officer.LastAssignedMonth = world.Month;
        officer.LastAssignedCommand = commandType;
    }

    private static void MarkOfficersAssigned(WorldState world, List<int> officerIds, CommandType commandType)
    {
        foreach (var officerId in officerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            MarkOfficerAssigned(world, officer, commandType);
        }
    }

    private static int TransferOfficers(
        WorldState world,
        CityData sourceCity,
        CityData targetCity,
        List<int> requestedOfficerIds)
    {
        var movedOfficerCount = 0;
        foreach (var officerId in requestedOfficerIds)
        {
            if (!sourceCity.OfficerIds.Contains(officerId))
            {
                continue;
            }

            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            sourceCity.OfficerIds.Remove(officerId);
            if (!targetCity.OfficerIds.Contains(officerId))
            {
                targetCity.OfficerIds.Add(officerId);
            }

            officer.CityId = targetCity.Id;
            movedOfficerCount += 1;
        }

        return movedOfficerCount;
    }

    private static int GetEffectiveStat(
        WorldState world,
        OfficerData officer,
        Func<OfficerData, int> selector,
        Func<ItemData, int> bonusSelector,
        OfficerProgressionStat progressionStat)
    {
        var baseValue = selector(officer);
        var itemBonus = 0;
        foreach (var item in GetEquippedItems(world, officer.Id))
        {
            itemBonus += bonusSelector(item);
        }

        return ClampStat(baseValue + itemBonus + OfficerProgressionRules.GetStatBonus(officer, progressionStat));
    }

    private static IEnumerable<ItemData> GetEquippedItems(WorldState world, int officerId)
    {
        return world.Items.Where(item => item.EquippedOfficerId == officerId);
    }

    private static ItemData? GetEquippedItemInSlot(WorldState world, int officerId, ItemType itemType)
    {
        return world.Items.FirstOrDefault(item =>
            item.EquippedOfficerId == officerId &&
            AreItemsInSameSlot(item.ItemType, itemType));
    }

    private static bool AreItemsInSameSlot(ItemType a, ItemType b)
    {
        return GetItemSlotKey(a) == GetItemSlotKey(b);
    }

    private static string GetItemSlotKey(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Weapon => "weapon",
            ItemType.Horse => "horse",
            ItemType.Book => "special",
            ItemType.Treasure => "special",
            _ => "special"
        };
    }

    private static bool IsItemOwnedByFactionInventory(ItemData item, int factionId)
    {
        return item.OwnerFactionId == factionId &&
               item.EquippedOfficerId <= 0;
    }

    private static void MoveItemToFactionInventory(ItemData item, int factionId)
    {
        item.OwnerFactionId = factionId;
        item.OwnerCityId = 0;
        item.EquippedOfficerId = 0;
    }

    private static void EquipItemToOfficer(ItemData item, int factionId, int officerId)
    {
        item.OwnerFactionId = factionId;
        item.OwnerCityId = 0;
        item.EquippedOfficerId = officerId;
    }

    private static void AssignItemToOfficer(WorldState world, ItemData item, int factionId, int officerId)
    {
        var existingItem = GetEquippedItemInSlot(world, officerId, item.ItemType);
        if (existingItem != null && existingItem.Id != item.Id)
        {
            MoveItemToFactionInventory(existingItem, factionId);
        }

        EquipItemToOfficer(item, factionId, officerId);
    }

    private static void TransferEquippedItemsToFaction(WorldState world, int officerId, int factionId)
    {
        foreach (var item in world.Items.Where(item => item.EquippedOfficerId == officerId))
        {
            item.OwnerFactionId = factionId;
            item.OwnerCityId = 0;
        }
    }

    private static void AwardBattleExperience(WorldState world, IEnumerable<int> officerIds, int amount)
    {
        foreach (var officerId in officerIds.Distinct())
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            OfficerProgressionRules.AwardBattleExperience(officer, amount);
        }
    }

    private ItemData? TryFindDiscoverableItem(WorldState world, int cityId)
    {
        var candidates = world.Items
            .Where(item => item.OwnerFactionId <= 0 && item.OwnerCityId == cityId && item.EquippedOfficerId <= 0)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[_random.Next(candidates.Count)];
    }

    private static int GetItemGiftAcceptanceBonus(ItemData? item)
    {
        if (item == null)
        {
            return 0;
        }

        var rarityBonus = item.Rarity.ToLowerInvariant() switch
        {
            "epic" => 10,
            "rare" => 6,
            _ => 3
        };

        return rarityBonus +
               item.CharmBonus +
               item.LoyaltyBonus +
               (item.StrengthBonus + item.IntelligenceBonus + item.LeadershipBonus + item.PoliticsBonus + item.CombatBonus) / 2;
    }

    private string GetItemDisplayName(ItemData item, GameLanguage language)
    {
        if (language == GameLanguage.TraditionalChinese)
        {
            return !string.IsNullOrWhiteSpace(item.NameZhHant) ? item.NameZhHant : item.NameEn;
        }

        return !string.IsNullOrWhiteSpace(item.NameEn) ? item.NameEn : item.NameZhHant;
    }

    private OfficerData? TryFindDiscoverableOfficer(WorldState world, int factionId, int cityId)
    {
        var candidates = new List<OfficerData>();
        foreach (var officer in world.Officers)
        {
            if (!FreeOfficerMovement.IsFreeOfficer(world, officer))
            {
                continue;
            }

            if (officer.CityId > 0 && officer.CityId != cityId)
            {
                continue;
            }

            candidates.Add(officer);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var preferred = new List<OfficerData>();
        foreach (var officer in candidates)
        {
            if (MatchesFaction(officer.Belongs, factionId))
            {
                preferred.Add(officer);
            }
        }

        var pool = preferred.Count > 0 ? preferred : candidates;
        return pool[_random.Next(pool.Count)];
    }

    private void RevealFreeOfficerAtCity(CityData city, OfficerData officer)
    {
        officer.CityId = city.Id;
        officer.FreeOfficerStayMonths = Math.Max(officer.FreeOfficerStayMonths, 1);
    }

    private void RecruitFreeOfficerToCity(WorldState world, CityData city, OfficerData officer)
    {
        officer.CityId = city.Id;
        officer.FreeOfficerStayMonths = 0;
        officer.Loyalty = ClampStat(65 + _random.Next(0, 16));

        if (!city.OfficerIds.Contains(officer.Id))
        {
            city.OfficerIds.Add(officer.Id);
        }

        var faction = world.GetFaction(city.OwnerFactionId);
        if (faction != null && !faction.OfficerIds.Contains(officer.Id))
        {
            faction.OfficerIds.Add(officer.Id);
        }
    }

    private static bool DoesFreeOfficerAcceptHire(CityData city, OfficerData officer, int rulerCharm, int goldOffer, int foodOffer, ItemData? giftedItem)
    {
        var offerBonus = goldOffer / 50 + foodOffer / 250;
        return city.Loyalty + officer.Charm + rulerCharm / 2 + offerBonus + GetItemGiftAcceptanceBonus(giftedItem) - officer.Ambition >= 80;
    }

    private static bool DoesEmployedOfficerAcceptHire(OfficerData officer, int rulerCharm, int goldOffer, int foodOffer, ItemData? giftedItem)
    {
        var offerBonus = goldOffer / 40 + foodOffer / 200;
        return rulerCharm + officer.Charm + offerBonus + GetItemGiftAcceptanceBonus(giftedItem) - officer.Loyalty - officer.Ambition / 2 >= 40;
    }

    private static int GetRulerCharm(WorldState world, int factionId)
    {
        var faction = world.GetFaction(factionId);
        if (faction == null)
        {
            return 50;
        }

        var ruler = world.GetOfficer(faction.RulerOfficerId);
        return ruler?.Charm ?? 50;
    }

    private static bool MatchesFaction(string belongs, int factionId)
    {
        return factionId switch
        {
            1 => belongs.Equals("Shu", StringComparison.OrdinalIgnoreCase),
            2 => belongs.Equals("Wei", StringComparison.OrdinalIgnoreCase),
            3 => belongs.Equals("Wu", StringComparison.OrdinalIgnoreCase),
            4 => belongs.Equals("YellowTurban", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool IsOfficerOldEnoughToJoin(WorldState world, OfficerData officer)
    {
        if (officer.BirthYear <= 0)
        {
            return true;
        }

        return world.Year - officer.BirthYear >= 18;
    }

    private static string GetCityName(CityData city, GameLanguage language)
    {
        if (language == GameLanguage.TraditionalChinese)
        {
            if (!string.IsNullOrWhiteSpace(city.NameZhHant))
            {
                return city.NameZhHant;
            }

            if (!string.IsNullOrWhiteSpace(city.Name))
            {
                return city.Name;
            }

            return city.NameEn;
        }

        if (!string.IsNullOrWhiteSpace(city.NameEn))
        {
            return city.NameEn;
        }

        if (!string.IsNullOrWhiteSpace(city.Name))
        {
            return city.Name;
        }

        return city.NameZhHant;
    }

    private static string GetFactionName(FactionData faction, GameLanguage language)
    {
        if (language == GameLanguage.TraditionalChinese)
        {
            return !string.IsNullOrWhiteSpace(faction.NameZhHant)
                ? faction.NameZhHant
                : faction.NameEn;
        }

        return !string.IsNullOrWhiteSpace(faction.NameEn)
            ? faction.NameEn
            : faction.NameZhHant;
    }

    private static string GetOfficerDisplayName(OfficerData officer, GameLanguage language)
    {
        if (language == GameLanguage.TraditionalChinese)
        {
            if (!string.IsNullOrWhiteSpace(officer.NameZhHant))
            {
                return officer.NameZhHant;
            }

            return officer.Name;
        }

        return !string.IsNullOrWhiteSpace(officer.Name) ? officer.Name : officer.NameZhHant;
    }

    private string GetDiplomacyActionName(DiplomacyActionType actionType, GameLanguage language)
    {
        var key = actionType switch
        {
            DiplomacyActionType.Alliance => "command.diplomacy.alliance",
            DiplomacyActionType.Truce => "command.diplomacy.truce",
            DiplomacyActionType.Gift => "command.diplomacy.gift",
            _ => "command.diplomacy.alliance"
        };
        return _localization?.TForLanguage(language, key) ?? actionType.ToString();
    }

    private static string GetRulerDisplayName(WorldState world, int factionId, GameLanguage language)
    {
        var faction = world.GetFaction(factionId);
        if (faction == null)
        {
            return factionId > 0 ? factionId.ToString() : "-";
        }

        var ruler = world.GetOfficer(faction.RulerOfficerId);
        if (ruler != null)
        {
            return GetOfficerDisplayName(ruler, language);
        }

        if (language == GameLanguage.TraditionalChinese && !string.IsNullOrWhiteSpace(faction.NameZhHant))
        {
            return faction.NameZhHant;
        }

        return !string.IsNullOrWhiteSpace(faction.NameEn) ? faction.NameEn : faction.NameZhHant;
    }

    private static void ResolveCapturedCityOfficers(WorldState world, CityData capturedCity, int previousFactionId)
    {
        if (capturedCity.OfficerIds.Count == 0)
        {
            return;
        }

        var retreatCity = FindRetreatCity(world, previousFactionId, capturedCity.Id);
        var displacedOfficerIds = new List<int>(capturedCity.OfficerIds);
        capturedCity.OfficerIds.Clear();

        foreach (var officerId in displacedOfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (retreatCity != null)
            {
                officer.CityId = retreatCity.Id;
                if (!retreatCity.OfficerIds.Contains(officer.Id))
                {
                    retreatCity.OfficerIds.Add(officer.Id);
                }
            }
            else
            {
                officer.CityId = 0;
            }
        }
    }

    private static CityData? FindRetreatCity(WorldState world, int factionId, int excludedCityId)
    {
        foreach (var city in world.Cities)
        {
            if (city.Id != excludedCityId && city.OwnerFactionId == factionId)
            {
                return city;
            }
        }

        return null;
    }

    private static bool HasUsedDevelop(WorldState world, CityData city)
    {
        return city.LastDevelopYear == world.Year && city.LastDevelopMonth == world.Month;
    }

    private static void MarkDevelopUsed(WorldState world, CityData city)
    {
        city.LastDevelopYear = world.Year;
        city.LastDevelopMonth = world.Month;
    }

    private static bool HasUsedRecruit(WorldState world, CityData city)
    {
        return city.LastRecruitYear == world.Year && city.LastRecruitMonth == world.Month;
    }

    private static void MarkRecruitUsed(WorldState world, CityData city)
    {
        city.LastRecruitYear = world.Year;
        city.LastRecruitMonth = world.Month;
    }

    private static bool HasUsedSearch(WorldState world, CityData city)
    {
        return city.LastSearchYear == world.Year && city.LastSearchMonth == world.Month;
    }

    private static void MarkSearchUsed(WorldState world, CityData city)
    {
        city.LastSearchYear = world.Year;
        city.LastSearchMonth = world.Month;
    }

    private static bool HasUsedCivilRelief(WorldState world, CityData city)
    {
        return city.LastCivilReliefYear == world.Year && city.LastCivilReliefMonth == world.Month;
    }

    private static void MarkCivilReliefUsed(WorldState world, CityData city)
    {
        city.LastCivilReliefYear = world.Year;
        city.LastCivilReliefMonth = world.Month;
    }

    private static void UpsertPendingCommand(WorldState world, PendingCommandData pendingCommand)
    {
        // Military orders can stack per source city; core city actions stay one-pending-per-type.
        if (pendingCommand.Type == CommandType.Move || pendingCommand.Type == CommandType.Attack)
        {
            world.PendingCommands.Add(pendingCommand);
            return;
        }

        world.PendingCommands.RemoveAll(existing =>
            existing.SourceCityId == pendingCommand.SourceCityId &&
            existing.Type == pendingCommand.Type);
        world.PendingCommands.Add(pendingCommand);
    }

    private static bool IsConnected(CityData source, int targetCityId)
    {
        return source.ConnectedCityIds.Contains(targetCityId);
    }

    private static int ClampStat(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        if (value > 100)
        {
            return 100;
        }

        return value;
    }

}
