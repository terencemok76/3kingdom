using System;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private void EnsureDiplomacyProposalDialogWidgets()
    {
        if (_diplomacyProposalDialog == null)
        {
            return;
        }

        var existingRoot = _diplomacyProposalDialog.GetNodeOrNull<VBoxContainer>("DiplomacyProposalRoot");
        if (existingRoot != null)
        {
            _diplomacyProposalSummaryLabel = existingRoot.GetNodeOrNull<Label>("SummaryLabel");
            _diplomacyProposalAcceptButton = existingRoot.GetNodeOrNull<Button>("FooterRow/AcceptButton");
            _diplomacyProposalRejectButton = existingRoot.GetNodeOrNull<Button>("FooterRow/RejectButton");
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
        _diplomacyProposalDialog.AddChild(root);

        _diplomacyProposalSummaryLabel = new Label
        {
            Name = "SummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_diplomacyProposalSummaryLabel);

        var footer = new HBoxContainer
        {
            Name = "FooterRow",
            Alignment = BoxContainer.AlignmentMode.Center
        };
        footer.AddThemeConstantOverride("separation", 12);
        root.AddChild(footer);

        _diplomacyProposalAcceptButton = new Button
        {
            Name = "AcceptButton"
        };
        _diplomacyProposalAcceptButton.Pressed += OnDiplomacyProposalAcceptPressed;
        footer.AddChild(_diplomacyProposalAcceptButton);

        _diplomacyProposalRejectButton = new Button
        {
            Name = "RejectButton"
        };
        _diplomacyProposalRejectButton.Pressed += OnDiplomacyProposalRejectPressed;
        footer.AddChild(_diplomacyProposalRejectButton);
    }

    private void UpdateDiplomacyProposalDialogText()
    {
        if (_diplomacyProposalDialog == null || _localization == null)
        {
            return;
        }

        _diplomacyProposalDialog.Title = _localization.T("ui.diplomacy_proposal");
        if (_diplomacyProposalAcceptButton != null)
        {
            _diplomacyProposalAcceptButton.Text = _localization.T("ui.accept");
        }

        if (_diplomacyProposalRejectButton != null)
        {
            _diplomacyProposalRejectButton.Text = _localization.T("ui.reject");
        }

        if (_diplomacyProposalDialog.Visible)
        {
            UpdateDiplomacyProposalSummary();
        }
    }

    private void ShowDiplomacyProposalDialog(PendingCommandData pendingCommand)
    {
        if (_diplomacyProposalDialog == null || _localization == null)
        {
            return;
        }

        _pendingDiplomacyProposalCommand = pendingCommand;
        EnsureDiplomacyProposalDialogWidgets();
        UpdateDiplomacyProposalDialogText();
        UpdateDiplomacyProposalSummary();
        _diplomacyProposalDialog.PopupCentered(new Vector2I(660, 260));
    }

    private void UpdateDiplomacyProposalSummary()
    {
        if (_diplomacyProposalSummaryLabel == null)
        {
            return;
        }

        _diplomacyProposalSummaryLabel.Text = BuildDiplomacyProposalSummary();
    }

    private string BuildDiplomacyProposalSummary()
    {
        if (_turnManager?.World == null || _localization == null || _pendingDiplomacyProposalCommand == null)
        {
            return string.Empty;
        }

        var world = _turnManager.World;
        var pendingCommand = _pendingDiplomacyProposalCommand;
        var officer = world.GetOfficer(pendingCommand.OfficerIds.Count > 0 ? pendingCommand.OfficerIds[0] : 0);
        var sourceCity = world.GetCity(pendingCommand.SourceCityId);
        var sourceCityName = sourceCity != null ? _localization.GetCityName(sourceCity) : _localization.T("ui.unknown");
        var envoyName = officer != null ? _localization.GetOfficerName(officer) : _localization.T("ui.unknown");
        var actionName = _localization.T(GetDiplomacyActionLocaleKey(pendingCommand.DiplomacyActionType));

        return pendingCommand.DiplomacyActionType == DiplomacyActionType.Gift
            ? _localization.Format("fmt.diplomacy_proposal_gift", envoyName, sourceCityName, pendingCommand.GoldToSend)
            : _localization.Format("fmt.diplomacy_proposal_treaty", envoyName, sourceCityName, actionName, pendingCommand.DurationMonths);
    }

    private void OnDiplomacyProposalAcceptPressed()
    {
        if (_turnManager?.World == null || _commandResolver == null || _pendingDiplomacyProposalCommand == null)
        {
            return;
        }

        var pendingCommand = _pendingDiplomacyProposalCommand;
        var result = _commandResolver.ResolvePendingCommand(pendingCommand);
        _turnManager.World.PendingCommands.Remove(pendingCommand);
        _pendingDiplomacyProposalCommand = null;
        _diplomacyProposalDialog?.Hide();
        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        CheckFactionEliminations();
        ContinuePendingNonAttackResolution();
    }

    private void OnDiplomacyProposalRejectPressed()
    {
        if (_turnManager?.World == null || _localization == null || _pendingDiplomacyProposalCommand == null)
        {
            return;
        }

        var world = _turnManager.World;
        var pendingCommand = _pendingDiplomacyProposalCommand;
        if (pendingCommand.DiplomacyActionType == DiplomacyActionType.Gift && pendingCommand.GoldToSend > 0)
        {
            var sourceCity = world.GetCity(pendingCommand.SourceCityId);
            if (sourceCity != null)
            {
                sourceCity.Gold += pendingCommand.GoldToSend;
            }
        }

        world.PendingCommands.Remove(pendingCommand);
        var result = BuildDiplomacyProposalRejectedResult(pendingCommand);
        _pendingDiplomacyProposalCommand = null;
        _diplomacyProposalDialog?.Hide();
        AddLog(GetLocalizedResultMessage(result), isPlayerRelated: true);
        ContinuePendingNonAttackResolution();
    }

    private CommandResult BuildDiplomacyProposalRejectedResult(PendingCommandData pendingCommand)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return new CommandResult { Success = true, Message = string.Empty };
        }

        var world = _turnManager.World;
        var officer = world.GetOfficer(pendingCommand.OfficerIds.Count > 0 ? pendingCommand.OfficerIds[0] : 0);
        var sourceCity = world.GetCity(pendingCommand.SourceCityId);
        var envoyZh = officer != null
            ? (!string.IsNullOrWhiteSpace(officer.NameZhHant) ? officer.NameZhHant : officer.Name)
            : _localization.TForLanguage(GameLanguage.TraditionalChinese, "ui.unknown");
        var envoyEn = officer != null
            ? (!string.IsNullOrWhiteSpace(officer.Name) ? officer.Name : officer.NameZhHant)
            : _localization.TForLanguage(GameLanguage.English, "ui.unknown");
        var cityZh = sourceCity != null
            ? (!string.IsNullOrWhiteSpace(sourceCity.NameZhHant) ? sourceCity.NameZhHant : sourceCity.Name)
            : _localization.TForLanguage(GameLanguage.TraditionalChinese, "ui.unknown");
        var cityEn = sourceCity != null
            ? (!string.IsNullOrWhiteSpace(sourceCity.NameEn) ? sourceCity.NameEn : sourceCity.NameZhHant)
            : _localization.TForLanguage(GameLanguage.English, "ui.unknown");
        var actionKey = GetDiplomacyActionLocaleKey(pendingCommand.DiplomacyActionType);
        var actionZh = _localization.TForLanguage(GameLanguage.TraditionalChinese, actionKey);
        var actionEn = _localization.TForLanguage(GameLanguage.English, actionKey);
        var key = pendingCommand.DiplomacyActionType == DiplomacyActionType.Gift
            ? "cmd.diplomacy.player_rejected_gift"
            : "cmd.diplomacy.player_rejected_treaty";

        return new CommandResult
        {
            Success = true,
            Message = _localization.FormatForLanguage(GameLanguage.English, key, envoyEn, cityEn, pendingCommand.GoldToSend, actionEn, pendingCommand.DurationMonths),
            MessageZhHant = _localization.FormatForLanguage(GameLanguage.TraditionalChinese, key, envoyZh, cityZh, pendingCommand.GoldToSend, actionZh, pendingCommand.DurationMonths),
            MessageEn = _localization.FormatForLanguage(GameLanguage.English, key, envoyEn, cityEn, pendingCommand.GoldToSend, actionEn, pendingCommand.DurationMonths)
        };
    }

    private static string GetDiplomacyActionLocaleKey(DiplomacyActionType actionType)
    {
        return actionType switch
        {
            DiplomacyActionType.Alliance => "command.diplomacy.alliance",
            DiplomacyActionType.Truce => "command.diplomacy.truce",
            DiplomacyActionType.Gift => "command.diplomacy.gift",
            DiplomacyActionType.Demand => "command.diplomacy.demand",
            DiplomacyActionType.BreakPact => "command.diplomacy.break_pact",
            _ => "command.diplomacy.alliance"
        };
    }
}
