using System;
using System.Linq;
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
    private Button? _pauseButton;
    private Button? _resumeButton;
    private Button? _cancelCurrentMonthButton;
    private Button? _terminateButton;
    private Label? _warningLabel;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(500.0f, 420.0f);

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
        if (_pauseButton != null)
        {
            _pauseButton.Text = _context.Localization.T("ui.pause_internal_affairs");
        }
        if (_resumeButton != null)
        {
            _resumeButton.Text = _context.Localization.T("ui.resume_internal_affairs");
        }
        if (_cancelCurrentMonthButton != null)
        {
            _cancelCurrentMonthButton.Text = _context.Localization.T("ui.cancel_current_month_internal_affairs");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_internal_affairs");
        }

        UpdateSelectedOfficerSummary();
    }

    public void RefreshIfOpen()
    {
        if (OverlayRoot?.Visible != true)
        {
            return;
        }

        RefreshText();
        Populate();
        SetWarning(string.Empty);
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _jobOption = root.GetNodeOrNull<OptionButton>("JobRow/JobOption");
        _durationSpinBox = root.GetNodeOrNull<SpinBox>("DurationRow/DurationSpinBox");
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        _scheduleList = root.GetNodeOrNull<ItemList>("ScheduleList");
        _pauseButton = root.GetNodeOrNull<Button>("ScheduleActionsRow/PauseButton");
        _resumeButton = root.GetNodeOrNull<Button>("ScheduleActionsRow/ResumeButton");
        _cancelCurrentMonthButton = root.GetNodeOrNull<Button>("ScheduleActionsRow/CancelCurrentMonthButton");
        _terminateButton = root.GetNodeOrNull<Button>("TerminateButton");
        _warningLabel = root.GetNodeOrNull<Label>("WarningLabel");
        ApplyButtonThemes();

        if (!_signalsConnected)
        {
            if (_selectOfficerButton != null)
            {
                _selectOfficerButton.Pressed += OnSelectOfficerPressed;
            }
            if (_scheduleList != null)
            {
                _scheduleList.ItemSelected += OnScheduleSelected;
            }
            if (_terminateButton != null)
            {
                _terminateButton.Pressed += OnTerminatePressed;
            }
            if (_pauseButton != null)
            {
                _pauseButton.Pressed += OnPausePressed;
            }
            if (_resumeButton != null)
            {
                _resumeButton.Pressed += OnResumePressed;
            }
            if (_cancelCurrentMonthButton != null)
            {
                _cancelCurrentMonthButton.Pressed += OnCancelCurrentMonthPressed;
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

        if (_pauseButton != null)
        {
            _context.ApplyCommandButtonTheme(_pauseButton);
        }

        if (_resumeButton != null)
        {
            _context.ApplyCommandButtonTheme(_resumeButton);
        }

        if (_cancelCurrentMonthButton != null)
        {
            _context.ApplyCommandButtonTheme(_cancelCurrentMonthButton);
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
        SyncSelectedOfficerWithScheduleSelection();
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
            if (schedule.CityId != city.Id ||
                schedule.State is InternalAffairsScheduleState.Terminated or InternalAffairsScheduleState.Interrupted or InternalAffairsScheduleState.Completed)
            {
                continue;
            }

            var officer = world.GetOfficer(schedule.OfficerId);
            var officerName = officer != null ? localization.GetOfficerName(officer) : "-";
            var stateText = GetScheduleStateText(schedule);
            var itemIndex = _scheduleList.AddItem(
                localization.Format("fmt.internal_affairs_schedule_row", GetJobName(schedule.JobType), officerName, schedule.RemainingMonths, stateText));
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
        if (result.Success)
        {
            _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
            _context.UiEventHub.PublishOfficerStateChanged(_selectedOfficerId, city.Id, city.OwnerFactionId);
            RefreshScheduleList();
            _context.RefreshMapVisuals();
        }
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

    private void OnScheduleSelected(long _index)
    {
        SyncSelectedOfficerWithScheduleSelection();
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
        if (_context.SelectedCity != null && result.Success)
        {
            _context.UiEventHub.PublishCityStateChanged(_context.SelectedCity.Id, _context.SelectedCity.OwnerFactionId);
        }
        Populate();
    }

    private void OnPausePressed()
    {
        var commandResolver = _context.CommandResolver;
        var localization = _context.Localization;
        if (_context.TurnManager?.World == null || commandResolver == null || localization == null)
        {
            return;
        }

        var selectedIds = _context.GetSelectedItemMetadataIds(_scheduleList);
        if (selectedIds.Count == 0)
        {
            SetWarning(localization.T("ui.select_internal_affairs_schedule_warning"));
            return;
        }

        var result = commandResolver.PauseInternalAffairsSchedule(_context.TurnManager!.GetPlayerFactionId(), selectedIds[0]);
        HandleScheduleActionResult(result);
    }

    private void OnResumePressed()
    {
        var commandResolver = _context.CommandResolver;
        var localization = _context.Localization;
        if (_context.TurnManager?.World == null || commandResolver == null || localization == null)
        {
            return;
        }

        var selectedIds = _context.GetSelectedItemMetadataIds(_scheduleList);
        if (selectedIds.Count == 0)
        {
            SetWarning(localization.T("ui.select_internal_affairs_schedule_warning"));
            return;
        }

        var selectedSchedule = GetSelectedSchedule();
        var resumeOfficerId = selectedSchedule?.State == InternalAffairsScheduleState.Paused ? _selectedOfficerId : 0;
        var result = commandResolver.ResumeInternalAffairsSchedule(_context.TurnManager!.GetPlayerFactionId(), selectedIds[0], resumeOfficerId);
        HandleScheduleActionResult(result);
    }

    private void OnCancelCurrentMonthPressed()
    {
        var commandResolver = _context.CommandResolver;
        var localization = _context.Localization;
        if (_context.TurnManager?.World == null || commandResolver == null || localization == null)
        {
            return;
        }

        var selectedIds = _context.GetSelectedItemMetadataIds(_scheduleList);
        if (selectedIds.Count == 0)
        {
            SetWarning(localization.T("ui.select_internal_affairs_schedule_warning"));
            return;
        }

        var result = commandResolver.CancelCurrentMonthInternalAffairsSchedule(_context.TurnManager!.GetPlayerFactionId(), selectedIds[0]);
        HandleScheduleActionResult(result);
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

    private void SyncSelectedOfficerWithScheduleSelection()
    {
        var city = _context.SelectedCity;
        var schedule = GetSelectedSchedule();
        if (city == null || schedule == null)
        {
            UpdateSelectedOfficerSummary();
            return;
        }

        if (schedule.State == InternalAffairsScheduleState.Paused)
        {
            var availableOfficerIds = _context.GetAvailableCityOfficerIds();
            if (!availableOfficerIds.Contains(_selectedOfficerId))
            {
                _selectedOfficerId = _context.GetRecommendedInternalAffairsOfficerId(city.Id, schedule.JobType);
            }
        }

        UpdateSelectedOfficerSummary();
    }

    private InternalAffairsScheduleData? GetSelectedSchedule()
    {
        var world = _context.TurnManager?.World;
        var selectedIds = _context.GetSelectedItemMetadataIds(_scheduleList);
        if (world == null || selectedIds.Count == 0)
        {
            return null;
        }

        return world.InternalAffairsSchedules.FirstOrDefault(schedule => schedule.Id == selectedIds[0]);
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

    private string GetScheduleStateText(InternalAffairsScheduleData schedule)
    {
        var localization = _context.Localization;
        var world = _context.TurnManager?.World;
        if (localization == null || world == null)
        {
            return schedule.State.ToString();
        }

        if (schedule.SkipExecutionYear == world.Year && schedule.SkipExecutionMonth == world.Month)
        {
            return localization.T("ui.internal_affairs_status_cancelled_this_month");
        }

        return schedule.State switch
        {
            InternalAffairsScheduleState.Active => localization.T("ui.internal_affairs_status_active"),
            InternalAffairsScheduleState.Paused => localization.T("ui.internal_affairs_status_paused"),
            _ => schedule.State.ToString()
        };
    }

    private void HandleScheduleActionResult(CommandResult result)
    {
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        if (_context.SelectedCity != null && result.Success)
        {
            _context.UiEventHub.PublishCityStateChanged(_context.SelectedCity.Id, _context.SelectedCity.OwnerFactionId);
            _context.RefreshMapVisuals();
        }

        Populate();
        SetWarning(string.Empty);
    }

    private void SetWarning(string text)
    {
        if (_warningLabel != null)
        {
            _warningLabel.Text = text;
        }
    }
}
