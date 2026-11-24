using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private CanvasLayer? _exiledRulerLayer;

    private bool TryShowExiledRulerMovement(WorldState world)
    {
        var ruler = FreeOfficerMovement.GetPlayerExiledRuler(world);
        if (ruler == null || _localization == null || ruler.LastAssignedYear == world.Year && ruler.LastAssignedMonth == world.Month) return false;
        if (_exiledRulerLayer != null) return true;
        var origin = world.GetCity(ruler.CityId)!;
        var localization = _localization;
        var layer = new CanvasLayer { Layer = 250 };
        _exiledRulerLayer = layer;
        AddChild(layer);
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.72f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(shade);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(550, 0) };
        center.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        box.AddChild(new Label { Text = localization.T("ui.exile.title"), HorizontalAlignment = HorizontalAlignment.Center });
        box.AddChild(new Label { Text = localization.Format("ui.exile.prompt", localization.GetOfficerName(ruler), localization.GetCityName(origin)), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(530, 0) });
        var list = new VBoxContainer();
        box.AddChild(list);
        foreach (var city in origin.ConnectedCityIds.Distinct().Select(world.GetCity).Where(city => city != null).OrderBy(city => city!.Id))
        {
            var destination = city!;
            var button = new Button { Text = localization.Format(destination.OwnerFactionId == 0 ? "ui.exile.destination_restore" : "ui.exile.destination", localization.GetCityName(destination)) };
            list.AddChild(button);
            button.Pressed += () =>
            {
                if (!FreeOfficerMovement.TryMovePlayerExiledRuler(world, destination.Id)) return;
                AddLog(localization.Format("ui.exile.moved", localization.GetOfficerName(ruler), localization.GetCityName(destination)), true);
                _mapController?.SelectCityById(destination.Id);
                _mapController?.RefreshVisuals();
                _exiledRulerLayer = null;
                layer.QueueFree();
                RefreshSelectedCity();
            };
        }
        var cancel = new Button { Text = localization.T("ui.exile.cancel_move") };
        box.AddChild(cancel);
        cancel.Pressed += () =>
        {
            _exiledRulerLayer = null;
            layer.QueueFree();
        };
        return true;
    }
}
