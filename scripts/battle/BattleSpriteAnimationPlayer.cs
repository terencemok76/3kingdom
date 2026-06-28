using Godot;

namespace ThreeKingdom.Battle;

[Tool]
public partial class BattleSpriteAnimationPlayer : Node2D
{
    private const string SpriteNodeName = "Sprite";
    private const string DefaultSpriteSheetPath = "res://assets/battle/unit/infantry_idle_se.png";

    private Sprite2D? _sprite;
    private double _elapsed;
    private readonly Godot.Collections.Array<Texture2D> _frameTextures = new();
    private Texture2D? _builtFromTexture;
    private int _builtFrameCount;
    private int _builtInsetPixels;

    [Export]
    public Texture2D? SpriteSheet { get; set; }

    [Export(PropertyHint.Range, "1,32,1")]
    public int FrameCount { get; set; } = 4;

    [Export(PropertyHint.Range, "1,24,0.5")]
    public float FramesPerSecond { get; set; } = 5.0f;

    [Export]
    public bool PreviewInEditor { get; set; }

    [Export(PropertyHint.Range, "0,31,1")]
    public int PreviewFrame { get; set; }

    [Export]
    public Vector2 BaseOffset { get; set; } = new(0.0f, -6.0f);

    [Export]
    public Vector2 SpriteScale { get; set; } = Vector2.One;

    [Export(PropertyHint.Range, "8,96,1")]
    public float ClickRadius { get; set; } = 32.0f;

    [Export(PropertyHint.Range, "0,8,1")]
    public int FrameInsetPixels { get; set; } = 1;

    [Export]
    public Godot.Collections.Array<Vector2> FrameOffsets { get; set; } = new()
    {
        new Vector2(-4.5f, 0.0f),
        new Vector2(-0.5f, 0.0f),
        new Vector2(4.0f, 0.0f),
        new Vector2(7.0f, -1.0f)
    };

    public override void _Ready()
    {
        EnsureSprite();
        ApplySpriteSettings();
    }

    public override void _Process(double delta)
    {
        EnsureSprite();
        ApplySpriteSettings();

        if (_sprite == null || FrameCount <= 0)
        {
            return;
        }

        var frame = Mathf.Clamp(PreviewFrame, 0, FrameCount - 1);
        if (!Engine.IsEditorHint() || PreviewInEditor)
        {
            if (FramesPerSecond <= 0.0f)
            {
                return;
            }

            _elapsed += delta;
            frame = Mathf.FloorToInt((float)(_elapsed * FramesPerSecond)) % FrameCount;
        }

        if (_frameTextures.Count > 0)
        {
            _sprite.Texture = _frameTextures[frame];
            _sprite.Hframes = 1;
            _sprite.Frame = 0;
        }
        else
        {
            _sprite.Frame = frame;
        }

        _sprite.Offset = BaseOffset + GetFrameOffset(frame);
    }

    private void EnsureSprite()
    {
        _sprite ??= GetNodeOrNull<Sprite2D>(SpriteNodeName);
        if (_sprite != null)
        {
            return;
        }

        _sprite = new Sprite2D
        {
            Name = SpriteNodeName,
            Centered = true,
            TextureFilter = TextureFilterEnum.Nearest
        };
        AddChild(_sprite);

        if (Engine.IsEditorHint())
        {
            _sprite.Owner = GetTree()?.EditedSceneRoot;
        }
    }

    private void ApplySpriteSettings()
    {
        if (_sprite == null)
        {
            return;
        }

        SpriteSheet ??= GD.Load<Texture2D>(DefaultSpriteSheetPath);
        _sprite.Vframes = 1;
        _sprite.Scale = SpriteScale;
        RebuildFrameTexturesIfNeeded();
        if (_frameTextures.Count > 0)
        {
            _sprite.Texture = _frameTextures[0];
            _sprite.Hframes = 1;
            _sprite.Frame = 0;
            return;
        }

        _sprite.Texture = SpriteSheet;
        _sprite.Hframes = Mathf.Max(1, FrameCount);
    }

    private void RebuildFrameTexturesIfNeeded()
    {
        if (SpriteSheet == null ||
            (_builtFromTexture == SpriteSheet && _builtFrameCount == FrameCount && _builtInsetPixels == FrameInsetPixels))
        {
            return;
        }

        _frameTextures.Clear();
        _builtFromTexture = SpriteSheet;
        _builtFrameCount = FrameCount;
        _builtInsetPixels = FrameInsetPixels;

        if (FrameCount <= 0)
        {
            return;
        }

        var frameWidth = SpriteSheet.GetWidth() / FrameCount;
        var inset = Mathf.Clamp(FrameInsetPixels, 0, Mathf.Max(0, (frameWidth / 2) - 1));
        var regionWidth = frameWidth - (inset * 2);
        if (regionWidth <= 0)
        {
            return;
        }

        for (var frame = 0; frame < FrameCount; frame++)
        {
            _frameTextures.Add(new AtlasTexture
            {
                Atlas = SpriteSheet,
                Region = new Rect2((frame * frameWidth) + inset, 0.0f, regionWidth, SpriteSheet.GetHeight()),
                FilterClip = true
            });
        }
    }

    private Vector2 GetFrameOffset(int frame)
    {
        return frame >= 0 && frame < FrameOffsets.Count
            ? FrameOffsets[frame]
            : Vector2.Zero;
    }
}
