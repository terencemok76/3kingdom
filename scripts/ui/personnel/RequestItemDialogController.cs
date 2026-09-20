using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class RequestItemDialogController : FloatingOverlayController
{
    private readonly PersonnelUiContext _context;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private OptionButton? _itemOption;
    private Button? _advisorButton;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(330.0f, 150.0f);

    public RequestItemDialogController(PersonnelUiContext context)
        : base(context, "res://scenes/ui/personnel/RequestItemDialog.tscn")
    {
        _context = context;
    }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public void Show()
    {
        if (_context.SelectedCity == null || _context.TurnManager?.World == null || _context.Localization == null)
        {
            return;
        }

        RefreshText();
        Populate();
        ShowOverlay();
    }

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization.T("command.personnel.request_item"));
        SetLabelText("OfficerListLabel", _context.Localization.T("ui.request_item_officer"));
        SetLabelText("ItemLabel", _context.Localization.T("ui.request_item"));
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_request_item");
        }
        if (_advisorButton != null) _advisorButton.Text = _context.Localization.T("ui.personnel_advice_item");
        UpdateSelectedOfficerSummary();
        RefreshItemOptionTexts();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _itemOption = root.GetNodeOrNull<OptionButton>("ItemRow/ItemOption");
        _advisorButton = root.GetNodeOrNull<Button>("ConfirmRow/AdvisorButton");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (_selectOfficerButton != null)
        {
            _context.ApplyCommandButtonTheme(_selectOfficerButton);
        }
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }
        if (_advisorButton != null) _context.ApplyCommandButtonTheme(_advisorButton);
        if (!_signalsConnected)
        {
            if (_selectOfficerButton != null)
            {
                _selectOfficerButton.Pressed += OnSelectOfficerPressed;
            }
            if (_confirmButton != null)
            {
                _confirmButton.Pressed += OnConfirmPressed;
            }
            if (_advisorButton != null) _advisorButton.Pressed += OnAdvisorPressed;
            _signalsConnected = true;
        }
    }

    private void Populate()
    {
        var candidateOfficerIds = GetCandidateOfficerIds();
        if (!candidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        UpdateSelectedOfficerSummary();
        PopulateItemOption();
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>(nodeName) ??
                    GetOverlayContentNode<Label>($"ItemRow/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void PopulateItemOption()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (_itemOption == null || world == null || localization == null)
        {
            return;
        }

        _itemOption.Clear();
        _itemOption.AddItem(localization.T("ui.no_item"));
        _itemOption.SetItemMetadata(0, 0);

        if (_selectedOfficerId <= 0)
        {
            _itemOption.Select(0);
            return;
        }

        foreach (var item in world.Items
                     .Where(item => item.EquippedOfficerId == _selectedOfficerId)
                     .OrderBy(localization.GetItemName))
        {
            var row = localization.Format(
                "fmt.item_option",
                localization.GetItemName(item),
                localization.GetItemType(item),
                localization.GetItemRarity(item));
            _itemOption.AddItem(row);
            _itemOption.SetItemMetadata(_itemOption.ItemCount - 1, item.Id);
        }

        _itemOption.Select(0);
    }

    private void RefreshItemOptionTexts()
    {
        if (_itemOption == null || _context.Localization == null)
        {
            return;
        }

        var selectedItemId = _context.GetSelectedItemFromOption(_itemOption)?.Id ?? 0;
        PopulateItemOption();
        SelectItemOption(selectedItemId);
    }

    private void OnAdvisorPressed()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        var officer = world?.GetOfficer(_selectedOfficerId);
        var item = _context.GetSelectedItemFromOption(_itemOption);
        if (city == null || world == null || localization == null) return;
        var advisor = _context.FindPersonnelAdvisor();
        var role = advisor?.Id == world.GetFaction(city.OwnerFactionId)?.ChancellorOfficerId ? localization.T("ui.chancellor") : localization.T("ui.local_place");
        var message = BuildRecallAdvice(world, city, officer, item, localization);
        _context.ShowAdvisorMessage(advisor, role, message);
    }

    private string BuildRecallAdvice(WorldState world, CityData city, OfficerData? officer, ItemData? item, LocalizationService localization)
    {
        if (officer == null)
        {
            var suggestion = FindSuggestedRecall(world, city);
            return suggestion.Officer == null || suggestion.Item == null
                ? localization.T("ui.personnel_advice_item_no_holder")
                : localization.Format(
                    "fmt.personnel_advice_item_choose_holder",
                    localization.GetOfficerName(suggestion.Officer),
                    localization.GetItemName(suggestion.Item),
                    localization.GetItemType(suggestion.Item));
        }

        var equippedItems = world.Items
            .Where(equippedItem => equippedItem.EquippedOfficerId == officer.Id)
            .ToList();
        if (item == null)
        {
            var leastRiskItem = equippedItems
                .OrderBy(equippedItem => GetRecallImpact(equippedItem, GetOfficerAppointments(world, city, officer)))
                .ThenBy(localization.GetItemName)
                .FirstOrDefault();
            return leastRiskItem == null
                ? localization.Format("fmt.personnel_advice_item_no_equipment", localization.GetOfficerName(officer))
                : localization.Format(
                    "fmt.personnel_advice_item_choose_item",
                    localization.GetOfficerName(officer),
                    equippedItems.Count,
                    localization.GetItemName(leastRiskItem),
                    localization.GetItemType(leastRiskItem));
        }

        var appointments = GetOfficerAppointments(world, city, officer);
        var appointmentText = appointments.Count > 0
            ? string.Join("、", appointments.Select(localization.GetAppointmentName))
            : localization.T("ui.none");
        var impact = GetRecallImpact(item, appointments);
        var officerName = localization.GetOfficerName(officer);
        var itemName = localization.GetItemName(item);
        var alternateItem = equippedItems
            .Where(equippedItem => equippedItem.Id != item.Id)
            .OrderBy(equippedItem => GetRecallImpact(equippedItem, appointments))
            .FirstOrDefault();
        var recipient = FindSuggestedRecipient(world, city, officer, item);

        var conclusion = appointments.Count == 0 && impact <= 35
            ? localization.T("ui.personnel_advice_item_conclusion_safe")
            : appointments.Count > 0 && impact >= 50
                ? localization.T("ui.personnel_advice_item_conclusion_hold")
                : localization.T("ui.personnel_advice_item_conclusion_caution");
        var expected = localization.Format(
            "fmt.personnel_advice_item_expected",
            officerName,
            itemName,
            item.StrengthBonus,
            item.IntelligenceBonus,
            item.LeadershipBonus,
            item.PoliticsBonus,
            item.CombatBonus,
            item.CharmBonus,
            Math.Max(1, item.LoyaltyBonus));
        var risk = appointments.Count == 0
            ? localization.Format("fmt.personnel_advice_item_risk_general", impact)
            : localization.Format("fmt.personnel_advice_item_risk_appointment", appointmentText, impact);
        var alternative = alternateItem != null
            ? localization.Format("fmt.personnel_advice_item_alternative_item", localization.GetItemName(alternateItem))
            : recipient != null
                ? localization.Format("fmt.personnel_advice_item_alternative_recipient", localization.GetOfficerName(recipient), recipient.Politics, recipient.Intelligence, recipient.Leadership)
                : localization.T("ui.personnel_advice_item_alternative_reserve");

        return localization.Format("fmt.personnel_advice_item_actionable", conclusion, expected, risk, alternative);
    }

    private (OfficerData? Officer, ItemData? Item) FindSuggestedRecall(WorldState world, CityData city)
    {
        var candidate = city.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => officer != null)
            .Select(officer => new
            {
                Officer = officer!,
                Item = world.Items
                    .Where(item => item.EquippedOfficerId == officer!.Id)
                    .OrderByDescending(GetItemValue)
                    .FirstOrDefault()
            })
            .Where(entry => entry.Item != null)
            .OrderByDescending(entry => GetItemValue(entry.Item!))
            .FirstOrDefault();
        return candidate == null ? (null, null) : (candidate.Officer, candidate.Item);
    }

    private OfficerData? FindSuggestedRecipient(WorldState world, CityData city, OfficerData holder, ItemData item)
    {
        return city.OfficerIds
            .Select(world.GetOfficer)
            .Where(candidate => candidate != null && candidate.Id != holder.Id && !_context.IsFactionRuler(world, candidate))
            .OrderByDescending(candidate => GetRecipientFit(candidate!, item))
            .FirstOrDefault();
    }

    private static List<string> GetOfficerAppointments(WorldState world, CityData city, OfficerData officer)
    {
        var appointments = officer.Appointments
            .Where(appointment => !appointment.Equals(OfficerAppointmentRules.Lord, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var faction = world.GetFaction(city.OwnerFactionId);
        if (faction?.ChancellorOfficerId == officer.Id) appointments.Add(OfficerAppointmentRules.Chancellor);
        if (faction?.ChiefStrategistOfficerId == officer.Id) appointments.Add(OfficerAppointmentRules.ChiefStrategist);
        return appointments.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static int GetRecallImpact(ItemData item, IReadOnlyCollection<string> appointments)
    {
        var impact = item.StrengthBonus + item.IntelligenceBonus + item.LeadershipBonus + item.PoliticsBonus + item.CombatBonus + item.CharmBonus + Math.Max(1, item.LoyaltyBonus);
        foreach (var appointment in appointments)
        {
            if (appointment.Equals(OfficerAppointmentRules.Governor, StringComparison.OrdinalIgnoreCase) ||
                appointment.Equals(OfficerAppointmentRules.Chancellor, StringComparison.OrdinalIgnoreCase))
            {
                impact += item.PoliticsBonus * 2 + item.IntelligenceBonus;
            }
            else if (appointment.Equals(OfficerAppointmentRules.Strategist, StringComparison.OrdinalIgnoreCase) ||
                     appointment.Equals(OfficerAppointmentRules.ChiefStrategist, StringComparison.OrdinalIgnoreCase))
            {
                impact += item.IntelligenceBonus * 2 + item.LeadershipBonus + item.CombatBonus;
            }
        }

        return impact;
    }

    private static int GetItemValue(ItemData item) =>
        item.StrengthBonus + item.IntelligenceBonus + item.LeadershipBonus + item.PoliticsBonus + item.CombatBonus + item.CharmBonus + Math.Max(1, item.LoyaltyBonus);

    private static int GetRecipientFit(OfficerData officer, ItemData item) => item.ItemType switch
    {
        ItemType.Weapon => officer.Strength * 3 + officer.Combat * 2 + officer.Leadership,
        ItemType.Horse => officer.Leadership * 3 + officer.Combat * 2 + officer.Strength,
        ItemType.Book => officer.Intelligence * 3 + officer.Politics * 2,
        ItemType.Treasure => officer.Charm * 3 + officer.Politics * 2 + officer.Intelligence,
        _ => officer.Politics + officer.Intelligence
    };

    private void OnConfirmPressed()
    {
        var city = _context.SelectedCity;
        var turnManager = _context.TurnManager;
        var commandResolver = _context.CommandResolver;
        if (city == null || turnManager == null || commandResolver == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            _context.AddLog(_context.Localization?.T("ui.select_officer_warning") ?? string.Empty);
            ShowOverlay();
            return;
        }

        var item = _context.GetSelectedItemFromOption(_itemOption);
        if (item == null)
        {
            _context.AddLog(_context.Localization?.T("ui.select_item_warning") ?? string.Empty);
            ShowOverlay();
            return;
        }

        var result = commandResolver.ExecuteRecallOfficerItem(turnManager.GetPlayerFactionId(), city.Id, _selectedOfficerId, item.Id);
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        if (result.Success)
        {
            _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
            _context.UiEventHub.PublishOfficerStateChanged(_selectedOfficerId, city.Id, city.OwnerFactionId);
            _context.RefreshMapVisuals();
        }
        HideOverlay();
    }

    private void OnSelectOfficerPressed()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        var candidateOfficerIds = GetCandidateOfficerIds();
        if (candidateOfficerIds.Count == 0)
        {
            _context.AddLog(localization.T("ui.select_officer_warning"));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("ui.request_item_officer"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Politics,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
                PopulateItemOption();
            },
            titleFactory: () => _context.Localization?.T("ui.request_item_officer") ?? localization.T("ui.request_item_officer"));
    }

    private void UpdateSelectedOfficerSummary()
    {
        if (_selectedOfficerLabel == null || _context.Localization == null)
        {
            return;
        }

        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null ? _context.Localization.GetOfficerName(officer) : _context.Localization.T("ui.unassigned");
        _selectedOfficerLabel.Text = $"{_context.Localization.T("ui.request_item_officer")}: {officerName}";
    }

    private List<int> GetCandidateOfficerIds()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        if (city == null || world == null)
        {
            return new List<int>();
        }

        return city.OfficerIds
            .Where(officerId =>
            {
                var officer = world.GetOfficer(officerId);
                return officer != null && world.Items.Any(item => item.EquippedOfficerId == officer.Id);
            })
            .ToList();
    }

    private void SelectItemOption(int itemId)
    {
        if (_itemOption == null)
        {
            return;
        }

        for (var index = 0; index < _itemOption.ItemCount; index += 1)
        {
            var metadata = _itemOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == itemId)
            {
                _itemOption.Select(index);
                return;
            }
        }

        if (_itemOption.ItemCount > 0)
        {
            _itemOption.Select(0);
        }
    }
}
