using Godot;

namespace ThreeKingdom.UI;

/// <summary>
/// Applies the strategic-map panel treatment to a normal dialog panel.
/// The frame texture is a NinePatch so its ornate corners remain intact at
/// every supported dialog size.
/// </summary>
internal static class StrategyPanelChrome
{
    private const string FrameTexturePath = "res://assets/ui/map/leftPanel_2.png";
    private const int FrameInset = 28;
    // The texture has a transparent rim outside its ornamental gold edge.
    // Keep the dark fill inside that rim so it does not appear as a black box.
    private const int BackdropInset = 10;
    private const string ChromeNodeName = "StrategyPanelChrome";

    public static void Apply(PanelContainer panel)
    {
        var host = panel.GetParent() as Control;
        if (host == null)
        {
            return;
        }

        // Reserve the same safe area as the city and log panels so content
        // never overlaps the gold corners or decorative border.
        panel.AddThemeStyleboxOverride("panel", CreateContentPadding());
        panel.QueueSort();

        if (host.GetNodeOrNull<Control>(ChromeNodeName) != null)
        {
            Sync(panel);
            return;
        }

        var chrome = new Control
        {
            Name = ChromeNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        var backdrop = new ColorRect
        {
            Name = "Backdrop",
            Color = new Color(0.045f, 0.04f, 0.03f, 0.91f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.OffsetLeft = BackdropInset;
        backdrop.OffsetTop = BackdropInset;
        backdrop.OffsetRight = -BackdropInset;
        backdrop.OffsetBottom = -BackdropInset;

        var frame = new NinePatchRect
        {
            Name = "Frame",
            Texture = GD.Load<Texture2D>(FrameTexturePath),
            PatchMarginLeft = FrameInset,
            PatchMarginTop = FrameInset,
            PatchMarginRight = FrameInset,
            PatchMarginBottom = FrameInset,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = CreateFrameTransparencyMaterial()
        };
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        chrome.AddChild(backdrop);
        chrome.AddChild(frame);
        host.AddChild(chrome);
        host.MoveChild(chrome, panel.GetIndex());
        Sync(panel);
    }

    public static void Sync(PanelContainer panel)
    {
        var host = panel.GetParent() as Control;
        var chrome = host?.GetNodeOrNull<Control>(ChromeNodeName);
        if (chrome == null)
        {
            return;
        }

        chrome.Position = panel.Position;
        chrome.Size = panel.Size;
    }

    private static StyleBoxFlat CreateContentPadding()
    {
        return new StyleBoxFlat
        {
            ContentMarginLeft = FrameInset,
            ContentMarginTop = FrameInset,
            ContentMarginRight = FrameInset,
            ContentMarginBottom = FrameInset,
            BgColor = Colors.Transparent,
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0
        };
    }

    private static ShaderMaterial CreateFrameTransparencyMaterial()
    {
        var shader = new Shader
        {
            Code = """
                shader_type canvas_item;

                uniform float inner_alpha : hint_range(0.0, 1.0) = 0.72;

                void fragment() {
                    vec4 tex = texture(TEXTURE, UV);
                    float brightness = max(tex.r, max(tex.g, tex.b));
                    float ornament_mask = smoothstep(0.18, 0.48, brightness);
                    tex.a *= mix(inner_alpha, 1.0, ornament_mask);
                    COLOR = tex * COLOR;
                }
                """
        };

        return new ShaderMaterial { Shader = shader };
    }
}
