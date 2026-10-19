using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.Map;

public partial class CityNode : Node2D
{
    private static readonly Texture2D? CityTexture = GD.Load<Texture2D>("res://assets/map/city_1.png");
    private static readonly Texture2D? FlagTexture = GD.Load<Texture2D>("res://assets/map/flag_1.png");
    private static readonly Texture2D? FlagAnimationTexture = GD.Load<Texture2D>("res://assets/map/flag_anim2.png");
    private static readonly FlagAnimationSettings? FlagAnimationSettings = GD.Load<FlagAnimationSettings>("res://data/map/flag_animation_settings.tres");
    private static readonly RandomNumberGenerator FlagAnimationRandom = CreateFlagAnimationRandom();
    private static readonly Font? FlagFont = GD.Load<FontFile>("res://assets/fonts/LXGWWenKaiTC-Regular.ttf");
    private static readonly Texture2D? ArrowTexture = GD.Load<Texture2D>("res://assets/map/arrow_1.png");
    private static readonly Texture2D? BattleIconTexture = GD.Load<Texture2D>("res://assets/map/battle_icon.png");

    private const float MarkerSize = 56.0f;
    private const float FlagWidth = 48.0f;
    // Each flag_anim2 frame is 128x154; retain that cloth-and-pole aspect ratio.
    private const float FlagHeight = 57.75f;
    private const float FlagOffsetX = 20.0f; //10.0f;
    // Align the bottom of the flag with the top of the 56px city marker.
    private const float FlagOffsetY = -(MarkerSize * 0.5f) - (FlagHeight * 0.5f) + 16.0f;
    private const int FlagAnimationFrameCount = 4;
    private const float FlagAnimationFrameDurationSeconds = 0.40f;
    // Used only when the editor-authored settings resource is unavailable.
    private static readonly Vector2[] DefaultFlagAnimationFramePivotOffsets =
    {
        Vector2.Zero,
        new Vector2(4.0f, 0.0f),
        new Vector2(4.5f, 0.0f),
        new Vector2(8.0f, 0.0f)
    };
    // Keep the ruler mark within the visible cloth rather than letting it compete
    // with nearby city labels on the densely packed strategic map.
    private const int FlagLabelFontSize = 16;
    private const float FlagLabelClothOffsetX = -1.0f;
    private const float ArrowWidth = 64.0f;
    private const float ArrowHeight = 64.0f;
    private const float ArrowOffsetX = 0.0f;
    private const float ArrowOffsetY = -42.0f;
    private const float ArrowBobAmplitude = 4.0f;
    private const float ArrowBobSpeed = 3.4f;
    private const float CircleRadius = 12.0f;
    private const float EventRingRadius = 22.0f;
    private const float BattleIconSize = 52.0f;
    private const float LabelStartY = 30.0f;
    private const float LabelLineHeight = 16.0f;

    private CityData? _city;
    private string _displayLabel = string.Empty;
    private string _rulerFlagMark = string.Empty;
    private Color _fillColor = new("6d6d6d");
    private bool _isSelected;
    private CityFlagOverlay? _flagOverlay;
    private CityLabelOverlay? _labelOverlay;
    private CitySelectionArrowOverlay? _selectionArrowOverlay;
    private bool _hasEventOverlay;
    private bool _hasBattleAnimation;
    private string _eventTag = string.Empty;
    private Color _eventOverlayColor = Colors.Transparent;
    private bool _hasCampaignOverlay;
    private string _strategicDetailLabel = string.Empty;
    private double _eventOverlayStartTime;
    private double _eventOverlayEndTime;
    private int _flagAnimationFrame = -1;
    private int _flagAnimationStartFrame;

    public override void _Process(double delta)
    {
        UpdateFlagAnimationFrame();

        if (_isSelected)
        {
            _selectionArrowOverlay?.QueueRedraw();
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
        _flagOverlay = new CityFlagOverlay(this)
        {
            Name = "CityFlagOverlay",
            // Render above every sibling city marker, but below the city label.
            ZIndex = 5
        };
        AddChild(_flagOverlay);

        _labelOverlay = new CityLabelOverlay
        {
            Name = "CityLabelOverlay",
            ZIndex = 10
        };
        AddChild(_labelOverlay);

        _selectionArrowOverlay = new CitySelectionArrowOverlay(this)
        {
            Name = "CitySelectionArrowOverlay",
            // Selection is an active interaction cue and must win every overlap.
            ZIndex = 20
        };
        AddChild(_selectionArrowOverlay);
        RefreshLabelOverlay();
    }

    public void Bind(CityData city, string displayLabel, string rulerFlagMark, Color fillColor)
    {
        _city = city;
        _flagAnimationStartFrame = (int)FlagAnimationRandom.RandiRange(0, FlagAnimationFrameCount - 1);
        _flagAnimationFrame = -1;
        _displayLabel = displayLabel;
        _rulerFlagMark = rulerFlagMark ?? string.Empty;
        _fillColor = fillColor;
        _flagOverlay?.QueueRedraw();
        RefreshLabelOverlay();
        QueueRedraw();
    }

    public void SetDisplayLabel(string displayLabel)
    {
        _displayLabel = displayLabel;
        RefreshLabelOverlay();
        QueueRedraw();
    }

    public void SetRulerFlagMark(string rulerFlagMark)
    {
        _rulerFlagMark = rulerFlagMark ?? string.Empty;
        _flagOverlay?.QueueRedraw();
        QueueRedraw();
    }

    public void SetFillColor(Color fillColor)
    {
        _fillColor = fillColor;
        _flagOverlay?.QueueRedraw();
        QueueRedraw();
    }

    public void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;
        _selectionArrowOverlay?.QueueRedraw();
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
        _hasBattleAnimation = false;
        _eventTag = string.Empty;
        RefreshLabelOverlay();
        QueueRedraw();
    }

