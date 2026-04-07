using System.Text.Json;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public class WorldRepository
{
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
        return JsonSerializer.Deserialize<WorldState>(json, JsonOptions);
    }
}
