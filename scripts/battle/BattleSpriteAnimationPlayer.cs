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
    private int _builtFramesPerRow;
    private int _builtInsetPixels;
    private string _builtCropSignature = string.Empty;

    [Export]
    public Texture2D? SpriteSheet { get; set; }

    [Export(PropertyHint.Range, "1,32,1")]
    public int FrameCount { get; set; } = 4;

    [Export(PropertyHint.Range, "1,32,1")]
    public int FramesPerRow { get; set; } = 4;

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

    [Export]
    public bool FlipH { get; set; }

    [Export]
    public bool MirrorFrameOffsetsWhenFlipped { get; set; }

    [Export(PropertyHint.Range, "8,96,1")]
    public float ClickRadius { get; set; } = 32.0f;

    [Export(PropertyHint.Range, "0,8,1")]
    public int FrameInsetPixels { get; set; }

    [Export]
    public Godot.Collections.Array<Vector2> FrameCropPixels { get; set; } = new()
    {
        Vector2.Zero,
        Vector2.Zero,
        Vector2.Zero,
        Vector2.Zero
    };

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
        _sprite.FlipH = FlipH;
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
        var cropSignature = BuildCropSignature();
        if (SpriteSheet == null ||
            (_builtFromTexture == SpriteSheet &&
             _builtFrameCount == FrameCount &&
             _builtFramesPerRow == FramesPerRow &&
             _builtInsetPixels == FrameInsetPixels &&
             _builtCropSignature == cropSignature))
        {
            return;
        }

        _frameTextures.Clear();
        _builtFromTexture = SpriteSheet;
        _builtFrameCount = FrameCount;
        _builtFramesPerRow = FramesPerRow;
        _builtInsetPixels = FrameInsetPixels;
        _builtCropSignature = cropSignature;

        var framesPerRow = Mathf.Clamp(FramesPerRow, 1, Mathf.Max(1, FrameCount));
        if (FrameCount <= 0 || framesPerRow <= 0)
        {
            return;
        }

        var frameWidth = SpriteSheet.GetWidth() / framesPerRow;
        var frameHeight = SpriteSheet.GetHeight() / Mathf.CeilToInt((float)FrameCount / framesPerRow);
        var inset = Mathf.Clamp(FrameInsetPixels, 0, Mathf.Max(0, (frameWidth / 2) - 1));
        var regionWidth = frameWidth - (inset * 2);
        if (regionWidth <= 0 || frameHeight <= 0)
        {
            return;
        }

        for (var frame = 0; frame < FrameCount; frame++)
        {
            var frameColumn = frame % framesPerRow;
            var frameRow = frame / framesPerRow;
            var crop = GetFrameCrop(frame);
            var cropLeft = Mathf.RoundToInt(crop.X);
            var cropRight = Mathf.RoundToInt(crop.Y);
            var frameRegionWidth = regionWidth - cropLeft - cropRight;
            if (frameRegionWidth <= 0)
            {
                _frameTextures.Add(new AtlasTexture
                {
                    Atlas = SpriteSheet,
                    Region = new Rect2(frameColumn * frameWidth, frameRow * frameHeight, frameWidth, frameHeight),
                    FilterClip = true
                });
                continue;
            }

            var sourceX = (frameColumn * frameWidth) + inset + cropLeft;
            var sourceY = frameRow * frameHeight;
            if (sourceX < 0)
            {
                frameRegionWidth += sourceX;
                sourceX = 0;
            }

            if (sourceX + frameRegionWidth > SpriteSheet.GetWidth())
            {
                frameRegionWidth = SpriteSheet.GetWidth() - sourceX;
            }

            if (frameRegionWidth <= 0)
            {
                continue;
            }

            _frameTextures.Add(new AtlasTexture
            {
                Atlas = SpriteSheet,
                Region = new Rect2(sourceX, sourceY, frameRegionWidth, frameHeight),
                FilterClip = true
            });
        }
    }

    private Vector2 GetFrameOffset(int frame)
    {
        var offset = frame >= 0 && frame < FrameOffsets.Count
            ? FrameOffsets[frame]
            : Vector2.Zero;
        if (FlipH && MirrorFrameOffsetsWhenFlipped)
        {
            offset.X *= -1.0f;
        }

        return offset;
    }

    private Vector2 GetFrameCrop(int frame)
    {
        return frame >= 0 && frame < FrameCropPixels.Count
            ? FrameCropPixels[frame]
            : Vector2.Zero;
    }

    private string BuildCropSignature()
    {
        var signature = string.Empty;
        foreach (var crop in FrameCropPixels)
        {
            signature += $"{crop.X:0.###},{crop.Y:0.###};";
        }

        return signature;
    }
}