    public void PlayBattleAnimation(float durationSeconds)
    {
        _hasBattleAnimation = true;
        SetEventOverlay(new Color("e34c45"), durationSeconds, "交戰");
    }

    public void SetCampaignOverlay(bool hasCampaignOverlay)
    {
        _hasCampaignOverlay = hasCampaignOverlay;
        RefreshLabelOverlay();
        QueueRedraw();
    }

    public void SetStrategicMapPresentation(bool isEmphasized, string detailLabel)
    {
        Modulate = isEmphasized ? Colors.White : new Color(1.0f, 1.0f, 1.0f, 0.24f);
        _strategicDetailLabel = detailLabel ?? string.Empty;
        RefreshLabelOverlay();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (CityTexture != null)
        {
            var textureRect = new Rect2(new Vector2(-MarkerSize * 0.5f, -MarkerSize * 0.5f), new Vector2(MarkerSize, MarkerSize));
            DrawTextureRect(CityTexture, textureRect, false, Colors.White);
        }
        else
        {
            var borderColor = _isSelected ? new Color("fffbeb") : new Color("2d2a26");
            var borderWidth = _isSelected ? 3.0f : 2.0f;
            DrawCircle(Vector2.Zero, CircleRadius, _fillColor);
            DrawCircle(Vector2.Zero, CircleRadius + 1.0f, borderColor, false, borderWidth);
        }

        if (_hasCampaignOverlay && BattleIconTexture != null)
        {
            var iconRect = new Rect2(
                new Vector2(-BattleIconSize * 0.5f, -BattleIconSize * 0.5f),
                new Vector2(BattleIconSize, BattleIconSize));
            DrawTextureRect(BattleIconTexture, iconRect, false, Colors.White);
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

        if (_hasBattleAnimation)
        {
            var bladeColor = new Color("fff1c7");
            DrawLine(new Vector2(-10.0f, -10.0f), new Vector2(10.0f, 10.0f), bladeColor, 3.0f);
            DrawLine(new Vector2(10.0f, -10.0f), new Vector2(-10.0f, 10.0f), bladeColor, 3.0f);
        }
    }

    private float GetAnimatedArrowOffsetY()
    {
        var time = (float)(Time.GetTicksMsec() / 1000.0);
        return Mathf.Round(ArrowOffsetY + (Mathf.Sin(time * ArrowBobSpeed) * ArrowBobAmplitude));
    }

    private void UpdateFlagAnimationFrame()
    {
        if (FlagAnimationTexture == null)
        {
            return;
        }

        var elapsedSeconds = Time.GetTicksMsec() / 1000.0;
        var nextFrame = ((int)(elapsedSeconds / FlagAnimationFrameDurationSeconds) + _flagAnimationStartFrame) % FlagAnimationFrameCount;
        if (nextFrame == _flagAnimationFrame)
        {
            return;
        }

        _flagAnimationFrame = nextFrame;
        _flagOverlay?.QueueRedraw();
        QueueRedraw();
    }

    private static Vector2 GetFlagAnimationFramePivotOffset(int frameIndex)
    {
        var configuredOffsets = FlagAnimationSettings?.FramePivotOffsets;
        if (configuredOffsets != null && frameIndex >= 0 && frameIndex < configuredOffsets.Length)
        {
            return configuredOffsets[frameIndex];
        }

        return frameIndex >= 0 && frameIndex < DefaultFlagAnimationFramePivotOffsets.Length
            ? DefaultFlagAnimationFramePivotOffsets[frameIndex]
            : Vector2.Zero;
    }

    private static RandomNumberGenerator CreateFlagAnimationRandom()
    {
        var random = new RandomNumberGenerator();
        random.Randomize();
        return random;
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
        _labelOverlay.SetLabel(label, _eventTag, _strategicDetailLabel, _eventOverlayColor);
    }

    private sealed partial class CityFlagOverlay : Node2D
    {
        private readonly CityNode _owner;

        public CityFlagOverlay(CityNode owner)
        {
            _owner = owner;
        }

        public override void _Draw()
        {
            var flagTexture = FlagAnimationTexture ?? FlagTexture;
            if (flagTexture == null || _owner._city == null || _owner._city.OwnerFactionId <= 0)
            {
                return;
            }

            var flagRect = new Rect2(
                new Vector2(FlagOffsetX - (FlagWidth * 0.5f), FlagOffsetY - (FlagHeight * 0.5f)),
                new Vector2(FlagWidth, FlagHeight));
            var flagTint = GetFlagTintColor(_owner._fillColor);
            if (FlagAnimationTexture != null)
            {
                var frameWidth = FlagAnimationTexture.GetWidth() / FlagAnimationFrameCount;
                var frameIndex = Mathf.Clamp(_owner._flagAnimationFrame, 0, FlagAnimationFrameCount - 1);
                var sourceRect = new Rect2(
                    frameIndex * frameWidth,
                    0.0f,
                    frameWidth,
                    FlagAnimationTexture.GetHeight());
                var sourceToDisplayScale = FlagWidth / frameWidth;
                flagRect.Position += GetFlagAnimationFramePivotOffset(frameIndex) * sourceToDisplayScale;
                DrawTextureRectRegion(FlagAnimationTexture, flagRect, sourceRect, flagTint);
            }
            else
            {
                DrawTextureRect(flagTexture, flagRect, false, flagTint);
            }

            DrawRulerFlagMark(flagRect);
        }

        private void DrawRulerFlagMark(Rect2 flagRect)
        {
            if (string.IsNullOrWhiteSpace(_owner._rulerFlagMark))
            {
                return;
            }

            var font = FlagFont ?? ThemeDB.FallbackFont;
            var textSize = font.GetStringSize(_owner._rulerFlagMark, fontSize: FlagLabelFontSize);
            var textPosition = new Vector2(
                flagRect.GetCenter().X + FlagLabelClothOffsetX - (textSize.X * 0.5f),
                flagRect.GetCenter().Y + (textSize.Y * 0.30f));

            foreach (var offset in new[]
                     {
                         new Vector2(-1.0f, 0.0f), new Vector2(1.0f, 0.0f),
                         new Vector2(0.0f, -1.0f), new Vector2(0.0f, 1.0f)
                     })
            {
                DrawString(font, textPosition + offset, _owner._rulerFlagMark,
                    modulate: new Color(0.12f, 0.07f, 0.025f, 0.92f), fontSize: FlagLabelFontSize);
            }

            DrawString(font, textPosition, _owner._rulerFlagMark,
                modulate: new Color("fff0bd"), fontSize: FlagLabelFontSize);
        }
    }

    private sealed partial class CitySelectionArrowOverlay : Node2D
    {
        private readonly CityNode _owner;

        public CitySelectionArrowOverlay(CityNode owner)
        {
            _owner = owner;
        }

        public override void _Draw()
        {
            if (!_owner._isSelected || ArrowTexture == null)
            {
                return;
            }

            var arrowRect = new Rect2(
                new Vector2(ArrowOffsetX - (ArrowWidth * 0.5f), _owner.GetAnimatedArrowOffsetY() - (ArrowHeight * 0.5f)),
                new Vector2(ArrowWidth, ArrowHeight));
            DrawTextureRect(ArrowTexture, arrowRect, false, Colors.White);
        }
    }

    private sealed partial class CityLabelOverlay : Node2D
    {
        private string _label = string.Empty;
        private string _eventLabel = string.Empty;
        private string _strategicDetailLabel = string.Empty;
        private Color _eventLabelColor = Colors.Transparent;

        public void SetLabel(string label, string eventLabel, string strategicDetailLabel, Color eventColor)
        {
            _label = label ?? string.Empty;
            _eventLabel = eventLabel ?? string.Empty;
            _strategicDetailLabel = strategicDetailLabel ?? string.Empty;
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
                y += LabelLineHeight;
            }

            if (!string.IsNullOrWhiteSpace(_strategicDetailLabel))
            {
                var detailWidth = font.GetStringSize(_strategicDetailLabel).X;
                var detailPosition = new Vector2(-detailWidth * 0.5f, y);
                DrawString(font, detailPosition + new Vector2(1.0f, 1.0f), _strategicDetailLabel, modulate: new Color(0.04f, 0.04f, 0.04f, 0.8f));
                DrawString(font, detailPosition, _strategicDetailLabel, modulate: new Color("f5dfaa"));
            }
        }
    }
}
