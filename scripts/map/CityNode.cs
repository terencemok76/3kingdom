using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Map;

public partial class CityNode : Node2D
{
    private CityData? _city;
    private string _displayName = string.Empty;
    private bool _isSelected;

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

    public void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var fillColor = _isSelected ? new Color("f4d35e") : new Color("d8c27a");
        var borderColor = _isSelected ? new Color("fffbeb") : new Color("2d2a26");

        DrawCircle(Vector2.Zero, 16.0f, fillColor);
        DrawCircle(Vector2.Zero, 17.0f, borderColor, false, 2.0f);

        var label = !string.IsNullOrWhiteSpace(_displayName)
            ? _displayName
            : _city?.Name ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(label))
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(-36.0f, -22.0f), label);
        }
    }
}
