using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Map;

public partial class CityNode : Node2D
{
    private const float CircleRadius = 12.0f;
    private const float BorderRadius = 13.0f;
    private const float EventRingRadius = 18.0f;
    private const float LabelStartY = 30.0f;
    private const float LabelLineHeight = 16.0f;

    private CityData? _city;
    private string _displayLabel = string.Empty;
    private bool _isSelected;
    private CityLabelOverlay? _labelOverlay;
    private bool _hasEventOverlay;
    private string _eventTag = string.Empty;
    private Color _eventOverlayColor = Colors.Transparent;
    private double _eventOverlayStartTime;
    private double _eventOverlayEndTime;

    public override void _Process(double delta)
    {
        if (!_hasEventOverlay)
        {
            return;
        }

        var now = Time.GetTicksMsec() / 1000.0;
        if (_eventOverlayEndTime > _eventOverlayStartTime && now >= _eventOverlayEndTime)
        {
            ClearEventOverlay();
            return;
        }

        QueueRedraw();
    }

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

    public void SetEventOverlay(Color color, float durationSeconds, string eventTag)
    {
        _hasEventOverlay = true;
        _eventTag = eventTag ?? string.Empty;
        _eventOverlayColor = color;
        _eventOverlayStartTime = Time.GetTicksMsec() / 1000.0;
        _eventOverlayEndTime = durationSeconds > 0.0f
            ? _eventOverlayStartTime + Mathf.Max(durationSeconds, 0.5f)
            : _eventOverlayStartTime;
        RefreshLabelOverlay();
        QueueRedraw();
    }

    public void ClearEventOverlay()
    {
        if (!_hasEventOverlay)
        {
            return;
        }

        _hasEventOverlay = false;
        _eventTag = string.Empty;
        RefreshLabelOverlay();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var fillColor = GetFactionColor(_city?.OwnerFactionId ?? 0);
        var borderColor = _isSelected ? new Color("fffbeb") : new Color("2d2a26");
        var borderWidth = _isSelected ? 3.0f : 2.0f;

        DrawCircle(Vector2.Zero, CircleRadius, fillColor);
        DrawCircle(Vector2.Zero, BorderRadius, borderColor, false, borderWidth);

        if (_hasEventOverlay)
        {
            var now = Time.GetTicksMsec() / 1000.0;
            var elapsed = Mathf.Max((float)(now - _eventOverlayStartTime), 0.0f);
            var pulse = 0.55f + (0.45f * Mathf.Abs(Mathf.Sin(elapsed * 4.2f)));
            var glowAlpha = Mathf.Clamp(0.18f + (pulse * 0.18f), 0.16f, 0.38f);
            var ringAlpha = Mathf.Clamp(0.52f + (pulse * 0.32f), 0.46f, 0.9f);
            var glowColor = new Color(_eventOverlayColor, glowAlpha);
            var ringColor = new Color(_eventOverlayColor, ringAlpha);
            DrawCircle(Vector2.Zero, EventRingRadius + 2.5f, glowColor, false, 7.0f);
            DrawCircle(Vector2.Zero, EventRingRadius, ringColor, false, 3.5f);
        }
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
        _labelOverlay.SetLabel(label, _eventTag, _eventOverlayColor);
    }

    private static Color GetFactionColor(int factionId)
    {
        return factionId switch
        {
            1 => new Color("3f7f4c"), // Liu Bei - green
            2 => new Color("8a3e2f"), // Cao Cao - red brown
            3 => new Color("2f5f8a"), // Sun Jian - blue
            4 => new Color("b9932f"), // Zhang Jiao - yellow
            _ => new Color("6d6d6d")  // Neutral/unknown
        };
    }

    private sealed partial class CityLabelOverlay : Node2D
    {
        private string _label = string.Empty;
        private string _eventLabel = string.Empty;
        private Color _eventLabelColor = Colors.Transparent;

        public void SetLabel(string label, string eventLabel, Color eventColor)
        {
            _label = label ?? string.Empty;
            _eventLabel = eventLabel ?? string.Empty;
            _eventLabelColor = eventColor;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (string.IsNullOrWhiteSpace(_label))
            {
                return;
            }

            var font = ThemeDB.FallbackFont;
            var lines = _label.Split('\n');
            var y = LabelStartY;
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var lineWidth = font.GetStringSize(line).X;
                    DrawString(font, new Vector2(-lineWidth * 0.5f, y), line, modulate: new Color("f5f1e8"));
                }

                y += LabelLineHeight;
            }

            if (!string.IsNullOrWhiteSpace(_eventLabel))
            {
                var eventWidth = font.GetStringSize(_eventLabel).X;
                var eventPosition = new Vector2(-eventWidth * 0.5f, y);
                DrawString(font, eventPosition + new Vector2(1.0f, 1.0f), _eventLabel, modulate: new Color(0.05f, 0.05f, 0.05f, 0.85f));
                DrawString(font, eventPosition, _eventLabel, modulate: _eventLabelColor);
            }
        }
    }
}
