using Godot;
using System;
using System.Linq;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private void RefreshTeamHudCards(BattleHudTeamInfo attackerInfo, BattleHudTeamInfo defenderInfo)
    {
        RefreshTeamHudCard("Attacker", attackerInfo);
        RefreshTeamHudCard("Defender", defenderInfo);
    }

    private void RefreshTeamHudCard(string cardPrefix, BattleHudTeamInfo info)
    {
        var isCaravanEscort = _activeCampaign?.IsCaravanEscortBattle == true;
        var isTransport = isCaravanEscort && !BattleTeamIdentity.IsAttacker(info.Name);
        var sideLabel = isCaravanEscort
            ? BattleTeamIdentity.IsAttacker(info.Name)
                ? BattleText("ui.battle.caravan_ambush", "Ambush")
                : BattleText("ui.battle.caravan_transport", "Transport")
            : BattleTeamIdentity.IsAttacker(info.Name)
                ? BattleText("ui.battle.log_attacker", "Attacker")
                : BattleText("ui.battle.log_defender", "Defender");
        var displayedGold = isTransport ? _activeCampaign!.CaravanGold : info.TotalGold;
        var displayedFood = isTransport ? _activeCampaign!.CaravanFood : info.TotalFood;

        SetTeamHudCardLabel(cardPrefix, "FactionLabel", BattleFormat("ui.battle.team_card_faction", "{0} · {1}", sideLabel, FormatTeamName(info.Name)));
        // Each icon already expresses its category. Keep the card compact by
        // rendering only the live numeric value beside it.
        SetTeamHudCardLabel(cardPrefix, "TroopsLabel", info.TotalTroops.ToString("N0"));
        SetTeamHudCardLabel(cardPrefix, "OfficersLabel", info.TotalGenerals.ToString("N0"));
        SetTeamHudCardLabel(cardPrefix, "EngineersLabel", GetActiveWorkerCountForTeam(info.Name).ToString("N0"));
        SetTeamHudCardLabel(cardPrefix, "SiegeLabel", info.TotalSiegeUnits.ToString("N0"));
        SetTeamHudCardLabel(cardPrefix, "GoldLabel", displayedGold.ToString("N0"));
        SetTeamHudCardLabel(cardPrefix, "FoodLabel", displayedFood.ToString("N0"));
    }

    private void SetTeamHudCardLabel(string cardPrefix, string labelName, string text)
    {
        var label = GetNodeOrNull<Label>($"UiLayer/TopBar/{cardPrefix}Card/{cardPrefix}{labelName}");
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void RefreshTeamHudIdentity()
    {
        RefreshTeamHudPortrait(TeamAInfo.Name, "UiLayer/TopBar/AttackerCard/AttackerPortraitFrame/AttackerPortrait");
        RefreshTeamHudPortrait(TeamBInfo.Name, "UiLayer/TopBar/DefenderCard/DefenderPortraitFrame/DefenderPortrait");
        RefreshTeamHudRulerSurname(TeamAInfo.Name, "UiLayer/TopBar/AttackerCard/AttackerFlag/AttackerRulerSurnameLabel");
        RefreshTeamHudRulerSurname(TeamBInfo.Name, "UiLayer/TopBar/DefenderCard/DefenderFlag/DefenderRulerSurnameLabel");
    }

    private void RefreshTeamHudRulerSurname(string teamName, string nodePath)
    {
        var label = GetNodeOrNull<Label>(nodePath);
        if (label == null)
        {
            return;
        }

        var rulerName = GetTeamHudRulerName(teamName).Trim();
        label.Text = string.IsNullOrEmpty(rulerName) ? string.Empty : rulerName[..1];
    }

    private string GetTeamHudRulerName(string teamName)
    {
        if (_activeCampaign != null && CampaignRuntimeContext.World is { } world)
        {
            var factionId = BattleTeamIdentity.IsAttacker(teamName)
                ? _activeCampaign.AttackerFactionId
                : _activeCampaign.DefenderFactionId;
            var ruler = world.GetFaction(factionId) is { } faction
                ? world.GetOfficer(faction.RulerOfficerId)
                : null;
            if (ruler != null)
            {
                return _localization.GetOfficerName(ruler);
            }
        }

        // Standalone test battles do not have a campaign faction. Their
        // localized team display name still supplies the intended ruler name.
        return FormatTeamName(teamName);
    }

    private void RefreshTeamHudPortrait(string teamName, string nodePath)
    {
        var portrait = GetNodeOrNull<TextureRect>(nodePath);
        if (portrait == null)
        {
            return;
        }

        var commandingOfficer = _occupantsByGrid.Values
            .SelectMany(occupants => occupants)
            .Where(occupant => occupant.TeamName == teamName &&
                               IsGeneralCountedPiece(occupant.Category, occupant.OfficerName))
            .OrderByDescending(occupant => GetOfficerBattleAttribute(occupant.OfficerName))
            .FirstOrDefault();
        portrait.Texture = commandingOfficer == null ? null : GetOfficerPortraitTexture(commandingOfficer);
        portrait.Visible = portrait.Texture != null;
    }

    private void RefreshCoordinateLabel()
    {
        var coordinateLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/CoordinateLabel");
        if (coordinateLabel != null)
        {
            coordinateLabel.Text = BuildCoordinateText();
        }
    }

    private void RefreshInfoPanel()
    {
        var infoLabel = GetNodeOrNull<Label>("UiLayer/SidePanel/Margin/PanelContent/InfoScroll/InfoPadding/InfoLabel") ??
                        GetNodeOrNull<Label>("UiLayer/SidePanel/Margin/PanelContent/InfoScroll/InfoLabel") ??
                        GetNodeOrNull<Label>("UiLayer/SidePanel/Margin/PanelContent/InfoLabel");
        if (infoLabel == null)
        {
            return;
        }

        infoLabel.Text = BuildInfoText();
    }

    private string BuildCoordinateText()
    {
        var coordinateText = BattleFormat(
            "ui.battle.coordinate_hover_click",
            "Hover: {0}    Click: {1}",
            FormatGrid(_hoverGridKey, _hoverGrid),
            FormatGrid(_selectedGridKey, _selectedGrid));
        if (!TryGetMovePreview(_hoverGridKey, out var energyCost, out var remainingEnergy, out var remainingMoveRange))
        {
            return coordinateText;
        }

        return BattleFormat(
            "ui.battle.coordinate_move_preview",
            "{0}    Move: -{1}, Remaining Energy {2}, Move Range {3}/{4}; {5}",
            coordinateText,
            energyCost,
            remainingEnergy,
            remainingMoveRange,
            _selectedUnit!.MoveRange,
            remainingEnergy >= NormalAttackEnergyCost
                ? BattleText("ui.battle.can_attack_after_move", "can attack")
                : BattleText("ui.battle.cannot_attack_after_move", "cannot attack"));
    }

    private string BuildTeamHudText(BattleHudTeamInfo info)
    {
        var isCaravanEscort = _activeCampaign?.IsCaravanEscortBattle == true;
        var isTransport = isCaravanEscort && !BattleTeamIdentity.IsAttacker(info.Name);
        var sideLabel = isCaravanEscort
            ? BattleTeamIdentity.IsAttacker(info.Name)
                ? BattleText("ui.battle.caravan_ambush", "Ambush")
                : BattleText("ui.battle.caravan_transport", "Transport")
            : BattleTeamIdentity.IsAttacker(info.Name)
                ? BattleText("ui.battle.log_attacker", "Attacker")
                : BattleText("ui.battle.log_defender", "Defender");
        // Caravan gold and food are cargo, not battle upkeep. Display the
        // locked shipment values instead of the generic campaign supply pool,
        // which is intentionally zero for an escort battle.
        var displayedGold = isTransport ? _activeCampaign!.CaravanGold : info.TotalGold;
        var displayedFood = isTransport ? _activeCampaign!.CaravanFood : info.TotalFood;
        return BattleFormat(
            "ui.battle.team_hud_compact",
            "{0} · {1}\nTroops {2:N0}   Wounded {3:N0}\n  Generals {4:N0}    Workers {5:N0}    Siege {6:N0}\n  Gold {7:N0}      Food {8:N0}",
            sideLabel,
            FormatTeamName(info.Name),
            info.TotalTroops,
            info.WoundedTroops,
            info.TotalGenerals,
            GetActiveWorkerCountForTeam(info.Name),
            info.TotalSiegeUnits,
            displayedGold,
            displayedFood);
    }

    private int GetActiveWorkerCountForTeam(string teamName)
    {
        return _occupantsByGrid.Values
            .SelectMany(occupants => occupants)
            .Count(occupant => occupant.Category == CategoryUnit &&
                               occupant.TroopType == TroopWorker &&
                               occupant.TeamName == teamName);
    }

    private int GetTotalWoundedTroopsForTeam(string teamName)
    {
        var total = 0;
        foreach (var occupants in _occupantsByGrid.Values)
        {
            foreach (var occupant in occupants)
            {
                if (occupant.Category == CategoryUnit && occupant.TeamName == teamName)
                {
                    total += occupant.WoundedTroops;
                }
            }
        }

        return total;
    }

    private void ConfigureHud()
    {
        var battleTypeLabel = GetNodeOrNull<Label>("UiLayer/TopBar/TopBorder/TopHeaderRow/BattleTypeLabel");
        var battlePlaceLabel = GetNodeOrNull<Label>("UiLayer/TopBar/TopBorder/TopHeaderRow/BattlePlaceLabel");
        var battleDateLabel = GetNodeOrNull<Label>("UiLayer/TopBar/TopBorder/TopHeaderRow/BattleDateLabel");
        var battleDayLabel = GetNodeOrNull<Label>("UiLayer/TopBar/TopBorder/TopHeaderRow/BattleDayLabel");
        var weatherIconLabel = GetNodeOrNull<Label>("UiLayer/TopBar/TopBorder/TopHeaderRow/WeatherIconLabel");
        var timeSlotLabel = GetNodeOrNull<Label>("UiLayer/TopBar/TopBorder/TopHeaderRow/TimeSlotLabel");
        var climateLabel = GetNodeOrNull<Label>("UiLayer/TopBar/TopBorder/TopHeaderRow/ClimateLabel");
        var windLabel = GetNodeOrNull<Label>("UiLayer/TopBar/TopBorder/TopHeaderRow/WindLabel");
        var summaryLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/SummaryLabel");
        var teamBLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/TeamBLabel");
        var turnLabel = GetNodeOrNull<Label>("UiLayer/TopBar/TurnCard/TurnLabel");
        var actionLabel = GetNodeOrNull<Label>("UiLayer/TopBar/TurnCard/ActionLabel");
        var coordinateLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/CoordinateLabel");

        // Compact card fields supersede the original multi-line team summaries.
        // Keep these legacy labels hidden even if an older scene override enables them.
        summaryLabel?.Hide();
        teamBLabel?.Hide();
        if (battleTypeLabel != null) battleTypeLabel.Text = GetBattleHudWarType();
        if (battlePlaceLabel != null) battlePlaceLabel.Text = GetBattleHudPlace();
        if (battleDateLabel != null) battleDateLabel.Text = FormatBattleHudDate();
        if (battleDayLabel != null) battleDayLabel.Text = FormatBattleHudDay();
        if (weatherIconLabel != null) weatherIconLabel.Text = GetBattleWeatherIcon(GetCurrentBattleWeather());
        if (timeSlotLabel != null) timeSlotLabel.Text = FormatBattleTimeOfDay(GetCurrentBattleTimeOfDay());
        if (climateLabel != null) climateLabel.Text = FormatBattleWeather(GetCurrentBattleWeather());
        if (windLabel != null)
        {
            windLabel.Text = BattleFormat(
                "ui.battle.top_wind",
                "{0} {1}",
                FormatBattleWindDirectionShort(GetCurrentBattleWindDirection()),
                FormatBattleWindPower(GetCurrentBattleWindPower()));
        }

        if (turnLabel != null)
        {
            turnLabel.Text = BattleFormat(
                "ui.battle.turn_label",
                "Round {0}",
                _turnNumber);
        }

        if (actionLabel != null)
        {
            actionLabel.Text = BattleFormat(
                "ui.battle.action_label",
                "{0} Acting",
                FormatTeamName(GetCurrentTurnSideName()));
        }

        if (_timeButton != null)
        {
            _timeButton.Text = BattleFormat("ui.battle.debug_time", "Time: {0}", FormatBattleTimeOfDay(GetCurrentBattleTimeOfDay()));
        }

        if (_weatherButton != null)
        {
            _weatherButton.Text = BattleFormat("ui.battle.debug_weather", "Weather: {0}", FormatBattleWeather(GetCurrentBattleWeather()));
        }

        if (_windButton != null)
        {
            _windButton.Text = BattleFormat("ui.battle.debug_wind", "Wind: {0}", FormatBattleWindDirectionShort(GetCurrentBattleWindDirection()));
        }

        if (_windPowerButton != null)
        {
            _windPowerButton.Text = BattleFormat("ui.battle.debug_wind_power", "Wind power: {0}", FormatBattleWindPower(GetCurrentBattleWindPower()));
        }

        ConfigureEnvironmentForecastTooltips();

        if (_endTurnButton != null)
        {
            _endTurnButton.Text = BattleText("ui.battle.end_turn", "End Turn");
        }

        ConfigureFieldAiTestControls();

        if (_battleOptionButton != null)
        {
            _battleOptionButton.Text = BattleText("ui.option", "Option");
        }

        if (_windowTitleLabel != null)
        {
            RefreshUnitCommandTitle();
        }

        RefreshBattleOptionDialogText();
        RefreshBattleDebugDialogText();

        var attackerInfo = TeamAInfo with { TotalTroops = _teamATotalTroops, WoundedTroops = GetTotalWoundedTroopsForTeam(TeamAInfo.Name), TotalSiegeUnits = _teamASiegeUnits, TotalGenerals = _teamAGenerals, StrategyPlans = _teamAStrategyPlans, TotalGold = _teamAGold, TotalFood = _teamAFood };
        var defenderInfo = TeamBInfo with { TotalTroops = _teamBTotalTroops, WoundedTroops = GetTotalWoundedTroopsForTeam(TeamBInfo.Name), TotalSiegeUnits = _teamBSiegeUnits, TotalGenerals = _teamBGenerals, StrategyPlans = _teamBStrategyPlans, TotalGold = _teamBGold, TotalFood = _teamBFood };
        RefreshTeamHudCards(attackerInfo, defenderInfo);

        RefreshTeamHudIdentity();

        if (coordinateLabel != null)
        {
            coordinateLabel.Text = BuildCoordinateText();
        }

        RefreshHiddenUnitVisibility();
        RefreshInfoPanel();
        RefreshBattleResultState();
    }

    private string FormatScenarioName(string scenarioName)
    {
        return scenarioName switch
        {
            "Field Battle (Luoyang)" => BattleText("ui.battle.scenario.field_luoyang", "Field Battle (Luoyang)"),
            "Field Battle (Hanzhong)" => BattleText("ui.battle.scenario.field_hanzhong", "Field Battle (Hanzhong)"),
            "Field Battle (Ye)" => BattleText("ui.battle.scenario.field_ye", "Field Battle (Ye)"),
            "Field Battle (Jinyang)" => BattleText("ui.battle.scenario.field_jinyang", "Field Battle (Jinyang)"),
            "Field Battle (Xiapi)" => BattleText("ui.battle.scenario.field_xiapi", "Field Battle (Xiapi)"),
            "Field Battle (Jianye)" => BattleText("ui.battle.scenario.field_jianye", "Field Battle (Jianye)"),
            "Field Battle (Xiangyang)" => BattleText("ui.battle.scenario.field_xiangyang", "Field Battle (Xiangyang)"),
            "Field Battle (Jiangling)" => BattleText("ui.battle.scenario.field_jiangling", "Field Battle (Jiangling)"),
            _ => scenarioName
        };
    }

    private string FormatBattleDate()
    {
        return BattleFormat("ui.battle.date", "{0} Apr {1}", _battleDateYear, _battleDateDay, _battleDateMonth);
    }

    private string FormatBattleMonthTimeline()
    {
        var battleDayThisMonth = _activeCampaign != null
            ? _activeCampaign.BattleDaysThisMonth + 1
            : _battleDateDay;
        var monthlyBattleDayLimit = GetMonthlyBattleDayLimit();
        battleDayThisMonth = Math.Clamp(battleDayThisMonth, 1, monthlyBattleDayLimit);
        if (_activeCampaign?.IsCaravanEscortBattle == true)
        {
            return BattleFormat(
                "ui.battle.caravan_escort_timeline",
                "Year {0}, Month {1:00} | Battle Day: {2} | {3} | Turn: {4}",
                _battleDateYear,
                _battleDateMonth,
                battleDayThisMonth,
                FormatBattleTimeOfDay(GetCurrentBattleTimeOfDay()),
                _turnNumber);
        }
        return BattleFormat(
            "ui.battle.month_battle_timeline",
            "Year {0}, Month {1:00} | Battle Day: {2}/{3} | {4} | Turn: {5}",
            _battleDateYear,
            _battleDateMonth,
            battleDayThisMonth,
            monthlyBattleDayLimit,
            FormatBattleTimeOfDay(GetCurrentBattleTimeOfDay()),
            _turnNumber);
    }

    private string GetBattleHudWarType()
    {
        if (_activeCampaign?.IsCaravanEscortBattle == true)
        {
            return BattleText("ui.battle.top_type_caravan", "Escort");
        }

        if (_activeCampaign != null)
        {
            return _activeCampaign.Stage == CampaignStage.FieldBattle
                ? BattleText("ui.battle.top_type_field", "Field Battle")
                : BattleText("ui.battle.top_type_siege", "Siege Battle");
        }

        return ResolveScenarioDefinition().ScenarioType switch
        {
            BattleScenarioType.FieldBattle => BattleText("ui.battle.top_type_field", "Field Battle"),
            BattleScenarioType.MoatSiegeBattle => BattleText("ui.battle.top_type_moat_siege", "Moat Siege"),
            _ => BattleText("ui.battle.top_type_siege", "Siege Battle")
        };
    }

    private string GetBattleHudPlace()
    {
        if (_activeCampaign != null && CampaignRuntimeContext.World?.GetCity(_activeCampaign.TargetCityId) is { } targetCity)
        {
            return _localization.GetCityName(targetCity);
        }

        var scenarioName = FormatScenarioName(ResolveScenarioDefinition().DisplayName);
        var openingBracket = scenarioName.IndexOf('（');
        var closingBracket = scenarioName.IndexOf('）', openingBracket + 1);
        if (openingBracket >= 0 && closingBracket > openingBracket)
        {
            return scenarioName[(openingBracket + 1)..closingBracket];
        }

        openingBracket = scenarioName.IndexOf('(');
        closingBracket = scenarioName.IndexOf(')', openingBracket + 1);
        return openingBracket >= 0 && closingBracket > openingBracket
            ? scenarioName[(openingBracket + 1)..closingBracket]
            : BattleText("ui.battle.top_place_unknown", "Unknown");
    }

    private string FormatBattleHudDate()
    {
        return BattleFormat("ui.battle.top_date", "Year {0}, Month {1:00}", _battleDateYear, _battleDateMonth);
    }

    private string FormatBattleHudDay()
    {
        var battleDay = _activeCampaign != null ? _activeCampaign.BattleDaysThisMonth + 1 : _battleDateDay;
        var monthlyLimit = GetMonthlyBattleDayLimit();
        battleDay = Math.Clamp(battleDay, 1, monthlyLimit);
        return BattleFormat("ui.battle.top_day", "Day {0}/{1}", battleDay, monthlyLimit);
    }

    private static string GetBattleWeatherIcon(BattleWeatherType weather)
    {
        return weather switch
        {
            BattleWeatherType.Cloudy => "☁",
            BattleWeatherType.Rain => "☂",
            _ => "☀"
        };
    }

    // The scene supplies the original lacquer/bronze texture atlas.  Styling is
    // kept here so all live battle labels and buttons retain their existing ids
    // and localization/update flow.
    private void ApplyBattleHudPresentation()
    {
        ApplyBattleTitleFont();

        var topBar = GetNodeOrNull<ColorRect>("UiLayer/TopBar");
        if (topBar != null)
        {
            // The header and three NinePatch panels provide their own artwork.
            // Keeping this full-width parent transparent lets the battlefield
            // remain visible in the intentional gaps between those panels.
            topBar.Color = Colors.Transparent;
            // It must not absorb clicks intended for its header buttons.  The
            // individual buttons below remain STOP controls and own their input.
            topBar.MouseFilter = Control.MouseFilterEnum.Pass;
            topBar.ZIndex = 100;
        }

        var topBorder = GetNodeOrNull<NinePatchRect>("UiLayer/TopBar/TopBorder");
        if (topBorder != null)
        {
            topBorder.MouseFilter = Control.MouseFilterEnum.Pass;
        }

        var topHeaderRow = GetNodeOrNull<HBoxContainer>("UiLayer/TopBar/TopBorder/TopHeaderRow");
        if (topHeaderRow != null)
        {
            topHeaderRow.MouseFilter = Control.MouseFilterEnum.Pass;
        }

        var attackerLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/SummaryLabel");
        var defenderLabel = GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/TeamBLabel");
        foreach (var label in new[]
        {
            GetNodeOrNull<Label>("UiLayer/TopBar/Margin/TopBarContent/CoordinateLabel")
        })
        {
            label?.AddThemeColorOverride("font_color", new Color(0.94f, 0.9f, 0.79f, 1.0f));
            label?.AddThemeColorOverride("font_outline_color", new Color(0.035f, 0.02f, 0.008f, 1.0f));
            label?.AddThemeConstantOverride("outline_size", 2);
        }
        attackerLabel?.AddThemeColorOverride("font_color", new Color(1.0f, 0.82f, 0.68f, 1.0f));
        attackerLabel?.AddThemeColorOverride("font_outline_color", new Color(0.12f, 0.015f, 0.01f, 1.0f));
        attackerLabel?.AddThemeConstantOverride("outline_size", 2);
        defenderLabel?.AddThemeColorOverride("font_color", new Color(0.77f, 0.89f, 1.0f, 1.0f));
        defenderLabel?.AddThemeColorOverride("font_outline_color", new Color(0.005f, 0.025f, 0.09f, 1.0f));
        defenderLabel?.AddThemeConstantOverride("outline_size", 2);

        foreach (var button in new[] { _timeButton, _weatherButton, _windButton, _windPowerButton, _battleOptionButton })
        {
            if (button != null)
            {
                button.MouseFilter = Control.MouseFilterEnum.Stop;
            }
            ApplyBattleHudButtonStyle(button, new Color(0.11f, 0.12f, 0.11f, 0.96f), new Color(0.72f, 0.57f, 0.29f, 1.0f));
        }

        if (_endTurnButton != null)
        {
            _endTurnButton.MouseFilter = Control.MouseFilterEnum.Stop;
        }
        ApplyBattleHudButtonStyle(_endTurnButton, new Color(0.38f, 0.06f, 0.045f, 0.98f), new Color(0.9f, 0.67f, 0.28f, 1.0f));
    }

    private void ApplyBattleTitleFont()
    {
        const string titleFontPath = "res://assets/fonts/LXGWWenKaiTC-Regular.ttf";
        var fontData = Godot.FileAccess.GetFileAsBytes(titleFontPath);
        if (fontData.Length == 0)
        {
            return;
        }

        var titleFont = new FontFile { Data = fontData };
        foreach (var labelPath in new[]
        {
            "UiLayer/TopBar/TopBorder/TopHeaderRow/BattleTypeLabel",
            "UiLayer/TopBar/TopBorder/TopHeaderRow/BattlePlaceLabel",
            "UiLayer/TopBar/AttackerCard/AttackerFlag/AttackerRulerSurnameLabel",
            "UiLayer/TopBar/DefenderCard/DefenderFlag/DefenderRulerSurnameLabel"
        })
        {
            GetNodeOrNull<Label>(labelPath)?.AddThemeFontOverride("font", titleFont);
        }
    }

    private static void ApplyBattleHudButtonStyle(Button? button, Color background, Color border)
    {
        if (button == null)
        {
            return;
        }

        var normal = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.55f),
            ShadowSize = 4
        };
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = background.Lightened(0.16f);
        var pressed = (StyleBoxFlat)normal.Duplicate();
        pressed.BgColor = background.Darkened(0.2f);

        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("disabled", pressed);
        button.AddThemeColorOverride("font_color", new Color(0.96f, 0.88f, 0.67f, 1.0f));
        button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.95f, 0.79f, 1.0f));
        button.AddThemeColorOverride("font_outline_color", new Color(0.03f, 0.02f, 0.01f, 1.0f));
        button.AddThemeConstantOverride("outline_size", 2);
    }
}
