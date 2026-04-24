using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class AiController
{
    private CommandResolver? _commandResolver;
    private TurnManager? _turnManager;
    private LocalizationService? _localization;

    public void Initialize(CommandResolver commandResolver, TurnManager turnManager, LocalizationService localization)
    {
        _commandResolver = commandResolver;
        _turnManager = turnManager;
        _localization = localization;
    }

    public CommandResult RunSingleCityDecision(int factionId, int cityId)
    {
        if (_commandResolver == null || _turnManager?.World == null)
        {
            return LocalizedResult(false, "cmd.ai_not_initialized");
        }

        var world = _turnManager.World;
        var city = world.GetCity(cityId);
        if (city == null)
        {
            return LocalizedResult(false, "cmd.ai_city_not_found");
        }

        var availableOfficerIds = GetAvailableOfficerIds(world, city);

        CommandResult? militaryResult = null;
        foreach (var targetId in city.ConnectedCityIds)
        {
            var target = world.GetCity(targetId);
            if (target == null)
            {
                continue;
            }

            if (target.OwnerFactionId == factionId)
            {
                continue;
            }

            if (city.Troops > target.Troops + 300 && availableOfficerIds.Count > 0)
            {
                militaryResult = _commandResolver.Execute(new CommandRequest
                {
                    Type = CommandType.Attack,
                    ActorFactionId = factionId,
                    SourceCityId = cityId,
                    TargetCityId = targetId,
                    TroopsToSend = city.Troops / 2,
                    OfficerIds = new System.Collections.Generic.List<int>(availableOfficerIds)
                });
                break;
            }
        }

        if (militaryResult == null)
        {
            foreach (var targetId in city.ConnectedCityIds)
            {
                var target = world.GetCity(targetId);
                if (target == null || target.OwnerFactionId != factionId)
                {
                    continue;
                }

                if (city.Troops > target.Troops + 800)
                {
                    militaryResult = _commandResolver.Execute(new CommandRequest
                    {
                        Type = CommandType.Move,
                        ActorFactionId = factionId,
                        SourceCityId = cityId,
                        TargetCityId = targetId,
                        TroopsToSend = city.Troops / 2,
                        GoldToSend = city.Gold / 3,
                        FoodToSend = city.Food / 3
                    });
                    break;
                }
            }
        }

        var coreResults = new System.Collections.Generic.List<CommandResult>();
        var recruitOfficerId = GetBestOfficerId(world, city, availableOfficerIds, officer => officer.Charm + officer.Leadership);
        var developOfficerId = GetBestOfficerId(world, city, availableOfficerIds, officer => officer.Intelligence + officer.Politics);
        var searchOfficerId = GetBestOfficerId(world, city, availableOfficerIds, officer => officer.Intelligence + officer.Charm);
        if (city.Troops < 2200 &&
            city.Gold >= 120 &&
            city.Food >= 80 &&
            recruitOfficerId > 0 &&
            !(city.LastRecruitYear == world.Year && city.LastRecruitMonth == world.Month))
        {
            coreResults.Add(_commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Recruit,
                ActorFactionId = factionId,
                SourceCityId = cityId,
                OfficerIds = new System.Collections.Generic.List<int> { recruitOfficerId }
            }));
            availableOfficerIds.Remove(recruitOfficerId);
            if (developOfficerId == recruitOfficerId)
            {
                developOfficerId = GetBestOfficerId(world, city, availableOfficerIds, officer => officer.Intelligence + officer.Politics);
            }

            if (searchOfficerId == recruitOfficerId)
            {
                searchOfficerId = GetBestOfficerId(world, city, availableOfficerIds, officer => officer.Intelligence + officer.Charm);
            }
        }

        if (city.Gold >= 100 &&
            developOfficerId > 0 &&
            !(city.LastDevelopYear == world.Year && city.LastDevelopMonth == world.Month))
        {
            coreResults.Add(_commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Develop,
                ActorFactionId = factionId,
                SourceCityId = cityId,
                OfficerIds = new System.Collections.Generic.List<int> { developOfficerId }
            }));
            availableOfficerIds.Remove(developOfficerId);
            if (searchOfficerId == developOfficerId)
            {
                searchOfficerId = GetBestOfficerId(world, city, availableOfficerIds, officer => officer.Intelligence + officer.Charm);
            }
        }

        if (searchOfficerId > 0 &&
            !(city.LastSearchYear == world.Year && city.LastSearchMonth == world.Month))
        {
            coreResults.Add(_commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Search,
                ActorFactionId = factionId,
                SourceCityId = cityId,
                OfficerIds = new System.Collections.Generic.List<int> { searchOfficerId }
            }));
        }

        if (coreResults.Count == 0)
        {
            coreResults.Add(_commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Pass,
                ActorFactionId = factionId,
                SourceCityId = cityId
            }));
        }

        if (militaryResult == null)
        {
            return CombineResults(coreResults);
        }

        var messages = new System.Collections.Generic.List<string>();
        var messagesZh = new System.Collections.Generic.List<string>();
        var messagesEn = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(militaryResult.Message))
        {
            messages.Add(militaryResult.Message);
            if (!string.IsNullOrWhiteSpace(militaryResult.MessageZhHant))
            {
                messagesZh.Add(militaryResult.MessageZhHant);
            }

            if (!string.IsNullOrWhiteSpace(militaryResult.MessageEn))
            {
                messagesEn.Add(militaryResult.MessageEn);
            }
        }

        foreach (var coreResult in coreResults)
        {
            if (string.IsNullOrWhiteSpace(coreResult.Message) ||
                coreResult.Message.Equals("Pass", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            messages.Add(coreResult.Message);
            if (!string.IsNullOrWhiteSpace(coreResult.MessageZhHant))
            {
                messagesZh.Add(coreResult.MessageZhHant);
            }

            if (!string.IsNullOrWhiteSpace(coreResult.MessageEn))
            {
                messagesEn.Add(coreResult.MessageEn);
            }
        }

        var anyCoreSuccess = false;
        foreach (var result in coreResults)
        {
            anyCoreSuccess |= result.Success;
        }

        return new CommandResult
        {
            Success = militaryResult.Success || anyCoreSuccess,
            Message = messages.Count > 0 ? string.Join(" | ", messages) : (_localization?.TForLanguage(GameLanguage.English, "cmd.pass") ?? "Pass"),
            MessageZhHant = messagesZh.Count > 0 ? string.Join(" | ", messagesZh) : (_localization?.TForLanguage(GameLanguage.TraditionalChinese, "cmd.pass") ?? "Pass"),
            MessageEn = messagesEn.Count > 0 ? string.Join(" | ", messagesEn) : (_localization?.TForLanguage(GameLanguage.English, "cmd.pass") ?? "Pass")
        };
    }

    private CommandResult CombineResults(System.Collections.Generic.List<CommandResult> results)
    {
        if (results.Count == 0)
        {
            return LocalizedResult(true, "cmd.pass");
        }

        if (results.Count == 1)
        {
            return results[0];
        }

        var messages = new System.Collections.Generic.List<string>();
        var messagesZh = new System.Collections.Generic.List<string>();
        var messagesEn = new System.Collections.Generic.List<string>();
        var anySuccess = false;

        foreach (var result in results)
        {
            anySuccess |= result.Success;
            if (!string.IsNullOrWhiteSpace(result.Message) &&
                !result.Message.Equals("Pass", System.StringComparison.OrdinalIgnoreCase))
            {
                messages.Add(result.Message);
            }

            if (!string.IsNullOrWhiteSpace(result.MessageZhHant) &&
                !result.MessageZhHant.Equals(_localization?.TForLanguage(GameLanguage.TraditionalChinese, "cmd.pass"), System.StringComparison.OrdinalIgnoreCase))
            {
                messagesZh.Add(result.MessageZhHant);
            }

            if (!string.IsNullOrWhiteSpace(result.MessageEn) &&
                !result.MessageEn.Equals("Pass", System.StringComparison.OrdinalIgnoreCase))
            {
                messagesEn.Add(result.MessageEn);
            }
        }

        return new CommandResult
        {
            Success = anySuccess,
            Message = messages.Count > 0 ? string.Join(" | ", messages) : (_localization?.TForLanguage(GameLanguage.English, "cmd.pass") ?? "Pass"),
            MessageZhHant = messagesZh.Count > 0 ? string.Join(" | ", messagesZh) : (_localization?.TForLanguage(GameLanguage.TraditionalChinese, "cmd.pass") ?? "Pass"),
            MessageEn = messagesEn.Count > 0 ? string.Join(" | ", messagesEn) : (_localization?.TForLanguage(GameLanguage.English, "cmd.pass") ?? "Pass")
        };
    }

    private CommandResult LocalizedResult(bool success, string key)
    {
        var zh = _localization?.TForLanguage(GameLanguage.TraditionalChinese, key) ?? key;
        var en = _localization?.TForLanguage(GameLanguage.English, key) ?? key;
        return new CommandResult
        {
            Success = success,
            Message = en,
            MessageZhHant = zh,
            MessageEn = en
        };
    }

    private static System.Collections.Generic.List<int> GetAvailableOfficerIds(WorldState world, CityData city)
    {
        var result = new System.Collections.Generic.List<int>();
        foreach (var officerId in city.OfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            if (officer.LastAssignedYear == world.Year && officer.LastAssignedMonth == world.Month)
            {
                continue;
            }

            result.Add(officerId);
        }

        return result;
    }

    private static int GetBestOfficerId(
        WorldState world,
        CityData city,
        System.Collections.Generic.List<int> availableOfficerIds,
        System.Func<OfficerData, int> scoreSelector)
    {
        var bestOfficerId = -1;
        var bestScore = int.MinValue;

        foreach (var officerId in availableOfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null || !city.OfficerIds.Contains(officerId))
            {
                continue;
            }

            var score = scoreSelector(officer);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestOfficerId = officerId;
        }

        return bestOfficerId;
    }
}
