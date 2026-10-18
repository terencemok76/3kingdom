using Godot;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private void ApplyBattleScrollBarStyles()
    {
        ApplyBattleScrollBarStyle(_commandScroll);
        ApplyBattleScrollBarStyle(
            GetNodeOrNull<ScrollContainer>("UiLayer/SidePanel/Margin/PanelContent/InfoScroll"));
        ApplyBattleScrollBarStyle(
            GetNodeOrNull<ScrollContainer>("UiLayer/BattleLogPanel/Margin/LogContent/LogScroll"));
    }

    private static void ApplyBattleScrollBarStyle(ScrollContainer? scrollContainer)
    {
        if (scrollContainer == null)
        {
            return;
        }

        var scrollBar = scrollContainer.GetVScrollBar();
        scrollBar.CustomMinimumSize = new Vector2(8.0f, 0.0f);

        var track = new StyleBoxFlat
        {
            BgColor = new Color(0.055f, 0.042f, 0.018f, 0.86f),
            BorderColor = new Color(0.34f, 0.25f, 0.10f, 0.9f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };

        var grabber = new StyleBoxFlat
        {
            BgColor = new Color(0.55f, 0.40f, 0.15f, 0.96f),
            BorderColor = new Color(0.86f, 0.66f, 0.27f, 1.0f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        };
        var hoverGrabber = (StyleBoxFlat)grabber.Duplicate();
        hoverGrabber.BgColor = new Color(0.82f, 0.62f, 0.22f, 1.0f);
        hoverGrabber.BorderColor = new Color(1.0f, 0.84f, 0.44f, 1.0f);
        var pressedGrabber = (StyleBoxFlat)grabber.Duplicate();
        pressedGrabber.BgColor = new Color(0.37f, 0.27f, 0.09f, 1.0f);

        scrollBar.AddThemeStyleboxOverride("scroll", track);
        scrollBar.AddThemeStyleboxOverride("scroll_focus", track);
        scrollBar.AddThemeStyleboxOverride("grabber", grabber);
        scrollBar.AddThemeStyleboxOverride("grabber_highlight", hoverGrabber);
        scrollBar.AddThemeStyleboxOverride("grabber_pressed", pressedGrabber);
    }
}
