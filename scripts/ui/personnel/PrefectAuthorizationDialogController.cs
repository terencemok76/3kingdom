using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class PrefectAuthorizationDialogController : FloatingOverlayController
{
    private readonly PersonnelUiContext _context;
    private OptionButton? _authorizationOption;
    private HBoxContainer? _planJobRow;
    private HBoxContainer? _planDurationRow;
    private OptionButton? _planJobOption;
    private SpinBox? _planDurationSpinBox;
    private Label? _summaryLabel;
    private Button? _confirmButton;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(500.0f, 320.0f);

    public PrefectAuthorizationDialogController(PersonnelUiContext context)
        : base(context, "res://scenes/ui/personnel/PrefectAuthorizationDialog.tscn")
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

        Populate();
        RefreshText();
        ShowOverlay();
    }

    public void RefreshIfOpen()
    {
        if (OverlayRoot?.Visible != true)
        {
            return;
        }

        Populate();
        RefreshText();
    }

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization.T("command.personnel.prefect_authorization"));
        SetLabelText("AuthorizationLabel", _context.Localization.T("ui.prefect_authorization_mode"));
        SetLabelText("PlanJobRow/PlanJobLabel", _context.Localization.T("ui.internal_affairs_job"));
        SetLabelText("PlanDurationRow/PlanDurationLabel", _context.Localization.T("ui.internal_affairs_duration"));
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_plan");
        }

        RefreshAuthorizationOptionTexts();
        RefreshPlanJobOptionTexts();
        UpdateSummary();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _authorizationOption = root.GetNodeOrNull<OptionButton>("AuthorizationOption");
        _planJobRow = root.GetNodeOrNull<HBoxContainer>("PlanJobRow");
        _planDurationRow = root.GetNodeOrNull<HBoxContainer>("PlanDurationRow");
        _planJobOption = root.GetNodeOrNull<OptionButton>("PlanJobRow/PlanJobOption");
        _planDurationSpinBox = root.GetNodeOrNull<SpinBox>("PlanDurationRow/PlanDurationSpinBox");
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }

        if (!_signalsConnected)
        {
            if (_authorizationOption != null)
            {
                _authorizationOption.ItemSelected += _ =>
                {
                    UpdatePlanInputsVisibility();
                    UpdateSummary();
                };
            }

            if (_planJobOption != null)
            {
                _planJobOption.ItemSelected += _ => UpdateSummary();
            }

            if (_confirmButton != null)
            {
                _confirmButton.Pressed += OnConfirmPressed;
            }

            _signalsConnected = true;
        }
    }

    private void Populate()
    {
        if (_authorizationOption == null || _context.Localization == null)
        {
            return;
        }

        _authorizationOption.Clear();
        AddAuthorizationOption(PrefectAuthorizationType.None);
        AddAuthorizationOption(PrefectAuthorizationType.Half);
        AddAuthorizationOption(PrefectAuthorizationType.Full);
        PopulatePlanJobOptions();

        var city = _context.SelectedCity;
        var selectedIndex = 0;
        if (city != null)
        {
            selectedIndex = city.PrefectAuthorizationType switch
            {
                PrefectAuthorizationType.Half => 1,
                PrefectAuthorizationType.Full => 2,
                _ => 0
            };
        }

        if (_authorizationOption.ItemCount > selectedIndex)
        {
            _authorizationOption.Select(selectedIndex);
        }

        if (_planDurationSpinBox != null)
        {
            _planDurationSpinBox.Value = city != null && city.PrefectPlanTotalMonths > 0
                ? city.PrefectPlanTotalMonths
                : 3;
        }

        if (_planJobOption != null && city != null)
        {
            for (var index = 0; index < _planJobOption.ItemCount; index += 1)
            {
                var metadata = _planJobOption.GetItemMetadata(index);
                if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == (int)city.PrefectPlanJobType)
                {
                    _planJobOption.Select(index);
                    break;
                }
            }
        }

        UpdatePlanInputsVisibility();
        UpdateSummary();
    }

    private void AddAuthorizationOption(PrefectAuthorizationType authorizationType)
    {
        if (_authorizationOption == null || _context.Localization == null)
        {
            return;
        }

        _authorizationOption.AddItem(GetAuthorizationDisplayName(authorizationType));
        _authorizationOption.SetItemMetadata(_authorizationOption.ItemCount - 1, (int)authorizationType);
    }

    private void RefreshAuthorizationOptionTexts()
    {
        if (_authorizationOption == null || _context.Localization == null)
        {
            return;
        }

        var selectedAuthorizationType = GetSelectedAuthorizationType();
        _authorizationOption.Clear();
        AddAuthorizationOption(PrefectAuthorizationType.None);
        AddAuthorizationOption(PrefectAuthorizationType.Half);
        AddAuthorizationOption(PrefectAuthorizationType.Full);
        SelectAuthorizationOption(selectedAuthorizationType);
        UpdatePlanInputsVisibility();
    }

    private void RefreshPlanJobOptionTexts()
    {
        if (_planJobOption == null || _context.Localization == null)
        {
            return;
        }

        var selectedJobType = GetSelectedPlanJobType();
        PopulatePlanJobOptions();
        SelectPlanJobOption(selectedJobType);
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
        var turnManager = _context.TurnManager;
        var commandResolver = _context.CommandResolver;
        if (city == null || turnManager == null || commandResolver == null)
        {
            return;
        }

        var authorizationType = GetSelectedAuthorizationType();
        var result = commandResolver.ExecuteAuthorizePrefect(
            turnManager.GetPlayerFactionId(),
            city.Id,
            authorizationType);
        var logMessages = new System.Collections.Generic.List<string> { _context.GetLocalizedResultMessage(result) };
        if (result.Success && authorizationType == PrefectAuthorizationType.Half)
        {
            var planResult = commandResolver.ExecuteSetPrefectPlan(
                turnManager.GetPlayerFactionId(),
                city.Id,
                GetSelectedPlanJobType(),
                GetSelectedPlanDuration());
            logMessages.Add(_context.GetLocalizedResultMessage(planResult));
            result = planResult;
        }

        _context.AddLog(string.Join(" | ", logMessages.Where(message => !string.IsNullOrWhiteSpace(message))), isPlayerRelated: true);
        if (result.Success)
        {
            _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
        }

        HideOverlay();
    }

    private void UpdateSummary()
    {
        if (_summaryLabel == null || _context.Localization == null)
        {
            return;
        }

        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        if (city == null || world == null)
        {
            _summaryLabel.Text = string.Empty;
            return;
        }

        var prefect = GetCityPrefect(world, city);
        var prefectName = prefect != null
            ? _context.Localization.GetOfficerName(prefect)
            : _context.Localization.T("ui.unassigned");

        var currentMode = GetAuthorizationDisplayName(city.PrefectAuthorizationType);
        var selectedMode = GetAuthorizationDisplayName(GetSelectedAuthorizationType());
        var planText = city.PrefectPlanRemainingMonths > 0
            ? $"{GetJobDisplayName(city.PrefectPlanJobType)} / {city.PrefectPlanRemainingMonths}"
            : _context.Localization.T("ui.none");
        var currentPlanSourceText = city.PrefectPlanRemainingMonths > 0
            ? GetPlanSourceDisplayName(city.PrefectPlanIsPlayerDirected)
            : _context.Localization.T("ui.none");
        var pendingPlanText = GetSelectedAuthorizationType() == PrefectAuthorizationType.Half
            ? $"{GetJobDisplayName(GetSelectedPlanJobType())} / {GetSelectedPlanDuration()}"
            : _context.Localization.T("ui.none");
        var pendingPlanSourceText = GetSelectedAuthorizationType() switch
        {
            PrefectAuthorizationType.Half => GetPlanSourceDisplayName(true),
            PrefectAuthorizationType.Full => GetPlanSourceDisplayName(false),
            _ => _context.Localization.T("ui.none")
        };

        _summaryLabel.Text =
            $"{_context.Localization.T("ui.prefect_authorization_prefect")}: {prefectName}\n" +
            $"{_context.Localization.T("ui.prefect_authorization_current")}: {currentMode}\n" +
            $"{_context.Localization.T("ui.prefect_authorization_pending")}: {selectedMode}\n" +
            $"{_context.Localization.T("ui.current_plan")}: {planText}\n" +
            $"{_context.Localization.T("ui.prefect_plan_source")}: {currentPlanSourceText}\n" +
            $"{_context.Localization.T("ui.pending_plan")}: {pendingPlanText}\n" +
            $"{_context.Localization.T("ui.prefect_plan_pending_source")}: {pendingPlanSourceText}";
    }

    private PrefectAuthorizationType GetSelectedAuthorizationType()
    {
        if (_authorizationOption == null || _authorizationOption.ItemCount == 0 || _authorizationOption.Selected < 0)
        {
            return PrefectAuthorizationType.None;
        }

        var metadata = _authorizationOption.GetItemMetadata(_authorizationOption.Selected);
        if (metadata.VariantType == Variant.Type.Int)
        {
            return (PrefectAuthorizationType)metadata.AsInt32();
        }

        return PrefectAuthorizationType.None;
    }

    private void SelectAuthorizationOption(PrefectAuthorizationType authorizationType)
    {
        if (_authorizationOption == null)
        {
            return;
        }

        for (var index = 0; index < _authorizationOption.ItemCount; index += 1)
        {
            var metadata = _authorizationOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == (int)authorizationType)
            {
                _authorizationOption.Select(index);
                return;
            }
        }

        if (_authorizationOption.ItemCount > 0)
        {
            _authorizationOption.Select(0);
        }
    }

    private string GetAuthorizationDisplayName(PrefectAuthorizationType authorizationType)
    {
        if (_context.Localization == null)
        {
            return authorizationType.ToString();
        }

        return authorizationType switch
        {
            PrefectAuthorizationType.None => _context.Localization.T("ui.prefect_authorization.none"),
            PrefectAuthorizationType.Half => _context.Localization.T("ui.prefect_authorization.half"),
            PrefectAuthorizationType.Full => _context.Localization.T("ui.prefect_authorization.full"),
            _ => authorizationType.ToString()
        };
    }

    private void PopulatePlanJobOptions()
    {
        if (_planJobOption == null)
        {
            return;
        }

        _planJobOption.Clear();
        AddPlanJobOption(InternalAffairsJobType.Farm);
        AddPlanJobOption(InternalAffairsJobType.Commercial);
        AddPlanJobOption(InternalAffairsJobType.Defend);
        AddPlanJobOption(InternalAffairsJobType.WaterControl);
        AddPlanJobOption(InternalAffairsJobType.Construction);
        if (_planJobOption.ItemCount > 0)
        {
            _planJobOption.Select(0);
        }
    }

    private void AddPlanJobOption(InternalAffairsJobType jobType)
    {
        if (_planJobOption == null)
        {
            return;
        }

        _planJobOption.AddItem(GetJobDisplayName(jobType));
        _planJobOption.SetItemMetadata(_planJobOption.ItemCount - 1, (int)jobType);
    }

    private void UpdatePlanInputsVisibility()
    {
        var visible = GetSelectedAuthorizationType() == PrefectAuthorizationType.Half;
        if (_planJobRow != null)
        {
            _planJobRow.Visible = visible;
        }

        if (_planDurationRow != null)
        {
            _planDurationRow.Visible = visible;
        }
    }

    private InternalAffairsJobType GetSelectedPlanJobType()
    {
        if (_planJobOption == null || _planJobOption.Selected < 0)
        {
            return InternalAffairsJobType.Farm;
        }

        var metadata = _planJobOption.GetItemMetadata(_planJobOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (InternalAffairsJobType)metadata.AsInt32()
            : InternalAffairsJobType.Farm;
    }

    private void SelectPlanJobOption(InternalAffairsJobType jobType)
    {
        if (_planJobOption == null)
        {
            return;
        }

        for (var index = 0; index < _planJobOption.ItemCount; index += 1)
        {
            var metadata = _planJobOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == (int)jobType)
            {
                _planJobOption.Select(index);
                return;
            }
        }

        if (_planJobOption.ItemCount > 0)
        {
            _planJobOption.Select(0);
        }
    }

    private int GetSelectedPlanDuration()
    {
        return _planDurationSpinBox != null
            ? Mathf.Clamp((int)_planDurationSpinBox.Value, 1, 24)
            : 3;
    }

    private string GetJobDisplayName(InternalAffairsJobType jobType)
    {
        return jobType switch
        {
            InternalAffairsJobType.Farm => _context.Localization?.T("command.internal_affairs.farm") ?? jobType.ToString(),
            InternalAffairsJobType.Commercial => _context.Localization?.T("command.internal_affairs.commercial") ?? jobType.ToString(),
            InternalAffairsJobType.Defend => _context.Localization?.T("command.internal_affairs.defend") ?? jobType.ToString(),
            InternalAffairsJobType.WaterControl => _context.Localization?.T("command.internal_affairs.disaster_prevention") ?? jobType.ToString(),
            InternalAffairsJobType.Construction => _context.Localization?.T("command.internal_affairs.construction") ?? jobType.ToString(),
            _ => jobType.ToString()
        };
    }

    private string GetPlanSourceDisplayName(bool isPlayerDirected)
    {
        if (_context.Localization == null)
        {
            return isPlayerDirected ? "Player" : "Prefect";
        }

        return isPlayerDirected
            ? _context.Localization.T("ui.prefect_plan_source.player")
            : _context.Localization.T("ui.prefect_plan_source.prefect");
    }

    private static OfficerData? GetCityPrefect(WorldState world, CityData city)
    {
        return city.OfficerIds
            .Select(world.GetOfficer)
            .FirstOrDefault(officer =>
                officer != null &&
                OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Governor));
    }
}
