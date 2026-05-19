using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    internal TurnManager? MerchantTurnManager => _turnManager;
    internal LocalizationService? MerchantLocalization => _localization;
    internal CityData? MerchantSelectedCity => _selectedCity;
    internal Control? MerchantOverlayParent => GetNodeOrNull<Control>("Root");

    internal void MerchantPopupDialog(Control? dialog) => ShowOverlay(dialog);
    internal void MerchantBringOverlayToFront(CanvasItem? item) => item?.MoveToFront();
    internal void MerchantPlayUiClickSfx() => PlayUiClickSfx();
    internal void MerchantApplyCommandButtonTheme(Button button)
    {
        if (MainHudViewButton != null)
        {
            CopyButtonTheme(MainHudViewButton, button);
        }
    }
    internal CommandResult MerchantExecuteCommand(int amount, MerchantTradeMode tradeMode) =>
        ExecutePlayerCommand(
            CommandType.Merchant,
            foodToSend: amount,
            sellFood: tradeMode == MerchantTradeMode.SellFood,
            merchantTradeMode: tradeMode);
}
