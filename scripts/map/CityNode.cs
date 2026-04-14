using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Map;

public partial class CityNode : Node2D
{
    private const float CircleRadius = 12.0f;
    private const float BorderRadius = 13.0f;

    private CityData? _city;
    private string _displayLabel = string.Empty;
    private bool _isSelected;
    private CityLabelOverlay? _labelOverlay;

    public override void _Ready()
    {
        _labelOverlay = new CityLabelOverlay
        {
            Name = "CityLabelOverlay",
            ZIndex = 10
        };
        AddChild(_labelOverlay);
        RefreshLabelOverlay();
    }

    public void Bind(CityData city, string displayLabel)
    {
        _city = city;
        _displayLabel = displayLabel;
        RefreshLabelOverlay();
        QueueRedraw();
    }

    public void SetDisplayLabel(string displayLabel)
    {
        _displayLabel = displayLabel;
        RefreshLabelOverlay();
        QueueRedraw();
    }

    public void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var fillColor = GetFactionColor(_city?.OwnerFactionId ?? 0);
        var borderColor = _isSelected ? new Color("fffbeb") : new Color("2d2a26");
        var borderWidth = _isSelected ? 3.0f : 2.0f;

        DrawCircle(Vector2.Zero, CircleRadius, fillColor);
        DrawCircle(Vector2.Zero, BorderRadius, borderColor, false, borderWidth);
    }

    private void RefreshLabelOverlay()
    {
        if (_labelOverlay == null)
        {
            return;
        }

        var label = !string.IsNullOrWhiteSpace(_displayLabel)
            ? _displayLabel
            : _city?.Name ?? string.Empty;
        _labelOverlay.SetLabel(label);
    }

    private static Color GetFactionColor(int factionId)
    {
        return factionId switch
        {
            1 => new Color("3f7f4c"), // Liu Bei - green
            2 => new Color("8a3e2f"), // Cao Cao - red brown
            3 => new Color("2f5f8a"), // Sun Quan - blue
            _ => new Color("6d6d6d")  // Neutral/unknown
        };
    }

    private sealed partial class CityLabelOverlay : Node2D
    {
        private string _label = string.Empty;

        public void SetLabel(string label)
        {
            _label = label ?? string.Empty;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (string.IsNullOrWhiteSpace(_label))
            {
                return;
            }

            var lines = _label.Split('\n');
            var y = -24.0f;
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    DrawString(ThemeDB.FallbackFont, new Vector2(-44.0f, y), line, modulate: new Color("f5f1e8"));
                }

                y += 16.0f;
            }
        }
    }
}
