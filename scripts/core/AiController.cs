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
                    TroopsToSend = city.Troops / 2
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

        CommandResult coreResult;
        if (city.Troops < 2200 && city.Gold >= 120 && city.Food >= 80)
        {
            coreResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Recruit,
                ActorFactionId = factionId,
                SourceCityId = cityId
            });
        }
        else if (city.Gold >= 100)
        {
            coreResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Develop,
                ActorFactionId = factionId,
                SourceCityId = cityId
            });
        }
        else if (city.LastSearchYear == world.Year && city.LastSearchMonth == world.Month)
        {
            coreResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Pass,
                ActorFactionId = factionId,
                SourceCityId = cityId
            });
        }
        else
        {
            coreResult = _commandResolver.Execute(new CommandRequest
            {
                Type = CommandType.Search,
                ActorFactionId = factionId,
                SourceCityId = cityId
            });
        }

        if (militaryResult == null)
        {
            return coreResult;
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

        if (!string.IsNullOrWhiteSpace(coreResult.Message) &&
            !coreResult.Message.Equals("Pass", System.StringComparison.OrdinalIgnoreCase))
        {
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

        return new CommandResult
        {
            Success = militaryResult.Success || coreResult.Success,
            Message = messages.Count > 0 ? string.Join(" | ", messages) : (_localization?.TForLanguage(GameLanguage.English, "cmd.pass") ?? "Pass"),
            MessageZhHant = messagesZh.Count > 0 ? string.Join(" | ", messagesZh) : (_localization?.TForLanguage(GameLanguage.TraditionalChinese, "cmd.pass") ?? "待命"),
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
