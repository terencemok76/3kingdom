using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class DiplomacyProposalDialogController
{
    private readonly DiplomacyUiContext _context;
    private readonly System.Func<bool> _hasPendingPlayerSuccession;
    private readonly System.Action _showSuccessionDialog;
    private Window? _dialog;
    private Label? _summaryLabel;
    private Button? _acceptButton;
    private Button? _rejectButton;

    public DiplomacyProposalDialogController(
        DiplomacyUiContext context,
        System.Func<bool> hasPendingPlayerSuccession,
        System.Action showSuccessionDialog)
    {
        _context = context;
        _hasPendingPlayerSuccession = hasPendingPlayerSuccession;
        _showSuccessionDialog = showSuccessionDialog;
    }

    public PendingCommandData? PendingProposalCommand { get; set; }

    public void Initialize()
    {
        _dialog = _context.CreateCodeWindow(OnCloseRequested);
        EnsureWidgets();
        _dialog.Hide();
    }

    public void Hide() => _dialog?.Hide();

    public void RefreshText()
    {
        if (_dialog == null || _context.Localization == null)
        {
            return;
        }

        _dialog.Title = _context.Localization.T("ui.diplomacy_proposal");
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
        if (_dialog.Visible)
        {
            UpdateSummary();
        }
    }

    public void Show(PendingCommandData pendingCommand)
    {
        if (_dialog == null || _context.Localization == null)
        {
            return;
        }

        PendingProposalCommand = pendingCommand;
        EnsureWidgets();
        RefreshText();
        UpdateSummary();
        _dialog.PopupCentered(new Vector2I(660, 260));
    }

    private void EnsureWidgets()
    {
        if (_dialog == null)
        {
            return;
        }

        var existingRoot = _dialog.GetNodeOrNull<VBoxContainer>("DiplomacyProposalRoot");
        if (existingRoot != null)
        {
            _summaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            _acceptButton = existingRoot.GetNodeOrNull<Button>("FooterRow/AcceptButton");
            _rejectButton = existingRoot.GetNodeOrNull<Button>("FooterRow/RejectButton");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "DiplomacyProposalRoot",
            CustomMinimumSize = new Vector2(620.0f, 220.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 12);
        _dialog.AddChild(root);

        _summaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_summaryLabel);

        var footer = new HBoxContainer
        {
            Name = "FooterRow",
            Alignment = BoxContainer.AlignmentMode.Center
        };
        footer.AddThemeConstantOverride("separation", 12);
        root.AddChild(footer);

        _acceptButton = new Button
        {
            Name = "AcceptButton"
        };
        _acceptButton.Pressed += OnAcceptPressed;
        footer.AddChild(_acceptButton);

        _rejectButton = new Button
        {
            Name = "RejectButton"
        };
        _rejectButton.Pressed += OnRejectPressed;
        footer.AddChild(_rejectButton);
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
        var sourceCityName = sourceCity != null ? _context.Localization.GetCityName(sourceCity) : _context.Localization.T("ui.unknown");
        var envoyName = officer != null ? _context.Localization.GetOfficerName(officer) : _context.Localization.T("ui.unknown");
        var actionName = _context.Localization.T(DiplomacyUiHelpers.GetActionLocaleKey(pendingCommand.DiplomacyActionType));

        return pendingCommand.DiplomacyActionType switch
        {
            DiplomacyActionType.Gift => _context.Localization.Format(
                "fmt.diplomacy_proposal_gift",
                envoyName,
                sourceCityName,
                DiplomacyUiHelpers.BuildDemandResourceSummary(_context.Localization, pendingCommand.GoldToSend, pendingCommand.FoodToSend, pendingCommand.HorsesToSend)),
            DiplomacyActionType.Demand => _context.Localization.Format(
                "fmt.diplomacy_proposal_demand",
                envoyName,
                sourceCityName,
                DiplomacyUiHelpers.BuildDemandResourceSummary(_context.Localization, pendingCommand.GoldToSend, pendingCommand.FoodToSend, pendingCommand.HorsesToSend)),
            DiplomacyActionType.BreakPact => _context.Localization.Format("fmt.diplomacy_proposal_break_pact_notice", envoyName, sourceCityName, actionName),
            _ => _context.Localization.Format("fmt.diplomacy_proposal_treaty", envoyName, sourceCityName, actionName, pendingCommand.DurationMonths)
        };
    }

    private bool IsNotificationOnly()
    {
        return PendingProposalCommand?.DiplomacyActionType == DiplomacyActionType.BreakPact;
    }

    private void OnCloseRequested()
    {
        if (IsNotificationOnly())
        {
            OnAcceptPressed();
            return;
        }

        OnRejectPressed();
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
        _dialog?.Hide();
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
        _dialog?.Hide();
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
        var cityZh = sourceCity != null
            ? (!string.IsNullOrWhiteSpace(sourceCity.NameZhHant) ? sourceCity.NameZhHant : sourceCity.Name)
            : _context.Localization.TForLanguage(GameLanguage.TraditionalChinese, "ui.unknown");
        var cityEn = sourceCity != null
            ? (!string.IsNullOrWhiteSpace(sourceCity.NameEn) ? sourceCity.NameEn : sourceCity.NameZhHant)
            : _context.Localization.TForLanguage(GameLanguage.English, "ui.unknown");
        var actionKey = DiplomacyUiHelpers.GetActionLocaleKey(pendingCommand.DiplomacyActionType);
        var actionZh = _context.Localization.TForLanguage(GameLanguage.TraditionalChinese, actionKey);
        var actionEn = _context.Localization.TForLanguage(GameLanguage.English, actionKey);
        object resourceArgument = pendingCommand.DiplomacyActionType == DiplomacyActionType.Demand
            ? DiplomacyUiHelpers.BuildDemandResourceSummary(_context.Localization, pendingCommand.GoldToSend, pendingCommand.FoodToSend, pendingCommand.HorsesToSend)
            : pendingCommand.GoldToSend;
        var key = pendingCommand.DiplomacyActionType switch
        {
            DiplomacyActionType.Gift => "cmd.diplomacy.player_rejected_gift",
            DiplomacyActionType.Demand => "cmd.diplomacy.player_rejected_demand",
            DiplomacyActionType.BreakPact => "cmd.diplomacy.player_rejected_break_pact",
            _ => "cmd.diplomacy.player_rejected_treaty"
        };

        return new CommandResult
        {
            Success = true,
            Message = _context.Localization.FormatForLanguage(GameLanguage.English, key, envoyEn, cityEn, resourceArgument, actionEn, pendingCommand.DurationMonths),
            MessageZhHant = _context.Localization.FormatForLanguage(GameLanguage.TraditionalChinese, key, envoyZh, cityZh, resourceArgument, actionZh, pendingCommand.DurationMonths),
            MessageEn = _context.Localization.FormatForLanguage(GameLanguage.English, key, envoyEn, cityEn, resourceArgument, actionEn, pendingCommand.DurationMonths)
        };
    }
}
