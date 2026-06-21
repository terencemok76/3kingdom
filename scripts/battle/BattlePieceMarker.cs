using Godot;

namespace ThreeKingdom.Battle;

public partial class BattlePieceMarker : Node2D
{
    private string _label = string.Empty;
    private Color _fillColor = Colors.White;
    private Color _borderColor = Colors.Black;
    private float _radius = 19.0f;

    public float Radius => _radius;

    public void Setup(string label, Color fillColor, Color borderColor, float radius = 19.0f)
    {
        _label = label;
        _fillColor = fillColor;
        _borderColor = borderColor;
        _radius = radius;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, _radius + 3.0f, new Color(0.08f, 0.08f, 0.08f, 0.30f));
        DrawCircle(Vector2.Zero, _radius, _fillColor);
        DrawArc(Vector2.Zero, _radius, 0.0f, Mathf.Tau, 36, _borderColor, 2.0f, true);

        var font = ThemeDB.FallbackFont;
        var size = font.GetStringSize(_label);
        DrawString(font, new Vector2(-size.X * 0.5f, 7.0f), _label, modulate: new Color("fff7e6"), fontSize: 24);
    }
}
