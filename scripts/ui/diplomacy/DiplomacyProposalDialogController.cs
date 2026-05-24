using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class DiplomacyProposalDialogController : FloatingOverlayController
{
    private readonly DiplomacyUiContext _context;
    private readonly System.Func<bool> _hasPendingPlayerSuccession;
    private readonly System.Action _showSuccessionDialog;
    private Label? _summaryLabel;
    private Button? _acceptButton;
    private Button? _rejectButton;
    protected override Vector2 MinimumOverlaySize => new(700.0f, 280.0f);

    public DiplomacyProposalDialogController(
        DiplomacyUiContext context,
        System.Func<bool> hasPendingPlayerSuccession,
        System.Action showSuccessionDialog)
        : base(context, "res://scenes/ui/diplomacy/DiplomacyProposalDialog.tscn")
    {
        _context = context;
        _hasPendingPlayerSuccession = hasPendingPlayerSuccession;
        _showSuccessionDialog = showSuccessionDialog;
    }

    public PendingCommandData? PendingProposalCommand { get; set; }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization.T("ui.diplomacy_proposal"));
        if (_acceptButton != null)
        {
            _acceptButton.Text = IsNotificationOnly()
                ? _context.Localization.T("ui.confirm_diplomacy")
                : _context.Localization.T("ui.accept");
        }

        if (_rejectButton != null)
        {
            _rejectButton.Text = _context.Localization.T("ui.reject");
            _rejectButton.Visible = !IsNotificationOnly();
        }

        if (OverlayRoot?.Visible == true)
        {
            UpdateSummary();
        }
    }

    public void Show(PendingCommandData pendingCommand)
    {
        if (_context.Localization == null)
        {
            return;
        }

        PendingProposalCommand = pendingCommand;
        RefreshText();
        UpdateSummary();
        ShowOverlay();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _acceptButton = root.GetNodeOrNull<Button>("FooterRow/AcceptButton");
        _rejectButton = root.GetNodeOrNull<Button>("FooterRow/RejectButton");

        if (_acceptButton != null)
        {
            _context.ApplyCommandButtonTheme(_acceptButton);
            _acceptButton.Pressed += OnAcceptPressed;
        }

        if (_rejectButton != null)
        {
            _context.ApplyCommandButtonTheme(_rejectButton);
            _rejectButton.Pressed += OnRejectPressed;
        }
    }

    protected override void OnOverlayCloseRequested()
    {
        if (IsNotificationOnly())
        {
            OnAcceptPressed();
            return;
        }

        OnRejectPressed();
    }

    private void UpdateSummary()
    {
        if (_summaryLabel == null)
        {
            return;
        }

        _summaryLabel.Text = BuildSummary();
    }

    private string BuildSummary()
    {
        if (_context.TurnManager?.World == null || _context.Localization == null || PendingProposalCommand == null)
        {
            return string.Empty;
        }

        var world = _context.TurnManager.World;
        var pendingCommand = PendingProposalCommand;
        var officer = world.GetOfficer(pendingCommand.OfficerIds.Count > 0 ? pendingCommand.OfficerIds[0] : 0);
        var sourceCity = world.GetCity(pendingCommand.SourceCityId);
        var envoyName = officer != null ? _context.Localization.GetOfficerName(officer) : _context.Localization.T("ui.unknown");
        var factionName = _context.Localization.GetFactionName(world, pendingCommand.ActorFactionId);
        var cityName = sourceCity != null ? _context.Localization.GetCityName(sourceCity) : _context.Localization.T("ui.unknown");
        var actionName = _context.Localization.T(DiplomacyUiHelpers.GetActionLocaleKey(pendingCommand.DiplomacyActionType));
        var resourceText = DiplomacyUiHelpers.BuildDemandResourceSummary(
            _context.Localization,
            pendingCommand.GoldToSend,
            pendingCommand.FoodToSend,
            pendingCommand.HorsesToSend);

        if (_context.Localization.IsTraditionalChinese)
        {
            return pendingCommand.DiplomacyActionType switch
            {
                DiplomacyActionType.Gift => $"「{factionName}」使者「{envoyName}」自「{cityName}」向我方送來 {resourceText} 的贈禮。是否接受？",
                DiplomacyActionType.Demand => $"「{factionName}」使者「{envoyName}」自「{cityName}」要求我方進貢 {resourceText}。是否接受？",
                DiplomacyActionType.BreakPact => $"「{factionName}」使者「{envoyName}」自「{cityName}」通知我方，將立即執行「{actionName}」，現有盟約會立刻結束。",
                _ => $"「{factionName}」使者「{envoyName}」自「{cityName}」提議與我方進行「{actionName}」，為期 {pendingCommand.DurationMonths} 個月。是否接受？"
            };
        }

        return pendingCommand.DiplomacyActionType switch
        {
            DiplomacyActionType.Gift => $"Envoy \"{envoyName}\" of \"{factionName}\", from \"{cityName}\", offers your faction a gift of {resourceText}. Accept?",
            DiplomacyActionType.Demand => $"Envoy \"{envoyName}\" of \"{factionName}\", from \"{cityName}\", demands tribute of {resourceText} from your faction. Accept?",
            DiplomacyActionType.BreakPact => $"Envoy \"{envoyName}\" of \"{factionName}\", from \"{cityName}\", notifies your faction that \"{actionName}\" will be executed immediately and the current pact will end at once.",
            _ => $"Envoy \"{envoyName}\" of \"{factionName}\", from \"{cityName}\", proposes \"{actionName}\" with your faction for {pendingCommand.DurationMonths} months. Accept?"
        };
    }

    private bool IsNotificationOnly()
    {
        return PendingProposalCommand?.DiplomacyActionType == DiplomacyActionType.BreakPact;
    }

    private void OnAcceptPressed()
    {
        if (_context.TurnManager?.World == null || _context.CommandResolver == null || PendingProposalCommand == null)
        {
            return;
        }

        var pendingCommand = PendingProposalCommand;
        var result = _context.CommandResolver.ResolvePendingCommand(pendingCommand);
        _context.TurnManager.World.PendingCommands.Remove(pendingCommand);
        PendingProposalCommand = null;
        HideOverlay();
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _context.CheckFactionEliminations();
        if (_hasPendingPlayerSuccession())
        {
            _showSuccessionDialog();
            return;
        }

        _context.ContinuePendingNonAttackResolution();
    }

    private void OnRejectPressed()
    {
        if (_context.TurnManager?.World == null || _context.Localization == null || PendingProposalCommand == null)
        {
            return;
        }

        var world = _context.TurnManager.World;
        var pendingCommand = PendingProposalCommand;
        if (pendingCommand.DiplomacyActionType == DiplomacyActionType.Gift)
        {
            var sourceCity = world.GetCity(pendingCommand.SourceCityId);
            if (sourceCity != null)
            {
                sourceCity.Gold += pendingCommand.GoldToSend;
                sourceCity.Food += pendingCommand.FoodToSend;
                sourceCity.Horses += pendingCommand.HorsesToSend;
            }
        }

        world.PendingCommands.Remove(pendingCommand);
        var result = BuildRejectedResult(pendingCommand);
        PendingProposalCommand = null;
        HideOverlay();
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        _context.ContinuePendingNonAttackResolution();
    }

    private CommandResult BuildRejectedResult(PendingCommandData pendingCommand)
    {
        if (_context.TurnManager?.World == null || _context.Localization == null)
        {
            return new CommandResult { Success = true, Message = string.Empty };
        }

        var world = _context.TurnManager.World;
        var officer = world.GetOfficer(pendingCommand.OfficerIds.Count > 0 ? pendingCommand.OfficerIds[0] : 0);
        var sourceCity = world.GetCity(pendingCommand.SourceCityId);
        var envoyZh = officer != null
            ? (!string.IsNullOrWhiteSpace(officer.NameZhHant) ? officer.NameZhHant : officer.Name)
            : _context.Localization.TForLanguage(GameLanguage.TraditionalChinese, "ui.unknown");
        var envoyEn = officer != null
            ? (!string.IsNullOrWhiteSpace(officer.Name) ? officer.Name : officer.NameZhHant)
            : _context.Localization.TForLanguage(GameLanguage.English, "ui.unknown");
        var factionZh = GetFactionNameForLanguage(world, pendingCommand.ActorFactionId, GameLanguage.TraditionalChinese);
        var factionEn = GetFactionNameForLanguage(world, pendingCommand.ActorFactionId, GameLanguage.English);
        var cityZh = GetCityNameForLanguage(sourceCity, GameLanguage.TraditionalChinese);
        var cityEn = GetCityNameForLanguage(sourceCity, GameLanguage.English);
        var actionKey = DiplomacyUiHelpers.GetActionLocaleKey(pendingCommand.DiplomacyActionType);
        var actionZh = _context.Localization.TForLanguage(GameLanguage.TraditionalChinese, actionKey);
        var actionEn = _context.Localization.TForLanguage(GameLanguage.English, actionKey);
        var resourceZh = DiplomacyUiHelpers.BuildDemandResourceSummary(_context.Localization, pendingCommand.GoldToSend, pendingCommand.FoodToSend, pendingCommand.HorsesToSend);
        var resourceEn = BuildEnglishDemandResourceSummary(pendingCommand);

        var zh = pendingCommand.DiplomacyActionType switch
        {
            DiplomacyActionType.Gift => $"你拒絕了「{factionZh}」使者「{envoyZh}」自「{cityZh}」送來的 {resourceZh} 贈禮。",
            DiplomacyActionType.Demand => $"你拒絕了「{factionZh}」使者「{envoyZh}」自「{cityZh}」提出的 {resourceZh} 進貢要求。",
            DiplomacyActionType.BreakPact => $"你拒絕了「{factionZh}」使者「{envoyZh}」自「{cityZh}」提出的「{actionZh}」提案，現有盟約維持不變。",
            _ => $"你拒絕了「{factionZh}」使者「{envoyZh}」自「{cityZh}」提出的「{actionZh}」提案。"
        };
        var en = pendingCommand.DiplomacyActionType switch
        {
            DiplomacyActionType.Gift => $"You rejected the gift of {resourceEn} from envoy \"{envoyEn}\" of \"{factionEn}\", from \"{cityEn}\".",
            DiplomacyActionType.Demand => $"You rejected the tribute demand of {resourceEn} from envoy \"{envoyEn}\" of \"{factionEn}\", from \"{cityEn}\".",
            DiplomacyActionType.BreakPact => $"You rejected the \"{actionEn}\" proposal from envoy \"{envoyEn}\" of \"{factionEn}\", from \"{cityEn}\". The current pact remains unchanged.",
            _ => $"You rejected the \"{actionEn}\" proposal from envoy \"{envoyEn}\" of \"{factionEn}\", from \"{cityEn}\"."
        };

        return new CommandResult
        {
            Success = true,
            Message = en,
            MessageZhHant = zh,
            MessageEn = en,
            IsPlayerRelated = true
        };
    }

    private string GetFactionNameForLanguage(WorldState world, int factionId, GameLanguage language)
    {
        if (_context.Localization == null)
        {
            return string.Empty;
        }

        if (factionId <= 0)
        {
            return _context.Localization.TForLanguage(language, "ui.neutral");
        }

        var faction = world.GetFaction(factionId);
        if (faction == null)
        {
            return _context.Localization.TForLanguage(language, "ui.unknown");
        }

        return language == GameLanguage.TraditionalChinese
            ? (!string.IsNullOrWhiteSpace(faction.NameZhHant) ? faction.NameZhHant : faction.NameEn)
            : (!string.IsNullOrWhiteSpace(faction.NameEn) ? faction.NameEn : faction.NameZhHant);
    }

    private string GetCityNameForLanguage(CityData? city, GameLanguage language)
    {
        if (_context.Localization == null || city == null)
        {
            return _context.Localization?.TForLanguage(language, "ui.unknown") ?? string.Empty;
        }

        return language == GameLanguage.TraditionalChinese
            ? (!string.IsNullOrWhiteSpace(city.NameZhHant) ? city.NameZhHant : city.Name)
            : (!string.IsNullOrWhiteSpace(city.NameEn) ? city.NameEn : city.NameZhHant);
    }

    private static string BuildEnglishDemandResourceSummary(PendingCommandData pendingCommand)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (pendingCommand.GoldToSend > 0)
        {
            parts.Add($"{pendingCommand.GoldToSend} gold");
        }

        if (pendingCommand.FoodToSend > 0)
        {
            parts.Add($"{pendingCommand.FoodToSend} food");
        }

        if (pendingCommand.HorsesToSend > 0)
        {
            parts.Add($"{pendingCommand.HorsesToSend} horse");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }
}
