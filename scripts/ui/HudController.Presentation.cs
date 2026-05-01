using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using ThreeKingdom.Map;

namespace ThreeKingdom.UI;

public partial class HudController : CanvasLayer
{
    private void OnLanguageChanged()
    {
        RefreshAllText();
    }

    private void ApplyOfficerListDialogTheme()
    {
        if (_officerListDialog == null)
        {
            return;
        }

        var dialogPanel = new StyleBoxFlat
        {
            BgColor = new Color(0.96f, 0.94f, 0.88f, 1.0f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.56f, 0.45f, 0.29f, 1.0f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusBottomLeft = 10
        };
        _officerListDialog.AddThemeStyleboxOverride("panel", dialogPanel);
        var embeddedBorder = new StyleBoxFlat
        {
            BgColor = new Color(0.92f, 0.88f, 0.8f, 1.0f),
            BorderWidthLeft = 2,
            BorderWidthTop = 26,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.56f, 0.45f, 0.29f, 1.0f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusBottomLeft = 10
        };
        var embeddedBorderUnfocused = (StyleBoxFlat)embeddedBorder.Duplicate();
        embeddedBorderUnfocused.BgColor = new Color(0.88f, 0.84f, 0.77f, 1.0f);
        embeddedBorderUnfocused.BorderColor = new Color(0.54f, 0.45f, 0.33f, 1.0f);
        _officerListDialog.AddThemeStyleboxOverride("embedded_border", embeddedBorder);
        _officerListDialog.AddThemeStyleboxOverride("embedded_unfocused_border", embeddedBorderUnfocused);
        _officerListDialog.AddThemeColorOverride("title_color", new Color(0.27f, 0.2f, 0.12f, 1.0f));
        var titleButtonNormal = new StyleBoxFlat
        {
            BgColor = new Color(0.92f, 0.88f, 0.8f, 1.0f),
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0
        };
        var titleButtonHover = (StyleBoxFlat)titleButtonNormal.Duplicate();
        titleButtonHover.BgColor = new Color(0.95f, 0.91f, 0.83f, 1.0f);
        var titleButtonPressed = (StyleBoxFlat)titleButtonNormal.Duplicate();
        titleButtonPressed.BgColor = new Color(0.84f, 0.77f, 0.64f, 1.0f);
        _officerListDialog.AddThemeStyleboxOverride("close", titleButtonNormal);
        _officerListDialog.AddThemeStyleboxOverride("close_pressed", titleButtonPressed);
        _officerListDialog.AddThemeStyleboxOverride("title_button_normal", titleButtonNormal);
        _officerListDialog.AddThemeStyleboxOverride("title_button_hover", titleButtonHover);
        _officerListDialog.AddThemeStyleboxOverride("title_button_pressed", titleButtonPressed);
        _officerListDialog.AddThemeColorOverride("close_color", new Color(0.34f, 0.24f, 0.14f, 1.0f));
        _officerListDialog.AddThemeColorOverride("close_hover_color", new Color(0.22f, 0.16f, 0.09f, 1.0f));

        if (_officerListHeaderPanel != null)
        {
            var headerPanel = new StyleBoxFlat
            {
                BgColor = new Color(0.9f, 0.84f, 0.71f, 0.98f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.62f, 0.49f, 0.29f, 1.0f),
                CornerRadiusTopLeft = 7,
                CornerRadiusTopRight = 7,
                CornerRadiusBottomRight = 7,
                CornerRadiusBottomLeft = 7,
                ContentMarginLeft = 10,
                ContentMarginTop = 6,
                ContentMarginRight = 10,
                ContentMarginBottom = 6
            };
            _officerListHeaderPanel.AddThemeStyleboxOverride("panel", headerPanel);
        }

        if (_officerListTitlebarFill != null)
        {
            var titlebarFillPanel = new StyleBoxFlat
            {
                BgColor = new Color(0.92f, 0.88f, 0.8f, 1.0f),
                BorderWidthLeft = 0,
                BorderWidthTop = 0,
                BorderWidthRight = 0,
                BorderWidthBottom = 0,
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10
            };
            _officerListTitlebarFill.AddThemeStyleboxOverride("panel", titlebarFillPanel);
        }

        if (_officerListHeaderLabel != null)
        {
            _officerListHeaderLabel.AddThemeColorOverride("font_color", new Color(0.25f, 0.18f, 0.1f, 1.0f));
        }

        if (_officerListCloseButton != null)
        {
            var closeNormal = new StyleBoxFlat
            {
                BgColor = new Color(0.86f, 0.78f, 0.62f, 1.0f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.54f, 0.42f, 0.25f, 1.0f),
                CornerRadiusTopLeft = 5,
                CornerRadiusTopRight = 5,
                CornerRadiusBottomRight = 5,
                CornerRadiusBottomLeft = 5
            };
            var closeHover = (StyleBoxFlat)closeNormal.Duplicate();
            closeHover.BgColor = new Color(0.94f, 0.84f, 0.66f, 1.0f);
            var closePressed = (StyleBoxFlat)closeNormal.Duplicate();
            closePressed.BgColor = new Color(0.73f, 0.61f, 0.42f, 1.0f);

            _officerListCloseButton.AddThemeStyleboxOverride("normal", closeNormal);
            _officerListCloseButton.AddThemeStyleboxOverride("hover", closeHover);
            _officerListCloseButton.AddThemeStyleboxOverride("pressed", closePressed);
            _officerListCloseButton.AddThemeColorOverride("font_color", new Color(0.22f, 0.15f, 0.08f, 1.0f));
        }

        var okNormal = new StyleBoxFlat
        {
            BgColor = new Color(0.86f, 0.78f, 0.6f, 1.0f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.48f, 0.36f, 0.2f, 1.0f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusBottomLeft = 6
        };
        var okHover = (StyleBoxFlat)okNormal.Duplicate();
        okHover.BgColor = new Color(0.91f, 0.84f, 0.67f, 1.0f);
        var okPressed = (StyleBoxFlat)okNormal.Duplicate();
        okPressed.BgColor = new Color(0.76f, 0.65f, 0.46f, 1.0f);

        _officerListDialog.GetOkButton().Visible = false;
        if (_officerListConfirmButton != null)
        {
            _officerListConfirmButton.AddThemeStyleboxOverride("normal", okNormal);
            _officerListConfirmButton.AddThemeStyleboxOverride("hover", okHover);
            _officerListConfirmButton.AddThemeStyleboxOverride("pressed", okPressed);
            _officerListConfirmButton.AddThemeColorOverride("font_color", new Color(0.14f, 0.1f, 0.06f, 1.0f));
        }

        foreach (var button in new[] { _viewCityOfficersDialogButton, _viewFactionOfficersDialogButton, _viewCitiesDialogButton })
        {
            if (button == null)
            {
                continue;
            }

            var normal = new StyleBoxFlat
            {
                BgColor = new Color(0.88f, 0.81f, 0.65f, 0.97f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.54f, 0.42f, 0.24f, 1.0f),
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomRight = 6,
                CornerRadiusBottomLeft = 6
            };
            var hover = (StyleBoxFlat)normal.Duplicate();
            hover.BgColor = new Color(0.93f, 0.87f, 0.72f, 1.0f);
            var disabled = (StyleBoxFlat)normal.Duplicate();
            disabled.BgColor = new Color(0.76f, 0.72f, 0.65f, 0.92f);
            disabled.BorderColor = new Color(0.58f, 0.54f, 0.47f, 1.0f);

            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("disabled", disabled);
            button.AddThemeColorOverride("font_color", new Color(0.16f, 0.12f, 0.08f, 1.0f));
            button.AddThemeColorOverride("font_disabled_color", new Color(0.32f, 0.29f, 0.24f, 1.0f));
        }

        if (_officerListTable != null)
        {
            var tablePanel = new StyleBoxFlat
            {
                BgColor = new Color(0.96f, 0.93f, 0.86f, 0.98f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.47f, 0.39f, 0.27f, 0.95f)
            };
            var focusPanel = (StyleBoxFlat)tablePanel.Duplicate();
            focusPanel.BorderColor = new Color(0.65f, 0.49f, 0.25f, 1.0f);
            var selectedPanel = new StyleBoxFlat
            {
                BgColor = new Color(0.82f, 0.72f, 0.52f, 1.0f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.55f, 0.4f, 0.2f, 1.0f)
            };
            var selectedFocusPanel = (StyleBoxFlat)selectedPanel.Duplicate();
            selectedFocusPanel.BgColor = new Color(0.86f, 0.76f, 0.56f, 1.0f);

            var titleNormal = new StyleBoxFlat
            {
                BgColor = new Color(0.67f, 0.53f, 0.31f, 1.0f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.33f, 0.24f, 0.13f, 1.0f)
            };
            var titleHover = (StyleBoxFlat)titleNormal.Duplicate();
            titleHover.BgColor = new Color(0.75f, 0.6f, 0.37f, 1.0f);
            var titlePressed = (StyleBoxFlat)titleNormal.Duplicate();
            titlePressed.BgColor = new Color(0.56f, 0.43f, 0.24f, 1.0f);

            _officerListTable.AddThemeStyleboxOverride("panel", tablePanel);
            _officerListTable.AddThemeStyleboxOverride("focus", focusPanel);
            _officerListTable.AddThemeStyleboxOverride("selected", selectedPanel);
            _officerListTable.AddThemeStyleboxOverride("selected_focus", selectedFocusPanel);
            _officerListTable.AddThemeStyleboxOverride("title_button_normal", titleNormal);
            _officerListTable.AddThemeStyleboxOverride("title_button_hover", titleHover);
            _officerListTable.AddThemeStyleboxOverride("title_button_pressed", titlePressed);
            _officerListTable.AddThemeColorOverride("font_color", new Color(0.17f, 0.13f, 0.09f, 1.0f));
            _officerListTable.AddThemeColorOverride("font_hovered_color", new Color(0.12f, 0.09f, 0.06f, 1.0f));
            _officerListTable.AddThemeColorOverride("font_selected_color", new Color(0.12f, 0.09f, 0.06f, 1.0f));
            _officerListTable.AddThemeColorOverride("font_hovered_selected_color", new Color(0.1f, 0.07f, 0.04f, 1.0f));
            _officerListTable.AddThemeColorOverride("font_outline_color", new Color(0.96f, 0.93f, 0.86f, 0.0f));
            _officerListTable.AddThemeColorOverride("custom_button_font_highlight", new Color(0.12f, 0.09f, 0.06f, 1.0f));
            _officerListTable.AddThemeColorOverride("custom_button_font_highlight_pressed", new Color(0.1f, 0.07f, 0.04f, 1.0f));
            _officerListTable.AddThemeColorOverride("title_button_color", new Color(0.98f, 0.95f, 0.9f, 1.0f));
            _officerListTable.AddThemeColorOverride("title_button_hover_color", Colors.White);
            _officerListTable.AddThemeColorOverride("title_button_pressed_color", Colors.White);
            _officerListTable.AddThemeColorOverride("guide_color", new Color(0.58f, 0.5f, 0.38f, 0.65f));
            _officerListTable.AddThemeColorOverride("drop_position_color", new Color(0.75f, 0.55f, 0.22f, 1.0f));
        }
    }

    private void RefreshAllText()
    {
        if (_localization == null)
        {
            return;
        }

        RefreshMonth();
        RefreshPlayerFaction();
        RefreshStoryName();

        if (_commandsTitle != null)
        {
            _commandsTitle.Text = _localization.T("ui.commands");
        }

        if (_cityOfficerListTitle != null)
        {
            _cityOfficerListTitle.Text = _localization.T("ui.officer_list");
        }

        if (_endTurnButton != null)
        {
            _endTurnButton.Text = _localization.T("ui.end_turn");
        }

        if (_developButton != null)
        {
            _developButton.Text = _localization.T("ui.internal_affairs");
        }

        if (_recruitButton != null)
        {
            _recruitButton.Text = _localization.T("ui.military");
        }

        if (_moveButton != null)
        {
            _moveButton.Text = _localization.T("ui.move");
            _moveButton.Visible = false;
        }

        if (_searchButton != null)
        {
            _searchButton.Text = _localization.T("ui.search");
        }

        if (_merchantButton != null)
        {
            _merchantButton.Text = _localization.T("ui.merchant");
        }

        if (_personnelButton != null)
        {
            _personnelButton.Text = _localization.T("ui.personnel");
        }

        if (_civilButton != null)
        {
            _civilButton.Text = _localization.T("ui.civil");
        }

        if (_attackButton != null)
        {
            _attackButton.Text = _localization.T("ui.attack");
            _attackButton.Visible = false;
        }

        if (_viewButton != null)
        {
            _viewButton.Text = _localization.T("ui.view");
        }

        if (_officerListDialog != null)
        {
            _officerListDialog.OkButtonText = _localization.T("ui.confirm_officer_selection");
        }

        if (_officerListConfirmButton != null)
        {
            _officerListConfirmButton.Text = _localization.T("ui.confirm_officer_selection");
        }

        UpdateOfficerListToolbar();
        UpdateOfficerListDialogTitle();

        UpdateMerchantDialogText();
        UpdateMilitaryDialogText();
        UpdatePersonnelDialogText();
        UpdatePersonnelBonusDialogText();
        UpdateAssignRoleDialogText();
        UpdateHireOfficerDialogText();
        UpdateCivilDialogText();
        UpdateCivilReliefDialogText();
        UpdateInternalAffairsDialogText();
        UpdateMoveDialogText();
        UpdateAttackDialogText();

        if (_officerDetailDialog != null)
        {
            _officerDetailDialog.Title = _localization.T("ui.officer_detail");
        }

        if (_officerPortraitPlaceholderLabel != null && (_officerDetailDialog == null || !_officerDetailDialog.Visible))
        {
            _officerPortraitPlaceholderLabel.Visible = true;
            _officerPortraitPlaceholderLabel.Text = _localization.T("ui.portrait_pending_asset");
        }

        if (_languageButton != null)
        {
            _languageButton.Text = _localization.IsTraditionalChinese
                ? _localization.T("ui.lang_btn_en")
                : _localization.T("ui.lang_btn_zh");
        }

        RefreshSelectedCity();
    }

    private void RefreshPlayerFaction()
    {
        if (_playerFactionLabel == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        var factionName = _localization.GetFactionName(_turnManager.World, playerFactionId);
        _playerFactionLabel.Text = _localization.FormatPlayerFaction(factionName);
    }

    private void RefreshStoryName()
    {
        if (_storyLabel == null || _turnManager?.World == null || _localization == null)
        {
            return;
        }

        var world = _turnManager.World;
        _storyLabel.Text = _localization.IsTraditionalChinese
            ? (!string.IsNullOrWhiteSpace(world.StoryNameZhHant) ? world.StoryNameZhHant : world.StoryNameEn)
            : (!string.IsNullOrWhiteSpace(world.StoryNameEn) ? world.StoryNameEn : world.StoryNameZhHant);
    }

    private void RefreshSelectedCity()
    {
        if (_localization == null || _turnManager?.World == null)
        {
            return;
        }

        if (_selectedCity == null)
        {
            if (_cityNameLabel != null)
            {
                _cityNameLabel.Text = _localization.FormatCityHeader("-");
            }

            if (_cityStatsLabel != null)
            {
                _cityStatsLabel.Text =
                    _localization.FormatOwnerLine("-") +
                    "\n" +
                    _localization.FormatEmptyCityStats();
            }

            if (_cityOfficerListText != null)
            {
                _cityOfficerListText.Text = _localization.T("ui.none");
            }

            UpdateGameplayButtonStates();
            return;
        }

        if (_cityNameLabel != null)
        {
            _cityNameLabel.Text = _localization.FormatCityHeader(_localization.GetCityName(_selectedCity));
        }

        if (_cityStatsLabel != null)
        {
            var ownerName = _localization.GetFactionName(_turnManager.World, _selectedCity.OwnerFactionId);
            var freeOfficerCount = _turnManager.World.Officers.Count(officer =>
                officer.CityId == _selectedCity.Id &&
                FreeOfficerMovement.IsVisibleFreeOfficer(_turnManager.World, officer));
            _cityStatsLabel.Text =
                _localization.FormatOwnerLine(ownerName) +
                "\n" +
                _localization.FormatCityStats(_selectedCity, freeOfficerCount);
        }

        if (_cityOfficerListText != null)
        {
            _cityOfficerListText.Text = BuildOfficerListText(_selectedCity);
        }

        UpdateGameplayButtonStates();
    }

    private void EnsureOfficerListWidgets(VBoxContainer leftPanel)
    {
        _cityOfficerListTitle = GetNodeOrNull<Label>("Root/LeftPanel/OfficerListTitle");
        if (_cityOfficerListTitle == null)
        {
            _cityOfficerListTitle = new Label
            {
                Name = "OfficerListTitle"
            };
            leftPanel.AddChild(_cityOfficerListTitle);
        }

        _cityOfficerListText = GetNodeOrNull<RichTextLabel>("Root/LeftPanel/OfficerListText");
        if (_cityOfficerListText == null)
        {
            _cityOfficerListText = new RichTextLabel
            {
                Name = "OfficerListText",
                FitContent = true,
                ScrollActive = true,
                CustomMinimumSize = new Vector2(0.0f, 180.0f)
            };
            leftPanel.AddChild(_cityOfficerListText);
        }
    }

    private string BuildOfficerListText(CityData city)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return string.Empty;
        }

        if (city.OfficerIds.Count == 0)
        {
            return _localization.T("ui.none");
        }

        var officerLines = new List<string>();
        foreach (var officerId in city.OfficerIds)
        {
            var officer = _turnManager.World.GetOfficer(officerId);
            if (officer == null)
            {
                continue;
            }

            var roleName = _localization.GetOfficerRole(officer);
            officerLines.Add(
                $"{_localization.GetOfficerName(officer)} | {roleName} | {BuildOfficerStatusText(officer)} | {_localization.T("ui.strength")} {officer.Strength} | {_localization.T("ui.intelligence")} {officer.Intelligence} | {_localization.T("ui.charm")} {officer.Charm}");
        }

        return officerLines.Count == 0 ? _localization.T("ui.none") : string.Join("\n", officerLines);
    }

    private string BuildOfficerDetailText(OfficerData officer)
    {
        var officerName = _localization?.GetOfficerName(officer) ?? officer.Name;
        var roleName = _localization?.GetOfficerRole(officer) ?? officer.Role;
        var currentYear = _turnManager?.World?.Year ?? 0;
        var officerAge = CalculateOfficerAge(officer, currentYear);
        var itemSummary = BuildOfficerItemSummary(officer);
        return
            $"{officerName}\n" +
            $"{_localization?.T("ui.role") ?? "Role"}: {roleName}\n" +
            $"{BuildOfficerStatusText(officer)}\n" +
            $"{_localization?.T("ui.age") ?? "Age"}: {officerAge}\n" +
            $"{_localization?.T("ui.strength") ?? "STR"}: {officer.Strength}\n" +
            $"{_localization?.T("ui.intelligence") ?? "INT"}: {officer.Intelligence}\n" +
            $"{_localization?.T("ui.charm") ?? "CHA"}: {officer.Charm}\n" +
            $"{_localization?.T("ui.leadership") ?? "LEA"}: {officer.Leadership}\n" +
            $"{_localization?.T("ui.politics") ?? "POL"}: {officer.Politics}\n" +
            $"{_localization?.T("ui.combat") ?? "COM"}: {officer.Combat}\n" +
            $"{_localization?.T("ui.loyalty_short") ?? "LOY"}: {officer.Loyalty}\n" +
            $"{_localization?.T("ui.ambition") ?? "AMB"}: {officer.Ambition}\n" +
            $"{itemSummary}";
    }

    private static int CalculateOfficerAge(OfficerData officer, int currentYear)
    {
        if (officer.BirthYear <= 0 || currentYear <= 0)
        {
            return 0;
        }

        return Math.Max(0, currentYear - officer.BirthYear);
    }

    private static bool IsOfficerOldEnoughToJoin(WorldState world, OfficerData officer)
    {
        return officer.BirthYear <= 0 || world.Year - officer.BirthYear >= 18;
    }

    private void EnsureOfficerDetailWidgets()
    {
        if (_officerDetailDialog == null)
        {
            return;
        }

        var existingRoot = _officerDetailDialog.GetNodeOrNull<HBoxContainer>("OfficerDetailRoot");
        if (existingRoot != null)
        {
            _officerPortraitRect = existingRoot.GetNodeOrNull<TextureRect>("PortraitPanel/PortraitRect");
            _officerPortraitPlaceholderLabel = existingRoot.GetNodeOrNull<Label>("PortraitPanel/PortraitPlaceholder");
            _officerDetailText = existingRoot.GetNodeOrNull<RichTextLabel>("DetailText");
            return;
        }

        var root = new HBoxContainer
        {
            Name = "OfficerDetailRoot",
            CustomMinimumSize = new Vector2(460.0f, 240.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 16);
        _officerDetailDialog.AddChild(root);

        var portraitPanel = new PanelContainer
        {
            Name = "PortraitPanel",
            CustomMinimumSize = new Vector2(160.0f, 220.0f)
        };
        root.AddChild(portraitPanel);

        var portraitCenter = new CenterContainer();
        portraitPanel.AddChild(portraitCenter);

        var portraitStack = new VBoxContainer();
        portraitStack.Alignment = BoxContainer.AlignmentMode.Center;
        portraitStack.AddThemeConstantOverride("separation", 8);
        portraitCenter.AddChild(portraitStack);

        _officerPortraitRect = new TextureRect
        {
            Name = "PortraitRect",
            CustomMinimumSize = new Vector2(128.0f, 160.0f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Modulate = new Color(0.7f, 0.7f, 0.75f, 1.0f)
        };
        portraitStack.AddChild(_officerPortraitRect);

        _officerPortraitPlaceholderLabel = new Label
        {
            Name = "PortraitPlaceholder",
            Text = _localization?.T("ui.portrait_pending_asset") ?? "Portrait\nPending Asset",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        portraitStack.AddChild(_officerPortraitPlaceholderLabel);

        _officerDetailText = new RichTextLabel
        {
            Name = "DetailText",
            FitContent = true,
            ScrollActive = true,
            CustomMinimumSize = new Vector2(260.0f, 220.0f),
            BbcodeEnabled = false
        };
        root.AddChild(_officerDetailText);
    }

    private string BuildOfficerListRowText(OfficerData officer, bool includeCityName = false)
    {
        var officerName = _localization?.GetOfficerName(officer) ?? officer.Name;
        var roleName = _localization?.GetOfficerRole(officer) ?? officer.Role;
        var cityText = string.Empty;
        if (includeCityName && _turnManager?.World != null && _localization != null)
        {
            var city = _turnManager.World.GetCity(officer.CityId);
            if (city != null)
            {
                cityText = $" | {_localization.T("ui.city")} {_localization.GetCityName(city)}";
            }
        }

        return $"{officerName} | {roleName} | {BuildOfficerStatusText(officer)}{cityText} | {_localization?.T("ui.strength") ?? "STR"} {officer.Strength} | {_localization?.T("ui.intelligence") ?? "INT"} {officer.Intelligence}";
    }

    private string BuildOfficerItemSummary(OfficerData officer)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return $"{_localization?.T("ui.officer_items") ?? "Equipped Items"}: -";
        }

        var items = _turnManager.World.Items
            .Where(item => item.EquippedOfficerId == officer.Id)
            .Select(item => _localization.GetItemName(item))
            .ToList();
        var names = items.Count == 0 ? "-" : string.Join(", ", items);
        return _localization.Format("fmt.officer_items", _localization.T("ui.officer_items"), names);
    }

    private string BuildCityListRowText(CityData city)
    {
        var cityName = _localization?.GetCityName(city) ?? city.NameEn;
        var ownerName = _turnManager?.World != null && _localization != null
            ? _localization.GetFactionName(_turnManager.World, city.OwnerFactionId)
            : city.OwnerFactionId.ToString();
        return $"{cityName} | {_localization?.T("ui.owner") ?? "Owner"} {ownerName} | {_localization?.T("ui.gold") ?? "Gold"} {city.Gold} | {_localization?.T("ui.food") ?? "Food"} {city.Food} | {_localization?.T("ui.troops") ?? "Troops"} {city.Troops} | {_localization?.T("ui.officers") ?? "Officers"} {city.OfficerIds.Count}";
    }

    private string BuildOfficerStatusText(OfficerData officer)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return "Status: Idle";
        }

        return $"{_localization.T("ui.status")}: {_localization.GetOfficerStatus(_turnManager.World, officer)}";
    }

