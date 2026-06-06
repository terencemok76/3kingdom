using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class PrisonerManagementDialogController : FloatingOverlayController
{
    private readonly PersonnelUiContext _context;
    private Label? _summaryLabel;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private Label? _warningLabel;
    private Button? _recruitButton;
    private Button? _freeButton;
    private Button? _executeButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(760.0f, 250.0f);

    public PrisonerManagementDialogController(PersonnelUiContext context)
        : base(context, "res://scenes/ui/personnel/PrisonerManagementDialog.tscn")
    {
        _context = context;
    }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public void RefreshText()
    {
        if (IsOverlayVisible)
        {
            Show();
        }
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
            _warningLabel.Text = prisoners.Count == 0
                ? localization.T("ui.no_prisoners")
                : string.Empty;
        }

        UpdateSelectedOfficerSummary();
        ShowOverlay();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _warningLabel = root.GetNodeOrNull<Label>("WarningLabel");
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
                _warningLabel.Text = localization.T("ui.no_prisoners");
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
                if (_warningLabel != null)
                {
                    _warningLabel.Text = string.Empty;
                }
            },
            titleFactory: () => _context.Localization?.T("ui.prisoner_management") ?? localization.T("ui.prisoner_management"));
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
                _warningLabel.Text = _context.Localization?.T("ui.select_officer_warning") ?? "Select an officer.";
            }

            return;
        }

        var result = commandResolver.ResolveCapturedOfficerDisposition(city.OwnerFactionId, _selectedOfficerId, disposition);
        if (!result.Success)
        {
            if (_warningLabel != null)
            {
                _warningLabel.Text = _context.GetLocalizedResultMessage(result);
            }

            ShowOverlay();
            return;
        }

        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
        _context.RefreshSelectedCity();
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

    private static List<OfficerData> GetPrisonersInSelectedCity(WorldState world, CityData city)
    {
        return world.Officers
            .Where(officer => officer.CaptiveFactionId == city.OwnerFactionId && officer.JailedCityId == city.Id)
            .OrderBy(officer => officer.NameZhHant)
            .ThenBy(officer => officer.Name)
            .ToList();
    }
}
