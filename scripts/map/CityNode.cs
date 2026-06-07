using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Map;

public partial class CityNode : Node2D
{
    private static readonly Texture2D? CityTexture = GD.Load<Texture2D>("res://assets/map/city_1.png");
    private static readonly Texture2D? FlagTexture = GD.Load<Texture2D>("res://assets/map/flag_1.png");
    private static readonly Texture2D? ArrowTexture = GD.Load<Texture2D>("res://assets/map/arrow_1.png");

    private const float MarkerSize = 56.0f;
    private const float FlagWidth = 48.0f;
    private const float FlagHeight = 48.0f;
    private const float FlagOffsetX = 10.0f;
    private const float FlagOffsetY = -24.0f;
    private const float ArrowWidth = 64.0f;
    private const float ArrowHeight = 64.0f;
    private const float ArrowOffsetX = 0.0f;
    private const float ArrowOffsetY = -42.0f;
    private const float ArrowBobAmplitude = 4.0f;
    private const float ArrowBobSpeed = 3.4f;
    private const float CircleRadius = 12.0f;
    private const float EventRingRadius = 22.0f;
    private const float LabelStartY = 40.0f;
    private const float LabelLineHeight = 16.0f;

    private CityData? _city;
    private string _displayLabel = string.Empty;
    private Color _fillColor = new("6d6d6d");
    private bool _isSelected;
    private CityLabelOverlay? _labelOverlay;
    private bool _hasEventOverlay;
    private string _eventTag = string.Empty;
    private Color _eventOverlayColor = Colors.Transparent;
    private double _eventOverlayStartTime;
    private double _eventOverlayEndTime;

    public override void _Process(double delta)
    {
        if (_isSelected)
        {
            QueueRedraw();
        }

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

    public void Bind(CityData city, string displayLabel, Color fillColor)
    {
        _city = city;
        _displayLabel = displayLabel;
        _fillColor = fillColor;
        RefreshLabelOverlay();
        QueueRedraw();
    }

    public void SetDisplayLabel(string displayLabel)
    {
        _displayLabel = displayLabel;
        RefreshLabelOverlay();
        QueueRedraw();
    }

    public void SetFillColor(Color fillColor)
    {
        _fillColor = fillColor;
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
        if (CityTexture != null)
        {
            var textureRect = new Rect2(new Vector2(-MarkerSize * 0.5f, -MarkerSize * 0.5f), new Vector2(MarkerSize, MarkerSize));
            DrawTextureRect(CityTexture, textureRect, false, Colors.White);
            DrawFactionFlag();
            DrawSelectionArrow();
        }
        else
        {
            var borderColor = _isSelected ? new Color("fffbeb") : new Color("2d2a26");
            var borderWidth = _isSelected ? 3.0f : 2.0f;
            DrawCircle(Vector2.Zero, CircleRadius, _fillColor);
            DrawCircle(Vector2.Zero, CircleRadius + 1.0f, borderColor, false, borderWidth);
        }

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

    private void DrawSelectionArrow()
    {
        if (!_isSelected || ArrowTexture == null)
        {
            return;
        }

        var arrowRect = new Rect2(
            new Vector2(ArrowOffsetX - (ArrowWidth * 0.5f), GetAnimatedArrowOffsetY() - (ArrowHeight * 0.5f)),
            new Vector2(ArrowWidth, ArrowHeight));
        DrawTextureRect(ArrowTexture, arrowRect, false, Colors.White);
    }

    private float GetAnimatedArrowOffsetY()
    {
        var time = (float)(Time.GetTicksMsec() / 1000.0);
        return Mathf.Round(ArrowOffsetY + (Mathf.Sin(time * ArrowBobSpeed) * ArrowBobAmplitude));
    }

    private void DrawFactionFlag()
    {
        if (FlagTexture == null || _city == null || _city.OwnerFactionId <= 0)
        {
            return;
        }

        var flagRect = new Rect2(
            new Vector2(FlagOffsetX - (FlagWidth * 0.5f), FlagOffsetY - (FlagHeight * 0.5f)),
            new Vector2(FlagWidth, FlagHeight));
        var flagTint = GetFlagTintColor(_fillColor);
        DrawTextureRect(FlagTexture, flagRect, false, flagTint);
    }

    private static Color GetFlagTintColor(Color sourceColor)
    {
        var hue = sourceColor.H;
        var saturation = Mathf.Max(sourceColor.S, 0.9f);
        var vividColor = Color.FromHsv(hue, saturation, 1.0f, 1.0f);
        return vividColor.Lerp(Colors.White, 0.12f);
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
