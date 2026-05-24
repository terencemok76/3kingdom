using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class EventPresentationCatalog
{
    private const string EventDefinitionPath = "res://data/events/events.json";
    private static Dictionary<MonthlyCityEventType, EventPresentationDefinition>? _definitions;

    public static bool TryGetDefinition(MonthlyCityEventType eventType, out EventPresentationDefinition definition)
    {
        var definitions = GetDefinitions();
        return definitions.TryGetValue(eventType, out definition!);
    }

    public static IReadOnlyDictionary<MonthlyCityEventType, EventPresentationDefinition> GetDefinitions()
    {
        _definitions ??= LoadDefinitions();
        return _definitions;
    }

    private static Dictionary<MonthlyCityEventType, EventPresentationDefinition> LoadDefinitions()
    {
        var definitions = new Dictionary<MonthlyCityEventType, EventPresentationDefinition>();
        if (!FileAccess.FileExists(EventDefinitionPath))
        {
            GD.PushWarning($"Event definition file missing: {EventDefinitionPath}");
            return definitions;
        }

        using var file = FileAccess.Open(EventDefinitionPath, FileAccess.ModeFlags.Read);
        var rawText = file.GetAsText();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return definitions;
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<EventPresentationDefinition>>(rawText);
            if (entries == null)
            {
                return definitions;
            }

            foreach (var entry in entries)
            {
                if (!TryParseEventType(entry.EventType, out var eventType))
                {
                    GD.PushWarning($"Unknown event type in {EventDefinitionPath}: {entry.EventType}");
                    continue;
                }

                definitions[eventType] = entry;
            }
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Failed to parse {EventDefinitionPath}: {exception.Message}");
        }

        return definitions;
    }

    private static bool TryParseEventType(string eventTypeKey, out MonthlyCityEventType eventType)
    {
        switch (eventTypeKey?.Trim())
        {
            case "flooding":
                eventType = MonthlyCityEventType.Flooding;
                return true;
            case "drought":
                eventType = MonthlyCityEventType.Drought;
                return true;
            case "earthquake":
                eventType = MonthlyCityEventType.Earthquake;
                return true;
            case "insectDisaster":
                eventType = MonthlyCityEventType.InsectDisaster;
                return true;
            case "plague":
                eventType = MonthlyCityEventType.Plague;
                return true;
            case "rebellion":
                eventType = MonthlyCityEventType.Rebellion;
                return true;
            case "bandit":
                eventType = MonthlyCityEventType.Bandit;
                return true;
            case "snow":
                eventType = MonthlyCityEventType.Snow;
                return true;
            case "typhoon":
                eventType = MonthlyCityEventType.Typhoon;
                return true;
            case "bumperHarvest":
                eventType = MonthlyCityEventType.BumperHarvest;
                return true;
            case "fire":
                eventType = MonthlyCityEventType.Fire;
                return true;
            default:
                eventType = default;
                return false;
        }
    }
}
