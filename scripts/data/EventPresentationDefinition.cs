using System.Text.Json.Serialization;

namespace ThreeKingdom.Data;

public sealed class EventPresentationDefinition
{
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("picture")]
    public string Picture { get; set; } = string.Empty;

    [JsonPropertyName("sound")]
    public string Sound { get; set; } = string.Empty;

    [JsonPropertyName("durationSeconds")]
    public float DurationSeconds { get; set; } = 2.5f;

    [JsonPropertyName("mapMarkerColor")]
    public string MapMarkerColor { get; set; } = "#C98B2B";
}