    private void LoadPortraitData()
    {
        _portraitRegions.Clear();
        _portraitSheetTexture = ResourceLoader.Load<Texture2D>(PortraitSheetPath);
        if (_portraitSheetTexture == null || !FileAccess.FileExists(PortraitMappingPath))
        {
            return;
        }

        using var file = FileAccess.Open(PortraitMappingPath, FileAccess.ModeFlags.Read);
        var rawText = file.GetAsText();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return;
        }

        var matches = Regex.Matches(
            rawText,
            "\\{\\s*\"id\"\\s*:\\s*(\\d+)\\s*,.*?\"filename\"\\s*:\\s*\"([^\"]+)\"\\s*,.*?\"row\"\\s*:\\s*(\\d+)\\s*,.*?\"col\"\\s*:\\s*(\\d+)",
            RegexOptions.Singleline);

        if (matches.Count == 0)
        {
            return;
        }

        var entries = new List<(int Id, int Row, int Col)>();
        var maxRow = 0;
        var maxCol = 0;
        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            var id = int.Parse(match.Groups[1].Value);
            var row = int.Parse(match.Groups[3].Value);
            var col = int.Parse(match.Groups[4].Value);
            entries.Add((id, row, col));
            if (row > maxRow)
            {
                maxRow = row;
            }

            if (col > maxCol)
            {
                maxCol = col;
            }
        }

        if (entries.Count == 0 || maxRow <= 0 || maxCol <= 0)
        {
            return;
        }

        var tileWidth = _portraitSheetTexture.GetWidth() / (float)maxCol;
        var tileHeight = _portraitSheetTexture.GetHeight() / (float)maxRow;
        foreach (var entry in entries)
        {
            var region = new Rect2(
                (entry.Col - 1) * tileWidth,
                (entry.Row - 1) * tileHeight,
                tileWidth,
                tileHeight);
            _portraitRegions[entry.Id] = region;
        }
    }

    private Texture2D? BuildOfficerPortraitTexture(int officerId)
    {
        if (_portraitSheetTexture == null || !_portraitRegions.TryGetValue(officerId, out var region))
        {
            return null;
        }

        var atlasTexture = new AtlasTexture
        {
            Atlas = _portraitSheetTexture,
            Region = region
        };
        return atlasTexture;
    }

}
