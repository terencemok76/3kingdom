using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class PersonnelUiContext : IFloatingOverlayContext
{
    private readonly HudController _owner;

    public PersonnelUiContext(HudController owner)
    {
        _owner = owner;
    }

    public TurnManager? TurnManager => _owner.PersonnelTurnManager;
    public CommandResolver? CommandResolver => _owner.PersonnelCommandResolver;
    public LocalizationService? Localization => _owner.PersonnelLocalization;
    public CityData? SelectedCity => _owner.PersonnelSelectedCity;

    public Window CreateWindow(string scenePath, Action<Window> closeAction)
    {
        var dialog = GD.Load<PackedScene>(scenePath).Instantiate<Window>();
        dialog.Exclusive = false;
        dialog.Unresizable = true;
        dialog.CloseRequested += () =>
        {
            _owner.PersonnelPlayUiClickSfx();
            closeAction(dialog);
        };
        _owner.AddChild(dialog);
        return dialog;
    }

    public Control CreateOverlay(string scenePath, Action closeAction)
    {
        var dialog = GD.Load<PackedScene>(scenePath).Instantiate<Control>();
        dialog.Visible = false;
        Node parent = _owner;
        if (_owner.PersonnelOverlayParent != null)
        {
            parent = _owner.PersonnelOverlayParent;
        }

        parent.AddChild(dialog);
        return dialog;
    }

    public void AddChild(Node node) => _owner.AddChild(node);

    public void PlayUiClickSfx() => _owner.PersonnelPlayUiClickSfx();

    public void PopupDialog(Window? dialog) => _owner.PersonnelPopupDialog(dialog);
    public void PopupDialog(Control? dialog) => _owner.PersonnelPopupDialog(dialog);
    public void CloseOverlay(Action closeAction)
    {
        _owner.PersonnelPlayUiClickSfx();
        closeAction();
    }
    public void BringOverlayToFront(CanvasItem? item) => _owner.PersonnelBringOverlayToFront(item);

    public void AddLog(string message, bool isPlayerRelated = false) => _owner.PersonnelAddLog(message, isPlayerRelated);

    public void RefreshSelectedCity() => _owner.PersonnelRefreshSelectedCity();
    public void RefreshMapVisuals() => _owner.PersonnelRefreshMapVisuals();
    public UiEventHub UiEventHub => _owner.PersonnelUiEventHub;

    public void ConfigureMoveSpinBox(SpinBox? spinBox, int maxValue, int value) => _owner.PersonnelConfigureMoveSpinBox(spinBox, maxValue, value);

    public void ShowOfficerSelectorDialog(
        string title,
        List<int> candidateOfficerIds,
        HudController.OfficerSelectorPrimaryStat primaryStat,
        Action<int> confirmedAction,
        IEnumerable<HudController.OfficerSelectorScopeOption>? scopeOptions = null,
        string? initialScopeKey = null,
        Func<string>? titleFactory = null,
        Func<IEnumerable<HudController.OfficerSelectorScopeOption>?>? scopeOptionsFactory = null,
        HudController.OfficerSelectorDisplayConfig? displayConfig = null,
        Func<HudController.OfficerSelectorDisplayConfig?>? displayConfigFactory = null) =>
        _owner.PersonnelShowOfficerSelectorDialog(title, candidateOfficerIds, primaryStat, confirmedAction, scopeOptions, initialScopeKey, titleFactory, scopeOptionsFactory, displayConfig, displayConfigFactory);

    public void ShowAssignRoleOfficerSelectorDialog(
        string title,
        List<int> candidateOfficerIds,
        Action<int> confirmedAction,
        IEnumerable<HudController.OfficerSelectorScopeOption>? scopeOptions = null,
        string? initialScopeKey = null,
        Func<string>? titleFactory = null,
        Func<IEnumerable<HudController.OfficerSelectorScopeOption>?>? scopeOptionsFactory = null,
        Func<HudController.OfficerSelectorDisplayConfig?>? displayConfigFactory = null) =>
        _owner.PersonnelShowAssignRoleOfficerSelectorDialog(title, candidateOfficerIds, confirmedAction, scopeOptions, initialScopeKey, titleFactory, scopeOptionsFactory, displayConfigFactory);

    public void ContinuePendingNonAttackResolution() => _owner.PersonnelContinuePendingNonAttackResolution();

    public string GetLocalizedResultMessage(CommandResult result) => _owner.PersonnelGetLocalizedResultMessage(result);

    public bool IsFactionRuler(WorldState world, OfficerData officer) => _owner.PersonnelIsFactionRuler(world, officer);

    public bool IsOfficerOldEnoughToJoin(WorldState world, OfficerData officer) => _owner.PersonnelIsOfficerOldEnoughToJoin(world, officer);
    public Texture2D? BuildOfficerPortraitTexture(int officerId) => _owner.PersonnelBuildOfficerPortraitTexture(officerId);
    public string BuildOfficerDetailText(OfficerData officer) => _owner.PersonnelBuildOfficerDetailText(officer);
    public string GetPortraitLabel() => _owner.PersonnelGetPortraitLabel();
    public void ApplyCommandButtonTheme(Button button) => _owner.PersonnelApplyCommandButtonTheme(button);

    public List<int> GetAvailableOfficerIdsForOrder() => _owner.PersonnelGetAvailableOfficerIdsForOrder();

    public List<int> GetNonRulerCityOfficerIds()
    {
        var city = SelectedCity;
        var world = TurnManager?.World;
        if (city == null || world == null)
        {
            return new List<int>();
        }

        var faction = world.GetFaction(city.OwnerFactionId);
        if (faction == null)
        {
            return new List<int>();
        }

        return faction.OfficerIds
            .Where(officerId =>
            {
                var officer = world.GetOfficer(officerId);
                return officer != null && officer.CityId == city.Id;
            })
            .Where(officerId =>
            {
                var officer = world.GetOfficer(officerId);
                return officer != null && !IsFactionRuler(world, officer);
            })
            .ToList();
    }

    public List<int> GetNonRulerFactionOfficerIds()
    {
        var city = SelectedCity;
        var world = TurnManager?.World;
        if (city == null || world == null)
        {
            return new List<int>();
        }

        var faction = world.GetFaction(city.OwnerFactionId);
        if (faction == null)
        {
            return new List<int>();
        }

        return faction.OfficerIds
            .Where(officerId =>
            {
                var officer = world.GetOfficer(officerId);
                return officer != null && !IsFactionRuler(world, officer);
            })
            .ToList();
    }

    public void PopulateFactionInventoryOption(OptionButton? option)
    {
        var city = SelectedCity;
        var world = TurnManager?.World;
        var localization = Localization;
        if (option == null || city == null || world == null || localization == null)
        {
            return;
        }

        option.Clear();
        option.AddItem(localization.T("ui.no_item"));
        option.SetItemMetadata(0, 0);

        foreach (var item in world.Items
                     .Where(item => item.OwnerFactionId == city.OwnerFactionId && item.EquippedOfficerId <= 0)
                     .OrderBy(localization.GetItemName))
        {
            var row = localization.Format(
                "fmt.item_option",
                localization.GetItemName(item),
                localization.GetItemType(item),
                localization.GetItemRarity(item));
            option.AddItem(row);
            option.SetItemMetadata(option.ItemCount - 1, item.Id);
        }

        option.Select(0);
    }

    public ItemData? GetSelectedItemFromOption(OptionButton? option)
    {
        var world = TurnManager?.World;
        if (option == null || world == null)
        {
            return null;
        }

        var selectedIndex = option.Selected;
        if (selectedIndex < 0)
        {
            return null;
        }

        var metadata = option.GetItemMetadata(selectedIndex);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return null;
        }

        var itemId = metadata.AsInt32();
        return itemId > 0 ? world.GetItem(itemId) : null;
    }

    public void PopulateAppointmentOption(OptionButton? option)
    {
        var localization = Localization;
        if (option == null || localization == null)
        {
            return;
        }

        option.Clear();
        option.AddItem(localization.T("ui.none"));
        option.SetItemMetadata(0, string.Empty);

        AddAppointmentOption(option, OfficerAppointmentRules.Governor);
        AddAppointmentOption(option, OfficerAppointmentRules.Strategist);
        AddAppointmentOption(option, OfficerAppointmentRules.Chancellor);
        AddAppointmentOption(option, OfficerAppointmentRules.ChiefStrategist);
        option.Select(0);
    }

    public string GetAppointmentDisplayName(string appointment)
    {
        var localization = Localization;
        if (localization == null)
        {
            return appointment;
        }

        return appointment.ToLowerInvariant() switch
        {
            "governor" => localization.T("role.governor"),
            "strategist" => localization.T("role.strategist"),
            "chancellor" => localization.T("ui.chancellor"),
            "chiefstrategist" => localization.T("ui.chief_strategist"),
            _ => appointment
        };
    }

    public string GetSelectedAppointmentFromOption(OptionButton? option)
    {
        if (option == null || option.Selected < 0)
        {
            return string.Empty;
        }

        var metadata = option.GetItemMetadata(option.Selected);
        return metadata.VariantType == Variant.Type.String ? metadata.AsString() : string.Empty;
    }

    private void AddAppointmentOption(OptionButton option, string appointment)
    {
        option.AddItem(GetAppointmentDisplayName(appointment));
        option.SetItemMetadata(option.ItemCount - 1, appointment);
    }

    public HudController.OfficerSelectorDisplayConfig BuildAssignRoleOfficerSelectorDisplayConfig()
        => _owner.PersonnelBuildAssignRoleOfficerSelectorDisplayConfig();

    public HudController.OfficerSelectorDisplayConfig BuildPrisonerOfficerSelectorDisplayConfig()
        => _owner.PersonnelBuildPrisonerOfficerSelectorDisplayConfig();
}
