using Godot;

namespace ThreeKingdom.Map;

[GlobalClass]
public partial class FlagAnimationSettings : Resource
{
    // Source-pixel corrections for each horizontal animation frame.
    // Edit these directly in the Godot Inspector on flag_animation_settings.tres.
    [Export]
    public Vector2[] FramePivotOffsets { get; set; } =
    {
        Vector2.Zero,
        new Vector2(4.0f, 0.0f),
        new Vector2(4.5f, 0.0f),
        new Vector2(8.0f, 0.0f)
    };
}
