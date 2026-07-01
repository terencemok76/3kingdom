using Godot;

namespace ThreeKingdom.Battle;

public partial class BattlePieceMarker : Node2D
{
    private string _label = string.Empty;
    private Color _fillColor = Colors.White;
    private Color _borderColor = Colors.Black;
    private float _radius = 19.0f;
    private BattleSpriteAnimationPlayer? _spriteVisual;
    private bool _usesSpriteVisual;

    public float Radius => _radius;

    public void Setup(string label, Color fillColor, Color borderColor, float radius = 19.0f)
    {
        _label = label;
        _fillColor = fillColor;
        _borderColor = borderColor;
        _radius = radius;
        QueueRedraw();
    }

    public void SetupSpriteAnimationScene(string scenePath)
    {
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

        _spriteVisual.ZIndex = 1;
        AddChild(_spriteVisual);
        _usesSpriteVisual = true;
        _radius = Mathf.Max(_radius, _spriteVisual.ClickRadius);
        QueueRedraw();
    }

    public void MoveTo(Vector2 destination, double duration, string moveScenePath, string idleScenePath)
    {
        SetupSpriteAnimationScene(moveScenePath);
        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.InOut);
        tween.SetTrans(Tween.TransitionType.Linear);
        tween.TweenProperty(this, "position", destination, duration);
        tween.TweenCallback(Callable.From(() => SetupSpriteAnimationScene(idleScenePath)));
    }

    public void PlayAction(string actionScenePath, string idleScenePath, double duration)
    {
        SetupSpriteAnimationScene(actionScenePath);
        var tween = CreateTween();
        tween.TweenInterval(duration);
        tween.TweenCallback(Callable.From(() => SetupSpriteAnimationScene(idleScenePath)));
    }

    public override void _Draw()
    {
        var shadowCenterY = _usesSpriteVisual ? _radius * 0.48f : _radius * 0.68f;
        DrawFilledEllipse(new Vector2(0.0f, shadowCenterY), _radius * 0.95f, _radius * 0.28f, new Color(0.05f, 0.04f, 0.03f, 0.28f));
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

}
