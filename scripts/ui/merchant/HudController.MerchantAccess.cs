using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    internal TurnManager? MerchantTurnManager => _turnManager;
    internal LocalizationService? MerchantLocalization => _localization;
    internal CityData? MerchantSelectedCity => _selectedCity;

    internal void MerchantPopupDialog(Window? dialog) => PopupDialogUsingSceneSize(dialog);
    internal void MerchantPlayUiClickSfx() => PlayUiClickSfx();
    internal CommandResult MerchantExecuteCommand(int amount, MerchantTradeMode tradeMode) =>
        ExecutePlayerCommand(
            CommandType.Merchant,
            foodToSend: amount,
            sellFood: tradeMode == MerchantTradeMode.SellFood,
            merchantTradeMode: tradeMode);
}
