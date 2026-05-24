using System.Collections.Generic;
using Godot;

namespace ThreeKingdom.UI;

public sealed class SpyUiController
{
    private readonly SpyDialogController _dialogController;

    public SpyUiController(HudController owner)
    {
        _dialogController = new SpyDialogController(new SpyUiContext(owner));
    }

    public void Initialize()
    {
        _dialogController.Initialize();
    }

    public void HideDialogs()
    {
        _dialogController.Hide();
    }

    public void RefreshText()
    {
        _dialogController.RefreshText();
    }

    public void ShowSpyDialog() => _dialogController.Show();

    public void CollectVisibleDialogOverlays(List<Control> overlays)
    {
        if (_dialogController.OverlayControl?.Visible == true)
        {
            overlays.Add(_dialogController.OverlayControl);
        }
    }
}
