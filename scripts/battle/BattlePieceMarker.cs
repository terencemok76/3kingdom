using Godot;
using System;

namespace ThreeKingdom.Battle;

public partial class BattlePieceMarker : Node2D
{
    private const int MovingZIndexBoost = 100;

    private string _label = string.Empty;
    private string _namePlateText = string.Empty;
    private Color _fillColor = Colors.White;
    private Color _borderColor = Colors.Black;
    private Color _teamArrowColor = Colors.Transparent;
    private float _healthRatio = 1.0f;
    private float _activeTroopRatio = 1.0f;
    private float _woundedTroopRatio;
    private float _deadTroopRatio;
    private float _radius = 19.0f;
    private BattleSpriteAnimationPlayer? _spriteVisual;
    private string _spriteScenePath = string.Empty;
    private bool _usesSpriteVisual;
    private bool _hasNamePlate;
    private bool _hasTeamArrow;
    private bool _hasHealthBar;
    private bool _usesSegmentedTroopBar;
    private bool _hasStatusIndicator;
    private string _statusIndicatorText = string.Empty;
    private Color _statusIndicatorColor = Colors.Transparent;

    public float Radius => _radius;

    public void Setup(string label, Color fillColor, Color borderColor, float radius = 19.0f)
    {
        _label = label;
        _fillColor = fillColor;
        _borderColor = borderColor;
        _radius = radius;
        QueueRedraw();
    }

    public void SetupTeamArrow(Color arrowColor)
    {
        _teamArrowColor = arrowColor;
        _hasTeamArrow = true;
        QueueRedraw();
    }

    public void SetupNamePlate(string name)
    {
        _namePlateText = name;
        _hasNamePlate = !string.IsNullOrWhiteSpace(name);
        QueueRedraw();
    }

    public void SetupHealthBar(int current, int max)
    {
        if (max <= 0)
        {
            _hasHealthBar = false;
            _usesSegmentedTroopBar = false;
            QueueRedraw();
            return;
        }

        _healthRatio = Mathf.Clamp((float)current / max, 0.0f, 1.0f);
        _hasHealthBar = true;
        _usesSegmentedTroopBar = false;
        QueueRedraw();
    }

    public void SetupTroopSegmentBar(int activeTroops, int woundedTroops, int maxTroops)
    {
        if (maxTroops <= 0)
        {
            _hasHealthBar = false;
            _usesSegmentedTroopBar = false;
            QueueRedraw();
            return;
        }

        var active = Mathf.Clamp(activeTroops, 0, maxTroops);
        var wounded = Mathf.Clamp(woundedTroops, 0, maxTroops - active);
        var dead = Mathf.Max(0, maxTroops - active - wounded);
        _activeTroopRatio = (float)active / maxTroops;
        _woundedTroopRatio = (float)wounded / maxTroops;
        _deadTroopRatio = (float)dead / maxTroops;
        _healthRatio = _activeTroopRatio;
        _hasHealthBar = true;
        _usesSegmentedTroopBar = true;
        QueueRedraw();
    }

    public void SetupStatusIndicator(string text, Color color)
    {
        _statusIndicatorText = text;
        _statusIndicatorColor = color;
        _hasStatusIndicator = !string.IsNullOrWhiteSpace(text);
        QueueRedraw();
    }

    public void SetupSpriteAnimationScene(string scenePath)
    {
        _spriteScenePath = scenePath;
        var scene = GD.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            GD.PushWarning($"Battle unit animation scene could not be loaded: {scenePath}");
            return;
        }

        _spriteVisual?.QueueFree();
        _spriteVisual = scene.InstantiateOrNull<BattleSpriteAnimationPlayer>();
        if (_spriteVisual == null)
        {
            GD.PushWarning($"Battle unit animation scene has unexpected root type: {scenePath}");
            return;
        }

