using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class PrisonerManagementDialogController : FloatingOverlayController
{
    private readonly PersonnelUiContext _context;
    private readonly PrisonerRecruitAcceptedDialogController _recruitAcceptedDialogController;
    private Label? _summaryLabel;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private TextureRect? _portraitRect;
    private Label? _portraitPlaceholder;
    private RichTextLabel? _detailLabel;
    private RichTextLabel? _speechLabel;
    private SpinBox? _goldSpinBox;
    private OptionButton? _itemOption;
    private OptionButton? _appointmentOption;
    private Label? _offerSummaryLabel;
    private RichTextLabel? _warningLabel;
    private Button? _recruitButton;
    private Button? _freeButton;
    private Button? _executeButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;

    protected override Vector2 MinimumOverlaySize => new(920.0f, 560.0f);

    public PrisonerManagementDialogController(PersonnelUiContext context)
        : base(context, "res://scenes/ui/personnel/PrisonerManagementDialog.tscn")
    {
        _context = context;
        _recruitAcceptedDialogController = new PrisonerRecruitAcceptedDialogController(context);
    }

    public void Initialize()
    {
        InitializeOverlay();
        _recruitAcceptedDialogController.Initialize();
    }

    public void Hide()
    {
        HideOverlay();
        _recruitAcceptedDialogController.Hide();
    }

    public void RefreshText()
    {
        if (IsOverlayVisible)
        {
            Show();
        }

        _recruitAcceptedDialogController.RefreshText();
    }

    public void Show()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var localization = _context.Localization;
        if (world == null || city == null || localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(localization.T("ui.prisoner_management"));
        var prisoners = GetPrisonersInSelectedCity(world, city);
        if (_summaryLabel != null)
        {
            _summaryLabel.Text = localization.Format(
                "ui.prisoner_management_summary",
                localization.GetCityName(city),
                prisoners.Count);
        }

        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = localization.T("ui.select_officer");
        }

        SetLabelText("GoldLabel", localization.T("ui.personnel_bonus_gold"));
        SetLabelText("ItemLabel", localization.T("ui.personnel_bonus_item"));
        SetLabelText("AppointmentLabel", localization.T("ui.assign_appointment"));

        if (_recruitButton != null)
        {
            _recruitButton.Text = localization.T("ui.captured_officer.recruit");
            _recruitButton.Disabled = prisoners.Count == 0;
        }

        if (_freeButton != null)
        {
            _freeButton.Text = localization.T("ui.captured_officer.free");
            _freeButton.Disabled = prisoners.Count == 0;
        }

        if (_executeButton != null)
        {
            _executeButton.Text = localization.T("ui.captured_officer.kill");
            _executeButton.Disabled = prisoners.Count == 0;
        }

        if (!prisoners.Any(officer => officer.Id == _selectedOfficerId))
        {
            _selectedOfficerId = prisoners.FirstOrDefault()?.Id ?? -1;
        }

        if (_warningLabel != null)
        {
            SetWarningMessage(
                prisoners.Count == 0 ? localization.T("ui.no_prisoners") : string.Empty,
                "#D7C08A");
        }

        SyncGoldOfferInput();
        RefreshOfferOptionTexts();
        UpdateSelectedOfficerSummary();
        UpdateSelectedOfficerPresentation();
        UpdateOfferSummary();
        ShowOverlay();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("PrisonerInfoRow/InfoColumn/OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("PrisonerInfoRow/InfoColumn/OfficerSelectorRow/SelectOfficerButton");
        _portraitRect = root.GetNodeOrNull<TextureRect>("PrisonerInfoRow/PortraitPanel/PortraitCenter/PortraitStack/PortraitRect");
        _portraitPlaceholder = root.GetNodeOrNull<Label>("PrisonerInfoRow/PortraitPanel/PortraitCenter/PortraitStack/PortraitPlaceholder");
        _detailLabel = root.GetNodeOrNull<RichTextLabel>("PrisonerInfoRow/InfoColumn/DetailLabel");
        _speechLabel = root.GetNodeOrNull<RichTextLabel>("PrisonerInfoRow/InfoColumn/SpeechLabel");
        _goldSpinBox = root.GetNodeOrNull<SpinBox>("GoldRow/GoldSpinBox");
        _itemOption = root.GetNodeOrNull<OptionButton>("ItemRow/ItemOption");
        _appointmentOption = root.GetNodeOrNull<OptionButton>("AppointmentRow/AppointmentOption");
        _offerSummaryLabel = root.GetNodeOrNull<Label>("OfferSummaryLabel");
        _warningLabel = root.GetNodeOrNull<RichTextLabel>("WarningLabel");
        _recruitButton = root.GetNodeOrNull<Button>("ActionRow/RecruitButton");
        _freeButton = root.GetNodeOrNull<Button>("ActionRow/FreeButton");
        _executeButton = root.GetNodeOrNull<Button>("ActionRow/ExecuteButton");

        foreach (var button in new[] { _selectOfficerButton, _recruitButton, _freeButton, _executeButton })
        {
            if (button != null)
            {
                _context.ApplyCommandButtonTheme(button);
            }
        }

        if (_signalsConnected)
        {
            return;
        }

        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Pressed += OnSelectOfficerPressed;
        }

        if (_goldSpinBox != null)
        {
            _goldSpinBox.ValueChanged += _ => UpdateOfferSummary();
        }

        if (_itemOption != null)
        {
            _itemOption.ItemSelected += _ => UpdateOfferSummary();
        }

        if (_appointmentOption != null)
        {
            _appointmentOption.ItemSelected += _ => UpdateOfferSummary();
        }

        if (_recruitButton != null)
        {
            _recruitButton.Pressed += () => ResolveDisposition(CapturedOfficerDisposition.Recruit);
        }

        if (_freeButton != null)
        {
            _freeButton.Pressed += () => ResolveDisposition(CapturedOfficerDisposition.Free);
        }

        if (_executeButton != null)
        {
            _executeButton.Pressed += () => ResolveDisposition(CapturedOfficerDisposition.Kill);
        }

        _signalsConnected = true;
        PopulateOfferInputs();
    }

    private void OnSelectOfficerPressed()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var localization = _context.Localization;
        if (world == null || city == null || localization == null)
        {
            return;
        }

        var prisonerIds = GetPrisonersInSelectedCity(world, city)
            .Select(officer => officer.Id)
            .ToList();
        if (prisonerIds.Count == 0)
        {
            if (_warningLabel != null)
            {
                SetWarningMessage(localization.T("ui.no_prisoners"), "#D7C08A");
            }

            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("ui.prisoner_management"),
            prisonerIds,
            HudController.OfficerSelectorPrimaryStat.Intelligence,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
                UpdateSelectedOfficerPresentation();
                if (_warningLabel != null)
                {
                    SetWarningMessage(string.Empty);
                }
            },
            titleFactory: () => _context.Localization?.T("ui.prisoner_management") ?? localization.T("ui.prisoner_management"),
            displayConfig: _context.BuildPrisonerOfficerSelectorDisplayConfig());
    }

    private void ResolveDisposition(CapturedOfficerDisposition disposition)
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var commandResolver = _context.CommandResolver;
        if (world == null || city == null || commandResolver == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            if (_warningLabel != null)
            {
                SetWarningMessage(_context.Localization?.T("ui.select_officer_warning") ?? "Select an officer.", "#E39A74");
            }

            return;
        }

        var recruitOffer = disposition == CapturedOfficerDisposition.Recruit
            ? BuildRecruitOffer()
            : null;
        var officer = world.GetOfficer(_selectedOfficerId);
        var result = commandResolver.ResolveCapturedOfficerDisposition(city.OwnerFactionId, _selectedOfficerId, disposition, recruitOffer);
        if (!result.Success)
        {
            if (_warningLabel != null)
            {
                SetWarningMessage(
                    _context.GetLocalizedResultMessage(result),
                    disposition == CapturedOfficerDisposition.Recruit ? "#E39A74" : "#D7C08A");
            }

            ShowOverlay();
            return;
        }

        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
        _context.UiEventHub.PublishOfficerStateChanged(_selectedOfficerId, city.Id, city.OwnerFactionId);
        _context.UiEventHub.PublishOfficerAppointmentsChanged(_selectedOfficerId, city.Id, city.OwnerFactionId);
        _context.UiEventHub.PublishFactionLeadershipChanged(city.OwnerFactionId, city.Id);
        _context.RefreshSelectedCity();

        if (officer != null &&
            (disposition == CapturedOfficerDisposition.Recruit ||
             disposition == CapturedOfficerDisposition.Free ||
             disposition == CapturedOfficerDisposition.Kill))
        {
            HideOverlay();
            _recruitAcceptedDialogController.ShowDispositionResult(
                officer,
                city,
                disposition,
                recruitOffer,
                Show);
            return;
        }

        Show();
    }

    private void UpdateSelectedOfficerSummary()
    {
        if (_selectedOfficerLabel == null || _context.Localization == null)
        {
            return;
        }

        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null
            ? _context.Localization.GetOfficerName(officer)
            : _context.Localization.T("ui.unassigned");
        _selectedOfficerLabel.Text = $"{_context.Localization.T("ui.officers")}: {officerName}";
    }

    private void UpdateSelectedOfficerPresentation()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var localization = _context.Localization;
        var officer = _selectedOfficerId > 0 ? world?.GetOfficer(_selectedOfficerId) : null;
        if (localization == null || world == null || city == null)
        {
            return;
        }

        if (_detailLabel != null)
        {
            _detailLabel.Text = officer != null ? _context.BuildOfficerDetailText(officer) : string.Empty;
        }

        if (_speechLabel != null)
        {
            _speechLabel.Text = officer != null
                ? BuildCapturedOfficerSpeech(world, officer, city.OwnerFactionId)
                : string.Empty;
        }

        if (_portraitRect != null)
        {
            _portraitRect.Texture = officer != null
                ? _context.BuildOfficerPortraitTexture(officer.Id)
                : null;
        }

        if (_portraitPlaceholder != null)
        {
            var officerName = officer != null ? localization.GetOfficerName(officer) : localization.T("ui.unassigned");
            _portraitPlaceholder.Visible = _portraitRect?.Texture == null;
            _portraitPlaceholder.Text = $"{_context.GetPortraitLabel()}\n{officerName}";
        }
    }

    private void PopulateOfferInputs()
    {
        SyncGoldOfferInput();
        _context.PopulateFactionInventoryOption(_itemOption);
        _context.PopulateAppointmentOption(_appointmentOption);
        UpdateOfferSummary();
    }

    private void SyncGoldOfferInput()
    {
        var city = _context.SelectedCity;
        if (city == null || _goldSpinBox == null)
        {
            return;
        }

        var currentValue = (int)_goldSpinBox.Value;
        _context.ConfigureMoveSpinBox(_goldSpinBox, city.Gold, Math.Min(currentValue, city.Gold));
        _goldSpinBox.Step = 100;
    }

    private void RefreshOfferOptionTexts()
    {
        if (_context.Localization == null)
        {
            return;
        }

        var selectedItemId = _context.GetSelectedItemFromOption(_itemOption)?.Id ?? 0;
        var selectedAppointment = _context.GetSelectedAppointmentFromOption(_appointmentOption);
        _context.PopulateFactionInventoryOption(_itemOption);
        _context.PopulateAppointmentOption(_appointmentOption);
        SelectItemOption(selectedItemId);
        SelectAppointmentOption(selectedAppointment);
    }

    private void UpdateOfferSummary()
    {
        if (_offerSummaryLabel == null || _context.Localization == null)
        {
            return;
        }

        var goldAmount = (int)(_goldSpinBox?.Value ?? 0);
        var item = _context.GetSelectedItemFromOption(_itemOption);
        var appointment = _context.GetSelectedAppointmentFromOption(_appointmentOption);
        var itemName = item != null ? _context.Localization.GetItemName(item) : _context.Localization.T("ui.none");
        var appointmentName = string.IsNullOrWhiteSpace(appointment)
            ? _context.Localization.T("ui.none")
            : _context.GetAppointmentDisplayName(appointment);

        _offerSummaryLabel.Text = _context.Localization.Format(
            "ui.prisoner_recruit_offer_summary",
            goldAmount,
            itemName,
            appointmentName);
    }

    private CapturedOfficerRecruitOfferData BuildRecruitOffer()
    {
        return new CapturedOfficerRecruitOfferData
        {
            GoldAmount = (int)(_goldSpinBox?.Value ?? 0),
            ItemId = _context.GetSelectedItemFromOption(_itemOption)?.Id ?? 0,
            Appointment = _context.GetSelectedAppointmentFromOption(_appointmentOption)
        };
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>(nodeName) ??
                    GetOverlayContentNode<Label>($"GoldRow/{nodeName}") ??
                    GetOverlayContentNode<Label>($"ItemRow/{nodeName}") ??
                    GetOverlayContentNode<Label>($"AppointmentRow/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void SetWarningMessage(string text, string color = "#D7C08A")
    {
        if (_warningLabel == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            _warningLabel.Text = string.Empty;
            return;
        }

        _warningLabel.Text = $"[color={color}]{EscapeBb(text)}[/color]";
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

    private void SelectAppointmentOption(string appointment)
    {
        if (_appointmentOption == null)
        {
            return;
        }

        for (var index = 0; index < _appointmentOption.ItemCount; index += 1)
        {
            var metadata = _appointmentOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.String && metadata.AsString() == appointment)
            {
                _appointmentOption.Select(index);
                return;
            }
        }

        if (_appointmentOption.ItemCount > 0)
        {
            _appointmentOption.Select(0);
        }
    }

    private string BuildCapturedOfficerSpeech(WorldState world, OfficerData officer, int captorFactionId)
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return string.Empty;
        }

        var relationshipBonus = GetCaptorRelationshipBonus(world, officer, captorFactionId);
        var speechKey = "ui.captured_officer.speech_wait_and_see";
        if (relationshipBonus >= 0.18 || officer.Loyalty <= 55)
        {
            speechKey = "ui.captured_officer.speech_accept";
        }
        else if (officer.Loyalty >= 92 && officer.Ambition <= 55)
        {
            speechKey = "ui.captured_officer.speech_kill_me";
        }
        else if (officer.Loyalty >= 85)
        {
            speechKey = "ui.captured_officer.speech_defiant";
        }
        else if (officer.Ambition >= 82)
        {
            speechKey = "ui.captured_officer.speech_offer_more";
        }

        var bb = new StringBuilder();
        bb.Append("[color=#D8B56A]");
        bb.Append(EscapeBb(localization.T("ui.captured_officer.speech_label")));
        bb.Append("[/color]\n");
        bb.Append(EscapeBb(localization.T(speechKey)));
        return bb.ToString();
    }

    private static double GetCaptorRelationshipBonus(WorldState world, OfficerData captiveOfficer, int captorFactionId)
    {
        if (captorFactionId <= 0 || captiveOfficer.RelationshipType == null || captiveOfficer.RelationshipType.Count == 0)
        {
            return 0.0;
        }

        var faction = world.GetFaction(captorFactionId);
        if (faction == null)
        {
            return 0.0;
        }

        var bestBonus = 0.0;
        var ruler = world.GetOfficer(faction.RulerOfficerId);
        if (HasRelationshipWith(captiveOfficer, ruler))
        {
            bestBonus = Math.Max(bestBonus, 0.35);
        }

        foreach (var officerId in faction.OfficerIds)
        {
            var factionOfficer = world.GetOfficer(officerId);
            if (factionOfficer == null || factionOfficer.Id == ruler?.Id)
            {
                continue;
            }

            if (HasRelationshipWith(captiveOfficer, factionOfficer))
            {
                bestBonus = Math.Max(bestBonus, 0.18);
            }
        }

        return bestBonus;
    }

    private static bool HasRelationshipWith(OfficerData sourceOfficer, OfficerData? targetOfficer)
    {
        if (targetOfficer == null || sourceOfficer.RelationshipType == null || sourceOfficer.RelationshipType.Count == 0)
        {
            return false;
        }

        foreach (var relatedName in sourceOfficer.RelationshipType.Keys)
        {
            if (string.IsNullOrWhiteSpace(relatedName))
            {
                continue;
            }

            if ((!string.IsNullOrWhiteSpace(targetOfficer.NameZhHant) &&
                 relatedName.Equals(targetOfficer.NameZhHant, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(targetOfficer.Name) &&
                 relatedName.Equals(targetOfficer.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string EscapeBb(string value)
    {
        return value
            .Replace("[", "[lb]")
            .Replace("]", "[rb]");
    }

    private static List<OfficerData> GetPrisonersInSelectedCity(WorldState world, CityData city)
    {
        return world.Officers
            .Where(officer => officer.CaptiveFactionId == city.OwnerFactionId && officer.JailedCityId == city.Id)
            .OrderBy(officer => officer.NameZhHant)
            .ThenBy(officer => officer.Name)
            .ToList();
    }
}
