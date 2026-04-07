using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Map;

public partial class CityNode : Node2D
{
    private CityData? _city;
    private string _displayName = string.Empty;

    public void Bind(CityData city, string displayName)
    {
        _city = city;
        _displayName = displayName;
        QueueRedraw();
    }

    public void SetDisplayName(string displayName)
    {
        _displayName = displayName;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 16.0f, new Color("d8c27a"));
        DrawCircle(Vector2.Zero, 17.0f, new Color("2d2a26"), false, 2.0f);

        var label = !string.IsNullOrWhiteSpace(_displayName)
            ? _displayName
            : _city?.Name ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(label))
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(-36.0f, -22.0f), label);
        }
    }
}
