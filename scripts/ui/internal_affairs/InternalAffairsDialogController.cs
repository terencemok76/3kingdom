using System;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class InternalAffairsDialogController : FloatingOverlayController
{
    private readonly InternalAffairsUiContext _context;
    private OptionButton? _jobOption;
    private SpinBox? _durationSpinBox;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private Button? _confirmButton;
    private ItemList? _scheduleList;
    private Button? _terminateButton;
    private Label? _warningLabel;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(500.0f, 360.0f);

    public InternalAffairsDialogController(InternalAffairsUiContext context)
        : base(context, "res://scenes/ui/internal_affairs/InternalAffairsDialog.tscn")
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
        SetWarning(string.Empty);
        ShowOverlay();
    }

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization.T("ui.internal_affairs"));
        SetLabelText("JobRow/JobLabel", _context.Localization.T("ui.internal_affairs_job"));
        SetLabelText("DurationRow/DurationLabel", _context.Localization.T("ui.internal_affairs_duration"));
        SetLabelText("OfficerListLabel", _context.Localization.T("ui.internal_affairs_officer"));
        SetLabelText("ScheduleListLabel", _context.Localization.T("ui.internal_affairs_active_schedules"));
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }
        if (_terminateButton != null)
        {
            _terminateButton.Text = _context.Localization.T("ui.terminate_internal_affairs");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_internal_affairs");
        }

        UpdateSelectedOfficerSummary();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _jobOption = root.GetNodeOrNull<OptionButton>("JobRow/JobOption");
        _durationSpinBox = root.GetNodeOrNull<SpinBox>("DurationRow/DurationSpinBox");
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        _scheduleList = root.GetNodeOrNull<ItemList>("ScheduleList");
        _terminateButton = root.GetNodeOrNull<Button>("TerminateButton");
        _warningLabel = root.GetNodeOrNull<Label>("WarningLabel");
        ApplyButtonThemes();

        if (!_signalsConnected)
        {
            if (_selectOfficerButton != null)
            {
                _selectOfficerButton.Pressed += OnSelectOfficerPressed;
            }
            if (_terminateButton != null)
            {
                _terminateButton.Pressed += OnTerminatePressed;
            }
            if (_confirmButton != null)
            {
                _confirmButton.Pressed += OnConfirmPressed;
            }

            _signalsConnected = true;
        }
    }

    private void ApplyButtonThemes()
    {
        if (_selectOfficerButton != null)
        {
            _context.ApplyCommandButtonTheme(_selectOfficerButton);
        }

        if (_terminateButton != null)
        {
            _context.ApplyCommandButtonTheme(_terminateButton);
        }

        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }
    }

    private void Populate()
    {
        if (_context.SelectedCity == null || _context.TurnManager?.World == null)
        {
            return;
        }

        PopulateJobOptions();

        var availableOfficerIds = _context.GetAvailableCityOfficerIds();
        if (!availableOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = availableOfficerIds.Count > 0 ? availableOfficerIds[0] : -1;
        }

        UpdateSelectedOfficerSummary();
        RefreshScheduleList();
    }

    private void PopulateJobOptions()
    {
        if (_jobOption == null)
        {
            return;
        }

        _jobOption.Clear();
        AddJobOption(InternalAffairsJobType.Farm);
        AddJobOption(InternalAffairsJobType.Commercial);
        AddJobOption(InternalAffairsJobType.Defend);
        AddJobOption(InternalAffairsJobType.WaterControl);
        AddJobOption(InternalAffairsJobType.Construction);
    }

    private void AddJobOption(InternalAffairsJobType jobType)
    {
        if (_jobOption == null)
        {
            return;
        }

        _jobOption.AddItem(GetJobName(jobType));
        _jobOption.SetItemMetadata(_jobOption.ItemCount - 1, (int)jobType);
    }

    private void RefreshScheduleList()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (city == null || world == null || _scheduleList == null || localization == null)
        {
            return;
        }

        _scheduleList.Clear();
        foreach (var schedule in world.InternalAffairsSchedules)
        {
            if (schedule.State != InternalAffairsScheduleState.Active || schedule.CityId != city.Id)
            {
                continue;
            }

            var officer = world.GetOfficer(schedule.OfficerId);
            var officerName = officer != null ? localization.GetOfficerName(officer) : "-";
            var itemIndex = _scheduleList.AddItem(
                localization.Format("fmt.internal_affairs_schedule_row", GetJobName(schedule.JobType), officerName, schedule.RemainingMonths));
            _scheduleList.SetItemMetadata(itemIndex, schedule.Id);
        }
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>(nodeName);
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void OnConfirmPressed()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        var commandResolver = _context.CommandResolver;
        var localization = _context.Localization;
        if (city == null || world == null || commandResolver == null || localization == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            SetWarning(localization.T("ui.select_officer_warning"));
            _context.ReopenDialog(OverlayRoot);
            return;
        }

        var months = Math.Max(1, (int)(_durationSpinBox?.Value ?? 1));
        var result = commandResolver.ScheduleInternalAffairs(
            _context.TurnManager!.GetPlayerFactionId(),
            city.Id,
            _selectedOfficerId,
            GetSelectedJobType(),
            months);
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _context.RefreshSelectedCity();
        RefreshScheduleList();
        _context.RefreshMapVisuals();
        HideOverlay();
    }

    private void OnSelectOfficerPressed()
    {
        var localization = _context.Localization;
        if (_context.SelectedCity == null || _context.TurnManager?.World == null || localization == null)
        {
            return;
        }

        var candidateOfficerIds = _context.GetAvailableCityOfficerIds();
        if (candidateOfficerIds.Count == 0)
        {
            SetWarning(localization.T("ui.select_officer_warning"));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("ui.internal_affairs_officer"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Politics,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
                SetWarning(string.Empty);
            });
    }

    private void OnTerminatePressed()
    {
        var world = _context.TurnManager?.World;
        var commandResolver = _context.CommandResolver;
        var localization = _context.Localization;
        if (world == null || commandResolver == null || localization == null)
        {
            return;
        }

        var selectedIds = _context.GetSelectedItemMetadataIds(_scheduleList);
        if (selectedIds.Count == 0)
        {
            SetWarning(localization.T("ui.select_internal_affairs_schedule_warning"));
            return;
        }

        var result = commandResolver.TerminateInternalAffairsSchedule(_context.TurnManager!.GetPlayerFactionId(), selectedIds[0]);
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _context.RefreshSelectedCity();
        Populate();
    }

    private void UpdateSelectedOfficerSummary()
    {
        var localization = _context.Localization;
        if (_selectedOfficerLabel == null || localization == null)
        {
            return;
        }

        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null ? localization.GetOfficerName(officer) : localization.T("ui.unassigned");
        _selectedOfficerLabel.Text = $"{localization.T("ui.internal_affairs_officer")}: {officerName}";
    }

    private InternalAffairsJobType GetSelectedJobType()
    {
        if (_jobOption == null || _jobOption.Selected < 0)
        {
            return InternalAffairsJobType.Farm;
        }

        var metadata = _jobOption.GetItemMetadata(_jobOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (InternalAffairsJobType)metadata.AsInt32()
            : InternalAffairsJobType.Farm;
    }

    private string GetJobName(InternalAffairsJobType jobType)
    {
        if (_context.Localization == null)
        {
            return jobType.ToString();
        }

        return jobType switch
        {
            InternalAffairsJobType.Farm => _context.Localization.T("command.internal_affairs.farm"),
            InternalAffairsJobType.Commercial => _context.Localization.T("command.internal_affairs.commercial"),
            InternalAffairsJobType.Defend => _context.Localization.T("command.internal_affairs.defend"),
            InternalAffairsJobType.WaterControl => _context.Localization.T("command.internal_affairs.disaster_prevention"),
            InternalAffairsJobType.Construction => _context.Localization.T("command.internal_affairs.construction"),
            _ => jobType.ToString()
        };
    }

    private void SetWarning(string text)
    {
        if (_warningLabel != null)
        {
            _warningLabel.Text = text;
        }
    }
}
