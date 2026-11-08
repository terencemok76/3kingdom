using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public partial class CommandResolver
{
    public const int OfficerLetterCost = 50;
    public const int OfficerGiftCost = 150;
    public const int OfficerLetterGain = 5;
    public const int OfficerGiftGain = 12;

    private static bool CanContactOfficer(WorldState world, int actorFactionId, OfficerData? target)
    {
        if (target == null || target.CaptiveFactionId > 0 || !IsOfficerAlive(world, target) ||
            IsFactionRuler(world, target.Id) || !IsOfficerOldEnoughToJoin(world, target) ||
            BattleCampaignService.IsOfficerCommitted(world, target.Id) || OfficerAvailabilityRules.IsTravelingWithLogistics(world, target.Id)) return false;
        var city = world.GetCity(target.CityId);
        if (city == null || city.OwnerFactionId == actorFactionId) return false;
        return !FreeOfficerMovement.IsFreeOfficer(world, target) || FreeOfficerMovement.IsVisibleFreeOfficer(world, target);
    }

    public CommandResult ScheduleOfficerContact(int factionId, int cityId, int targetId, int envoyId, bool gift)
    {
        var world = _turnManager?.World;
        if (world == null) return LocalizedResult(false, "cmd.world_not_initialized");
        var city = world.GetCity(cityId);
        if (city == null || city.OwnerFactionId != factionId) return LocalizedResult(false, "cmd.city_not_controlled");
        var target = world.GetOfficer(targetId);
        if (!CanContactOfficer(world, factionId, target)) return LocalizedResult(false, "cmd.contact.invalid");
        var envoy = GetSingleAvailableOfficer(world, city, new List<int> { envoyId });
        if (envoy == null || envoy.Id == targetId) return LocalizedResult(false, "cmd.hire_officer.envoy_required", GetCityArgs(city, GameLanguage.TraditionalChinese), GetCityArgs(city, GameLanguage.English));
        var cost = gift ? OfficerGiftCost : OfficerLetterCost;
        var pendingContactCost = world.PendingCommands.Where(p => p.Type == CommandType.ContactOfficer && p.SourceCityId == cityId && p.ActorFactionId == factionId).Sum(p => Math.Max(0, p.GoldToSend));
        if (city.Gold - pendingContactCost < cost) return LocalizedResult(false, "cmd.contact.gold");
        var currentMonth = world.Year * 12 + world.Month;
        if (world.OfficerAffectionRecords.Any(record => record.OfficerId == targetId && record.LastContactMonth == currentMonth) ||
            world.PendingCommands.Any(p => p.Type == CommandType.ContactOfficer && p.TargetOfficerId == targetId))
            return LocalizedResult(false, "cmd.contact.cooldown");
        MarkOfficerAssigned(world, envoy, CommandType.ContactOfficer);
        UpsertPendingCommand(world, new PendingCommandData { Type = CommandType.ContactOfficer, ActorFactionId = factionId, SourceCityId = cityId, TargetOfficerId = targetId, TargetCityId = target!.CityId, GoldToSend = cost, OfficerIds = new List<int> { envoy.Id } });
        return LocalizedResult(true, "cmd.contact.scheduled");
    }

    private CommandResult ResolveOfficerContact(WorldState world, CityData city, PendingCommandData pending)
    {
        var target = world.GetOfficer(pending.TargetOfficerId);
        var envoy = world.GetOfficer(pending.OfficerIds.FirstOrDefault());
        if (city.OwnerFactionId != pending.ActorFactionId || !CanContactOfficer(world, pending.ActorFactionId, target) ||
            target!.CityId != pending.TargetCityId || envoy == null || envoy.CityId != city.Id ||
            envoy.CaptiveFactionId > 0 || !IsOfficerAlive(world, envoy) || !city.OfficerIds.Contains(envoy.Id))
            return LocalizedResult(false, "cmd.contact.cancelled");
        var cost = pending.GoldToSend;
        if ((cost != OfficerLetterCost && cost != OfficerGiftCost) || city.Gold < cost) return LocalizedResult(false, "cmd.contact.gold");
        var now = world.Year * 12 + world.Month;
        var record = world.GetOfficerAffection(pending.ActorFactionId, target.Id);
        if (world.OfficerAffectionRecords.Any(item => item.OfficerId == target.Id && item.LastContactMonth == now)) return LocalizedResult(false, "cmd.contact.cooldown");
        if (record == null)
        {
            record = new WorldState.OfficerAffectionData { FactionId = pending.ActorFactionId, OfficerId = target.Id };
            world.OfficerAffectionRecords.Add(record);
        }
        city.Gold -= cost;
        var gain = Math.Min(100 - record.Score, cost == OfficerGiftCost ? OfficerGiftGain : OfficerLetterGain);
        record.Score += gain;
        record.LastContactMonth = now;
        var result = LocalizedResult(true, "cmd.contact.success",
            new object[] { GetOfficerDisplayName(target, GameLanguage.TraditionalChinese), cost, gain, record.Score },
            new object[] { GetOfficerDisplayName(target, GameLanguage.English), cost, gain, record.Score });
        var speechKey = record.Score >= 60 ? "cmd.contact.speech_warm" :
            !FreeOfficerMovement.IsFreeOfficer(world, target) && target.Loyalty >= 85 ? "cmd.contact.speech_loyal" :
            cost == OfficerGiftCost ? "cmd.contact.speech_gift" : "cmd.contact.speech_letter";
        result.MessageZhHant += "\n" + (_localization?.FormatForLanguage(GameLanguage.TraditionalChinese, speechKey, GetOfficerDisplayName(target, GameLanguage.TraditionalChinese)) ?? string.Empty);
        result.MessageEn += "\n" + (_localization?.FormatForLanguage(GameLanguage.English, speechKey, GetOfficerDisplayName(target, GameLanguage.English)) ?? string.Empty);
        result.Message = result.MessageEn;
        var targetFactionId = world.GetCity(target.CityId)?.OwnerFactionId ?? 0;
        if (cost == OfficerGiftCost && !FreeOfficerMovement.IsFreeOfficer(world, target) && targetFactionId > 0 && _random.Next(100) < 10)
        {
            result.SpyExposed = true;
            ApplySpyExposurePenalty(world, pending.ActorFactionId, envoy, targetFactionId, 6, 3);
            result.MessageZhHant += " " + (_localization?.TForLanguage(GameLanguage.TraditionalChinese, "cmd.spy.exposed_suffix") ?? string.Empty);
            result.MessageEn += " " + (_localization?.TForLanguage(GameLanguage.English, "cmd.spy.exposed_suffix") ?? string.Empty);
            result.Message = result.MessageEn;
        }
        return result;
    }
}
