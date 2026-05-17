using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class MerchantUiContext
{
    private readonly HudController _owner;

    public MerchantUiContext(HudController owner)
    {
        _owner = owner;
    }

    public TurnManager? TurnManager => _owner.MerchantTurnManager;
    public LocalizationService? Localization => _owner.MerchantLocalization;
    public CityData? SelectedCity => _owner.MerchantSelectedCity;

    public Window CreateWindow(string scenePath, System.Action<Window> closeAction)
    {
        var dialog = GD.Load<PackedScene>(scenePath).Instantiate<Window>();
        dialog.Exclusive = false;
        dialog.Unresizable = true;
        dialog.CloseRequested += () =>
        {
            _owner.MerchantPlayUiClickSfx();
            closeAction(dialog);
        };
        _owner.AddChild(dialog);
        return dialog;
    }

    public void PopupDialog(Window? dialog) => _owner.MerchantPopupDialog(dialog);

    public CommandResult ExecuteMerchantCommand(int amount, MerchantTradeMode tradeMode) =>
        _owner.MerchantExecuteCommand(amount, tradeMode);
}
