using System;
using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class DiplomacyDialogController : FloatingOverlayController
{
    private readonly DiplomacyUiContext _context;
    private OptionButton? _actionOption;
    private OptionButton? _targetFactionOption;
    private SpinBox? _durationSpinBox;
    private SpinBox? _goldSpinBox;
    private SpinBox? _foodSpinBox;
    private SpinBox? _horseSpinBox;
    private Label? _selectedOfficerLabel;
    private Button? _selectOfficerButton;
    private Label? _relationInfoLabel;
    private Label? _summaryLabel;
    private Label? _warningLabel;
    private Button? _advisorButton;
    private Button? _confirmButton;
    private VBoxContainer? _campaignInvite;
    private OptionButton? _inviteCampaign;
    private OptionButton? _inviteSource;
    private Label? _inviteExisting;
    private Button? _inviteCancel;
    private SpinBox? _inviteTroops;
    private SpinBox? _inviteRequestedGold;
    private SpinBox? _inviteRequestedFood;
    private SpinBox? _inviteGold;
    private SpinBox? _inviteFood;
    private int _selectedOfficerId = -1;
    private bool _signalsConnected;
    private Vector2 _dialogSize = new(500.0f, 460.0f);
    protected override Vector2 MinimumOverlaySize => _dialogSize;

    public DiplomacyDialogController(DiplomacyUiContext context)
        : base(context, "res://scenes/ui/diplomacy/DiplomacyDialog.tscn")
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
        if (_context.SelectedCity == null || _context.Localization == null)
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

        SetOverlayTitleText(_context.Localization.T("ui.diplomacy"));
        SetLabelText("HeaderSection/ActionRow/ActionLabel", _context.Localization.T("ui.diplomacy_action"));
        SetLabelText("HeaderSection/TargetFactionRow/TargetFactionLabel", _context.Localization.T("ui.diplomacy_target_faction"));
        SetLabelText("MiddleSection/DurationRow/DurationLabel", _context.Localization.T("ui.diplomacy_duration"));
        SetLabelText("MiddleSection/GoldRow/GoldLabel", _context.Localization.T("ui.diplomacy_gift_gold"));
        SetLabelText("MiddleSection/FoodRow/FoodLabel", _context.Localization.T("ui.diplomacy_gift_food"));
        SetLabelText("MiddleSection/HorseRow/HorseLabel", _context.Localization.T("ui.diplomacy_gift_horse"));
        SetLabelText("FooterSection/OfficerListLabel", _context.Localization.T("ui.diplomacy_officer"));
        if (_selectOfficerButton != null)
        {
            _selectOfficerButton.Text = _context.Localization.T("ui.select_officer");
        }
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_diplomacy");
        }
        if (_advisorButton != null)
        {
            _advisorButton.Text = _context.Localization.T("ui.diplomacy_advice_button") ?? "Diplomacy Advice";
        }

        RefreshActionOptionTexts();
        RefreshTargetFactionOptionTexts();
        UpdateInputState();
        UpdateRelationInfo();
        UpdateSelectedOfficerSummary();
        UpdateSummary();
        UpdateConfirmButtonState();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _actionOption = root.GetNodeOrNull<OptionButton>("HeaderSection/ActionRow/ActionOption");
        _targetFactionOption = root.GetNodeOrNull<OptionButton>("HeaderSection/TargetFactionRow/TargetFactionOption");
        _durationSpinBox = root.GetNodeOrNull<SpinBox>("MiddleSection/DurationRow/DurationSpinBox");
        _goldSpinBox = root.GetNodeOrNull<SpinBox>("MiddleSection/GoldRow/GoldSpinBox");
        _foodSpinBox = root.GetNodeOrNull<SpinBox>("MiddleSection/FoodRow/FoodSpinBox");
        _horseSpinBox = root.GetNodeOrNull<SpinBox>("MiddleSection/HorseRow/HorseSpinBox");
        _selectedOfficerLabel = root.GetNodeOrNull<Label>("FooterSection/OfficerSelectorRow/SelectedOfficerLabel");
        _selectOfficerButton = root.GetNodeOrNull<Button>("FooterSection/OfficerSelectorRow/SelectOfficerButton");
        _relationInfoLabel = root.GetNodeOrNull<Label>("MiddleSection/RelationInfoLabel");
        _summaryLabel = root.GetNodeOrNull<Label>("FooterSection/NoticeScrollContainer/NoticeContent/SummaryLabel");
        _warningLabel = root.GetNodeOrNull<Label>("FooterSection/NoticeScrollContainer/NoticeContent/WarningLabel");
        _advisorButton = root.GetNodeOrNull<Button>("FooterSection/FooterRow/AdvisorButton");
        _confirmButton = root.GetNodeOrNull<Button>("FooterSection/FooterRow/ConfirmButton");
        _campaignInvite = root.GetNodeOrNull<VBoxContainer>("MiddleSection/CampaignInvite");
        _inviteCampaign = _campaignInvite?.GetNode<OptionButton>("Campaign");
        _inviteSource = _campaignInvite?.GetNodeOrNull<OptionButton>("Source");
        _inviteExisting = _campaignInvite?.GetNodeOrNull<Label>("Existing");
        _inviteCancel = _campaignInvite?.GetNodeOrNull<Button>("CancelRequest");
        _inviteTroops = _campaignInvite?.GetNode<SpinBox>("Inputs/Troops");
        _inviteRequestedGold = _campaignInvite?.GetNodeOrNull<SpinBox>("RequestedResources/Gold");
        _inviteRequestedFood = _campaignInvite?.GetNodeOrNull<SpinBox>("RequestedResources/Food");
        _inviteGold = _campaignInvite?.GetNode<SpinBox>("Inputs/Gold");
        _inviteFood = _campaignInvite?.GetNode<SpinBox>("Inputs/Food");
        ApplyButtonThemes();
        if (!_signalsConnected)
        {
            if (_actionOption != null) _actionOption.ItemSelected += _ => OnActionChanged();
            if (_targetFactionOption != null) _targetFactionOption.ItemSelected += _ => OnTargetChanged();
            if (_durationSpinBox != null) _durationSpinBox.ValueChanged += _ => UpdateSummary();
            if (_goldSpinBox != null) _goldSpinBox.ValueChanged += _ => OnResourceChanged();
            if (_foodSpinBox != null) _foodSpinBox.ValueChanged += _ => OnResourceChanged();
            if (_horseSpinBox != null) _horseSpinBox.ValueChanged += _ => OnResourceChanged();
            if (_selectOfficerButton != null) _selectOfficerButton.Pressed += OnSelectOfficerPressed;
            if (_advisorButton != null) _advisorButton.Pressed += OnAdvisorPressed;
            if (_confirmButton != null) _confirmButton.Pressed += OnConfirmPressed;
            if (_inviteCampaign != null) _inviteCampaign.ItemSelected += _ => RefreshInviteSources();
            if (_inviteSource != null) _inviteSource.ItemSelected += _ => UpdateConfirmButtonState();
            if (_inviteCancel != null) _inviteCancel.Pressed += CancelPrewarInvitation;
            if (_inviteTroops != null) _inviteTroops.ValueChanged += _ => UpdateConfirmButtonState();
            if (_inviteRequestedGold != null) _inviteRequestedGold.ValueChanged += _ => UpdateConfirmButtonState();
            if (_inviteRequestedFood != null) _inviteRequestedFood.ValueChanged += _ => UpdateConfirmButtonState();
            _signalsConnected = true;
        }
    }

    private void Populate()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var localization = _context.Localization;
        if (world == null || city == null || localization == null ||
            _actionOption == null || _targetFactionOption == null || _durationSpinBox == null || _goldSpinBox == null)
        {
            return;
        }

        _actionOption.Clear();
        AddActionOption(DiplomacyActionType.Alliance);
        AddActionOption(DiplomacyActionType.Truce);
        AddActionOption(DiplomacyActionType.Gift);
        AddActionOption(DiplomacyActionType.Demand);
        AddActionOption(DiplomacyActionType.Surrender);
        AddActionOption(DiplomacyActionType.BreakPact);
        AddActionOption(DiplomacyActionType.InviteJointAttack);

        _targetFactionOption.Clear();
        foreach (var faction in world.Factions.Where(faction =>
                     faction.Id != city.OwnerFactionId &&
                     world.Cities.Any(mapCity => mapCity.OwnerFactionId == faction.Id)))
        {
            AddTargetFactionOption(world, city, faction);
        }

        _durationSpinBox.Value = 3;
        _goldSpinBox.Value = 0;
        _goldSpinBox.MaxValue = city.Gold;
        if (_foodSpinBox != null)
        {
            _foodSpinBox.Value = 0;
            _foodSpinBox.MaxValue = city.Food;
        }
        if (_horseSpinBox != null)
        {
            _horseSpinBox.Value = 0;
            _horseSpinBox.MaxValue = city.Horses;
        }

        var candidateOfficerIds = DiplomacyUiHelpers.GetAvailableOfficerIds(_context.TurnManager, city);
        if (!candidateOfficerIds.Contains(_selectedOfficerId))
        {
            _selectedOfficerId = candidateOfficerIds.FirstOrDefault();
        }

        UpdateSelectedOfficerSummary();
        SetWarning(string.Empty);
        UpdateInputState();
        UpdateRelationInfo();
        UpdateSummary();
        UpdateConfirmButtonState();
        UpdateDialogSize();
        RefreshCampaignInvitations();
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>(nodeName);
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void ApplyButtonThemes()
    {
        if (_inviteCancel != null)
        {
            _context.ApplyCommandButtonTheme(_inviteCancel);
        }
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
    }

    private void AddActionOption(DiplomacyActionType actionType)
    {
        if (_actionOption == null || _context.Localization == null)
        {
            return;
        }

        _actionOption.AddItem(_context.Localization.T(DiplomacyUiHelpers.GetActionLocaleKey(actionType)));
        _actionOption.SetItemMetadata(_actionOption.ItemCount - 1, (int)actionType);
    }

    private void RefreshActionOptionTexts()
    {
        if (_actionOption == null || _context.Localization == null)
        {
            return;
        }

        var selectedActionType = GetSelectedActionType();
        _actionOption.Clear();
        AddActionOption(DiplomacyActionType.Alliance);
        AddActionOption(DiplomacyActionType.Truce);
        AddActionOption(DiplomacyActionType.Gift);
        AddActionOption(DiplomacyActionType.Demand);
        AddActionOption(DiplomacyActionType.Surrender);
        AddActionOption(DiplomacyActionType.BreakPact);
        AddActionOption(DiplomacyActionType.InviteJointAttack);
        SelectActionOption(selectedActionType);
    }

    private void RefreshTargetFactionOptionTexts()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var localization = _context.Localization;
        if (_targetFactionOption == null || world == null || city == null || localization == null)
        {
            return;
        }

        var selectedFactionId = GetSelectedTargetFactionId();
        _targetFactionOption.Clear();
        foreach (var faction in world.Factions.Where(faction =>
                     faction.Id != city.OwnerFactionId &&
                     world.Cities.Any(mapCity => mapCity.OwnerFactionId == faction.Id)))
        {
            AddTargetFactionOption(world, city, faction);
        }

        SelectTargetFactionOption(selectedFactionId);
    }

    private void AddTargetFactionOption(WorldState world, CityData city, FactionData faction)
    {
        if (_targetFactionOption == null || _context.Localization == null)
        {
            return;
        }

        var relation = world.GetDiplomacyRelation(city.OwnerFactionId, faction.Id);
        var score = relation?.RelationScore ?? 0;
        var signedScore = score > 0 ? $"+{score}" : score.ToString();
        var status = relation?.Status ?? DiplomacyStatusType.Neutral;
        var optionText = _context.Localization.Format(
            "fmt.diplomacy_target_faction_relation",
            _context.Localization.GetFactionName(world, faction.Id),
            signedScore,
            _context.GetStatusText(status));
        _targetFactionOption.AddItem(optionText);
        _targetFactionOption.SetItemMetadata(_targetFactionOption.ItemCount - 1, faction.Id);
    }

    private DiplomacyActionType GetSelectedActionType()
    {
        if (_actionOption == null || _actionOption.ItemCount == 0 || _actionOption.Selected < 0)
        {
            return DiplomacyActionType.Alliance;
        }

        var metadata = _actionOption.GetItemMetadata(_actionOption.Selected);
        return metadata.VariantType == Variant.Type.Int
            ? (DiplomacyActionType)metadata.AsInt32()
            : DiplomacyActionType.Alliance;
    }

    private int GetSelectedTargetFactionId()
    {
        if (_targetFactionOption == null || _targetFactionOption.ItemCount == 0 || _targetFactionOption.Selected < 0)
        {
            return -1;
        }

        var metadata = _targetFactionOption.GetItemMetadata(_targetFactionOption.Selected);
        return metadata.VariantType == Variant.Type.Int ? metadata.AsInt32() : -1;
    }

    private void SelectActionOption(DiplomacyActionType actionType)
    {
        if (_actionOption == null)
        {
            return;
        }

        for (var index = 0; index < _actionOption.ItemCount; index += 1)
        {
            var metadata = _actionOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == (int)actionType)
            {
                _actionOption.Select(index);
                return;
            }
        }

        if (_actionOption.ItemCount > 0)
        {
            _actionOption.Select(0);
        }
    }

    private void SelectTargetFactionOption(int factionId)
    {
        if (_targetFactionOption == null)
        {
            return;
        }

        for (var index = 0; index < _targetFactionOption.ItemCount; index += 1)
        {
            var metadata = _targetFactionOption.GetItemMetadata(index);
            if (metadata.VariantType == Variant.Type.Int && metadata.AsInt32() == factionId)
            {
                _targetFactionOption.Select(index);
                return;
            }
        }

        if (_targetFactionOption.ItemCount > 0)
        {
            _targetFactionOption.Select(0);
        }
    }

    private void OnActionChanged()
    {
        UpdateInputState();
        RefreshCampaignInvitations();
        UpdateSummary();
        UpdateConfirmButtonState();
        UpdateDialogSize();
    }

    private void OnTargetChanged()
    {
        UpdateRelationInfo();
        UpdateSummary();
        RefreshCampaignInvitations();
        RefreshExistingPrewarInvitation();
        UpdateConfirmButtonState();
    }

    private PrewarAllianceRequestData? GetExistingPrewarInvitation()
    {
        var world = _context.TurnManager?.World;
        var factionId = _context.SelectedCity?.OwnerFactionId ?? 0;
        var allyId = GetSelectedTargetFactionId();
        var targetId = SelectedId(_inviteCampaign);
        return world?.PrewarAllianceRequests.FirstOrDefault(item => item.InviterFactionId == factionId && item.AllyFactionId == allyId && item.TargetCityId == targetId);
    }

    private void RefreshExistingPrewarInvitation()
    {
        var request = GetExistingPrewarInvitation();
        var loc = _context.Localization;
        if (_inviteExisting != null)
        {
            _inviteExisting.Visible = request != null;
            if (request != null && loc != null)
                _inviteExisting.Text = request.IsPending
                    ? loc.Format("ui.campaign.invite_existing_pending", request.RequestedTroops, request.RequestedGold, request.RequestedFood)
                    : loc.Format("ui.campaign.invite_existing", request.RequestedTroops, request.RequestedGold, request.RequestedFood,
                        BattleCampaignService.GetPrewarAllianceMonthsRemaining(_context.TurnManager!.World!, request));
        }
        if (_inviteCancel != null)
        {
            _inviteCancel.Visible = request != null;
            if (loc != null) _inviteCancel.Text = loc.T("ui.campaign.invite_cancel");
        }
    }

    private void CancelPrewarInvitation()
    {
        var world = _context.TurnManager?.World;
        var request = GetExistingPrewarInvitation();
        if (world == null || request == null) return;
        world.PrewarAllianceRequests.Remove(request);
        RefreshExistingPrewarInvitation();
        UpdateConfirmButtonState();
        SetWarning(string.Empty);
    }

    private static int SelectedId(OptionButton? option) => option != null && option.Selected >= 0
        ? option.GetItemMetadata(option.Selected).AsInt32() : 0;

    private void RefreshCampaignInvitations()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var loc = _context.Localization;
        if (world == null || city == null || loc == null || _campaignInvite == null || _inviteCampaign == null) return;
        var allyId = GetSelectedTargetFactionId();
        var relation = world.GetDiplomacyRelation(city.OwnerFactionId, allyId);
        _campaignInvite.Visible = GetSelectedActionType() == DiplomacyActionType.InviteJointAttack;
        _inviteCampaign.Clear();
        foreach (var target in world.Cities.Where(c => c.OwnerFactionId > 0 && c.OwnerFactionId != city.OwnerFactionId && c.OwnerFactionId != allyId &&
                     world.Cities.Any(own => own.OwnerFactionId == city.OwnerFactionId && own.ConnectedCityIds.Contains(c.Id))).OrderBy(c => c.Id))
        {
            _inviteCampaign.AddItem(loc.GetCityName(target));
            _inviteCampaign.SetItemMetadata(_inviteCampaign.ItemCount - 1, target.Id);
        }
        _campaignInvite.GetNode<Label>("Title").Text = loc.T("ui.campaign.invite_heading");
        _campaignInvite.GetNode<Label>("SourceLabel").Text = loc.T("ui.campaign.invite_source");
        _campaignInvite.GetNode<Label>("TroopsLabel").Text = loc.T("ui.campaign.invite_troops");
        if (_inviteRequestedGold != null) _inviteRequestedGold.Prefix = loc.T("ui.campaign.invite_requested_gold") + " ";
        if (_inviteRequestedFood != null) _inviteRequestedFood.Prefix = loc.T("ui.campaign.invite_requested_food") + " ";
        if (_inviteGold != null) _inviteGold.Prefix = loc.T("ui.campaign.invite_gold") + " ";
        if (_inviteFood != null) _inviteFood.Prefix = loc.T("ui.campaign.invite_food") + " ";
        if (_campaignInvite.Visible && relation is not { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 })
            SetWarning(loc.T("ui.campaign.invite_alliance_required"));
        else SetWarning(string.Empty);
        RefreshInviteSources();
    }

    private void RefreshInviteSources()
    {
        var world = _context.TurnManager?.World;
        var loc = _context.Localization;
        if (world != null && loc != null && _inviteSource != null)
        {
            var previousId = SelectedId(_inviteSource);
            var targetId = SelectedId(_inviteCampaign);
            var allyId = GetSelectedTargetFactionId();
            _inviteSource.Clear();
            foreach (var source in world.Cities.Where(city => city.OwnerFactionId == allyId &&
                         BattleCampaignService.GetFriendlyRouteLinks(world, city.Id, targetId, allyId) > 0).OrderBy(city => city.Id))
            {
                var links = BattleCampaignService.GetFriendlyRouteLinks(world, source.Id, targetId, allyId);
                var responseDays = BattleCampaignService.GetInvitationResponseDays(world, targetId, allyId, source.Id, _selectedOfficerId);
                _inviteSource.AddItem(loc.Format("ui.campaign.invite_source_timing", loc.GetCityName(source), responseDays, links * 2));
                _inviteSource.SetItemMetadata(_inviteSource.ItemCount - 1, source.Id);
                if (source.Id == previousId) _inviteSource.Select(_inviteSource.ItemCount - 1);
            }
        }
        RefreshExistingPrewarInvitation();
        UpdateConfirmButtonState();
    }

    private void SendCampaignInvitation()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var loc = _context.Localization;
        if (world == null || city == null || loc == null) return;
        var target = world.GetCity(SelectedId(_inviteCampaign));
        var allyId = GetSelectedTargetFactionId();
        var relation = world.GetDiplomacyRelation(city.OwnerFactionId, allyId);
        if (target == null || relation is not { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 } ||
            !DiplomacyUiHelpers.GetAvailableOfficerIds(_context.TurnManager, city).Contains(_selectedOfficerId))
        {
            SetWarning(loc.T("ui.campaign.invite_invalid"));
            return;
        }
        try
        {
            var scheduled = BattleCampaignService.TrySchedulePrewarAlliance(world, new PrewarAllianceRequestData
            {
                InviterFactionId = city.OwnerFactionId, AllyFactionId = allyId,
                TargetCityId = target.Id, SourceCityId = SelectedId(_inviteSource), EnvoyOfficerId = _selectedOfficerId,
                RequestedTroops = (int)(_inviteTroops?.Value ?? 1000),
                RequestedGold = (int)(_inviteRequestedGold?.Value ?? 0), RequestedFood = (int)(_inviteRequestedFood?.Value ?? 0),
                PromisedGold = (int)(_inviteGold?.Value ?? 0), PromisedFood = (int)(_inviteFood?.Value ?? 0)
            });
            if (!scheduled)
            {
                SetWarning(loc.T("ui.campaign.invite_invalid"));
                return;
            }
            var message = loc.T("ui.campaign.invite_pending");
            var faction = world.GetFaction(allyId);
            var factionName = loc.GetFactionName(world, allyId);
            var resultMessage = loc.Format("ui.campaign.invite_response_message", factionName, message);
            _context.AddLog(resultMessage, true);
            SetWarning(string.Empty);
            HideOverlay();
            _context.ShowInvitationResult(loc.T("ui.campaign.invite_pending_title"), resultMessage, faction?.RulerOfficerId ?? 0);
        }
        catch (InvalidOperationException)
        {
            SetWarning(loc.T("ui.campaign.invite_invalid"));
        }
    }

    private void OnResourceChanged()
    {
        UpdateSummary();
        UpdateConfirmButtonState();
    }

    private void UpdateSummary()
    {
        if (_context.TurnManager?.World == null || _context.Localization == null || _summaryLabel == null)
        {
            return;
        }

        var actionType = GetSelectedActionType();
        if (actionType == DiplomacyActionType.InviteJointAttack)
        {
            _summaryLabel.Text = _context.Localization.T("ui.campaign.invite_summary");
            return;
        }
        var targetFactionId = GetSelectedTargetFactionId();
        var targetFaction = _context.TurnManager.World.GetFaction(targetFactionId);
        var targetFactionName = targetFaction != null ? _context.Localization.GetFactionName(_context.TurnManager.World, targetFaction.Id) : "-";
        var duration = Math.Max(1, (int)Math.Round(_durationSpinBox?.Value ?? 1));
        var gold = Math.Max(0, (int)Math.Round(_goldSpinBox?.Value ?? 0));
        var food = Math.Max(0, (int)Math.Round(_foodSpinBox?.Value ?? 0));
        var horses = Math.Max(0, (int)Math.Round(_horseSpinBox?.Value ?? 0));
        var actionName = _context.Localization.T(DiplomacyUiHelpers.GetActionLocaleKey(actionType));
        var summaryKey = actionType switch
        {
            DiplomacyActionType.Gift => "fmt.diplomacy_summary_gift",
            DiplomacyActionType.Demand => "fmt.diplomacy_summary_demand",
            DiplomacyActionType.BreakPact => "fmt.diplomacy_summary_break_pact",
            DiplomacyActionType.Surrender => "fmt.diplomacy_summary_surrender",
            _ => "fmt.diplomacy_summary_treaty"
        };
        _summaryLabel.Text = actionType switch
        {
            DiplomacyActionType.Gift => _context.Localization.Format(summaryKey, actionName, targetFactionName, DiplomacyUiHelpers.BuildDemandResourceSummary(_context.Localization, gold, food, horses)),
            DiplomacyActionType.Demand => _context.Localization.Format(summaryKey, actionName, targetFactionName, DiplomacyUiHelpers.BuildDemandResourceSummary(_context.Localization, gold, food, horses)),
            DiplomacyActionType.BreakPact => _context.Localization.Format(summaryKey, actionName, targetFactionName),
            DiplomacyActionType.Surrender => _context.Localization.Format(summaryKey, targetFactionName),
            _ => _context.Localization.Format(summaryKey, actionName, targetFactionName, duration)
        };
    }

    private void UpdateRelationInfo()
    {
        if (_context.TurnManager?.World == null || _context.Localization == null || _relationInfoLabel == null || _context.SelectedCity == null)
        {
            return;
        }

        var targetFactionId = GetSelectedTargetFactionId();
        var relation = _context.TurnManager.World.GetDiplomacyRelation(_context.SelectedCity.OwnerFactionId, targetFactionId);
        var status = relation?.Status ?? DiplomacyStatusType.Neutral;
        var remainingMonths = status == DiplomacyStatusType.Neutral ? "-" : (relation?.RemainingMonths ?? 0).ToString();
        var relationScore = relation?.RelationScore ?? 0;
        var trustScore = DiplomacyScoreRules.GetTrustScore(relation, targetFactionId, _context.SelectedCity.OwnerFactionId);
        var threatScore = DiplomacyScoreRules.CalculateThreatScore(_context.TurnManager.World, _context.SelectedCity.OwnerFactionId, targetFactionId, relation);
        var attitudeKey = threatScore >= 65
            ? "ui.diplomacy_attitude_wary"
            : relationScore <= -30
                ? "ui.diplomacy_attitude_hostile"
                : relationScore >= 30 && trustScore >= 50
                    ? "ui.diplomacy_attitude_friendly"
                    : "ui.diplomacy_attitude_neutral";

        _relationInfoLabel.Text =
            $"{_context.Localization.T("ui.diplomacy_attitude")}: {_context.Localization.T(attitudeKey)}\n" +
            $"{_context.Localization.T("ui.relation_status")}: {_context.GetStatusText(status)}\n" +
            $"{_context.Localization.T("ui.remaining_months")}: {remainingMonths}\n" +
            $"{_context.Localization.T("ui.relation_score")}: {relationScore}\n" +
            $"{_context.Localization.T("ui.trust_score")}: {trustScore}\n" +
            $"{_context.Localization.T("ui.threat_score")}: {threatScore}";
    }

    private void UpdateConfirmButtonState()
    {
        if (_confirmButton == null)
        {
            return;
        }

        var actionType = GetSelectedActionType();
        var hasOfficer = _selectedOfficerId > 0;
        var hasTarget = GetSelectedTargetFactionId() > 0;
        if (actionType == DiplomacyActionType.InviteJointAttack)
        {
            var city = _context.SelectedCity;
            var relation = city == null ? null : _context.TurnManager?.World?.GetDiplomacyRelation(city.OwnerFactionId, GetSelectedTargetFactionId());
            _confirmButton.Disabled = !hasOfficer || relation is not { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 } ||
                SelectedId(_inviteCampaign) <= 0 || SelectedId(_inviteSource) <= 0 ||
                GetExistingPrewarInvitation() != null ||
                ((_inviteTroops?.Value ?? 0) <= 0 && (_inviteRequestedGold?.Value ?? 0) <= 0 && (_inviteRequestedFood?.Value ?? 0) <= 0);
            return;
        }
        var hasRequiredResource = actionType switch
        {
            DiplomacyActionType.Gift => (_goldSpinBox?.Value ?? 0) > 0 || (_foodSpinBox?.Value ?? 0) > 0 || (_horseSpinBox?.Value ?? 0) > 0,
            DiplomacyActionType.Demand => (_goldSpinBox?.Value ?? 0) > 0 || (_foodSpinBox?.Value ?? 0) > 0 || (_horseSpinBox?.Value ?? 0) > 0,
            _ => true
        };
        _confirmButton.Disabled = !hasOfficer || !hasTarget || !hasRequiredResource;
    }

    private void UpdateInputState()
    {
        if (_context.Localization == null)
        {
            return;
        }

        var actionType = GetSelectedActionType();
        SetRowVisible("DurationRow", actionType is DiplomacyActionType.Alliance or DiplomacyActionType.Truce);
        SetRowVisible("GoldRow", actionType is DiplomacyActionType.Gift or DiplomacyActionType.Demand);
        SetRowVisible("FoodRow", actionType is DiplomacyActionType.Gift or DiplomacyActionType.Demand);
        SetRowVisible("HorseRow", actionType is DiplomacyActionType.Gift or DiplomacyActionType.Demand);
        SetLabelText("MiddleSection/GoldRow/GoldLabel", actionType == DiplomacyActionType.Demand ? _context.Localization.T("ui.diplomacy_demand_gold") : _context.Localization.T("ui.diplomacy_gift_gold"));
        SetLabelText("MiddleSection/FoodRow/FoodLabel", actionType == DiplomacyActionType.Demand ? _context.Localization.T("ui.diplomacy_demand_food") : _context.Localization.T("ui.diplomacy_gift_food"));
        SetLabelText("MiddleSection/HorseRow/HorseLabel", actionType == DiplomacyActionType.Demand ? _context.Localization.T("ui.diplomacy_demand_horse") : _context.Localization.T("ui.diplomacy_gift_horse"));
    }

    private void UpdateDialogSize()
    {
        var desiredHeight = GetSelectedActionType() switch
        {
            DiplomacyActionType.InviteJointAttack => 700,
            DiplomacyActionType.Gift or DiplomacyActionType.Demand => 520,
            DiplomacyActionType.BreakPact => 420,
            DiplomacyActionType.Surrender => 520,
            _ => 460
        };

        _dialogSize = new Vector2(500.0f, desiredHeight);
        if (OverlayRoot != null)
        {
            UpdateOverlayLayoutNow();
        }
    }

    private void SetRowVisible(string rowName, bool visible)
    {
        var row = GetOverlayContentNode<Control>($"MiddleSection/{rowName}");
        if (row != null)
        {
            row.Visible = visible;
        }
    }

    private void SetWarning(string text)
    {
        if (_warningLabel != null)
        {
            _warningLabel.Text = text;
        }
    }

    private void OnAdvisorPressed()
    {
        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        var targetFactionId = GetSelectedTargetFactionId();
        if (city == null || world == null || localization == null || targetFactionId <= 0)
        {
            return;
        }

        var advisor = FindDiplomacyAdvisor(world, city);
        var role = world.GetFaction(city.OwnerFactionId)?.ChiefStrategistOfficerId == advisor?.Id
            ? localization.T("ui.chief_strategist") ?? "Chief Strategist"
            : localization.T("ui.local_place") ?? "Local";
        if (GetSelectedActionType() == DiplomacyActionType.InviteJointAttack)
        {
            var relation = world.GetDiplomacyRelation(city.OwnerFactionId, targetFactionId);
            var key = relation is { Status: DiplomacyStatusType.Alliance, RemainingMonths: > 0 }
                ? "ui.campaign.invite_advice"
                : "ui.campaign.invite_advice_no_alliance";
            _context.ShowAdvisorMessage(advisor, role, localization.T(key));
            return;
        }
        _context.ShowAdvisorMessage(advisor, role, BuildDiplomacyAdvice(world, city, targetFactionId, localization));
    }

    private OfficerData? FindDiplomacyAdvisor(WorldState world, CityData city)
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
            .OrderByDescending(officer => officer.Charm)
            .ThenByDescending(officer => officer.Intelligence)
            .ThenBy(officer => officer.Id)
            .FirstOrDefault();
    }

    private static bool IsOfficerAvailableForAdvice(OfficerData? officer, WorldState world)
    {
        return officer != null &&
               officer.CaptiveFactionId <= 0 &&
               (officer.DeathYear <= 0 || world.Year <= officer.DeathYear);
    }

    private string BuildDiplomacyAdvice(WorldState world, CityData city, int targetFactionId, LocalizationService localization)
    {
        var targetFaction = world.GetFaction(targetFactionId);
        if (targetFaction == null)
        {
            return localization.T("ui.diplomacy_advice_no_target") ?? "Choose a target faction before asking for advice.";
        }

        var actionType = GetSelectedActionType();
        var actionName = localization.T(DiplomacyUiHelpers.GetActionLocaleKey(actionType));
        var targetName = localization.GetFactionName(world, targetFactionId);
        var relation = world.GetDiplomacyRelation(city.OwnerFactionId, targetFactionId);
        var relationScore = relation?.RelationScore ?? 0;
        var duration = Math.Max(1, (int)Math.Round(_durationSpinBox?.Value ?? 1));
        var gold = Math.Max(0, (int)Math.Round(_goldSpinBox?.Value ?? 0));
        var food = Math.Max(0, (int)Math.Round(_foodSpinBox?.Value ?? 0));
        var horses = Math.Max(0, (int)Math.Round(_horseSpinBox?.Value ?? 0));

        return actionType switch
        {
            DiplomacyActionType.Gift => localization.Format("fmt.diplomacy_advice_gift", actionName, targetName, gold, food, horses),
            DiplomacyActionType.BreakPact => localization.Format("fmt.diplomacy_advice_break_pact", actionName, targetName),
            DiplomacyActionType.Surrender => localization.Format(
                "fmt.diplomacy_advice_surrender",
                actionName,
                targetName,
                world.GetOfficer(targetFaction.RulerOfficerId)?.Ambition ?? 50,
                relationScore),
            _ => BuildDiplomacyChanceAdvice(world, city, targetFaction, relationScore, actionType, actionName, targetName, duration, localization)
        };
    }

    private string BuildDiplomacyChanceAdvice(
        WorldState world,
        CityData city,
        FactionData targetFaction,
        int relationScore,
        DiplomacyActionType actionType,
        string actionName,
        string targetName,
        int duration,
        LocalizationService localization)
    {
        var envoy = _selectedOfficerId > 0 ? world.GetOfficer(_selectedOfficerId) : null;
        var targetRuler = world.GetOfficer(targetFaction.RulerOfficerId);
        var envoyScore = envoy == null ? 50 : (envoy.Charm + envoy.Politics + envoy.Intelligence) / 3;
        var rulerScore = targetRuler == null ? 50 : (targetRuler.Charm + targetRuler.Politics) / 2;
        var baseChance = actionType switch
        {
            DiplomacyActionType.Alliance => 42,
            DiplomacyActionType.Truce => 58,
            DiplomacyActionType.Demand => 34,
            _ => 50
        };
        var pressureBonus = actionType == DiplomacyActionType.Demand
            ? (GetFactionTroopTotal(world, city.OwnerFactionId) - GetFactionTroopTotal(world, targetFaction.Id)) / 120
            : 0;
        var chance = Math.Clamp(
            baseChance + (envoyScore - rulerScore) / 4 + relationScore / 4 + Math.Max(0, city.Loyalty - 70) / 10 + pressureBonus,
            10,
            90);
        var assessment = chance >= 65
            ? localization.T("ui.diplomacy_advice_favorable")
            : chance >= 45
                ? localization.T("ui.diplomacy_advice_cautious")
                : localization.T("ui.diplomacy_advice_unfavorable");
        return localization.Format("fmt.diplomacy_advice_chance", actionName, targetName, duration, relationScore, chance, assessment);
    }

    private static int GetFactionTroopTotal(WorldState world, int factionId)
    {
        return world.Cities.Where(candidate => candidate.OwnerFactionId == factionId).Sum(candidate => candidate.Troops);
    }

    private void OnConfirmPressed()
    {
        if (GetSelectedActionType() == DiplomacyActionType.InviteJointAttack)
        {
            SendCampaignInvitation();
            return;
        }
        var city = _context.SelectedCity;
        var turnManager = _context.TurnManager;
        var commandResolver = _context.CommandResolver;
        var localization = _context.Localization;
        if (city == null || turnManager == null || commandResolver == null || localization == null)
        {
            return;
        }

        if (_selectedOfficerId <= 0)
        {
            SetWarning(localization.T("ui.select_officer_warning"));
            return;
        }

        var targetFactionId = GetSelectedTargetFactionId();
        if (targetFactionId <= 0)
        {
            SetWarning(localization.T("ui.diplomacy_target_required_warning"));
            return;
        }

        var actionType = GetSelectedActionType();
        var gold = Math.Max(0, (int)Math.Round(_goldSpinBox?.Value ?? 0));
        var food = Math.Max(0, (int)Math.Round(_foodSpinBox?.Value ?? 0));
        var horses = Math.Max(0, (int)Math.Round(_horseSpinBox?.Value ?? 0));
        if (actionType == DiplomacyActionType.Gift && gold <= 0 && food <= 0 && horses <= 0)
        {
            SetWarning(localization.T("ui.diplomacy_gift_resource_required_warning"));
            return;
        }

        if (actionType == DiplomacyActionType.Demand && gold <= 0 && food <= 0 && horses <= 0)
        {
            SetWarning(localization.T("ui.diplomacy_resource_required_warning"));
            return;
        }

        var result = commandResolver.Execute(new CommandRequest
        {
            Type = CommandType.Diplomacy,
            ActorFactionId = turnManager.GetPlayerFactionId(),
            SourceCityId = city.Id,
            TargetFactionId = targetFactionId,
            OfficerIds = new System.Collections.Generic.List<int> { _selectedOfficerId },
            GoldToSend = gold,
            FoodToSend = food,
            HorsesToSend = horses,
            DurationMonths = Math.Max(1, (int)Math.Round(_durationSpinBox?.Value ?? 1)),
            DiplomacyActionType = actionType
        });

        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        if (result.Success)
        {
            HideOverlay();
            _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
            return;
        }

        SetWarning(_context.GetLocalizedResultMessage(result));
    }

    private void OnSelectOfficerPressed()
    {
        var localization = _context.Localization;
        if (localization == null)
        {
            return;
        }

        var candidateOfficerIds = DiplomacyUiHelpers.GetAvailableOfficerIds(_context.TurnManager, _context.SelectedCity);
        if (candidateOfficerIds.Count == 0)
        {
            SetWarning(localization.T("ui.select_officer_warning"));
            return;
        }

        _context.ShowOfficerSelectorDialog(
            localization.T("ui.diplomacy_officer"),
            candidateOfficerIds,
            HudController.OfficerSelectorPrimaryStat.Charm,
            officerId =>
            {
                _selectedOfficerId = officerId;
                UpdateSelectedOfficerSummary();
                if (GetSelectedActionType() == DiplomacyActionType.InviteJointAttack) RefreshInviteSources();
                UpdateConfirmButtonState();
                SetWarning(string.Empty);
            },
            () => _context.Localization?.T("ui.diplomacy_officer") ?? localization.T("ui.diplomacy_officer"),
            BuildDiplomacyOfficerSelectorDisplayConfig());
    }

    private HudController.OfficerSelectorDisplayConfig BuildDiplomacyOfficerSelectorDisplayConfig()
    {
        var world = _context.TurnManager?.World;
        var localization = _context.Localization;
        if (world == null || localization == null)
        {
            throw new InvalidOperationException("Diplomacy officer selector requires world and localization.");
        }

        return new HudController.OfficerSelectorDisplayConfig
        {
            Columns =
            [
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.officers"), MinWidth = 120 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.role"), MinWidth = 80 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.city"), MinWidth = 95 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.status"), MinWidth = 78 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.loyalty"), MinWidth = 58 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.charm"), MinWidth = 58 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.intelligence"), MinWidth = 58 },
                new HudController.OfficerSelectorColumnDefinition { Title = localization.T("ui.politics"), MinWidth = 58 }
            ],
            BuildRowTexts = officer =>
            {
                var city = world.GetCity(officer.CityId);
                return
                [
                    localization.GetOfficerName(officer),
                    localization.GetOfficerRole(officer),
                    city != null ? localization.GetCityName(city) : "--",
                    localization.GetOfficerStatus(world, officer),
                    officer.Loyalty.ToString(),
                    officer.Charm.ToString(),
                    officer.Intelligence.ToString(),
                    officer.Politics.ToString()
                ];
            },
            PanelSize = new Vector2(760.0f, 360.0f)
        };
    }

    private void UpdateSelectedOfficerSummary()
    {
        if (_selectedOfficerLabel == null || _context.Localization == null)
        {
            return;
        }

        var officer = _selectedOfficerId > 0 ? _context.TurnManager?.World?.GetOfficer(_selectedOfficerId) : null;
        var officerName = officer != null ? _context.Localization.GetOfficerName(officer) : _context.Localization.T("ui.unassigned");
        _selectedOfficerLabel.Text = $"{_context.Localization.T("ui.diplomacy_officer")}: {officerName}";
    }
}
