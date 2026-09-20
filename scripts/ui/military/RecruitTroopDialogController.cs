using System;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class RecruitTroopDialogController : FloatingOverlayController
{
    private static readonly TroopType[] TroopTypes =
    {
        TroopType.Infantry,
        TroopType.Spearman,
        TroopType.Cavalry,
        TroopType.Archer,
        TroopType.Crossbow,
        TroopType.Siege
    };

    private readonly MilitaryUiContext _context;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private OptionButton? _troopTypeOption;
    private SpinBox? _troopCountSpinBox;
    private Label? _maxTroopsLabel;
    private Label? _costSummaryLabel;
    private Button? _advisorButton;
    private Button? _confirmButton;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;
    private bool _isUpdatingTroopCount;
    protected override Vector2 MinimumOverlaySize => new(420.0f, 310.0f);

    public RecruitTroopDialogController(MilitaryUiContext context)
        : base(context, "res://scenes/ui/military/RecruitTroopDialog.tscn")
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

        SetOverlayTitleText(_context.Localization.T("ui.military_recruit"));
        SetLabelText("OfficerListLabel", _context.Localization.T("ui.officers"));
        SetLabelText("TroopTypeLabel", _context.Localization.T("ui.recruit_troop_type"));
        SetLabelText("TroopCountLabel", _context.Localization.T("ui.recruit_troop_count"));
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }

        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_officer_selection");
        }
        if (_advisorButton != null)
        {
            _advisorButton.Text = _context.Localization.T("ui.recruit_advice_button") ?? "Recruit Advice";
        }

        RefreshTroopTypeOptionTexts();
        UpdateSelectedOfficerSummary();
        UpdateRecruitSummary();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("OfficerSelectorRow/SelectOfficerButton");
        _troopTypeOption = root.GetNodeOrNull<OptionButton>("TroopTypeOption");
        _troopCountSpinBox = root.GetNodeOrNull<SpinBox>("TroopCountSpinBox");
        _maxTroopsLabel = root.GetNodeOrNull<Label>("MaxTroopsLabel");
        _costSummaryLabel = root.GetNodeOrNull<Label>("CostSummaryLabel");
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
        if (_advisorButton != null)
        {
            _context.ApplyCommandButtonTheme(_advisorButton);
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
            if (_advisorButton != null)
            {
                _advisorButton.Pressed += OnAdvisorPressed;
            }

            if (_troopTypeOption != null)
            {
                _troopTypeOption.ItemSelected += _ => OnTroopTypeChanged();
            }

            if (_troopCountSpinBox != null)
            {
                _troopCountSpinBox.ValueChanged += _ => OnTroopCountChanged();
            }

            _signalsConnected = true;
        }
    }

    private void Populate()
    {
        var city = _context.SelectedCity;
        var candidateOfficerIds = _context.GetAvailableCityOfficerIds();
        if (!candidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        if (_troopTypeOption != null && city != null)
        {
            _troopTypeOption.Clear();
            foreach (var troopType in TroopTypes)
            {
                if (!RecruitRules.CanRecruitTroopType(city, troopType))
                {
                    continue;
                }

                _troopTypeOption.AddItem(_context.GetTroopTypeDisplayName(troopType));
                _troopTypeOption.SetItemMetadata(_troopTypeOption.ItemCount - 1, (int)troopType);
            }

            if (_troopTypeOption.ItemCount > 0)
            {
                _troopTypeOption.Select(0);
            }
        }

        UpdateSelectedOfficerSummary();
        ConfigureTroopCountInput(resetToSuggested: true);
        UpdateRecruitSummary();
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>(nodeName);
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void OnSelectOfficerPressed()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        var candidateOfficerIds = _context.GetAvailableCityOfficerIds();
        if (candidateOfficerIds.Count == 0)
        {
            _context.AddLog(localization.Format("ui.no_available_officer_for_command", _context.GetCommandName(CommandType.Recruit)));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("ui.military_recruit"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Charm,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
            },
            () => _context.Localization?.T("ui.military_recruit") ?? localization.T("ui.military_recruit"));
    }

    private void OnAdvisorPressed()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (city == null || world == null || localization == null)
        {
            return;
        }

        CommitTroopCountEdit();
        var advisor = FindRecruitAdvisor(world, city);
        var role = world.GetFaction(city.OwnerFactionId)?.ChiefStrategistOfficerId == advisor?.Id
            ? localization.T("ui.chief_strategist") ?? "Chief Strategist"
            : localization.T("ui.local_place") ?? "Local";
        _context.ShowAdvisorMessage(
            advisor,
            role,
            BuildRecruitAdvice(city, GetSelectedRecruitTroopType(), GetSelectedRecruitCount(), localization));
    }

    private static OfficerData? FindRecruitAdvisor(WorldState world, CityData city)
    {
        var faction = world.GetFaction(city.OwnerFactionId);
        var chiefStrategist = faction != null ? world.GetOfficer(faction.ChiefStrategistOfficerId) : null;
        if (IsOfficerAvailableForAdvice(chiefStrategist, world))
        {
            return chiefStrategist;
        }

        return city.OfficerIds
            .Select(world.GetOfficer)
            .Where(officer => IsOfficerAvailableForAdvice(officer, world))
            .Cast<OfficerData>()
            .OrderByDescending(officer => officer.Intelligence)
            .ThenByDescending(officer => officer.Leadership)
            .ThenBy(officer => officer.Id)
            .FirstOrDefault();
    }

    private static bool IsOfficerAvailableForAdvice(OfficerData? officer, WorldState world)
    {
        return officer != null &&
               officer.CaptiveFactionId <= 0 &&
               (officer.DeathYear <= 0 || world.Year <= officer.DeathYear);
    }

    private string BuildRecruitAdvice(CityData city, TroopType troopType, int requestedCount, LocalizationService localization)
    {
        var maximumCount = RecruitRules.GetMaxRecruitableCount(city, troopType);
        var recruitCount = Math.Clamp(requestedCount, 0, maximumCount);
        if (recruitCount <= 0)
        {
            return localization.T("ui.recruit_advice_choose_count") ?? "Choose a troop count before recruiting.";
        }

        var troopName = _context.GetTroopTypeDisplayName(troopType);
        var goldCost = RecruitRules.GetRecruitGoldCost(troopType, recruitCount);
        var foodCost = RecruitRules.GetRecruitFoodCost(troopType, recruitCount);
        var plan = localization.Format("fmt.recruit_advice_plan", troopName, recruitCount, maximumCount, goldCost, foodCost);
        var guidance = recruitCount * 100 >= maximumCount * 80
            ? localization.T("ui.recruit_advice_near_limit")
            : troopType switch
            {
                TroopType.Cavalry => localization.T("ui.recruit_advice_cavalry"),
                TroopType.Siege => localization.T("ui.recruit_advice_siege"),
                TroopType.Archer or TroopType.Crossbow => localization.T("ui.recruit_advice_ranged"),
                _ => localization.T("ui.recruit_advice_standard")
            };
        return $"{plan}{guidance}";
    }

    private void OnConfirmPressed()
    {
        var localization = _context.Localization;
        var city = _context.SelectedCity;
        if (localization == null || city == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            _context.AddLog(localization.T("ui.select_officer_warning"));
            ShowOverlay();
            return;
        }

        CommitTroopCountEdit();
        var recruitCount = GetSelectedRecruitCount();
        var result = _context.ExecutePlayerCommand(
            CommandType.Recruit,
            troopsToSend: recruitCount,
            officerIds: new System.Collections.Generic.List<int> { _selectedOfficerId },
            recruitTroopType: GetSelectedRecruitTroopType());
        if (result.Success)
        {
            _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
            _context.UiEventHub.PublishOfficerStateChanged(_selectedOfficerId, city.Id, city.OwnerFactionId);
            HideOverlay();
        }
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

    private void OnTroopTypeChanged()
    {
        ConfigureTroopCountInput(resetToSuggested: true);
        UpdateRecruitSummary();
    }

    private void OnTroopCountChanged()
    {
        if (_isUpdatingTroopCount)
        {
            return;
        }

        UpdateRecruitSummary();
    }

    private TroopType GetSelectedRecruitTroopType()
    {
        if (_troopTypeOption == null || _troopTypeOption.Selected < 0)
        {
            return TroopType.Infantry;
        }

        var metadata = _troopTypeOption.GetItemMetadata(_troopTypeOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (TroopType)metadata.AsInt32()
            : TroopType.Infantry;
    }

    private int GetSelectedRecruitCount()
    {
        return _troopCountSpinBox == null ? 0 : Mathf.RoundToInt((float)_troopCountSpinBox.Value);
    }

    private void ConfigureTroopCountInput(bool resetToSuggested)
    {
        if (_troopCountSpinBox == null)
        {
            return;
        }

        var city = _context.SelectedCity;
        var maxRecruitCount = city == null ? 0 : RecruitRules.GetMaxRecruitableCount(city, GetSelectedRecruitTroopType());
        _isUpdatingTroopCount = true;
        _troopCountSpinBox.MinValue = maxRecruitCount > 0 ? 1 : 0;
        _troopCountSpinBox.MaxValue = Math.Max(0, maxRecruitCount);
        if (resetToSuggested)
        {
            _troopCountSpinBox.Value = maxRecruitCount <= 0 ? 0 : Math.Min(100, maxRecruitCount);
        }
        else
        {
            _troopCountSpinBox.Value = Math.Clamp(_troopCountSpinBox.Value, _troopCountSpinBox.MinValue, _troopCountSpinBox.MaxValue);
        }

        _isUpdatingTroopCount = false;
    }

    private void UpdateRecruitSummary()
    {
        var localization = _context.Localization;
        var city = _context.SelectedCity;
        if (localization == null)
        {
            return;
        }

        var troopType = GetSelectedRecruitTroopType();
        var maxRecruitCount = city == null ? 0 : RecruitRules.GetMaxRecruitableCount(city, troopType);
        var selectedCount = Math.Clamp(GetSelectedRecruitCount(), 0, maxRecruitCount);
        var goldCost = RecruitRules.GetRecruitGoldCost(troopType, selectedCount);
        var foodCost = RecruitRules.GetRecruitFoodCost(troopType, selectedCount);
        var horseCost = troopType == TroopType.Cavalry ? selectedCount : 0;

        if (_maxTroopsLabel != null)
        {
            _maxTroopsLabel.Text = localization.Format("ui.recruit_max_count_value", maxRecruitCount);
        }

        if (_costSummaryLabel != null)
        {
            _costSummaryLabel.Text = troopType == TroopType.Cavalry
                ? localization.Format("ui.recruit_cost_summary_with_horse_value", goldCost, foodCost, horseCost)
                : localization.Format("ui.recruit_cost_summary_value", goldCost, foodCost);
        }

        if (_confirmButton != null)
        {
            _confirmButton.Disabled = city == null || _troopTypeOption == null || _troopTypeOption.ItemCount <= 0 || selectedCount <= 0;
        }
    }

    private void RefreshTroopTypeOptionTexts()
    {
        var city = _context.SelectedCity;
        if (_troopTypeOption == null || _context.Localization == null || city == null)
        {
            return;
        }

        var selectedTroopType = GetSelectedRecruitTroopType();
        _troopTypeOption.Clear();
        foreach (var troopType in TroopTypes)
        {
            if (!RecruitRules.CanRecruitTroopType(city, troopType))
            {
                continue;
            }

            _troopTypeOption.AddItem(_context.GetTroopTypeDisplayName(troopType));
            _troopTypeOption.SetItemMetadata(_troopTypeOption.ItemCount - 1, (int)troopType);
        }

        for (var index = 0; index < _troopTypeOption.ItemCount; index += 1)
        {
            var metadata = _troopTypeOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == (int)selectedTroopType)
            {
                _troopTypeOption.Select(index);
                return;
            }
        }

        if (_troopTypeOption.ItemCount > 0)
        {
            _troopTypeOption.Select(0);
        }
    }

    private void CommitTroopCountEdit()
    {
        if (_troopCountSpinBox?.GetLineEdit() is not LineEdit lineEdit)
        {
            return;
        }

        var text = lineEdit.Text?.Trim();
        if (!double.TryParse(text, out var parsedValue))
        {
            return;
        }

        _isUpdatingTroopCount = true;
        _troopCountSpinBox.Value = Math.Clamp(parsedValue, _troopCountSpinBox.MinValue, _troopCountSpinBox.MaxValue);
        _isUpdatingTroopCount = false;
        UpdateRecruitSummary();
    }
}