        _spriteVisual.ZIndex = 0;
        AddChild(_spriteVisual);
        _usesSpriteVisual = true;
        _radius = Mathf.Max(_radius, _spriteVisual.ClickRadius);
        QueueRedraw();
    }

    public Node2D? CreateSilhouetteVisual(Color modulate)
    {
        if (string.IsNullOrWhiteSpace(_spriteScenePath))
        {
            return null;
        }

        var silhouette = new BattlePieceMarker
        {
            ZIndex = 0
        };
        silhouette.Setup(_label, _fillColor, _borderColor, _radius);
        if (_hasNamePlate)
        {
            silhouette.SetupNamePlate(_namePlateText);
        }
        if (_hasTeamArrow)
        {
            silhouette.SetupTeamArrow(_teamArrowColor);
        }
        if (_hasStatusIndicator)
        {
            silhouette.SetupStatusIndicator(_statusIndicatorText, _statusIndicatorColor);
        }
        if (_hasHealthBar && _usesSegmentedTroopBar)
        {
            silhouette.SetupTroopSegmentBar(
                Mathf.RoundToInt(_activeTroopRatio * 1000.0f),
                Mathf.RoundToInt(_woundedTroopRatio * 1000.0f),
                1000);
        }
        else if (_hasHealthBar)
        {
            silhouette.SetupHealthBar(Mathf.RoundToInt(_healthRatio * 1000.0f), 1000);
        }

        silhouette.SetupSpriteAnimationScene(_spriteScenePath);
        if (silhouette._spriteVisual == null)
        {
            return null;
        }

        silhouette._spriteVisual.Modulate = modulate;
        return silhouette;
    }

    public void MoveTo(Vector2 destination, double duration, string moveScenePath, string idleScenePath, Action? onComplete = null)
    {
        SetupSpriteAnimationScene(moveScenePath);
        var originalZIndex = ZIndex;
        ZIndex = originalZIndex + MovingZIndexBoost;
        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.InOut);
        tween.SetTrans(Tween.TransitionType.Linear);
        tween.TweenProperty(this, "position", destination, duration);
        tween.TweenCallback(Callable.From(() =>
        {
            SetupSpriteAnimationScene(idleScenePath);
            ZIndex = originalZIndex;
            onComplete?.Invoke();
        }));
    }

    public void MoveVia(Vector2 waypoint, Vector2 destination, double duration, string moveScenePath, string idleScenePath)
    {
        MoveAlong(new[] { waypoint, destination }, duration, moveScenePath, idleScenePath);
    }

    public void MoveAlong(Vector2[] points, double duration, string moveScenePath, string idleScenePath, Action? onComplete = null, Color?[]? segmentModulates = null)
    {
        MoveAlong(points, duration, BuildRepeatedScenePathArray(points.Length, moveScenePath), idleScenePath, onComplete, segmentModulates);
    }

    public void MoveAlong(Vector2[] points, double duration, string[] moveScenePaths, string idleScenePath, Action? onComplete = null, Color?[]? segmentModulates = null)
    {
        if (points.Length == 0)
        {
            SetupSpriteAnimationScene(idleScenePath);
            onComplete?.Invoke();
            return;
        }

        var originalModulate = Modulate;
        var originalZIndex = ZIndex;
        ZIndex = originalZIndex + MovingZIndexBoost;
        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.InOut);
        tween.SetTrans(Tween.TransitionType.Linear);
        var segmentDuration = duration / points.Length;
        for (var index = 0; index < points.Length; index++)
        {
            var point = points[index];
            var scenePath = index < moveScenePaths.Length ? moveScenePaths[index] : moveScenePaths[^1];
            var segmentModulate = segmentModulates != null && index < segmentModulates.Length
                ? segmentModulates[index]
                : null;
            tween.TweenCallback(Callable.From(() =>
            {
                SetupSpriteAnimationScene(scenePath);
                Modulate = segmentModulate ?? originalModulate;
            }));
            tween.TweenProperty(this, "position", point, segmentDuration);
        }

        tween.TweenCallback(Callable.From(() =>
        {
            SetupSpriteAnimationScene(idleScenePath);
            Modulate = originalModulate;
            ZIndex = originalZIndex;
            onComplete?.Invoke();
        }));
    }

    public void PlayAction(string actionScenePath, string idleScenePath, double duration, Action? onComplete = null)
    {
        SetupSpriteAnimationScene(actionScenePath);
        var tween = CreateTween();
        tween.TweenInterval(duration);
        tween.TweenCallback(Callable.From(() =>
        {
            SetupSpriteAnimationScene(idleScenePath);
            onComplete?.Invoke();
        }));
    }

    public override void _Draw()
    {
        var shadowCenterY = _usesSpriteVisual ? _radius * 0.48f : _radius * 0.68f;
        DrawFilledEllipse(new Vector2(0.0f, shadowCenterY), _radius * 0.95f, _radius * 0.28f, new Color(0.05f, 0.04f, 0.03f, 0.28f));
        DrawNamePlate();
        DrawTeamArrow();
        DrawHealthBar();
        DrawStatusIndicator();
        if (_usesSpriteVisual)
        {
            return;
        }

        DrawArc(Vector2.Zero + new Vector2(0.0f, _radius * 0.66f), _radius * 0.72f, 0.05f, Mathf.Pi - 0.05f, 24, new Color("f5e0a8", 0.42f), 2.0f, true);
        DrawCircle(Vector2.Zero, _radius + 3.0f, new Color(0.08f, 0.08f, 0.08f, 0.30f));
        DrawCircle(Vector2.Zero, _radius, _fillColor);
        DrawArc(Vector2.Zero, _radius, 0.0f, Mathf.Tau, 36, _borderColor, 2.0f, true);

        var font = ThemeDB.FallbackFont;
        var size = font.GetStringSize(_label);
        DrawString(font, new Vector2(-size.X * 0.5f, 7.0f), _label, modulate: new Color("fff7e6"), fontSize: 24);
    }

    private void DrawStatusIndicator()
    {
        if (!_hasStatusIndicator)
        {
            return;
        }

        var ringCenter = _usesSpriteVisual
            ? new Vector2(0.0f, _radius * 0.42f)
            : Vector2.Zero;
        var ringRadius = _usesSpriteVisual ? _radius * 1.04f : _radius + 7.0f;
        DrawArc(ringCenter, ringRadius, 0.0f, Mathf.Tau, 48, WithAlpha(_statusIndicatorColor, 0.82f), 3.0f, true);
        DrawArc(ringCenter, ringRadius + 3.0f, -0.3f, Mathf.Tau - 0.3f, 48, WithAlpha(_statusIndicatorColor, 0.34f), 2.0f, true);

        var font = ThemeDB.FallbackFont;
        const int fontSize = 10;
        var textSize = font.GetStringSize(_statusIndicatorText);
        var badgeY = _usesSpriteVisual ? -_radius * 3.34f : -_radius * 2.58f;
        var badgeRect = new Rect2(
            new Vector2(-textSize.X * 0.5f - 5.0f, badgeY - 11.0f),
            new Vector2(textSize.X + 10.0f, 14.0f));
        DrawRect(badgeRect, new Color(0.10f, 0.03f, 0.02f, 0.84f), true);
        DrawRect(badgeRect, WithAlpha(_statusIndicatorColor, 0.92f), false, 1.0f);
        DrawString(
            font,
            new Vector2(-textSize.X * 0.5f, badgeY),
            _statusIndicatorText,
            modulate: new Color(1.0f, 0.88f, 0.70f, 0.98f),
            fontSize: fontSize);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, alpha);
    }

    private void DrawNamePlate()
    {
        if (!_hasNamePlate)
        {
            return;
        }

        var font = ThemeDB.FallbackFont;
        const int fontSize = 12;
        const float maxWidth = 74.0f;
        var text = CompactNamePlateText(_namePlateText, 14);
        var size = font.GetStringSize(text);
        var scale = size.X > 0.0f ? Mathf.Min(1.0f, maxWidth / size.X) : 1.0f;
        var y = _usesSpriteVisual ? -_radius * 3.0f : -_radius * 2.05f;
        DrawSetTransform(Vector2.Zero, 0.0f, new Vector2(scale, 1.0f));
        var origin = new Vector2(-size.X * 0.5f, y);
        foreach (var offset in new[] { new Vector2(-1.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(0.0f, -1.0f), new Vector2(0.0f, 1.0f) })
        {
            DrawString(
                font,
                origin + offset,
                text,
                modulate: new Color(0.05f, 0.025f, 0.01f, 0.88f),
                fontSize: fontSize);
        }

        DrawString(
            font,
            origin,
            text,
            modulate: new Color(1.0f, 0.93f, 0.72f, 0.96f),
            fontSize: fontSize);
        DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
    }

    private static string CompactNamePlateText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..Mathf.Max(1, maxLength - 1)] + ".";
    }

    private void DrawTeamArrow()
    {
        if (!_hasTeamArrow)
        {
            return;
        }

        var centerY = _usesSpriteVisual ? -_radius * 2.45f : -_radius * 1.65f;
        var halfWidth = _usesSpriteVisual ? 7.0f : 6.0f;
        var height = _usesSpriteVisual ? 13.0f : 11.0f;
        var points = new[]
        {
            new Vector2(0.0f, centerY + height * 0.5f),
            new Vector2(halfWidth, centerY - height * 0.5f),
            new Vector2(-halfWidth, centerY - height * 0.5f)
        };
        var colors = new[] { _teamArrowColor, _teamArrowColor, _teamArrowColor };
        DrawPolygon(points, colors);
        DrawPolyline(points, new Color(0.04f, 0.03f, 0.02f, 0.82f), 2.0f, true);
    }

    private void DrawHealthBar()
    {
        if (!_hasHealthBar)
        {
            return;
        }

        var width = _usesSpriteVisual ? 34.0f : 30.0f;
        var height = 4.0f;
        var y = _usesSpriteVisual ? -_radius * 2.02f : -_radius * 1.28f;
        var backgroundRect = new Rect2(-width * 0.5f, y, width, height);
        DrawRect(backgroundRect, new Color(0.03f, 0.025f, 0.02f, 0.86f), true);
        if (_usesSegmentedTroopBar)
        {
            DrawTroopSegmentBar(backgroundRect);
        }
        else
        {
            var fillRect = new Rect2(backgroundRect.Position + Vector2.One, new Vector2(Mathf.Max(0.0f, (width - 2.0f) * _healthRatio), height - 2.0f));
            DrawRect(fillRect, GetHealthBarFillColor(_healthRatio), true);
        }

        DrawRect(backgroundRect, new Color(1.0f, 0.92f, 0.72f, 0.72f), false, 1.0f);
    }

    private void DrawTroopSegmentBar(Rect2 backgroundRect)
    {
        var contentPosition = backgroundRect.Position + Vector2.One;
        var contentWidth = Mathf.Max(0.0f, backgroundRect.Size.X - 2.0f);
        var contentHeight = Mathf.Max(0.0f, backgroundRect.Size.Y - 2.0f);
        var x = contentPosition.X;
        DrawTroopSegment(ref x, contentPosition.Y, contentWidth, contentHeight, _activeTroopRatio, new Color(0.30f, 0.86f, 0.34f, 0.96f));
        DrawTroopSegment(ref x, contentPosition.Y, contentWidth, contentHeight, _woundedTroopRatio, new Color(1.0f, 0.34f, 0.68f, 0.96f));
        DrawTroopSegment(ref x, contentPosition.Y, contentWidth, contentHeight, _deadTroopRatio, new Color(0.02f, 0.018f, 0.015f, 0.98f));
    }

    private void DrawTroopSegment(ref float x, float y, float totalWidth, float height, float ratio, Color color)
    {
        var width = Mathf.Max(0.0f, totalWidth * ratio);
        if (width <= 0.0f)
        {
            return;
        }

        DrawRect(new Rect2(x, y, width, height), color, true);
        x += width;
    }

    private static Color GetHealthBarFillColor(float ratio)
    {
        if (ratio > 0.55f)
        {
            return new Color(0.30f, 0.86f, 0.34f, 0.96f);
        }

        if (ratio > 0.25f)
        {
            return new Color(1.0f, 0.74f, 0.20f, 0.96f);
        }

        return new Color(1.0f, 0.22f, 0.16f, 0.96f);
    }

    private void DrawFilledEllipse(Vector2 center, float radiusX, float radiusY, Color color)
    {
        const int pointCount = 32;
        var points = new Vector2[pointCount];
        var colors = new Color[pointCount];
        for (var index = 0; index < pointCount; index++)
        {
            var angle = Mathf.Tau * index / pointCount;
            points[index] = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
            colors[index] = color;
        }

        DrawPolygon(points, colors);
    }

    private static string[] BuildRepeatedScenePathArray(int count, string scenePath)
    {
        var scenePaths = new string[count];
        for (var index = 0; index < count; index++)
        {
            scenePaths[index] = scenePath;
        }

        return scenePaths;
    }

}
