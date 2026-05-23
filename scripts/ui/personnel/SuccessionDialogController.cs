using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ThreeKingdom.UI;

internal sealed class SuccessionDialogController : FloatingOverlayController
{
    private readonly PersonnelUiContext _context;
    private Label? _summaryLabel;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private Label? _warningLabel;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private int _pendingFactionId = -1;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(760.0f, 240.0f);

    public SuccessionDialogController(PersonnelUiContext context)
        : base(context, "res://scenes/ui/personnel/SuccessionDialog.tscn")
    {
        _context = context;
    }

    public int PendingFactionId
    {
        get => _pendingFactionId;
        set => _pendingFactionId = value;
    }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public bool HasPendingPlayerSuccession()
    {
        var world = _context.TurnManager?.World;
        if (world == null)
        {
            return false;
        }

        var playerFactionId = _context.TurnManager!.GetPlayerFactionId();
        return playerFactionId > 0 && world.GetPendingSuccession(playerFactionId) != null;
    }

    public void Show()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (world == null || localization == null || !EnsureOverlayReady())
        {
            return;
        }

        var factionId = _context.TurnManager!.GetPlayerFactionId();
        var pendingSuccession = world.GetPendingSuccession(factionId);
        var faction = world.GetFaction(factionId);
        if (pendingSuccession == null || faction == null)
        {
            return;
        }

        _pendingFactionId = factionId;
        SetOverlayTitleText(localization.T("ui.succession"));
        if (_confirmButton != null)
        {
            _confirmButton.Text = localization.T("ui.confirm_succession");
        }
        if (_summaryLabel != null)
        {
            _summaryLabel.Text = localization.Format("ui.succession_summary", localization.GetFactionName(world, factionId));
        }
        if (_warningLabel != null)
        {
            _warningLabel.Text = string.Empty;
        }
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = localization.T("ui.select_officer");
        }

        var candidateOfficer = pendingSuccession.CandidateOfficerIds.Contains(_selectedOfficerId)
            ? world.GetOfficer(_selectedOfficerId)
            : null;
        if (candidateOfficer == null)
        {
            _selectedOfficerId = pendingSuccession.CandidateOfficerIds.FirstOrDefault();
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
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (_selectOfficerButton != null)
        {
            _context.ApplyCommandButtonTheme(_selectOfficerButton);
        }
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }
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
            _signalsConnected = true;
        }
    }

    private void OnConfirmPressed()
    {
        var commandResolver = _context.CommandResolver;
        var localization = _context.Localization;
        if (commandResolver == null || localization == null || _pendingFactionId <= 0)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            if (_warningLabel != null)
            {
                _warningLabel.Text = localization.T("ui.select_officer_warning");
            }
            ShowOverlay();
            return;
        }

        var factionId = _pendingFactionId;
        var result = commandResolver.ResolvePlayerSuccession(factionId, _selectedOfficerId);
        if (!result.Success)
        {
            if (_warningLabel != null)
            {
                _warningLabel.Text = _context.GetLocalizedResultMessage(result);
            }
            ShowOverlay();
            return;
        }

        var successor = _context.TurnManager?.World?.GetOfficer(_selectedOfficerId);
        var cityId = successor?.CityId ?? _context.SelectedCity?.Id ?? 0;
        _pendingFactionId = -1;
        HideOverlay();
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _context.UiEventHub.PublishFactionLeadershipChanged(factionId, cityId);
        if (cityId > 0)
        {
            _context.UiEventHub.PublishCityStateChanged(cityId, factionId);
        }
        _context.ContinuePendingNonAttackResolution();
    }

    protected override void OnOverlayCloseRequested()
    {
        if (_pendingFactionId > 0)
        {
            Show();
            return;
        }

        HideOverlay();
    }

    private void OnSelectOfficerPressed()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (world == null || localization == null || _pendingFactionId <= 0)
        {
            return;
        }

        var pendingSuccession = world.GetPendingSuccession(_pendingFactionId);
        var candidateOfficerIds = pendingSuccession?.CandidateOfficerIds.ToList() ?? new List<int>();
        if (candidateOfficerIds.Count == 0)
        {
            if (_warningLabel != null)
            {
                _warningLabel.Text = localization.T("ui.select_officer_warning");
            }
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("ui.succession"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Politics,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
                if (_warningLabel != null)
                {
                    _warningLabel.Text = string.Empty;
                }
            });
    }

    private void UpdateSelectedOfficerSummary()
    {
        if (_selectedOfficerLabel == null || _context.Localization == null)
        {
            return;
        }

        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null ? _context.Localization.GetOfficerName(officer) : _context.Localization.T("ui.unassigned");
        _selectedOfficerLabel.Text = $"{_context.Localization.T("ui.officers")}: {officerName}";
    }
}
