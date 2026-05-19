using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class MerchantUiContext
    : IFloatingOverlayContext
{
    private readonly HudController _owner;

    public MerchantUiContext(HudController owner)
    {
        _owner = owner;
    }

    public TurnManager? TurnManager => _owner.MerchantTurnManager;
    public LocalizationService? Localization => _owner.MerchantLocalization;
    public CityData? SelectedCity => _owner.MerchantSelectedCity;

    public Control CreateOverlay(string scenePath, System.Action closeAction)
    {
        var dialog = GD.Load<PackedScene>(scenePath).Instantiate<Control>();
        dialog.Visible = false;
        Node parent = _owner;
        if (_owner.MerchantOverlayParent != null)
        {
            parent = _owner.MerchantOverlayParent;
        }

        parent.AddChild(dialog);
        return dialog;
    }

    public void PopupDialog(Control? dialog) => _owner.MerchantPopupDialog(dialog);

    public void CloseOverlay(System.Action closeAction)
    {
        _owner.MerchantPlayUiClickSfx();
        closeAction();
    }

    public void BringOverlayToFront(CanvasItem? item) => _owner.MerchantBringOverlayToFront(item);
    public void ApplyCommandButtonTheme(Button button) => _owner.MerchantApplyCommandButtonTheme(button);

    public CommandResult ExecuteMerchantCommand(int amount, MerchantTradeMode tradeMode) =>
        _owner.MerchantExecuteCommand(amount, tradeMode);
}
