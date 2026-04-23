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

            if (city.Troops > target.Troops + 300)
            {
                militaryResult = _commandResolver.Execute(new CommandRequest
                {
                    Type = CommandType.Attack,
                    ActorFactionId = factionId,
                    SourceCityId = cityId,
                    TargetCityId = targetId,
                    TroopsToSend = city.Troops / 2,
                    OfficerIds = new System.Collections.Generic.List<int>(city.OfficerIds)
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
        if (city.Troops < 2200 &&
            city.Gold >= 120 &&
            city.Food >= 80 &&
            !(city.LastRecruitYear == world.Year && city.LastRecruitMonth == world.Month))
        {
            coreResults.Add(_commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Recruit,
                ActorFactionId = factionId,
                SourceCityId = cityId
            }));
        }

        if (city.Gold >= 100 &&
            !(city.LastDevelopYear == world.Year && city.LastDevelopMonth == world.Month))
        {
            coreResults.Add(_commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Develop,
                ActorFactionId = factionId,
                SourceCityId = cityId
            }));
        }

        if (!(city.LastSearchYear == world.Year && city.LastSearchMonth == world.Month))
        {
            coreResults.Add(_commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Search,
                ActorFactionId = factionId,
                SourceCityId = cityId
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
}
