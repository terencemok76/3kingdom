using Godot;

namespace ThreeKingdom.Map;

public partial class ChinaBackgroundMap : Node2D
{
    private static readonly Vector2[] MainlandOutline =
    {
        new(188.0f, 286.0f), new(222.0f, 240.0f), new(270.0f, 210.0f), new(322.0f, 194.0f),
        new(372.0f, 176.0f), new(430.0f, 162.0f), new(495.0f, 150.0f), new(566.0f, 148.0f),
        new(624.0f, 154.0f), new(670.0f, 168.0f), new(712.0f, 176.0f), new(760.0f, 188.0f),
        new(804.0f, 214.0f), new(845.0f, 246.0f), new(878.0f, 288.0f), new(904.0f, 326.0f),
        new(930.0f, 362.0f), new(942.0f, 402.0f), new(934.0f, 440.0f), new(914.0f, 468.0f),
        new(884.0f, 494.0f), new(858.0f, 520.0f), new(836.0f, 548.0f), new(808.0f, 570.0f),
        new(770.0f, 590.0f), new(726.0f, 608.0f), new(676.0f, 624.0f), new(626.0f, 638.0f),
        new(572.0f, 650.0f), new(518.0f, 654.0f), new(468.0f, 648.0f), new(420.0f, 640.0f),
        new(374.0f, 628.0f), new(330.0f, 612.0f), new(292.0f, 594.0f), new(258.0f, 570.0f),
        new(234.0f, 540.0f), new(214.0f, 512.0f), new(198.0f, 478.0f), new(186.0f, 442.0f),
        new(178.0f, 404.0f), new(174.0f, 368.0f), new(176.0f, 334.0f)
    };

    private static readonly Vector2[] Hainan =
    {
        new(548.0f, 668.0f), new(574.0f, 657.0f), new(598.0f, 666.0f),
        new(592.0f, 688.0f), new(563.0f, 694.0f), new(544.0f, 681.0f)
    };

    private static readonly Vector2[] Taiwan =
    {
        new(902.0f, 564.0f), new(923.0f, 548.0f), new(936.0f, 573.0f),
        new(922.0f, 604.0f), new(901.0f, 595.0f)
    };

    public override void _Draw()
    {
        DrawSea();
        DrawLand();
        DrawProvinceHints();
        DrawRivers();
    }

    private void DrawSea()
    {
        DrawRect(new Rect2(0.0f, 0.0f, 1500.0f, 950.0f), new Color("0e161d"));
        DrawRect(new Rect2(90.0f, 100.0f, 1080.0f, 700.0f), new Color("17303b", 0.38f));
        DrawRect(new Rect2(120.0f, 130.0f, 1020.0f, 640.0f), new Color("244a58", 0.22f));
    }

    private void DrawLand()
    {
        DrawColoredPolygon(MainlandOutline, new Color("3f5b45"));
        DrawPolyline(MainlandOutline, new Color("b5d7bc"), 3.0f, true);

        DrawColoredPolygon(Hainan, new Color("3f5b45"));
        DrawPolyline(Hainan, new Color("b5d7bc"), 2.0f, true);

        DrawColoredPolygon(Taiwan, new Color("3f5b45"));
        DrawPolyline(Taiwan, new Color("b5d7bc"), 2.0f, true);

        DrawRect(new Rect2(250.0f, 220.0f, 610.0f, 360.0f), new Color("9bbd90", 0.08f));
    }

    private void DrawProvinceHints()
    {
        var c = new Color("2b4336", 0.46f);
        DrawLine(new Vector2(252.0f, 262.0f), new Vector2(790.0f, 262.0f), c, 1.6f);
        DrawLine(new Vector2(236.0f, 350.0f), new Vector2(860.0f, 350.0f), c, 1.6f);
        DrawLine(new Vector2(228.0f, 446.0f), new Vector2(840.0f, 446.0f), c, 1.6f);
        DrawLine(new Vector2(286.0f, 546.0f), new Vector2(782.0f, 546.0f), c, 1.6f);

        DrawLine(new Vector2(342.0f, 212.0f), new Vector2(300.0f, 596.0f), c, 1.3f);
        DrawLine(new Vector2(472.0f, 178.0f), new Vector2(438.0f, 638.0f), c, 1.3f);
        DrawLine(new Vector2(612.0f, 164.0f), new Vector2(602.0f, 640.0f), c, 1.3f);
        DrawLine(new Vector2(748.0f, 186.0f), new Vector2(732.0f, 604.0f), c, 1.3f);
    }

    private void DrawRivers()
    {
        var river = new Color("7eaec0", 0.50f);
        DrawCurve(new Vector2(260.0f, 334.0f), new Vector2(388.0f, 306.0f), new Vector2(566.0f, 348.0f), new Vector2(776.0f, 340.0f), river, 2.4f);
        DrawCurve(new Vector2(308.0f, 504.0f), new Vector2(432.0f, 474.0f), new Vector2(612.0f, 518.0f), new Vector2(796.0f, 500.0f), river, 2.0f);
        DrawCurve(new Vector2(402.0f, 240.0f), new Vector2(454.0f, 306.0f), new Vector2(468.0f, 390.0f), new Vector2(452.0f, 478.0f), river, 1.6f);
    }

    private void DrawCurve(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color color, float width)
    {
        var curve = new Curve2D();
        curve.AddPoint(p0);
        curve.AddPoint(p1);
        curve.AddPoint(p2);
        curve.AddPoint(p3);

        var baked = curve.GetBakedPoints();
        if (baked.Length >= 2)
        {
            DrawPolyline(baked, color, width, false);
        }
    }
}
