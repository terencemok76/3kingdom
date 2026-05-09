using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private enum SaveLoadConfirmAction
    {
        None,
        Save,
        Load
    }

    private const int SaveSlotCount = 10;
    private const string OptionSettingsPath = "user://settings/options.json";

    private sealed class OptionSettingsData
    {
        public bool BgmEnabled { get; set; } = true;
        public bool SfxEnabled { get; set; } = true;
        public float BgmVolume { get; set; } = 1.0f;
        public float SfxVolume { get; set; } = 1.0f;
    }

    private void EnsureOptionButton()
    {
        var commandButtons = GetNodeOrNull<GridContainer>("Root/LeftPanel/CommandButtons");
        if (commandButtons == null)
        {
            return;
        }

        _optionButton = commandButtons.GetNodeOrNull<Button>("OptionButton");
        if (_optionButton != null)
        {
            return;
        }

        _optionButton = new Button
        {
            Name = "OptionButton",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None
        };

        if (_viewButton != null)
        {
            CopyButtonTheme(_viewButton, _optionButton);
        }

        commandButtons.AddChild(_optionButton);
    }

    private void EnsureOptionDialogWidgets()
    {
        if (_optionDialog == null)
        {
            return;
        }

        _optionDialog.Title = GetOptionDialogTitle();
        var existingRoot = _optionDialog.GetNodeOrNull<VBoxContainer>("OptionDialogRoot");
        if (existingRoot != null)
        {
            _optionSaveLoadButton = existingRoot.GetNodeOrNull<Button>("SaveLoadButton");
            _optionBgmToggleButton = existingRoot.GetNodeOrNull<Button>("BgmToggleButton");
            _optionSfxToggleButton = existingRoot.GetNodeOrNull<Button>("SfxToggleButton");
            _optionBgmVolumeSlider = existingRoot.GetNodeOrNull<HSlider>("BgmVolumeRow/BgmVolumeSlider");
            _optionBgmVolumeValueLabel = existingRoot.GetNodeOrNull<Label>("BgmVolumeRow/BgmVolumeValueLabel");
            _optionSfxVolumeSlider = existingRoot.GetNodeOrNull<HSlider>("SfxVolumeRow/SfxVolumeSlider");
            _optionSfxVolumeValueLabel = existingRoot.GetNodeOrNull<Label>("SfxVolumeRow/SfxVolumeValueLabel");
            _optionSaveSettingsButton = existingRoot.GetNodeOrNull<Button>("SaveSettingsButton");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "OptionDialogRoot",
            CustomMinimumSize = new Vector2(420.0f, 320.0f)
        };
        root.AddThemeConstantOverride("separation", 10);
        _optionDialog.AddChild(root);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        root.AddChild(margin);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        margin.AddChild(content);

        _optionSaveLoadButton = CreateOptionActionButton("SaveLoadButton", OnOptionSaveLoadPressed);
        _optionBgmToggleButton = CreateOptionActionButton("BgmToggleButton", OnOptionBgmTogglePressed);
        _optionSfxToggleButton = CreateOptionActionButton("SfxToggleButton", OnOptionSfxTogglePressed);
        content.AddChild(_optionSaveLoadButton);
        content.AddChild(_optionBgmToggleButton);
        content.AddChild(CreateVolumeRow(
            "BgmVolumeRow",
            "BgmVolumeSlider",
            "BgmVolumeValueLabel",
            OnOptionBgmVolumeChanged,
            out _optionBgmVolumeSlider,
            out _optionBgmVolumeValueLabel));
        content.AddChild(_optionSfxToggleButton);
        content.AddChild(CreateVolumeRow(
            "SfxVolumeRow",
            "SfxVolumeSlider",
            "SfxVolumeValueLabel",
            OnOptionSfxVolumeChanged,
            out _optionSfxVolumeSlider,
            out _optionSfxVolumeValueLabel));
        _optionSaveSettingsButton = CreateOptionActionButton("SaveSettingsButton", OnOptionSaveSettingsPressed);
        content.AddChild(_optionSaveSettingsButton);
    }

    private static HBoxContainer CreateVolumeRow(
        string rowName,
        string sliderName,
        string valueLabelName,
        Action<double> valueChangedHandler,
        out HSlider? slider,
        out Label? valueLabel)
    {
        var row = new HBoxContainer
        {
            Name = rowName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 8);

        slider = new HSlider
        {
            Name = sliderName,
            MinValue = 0.0,
            MaxValue = 100.0,
            Step = 1.0,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        slider.ValueChanged += value => valueChangedHandler(value);
        row.AddChild(slider);

        valueLabel = new Label
        {
            Name = valueLabelName,
            CustomMinimumSize = new Vector2(52.0f, 0.0f),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        row.AddChild(valueLabel);

        return row;
    }

    private void EnsureSaveLoadDialogWidgets()
    {
        if (_saveLoadDialog == null)
        {
            return;
        }

        _saveLoadDialog.Title = GetSaveLoadDialogTitle();
        var existingRoot = _saveLoadDialog.GetNodeOrNull<VBoxContainer>("SaveLoadDialogRoot");
        if (existingRoot != null)
        {
            _saveSlotList = existingRoot.GetNodeOrNull<ItemList>("SlotList");
            _saveDescriptionLineEdit = existingRoot.GetNodeOrNull<LineEdit>("DescriptionLineEdit");
            _saveSlotSummaryLabel = existingRoot.GetNodeOrNull<RichTextLabel>("SummaryLabel");
            _saveSlotSaveButton = existingRoot.GetNodeOrNull<Button>("SaveSlotButton");
            _saveSlotLoadButton = existingRoot.GetNodeOrNull<Button>("LoadSlotButton");
            _saveSlotCloseButton = existingRoot.GetNodeOrNull<Button>("CloseSlotButton");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "SaveLoadDialogRoot",
            CustomMinimumSize = new Vector2(760.0f, 540.0f)
        };
        root.AddThemeConstantOverride("separation", 10);
        _saveLoadDialog.AddChild(root);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        root.AddChild(margin);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 10);
        margin.AddChild(content);

        var slotLabel = new Label
        {
            Text = GetSaveSlotListLabel()
        };
        content.AddChild(slotLabel);

        _saveSlotList = new ItemList
        {
            Name = "SlotList",
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0.0f, 220.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _saveSlotList.ItemSelected += OnSaveSlotSelected;
        content.AddChild(_saveSlotList);

        var descriptionLabel = new Label
        {
            Text = GetSaveDescriptionLabel()
        };
        content.AddChild(descriptionLabel);

        _saveDescriptionLineEdit = new LineEdit
        {
            Name = "DescriptionLineEdit",
            PlaceholderText = GetSaveDescriptionPlaceholder()
        };
        content.AddChild(_saveDescriptionLineEdit);

        var summaryTitle = new Label
        {
            Text = GetSaveSummaryLabel()
        };
        content.AddChild(summaryTitle);

        _saveSlotSummaryLabel = new RichTextLabel
        {
            Name = "SummaryLabel",
            FitContent = true,
            ScrollActive = true,
            CustomMinimumSize = new Vector2(0.0f, 140.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        content.AddChild(_saveSlotSummaryLabel);

        var buttonRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        buttonRow.AddThemeConstantOverride("separation", 10);
        content.AddChild(buttonRow);

        _saveSlotSaveButton = CreateOptionActionButton("SaveSlotButton", OnSaveSlotSavePressed);
        _saveSlotLoadButton = CreateOptionActionButton("LoadSlotButton", OnSaveSlotLoadPressed);
        _saveSlotCloseButton = CreateOptionActionButton("CloseSlotButton", OnSaveSlotClosePressed);
        _saveSlotSaveButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _saveSlotLoadButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _saveSlotCloseButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        buttonRow.AddChild(_saveSlotSaveButton);
        buttonRow.AddChild(_saveSlotLoadButton);
        buttonRow.AddChild(_saveSlotCloseButton);
    }

    private void EnsureSaveLoadConfirmDialogWidgets()
    {
        if (_saveLoadConfirmDialog == null)
        {
            return;
        }

        _saveLoadConfirmDialog.Title = GetSaveLoadConfirmTitle();
        var existingRoot = _saveLoadConfirmDialog.GetNodeOrNull<VBoxContainer>("ConfirmRoot");
        if (existingRoot != null)
        {
            _saveLoadConfirmLabel = existingRoot.GetNodeOrNull<Label>("ConfirmLabel");
            _saveLoadConfirmYesButton = existingRoot.GetNodeOrNull<Button>("ConfirmYesButton");
            _saveLoadConfirmNoButton = existingRoot.GetNodeOrNull<Button>("ConfirmNoButton");
            return;
        }

        var root = new VBoxContainer
        {
            Name = "ConfirmRoot",
            CustomMinimumSize = new Vector2(320.0f, 130.0f)
        };
        root.AddThemeConstantOverride("separation", 12);
        _saveLoadConfirmDialog.AddChild(root);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        root.AddChild(margin);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 12);
        margin.AddChild(content);

        _saveLoadConfirmLabel = new Label
        {
            Name = "ConfirmLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(320.0f, 70.0f)
        };
        content.AddChild(_saveLoadConfirmLabel);

        var buttonRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        buttonRow.AddThemeConstantOverride("separation", 10);
        content.AddChild(buttonRow);

        _saveLoadConfirmYesButton = CreateOptionActionButton("ConfirmYesButton", OnSaveLoadConfirmDialogConfirmed);
        _saveLoadConfirmNoButton = CreateOptionActionButton("ConfirmNoButton", OnSaveLoadConfirmNoPressed);
        _saveLoadConfirmYesButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _saveLoadConfirmNoButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        buttonRow.AddChild(_saveLoadConfirmYesButton);
        buttonRow.AddChild(_saveLoadConfirmNoButton);
    }

    private Button CreateOptionActionButton(string name, Action pressedHandler)
    {
        var button = new Button
        {
            Name = name,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0.0f, 34.0f)
        };
        if (_viewButton != null)
        {
            CopyButtonTheme(_viewButton, button);
        }

        button.Pressed += pressedHandler;
        return button;
    }

    private static void CopyButtonTheme(Button source, Button target)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
        {
            var style = source.GetThemeStylebox(state);
            if (style != null)
            {
                target.AddThemeStyleboxOverride(state, style);
            }
        }

        foreach (var colorName in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_disabled_color" })
        {
            target.AddThemeColorOverride(colorName, source.GetThemeColor(colorName));
        }
    }

    private void OnOptionPressed()
    {
        RefreshOptionDialogText();
        _optionDialog?.PopupCentered(new Vector2I(460, 360));
    }

    private void OnOptionSaveLoadPressed()
    {
        PopulateSaveSlotList();
        RefreshSaveLoadDialogText();
        _saveLoadDialog?.PopupCentered(new Vector2I(780, 580));
    }

    private void OnOptionBgmTogglePressed()
    {
        _bgmEnabled = !_bgmEnabled;
        ApplyAudioSettings();
        RefreshOptionDialogText();
    }

    private void OnOptionSfxTogglePressed()
    {
        _sfxEnabled = !_sfxEnabled;
        ApplyAudioSettings();
        RefreshOptionDialogText();
    }

    private void OnOptionBgmVolumeChanged(double value)
    {
        _bgmVolume = (float)(value / 100.0);
        ApplyAudioSettings();
        RefreshOptionDialogText();
    }

    private void OnOptionSfxVolumeChanged(double value)
    {
        _sfxVolume = (float)(value / 100.0);
        ApplyAudioSettings();
        RefreshOptionDialogText();
    }

    private void OnOptionSaveSettingsPressed()
    {
        SaveOptionSettings();
        AddLog(GetOptionSettingsSavedMessage(), isPlayerRelated: true);
    }

    private void OnSaveSlotSelected(long index)
    {
        _selectedSaveSlotIndex = (int)index;
        RefreshSelectedSaveSlotSummary();
    }

    private void OnSaveSlotSavePressed()
    {
        ShowSaveLoadConfirmDialog(SaveLoadConfirmAction.Save);
    }

    private void OnSaveSlotLoadPressed()
    {
        ShowSaveLoadConfirmDialog(SaveLoadConfirmAction.Load);
    }

    private void OnSaveLoadConfirmDialogConfirmed()
    {
        _saveLoadConfirmDialog?.Hide();

        switch (_pendingSaveLoadConfirmAction)
        {
            case SaveLoadConfirmAction.Save:
                PerformSaveSlotSave();
                break;
            case SaveLoadConfirmAction.Load:
                PerformSaveSlotLoad();
                break;
        }

        _pendingSaveLoadConfirmAction = SaveLoadConfirmAction.None;
    }

    private void OnSaveLoadConfirmNoPressed()
    {
        _pendingSaveLoadConfirmAction = SaveLoadConfirmAction.None;
        _saveLoadConfirmDialog?.Hide();
    }

    private void PerformSaveSlotSave()
    {
        if (_worldRepository == null || _turnManager?.World == null || _saveDescriptionLineEdit == null)
        {
            return;
        }

        var slotNumber = _selectedSaveSlotIndex + 1;
        var description = _saveDescriptionLineEdit.Text?.Trim() ?? string.Empty;
        var saved = _worldRepository.SaveGame(BuildSaveSlotPath(slotNumber), _turnManager.World, description, slotNumber);
        PopulateSaveSlotList();
        SelectSaveSlot(slotNumber - 1);
        AddLog(saved ? GetSaveSlotSavedMessage(slotNumber) : GetSaveSlotSaveFailedMessage(slotNumber), isPlayerRelated: true);
    }

    private void PerformSaveSlotLoad()
    {
        if (_worldRepository == null)
        {
            return;
        }

        var slotNumber = _selectedSaveSlotIndex + 1;
        var loadedWorld = _worldRepository.LoadSavedGame(BuildSaveSlotPath(slotNumber));
        if (loadedWorld == null)
        {
            AddLog(GetSaveSlotMissingMessage(slotNumber), isPlayerRelated: true);
            return;
        }

        ApplyLoadedWorld(loadedWorld);
        PopulateSaveSlotList();
        SelectSaveSlot(slotNumber - 1);
        AddLog(GetSaveSlotLoadedMessage(slotNumber), isPlayerRelated: true);
    }

    private void ShowSaveLoadConfirmDialog(SaveLoadConfirmAction action)
    {
        if (_saveLoadConfirmDialog == null || _saveLoadConfirmLabel == null)
        {
            return;
        }

        _pendingSaveLoadConfirmAction = action;
        _saveLoadConfirmDialog.Title = GetSaveLoadConfirmTitle();
        if (_saveLoadConfirmYesButton != null)
        {
            _saveLoadConfirmYesButton.Text = GetConfirmYesText();
        }

        if (_saveLoadConfirmNoButton != null)
        {
            _saveLoadConfirmNoButton.Text = GetConfirmNoText();
        }

        _saveLoadConfirmLabel.Text = action == SaveLoadConfirmAction.Save
            ? GetSaveConfirmMessage(_selectedSaveSlotIndex + 1)
            : GetLoadConfirmMessage(_selectedSaveSlotIndex + 1);
        _saveLoadConfirmDialog.PopupCentered(new Vector2I(380, 180));
    }

    private void OnSaveSlotClosePressed()
    {
        _saveLoadDialog?.Hide();
    }

    private void PopulateSaveSlotList()
    {
        if (_saveSlotList == null || _worldRepository == null)
        {
            return;
        }

        var previousIndex = _selectedSaveSlotIndex;
        _saveSlotList.Clear();
        for (var slotNumber = 1; slotNumber <= SaveSlotCount; slotNumber += 1)
        {
            var summary = _worldRepository.LoadSaveSlotSummary(BuildSaveSlotPath(slotNumber), slotNumber);
            var itemIndex = _saveSlotList.ItemCount;
            _saveSlotList.AddItem(BuildSaveSlotListText(summary));
            _saveSlotList.SetItemMetadata(itemIndex, slotNumber);
        }

        SelectSaveSlot(previousIndex);
    }

    private void SelectSaveSlot(int index)
    {
        if (_saveSlotList == null || _saveSlotList.ItemCount == 0)
        {
            return;
        }

        if (index < 0 || index >= _saveSlotList.ItemCount)
        {
            index = 0;
        }

        _selectedSaveSlotIndex = index;
        _saveSlotList.Select(index);
        RefreshSelectedSaveSlotSummary();
    }

    private void RefreshSelectedSaveSlotSummary()
    {
        if (_saveSlotSummaryLabel == null || _saveDescriptionLineEdit == null || _worldRepository == null)
        {
            return;
        }

        var slotNumber = _selectedSaveSlotIndex + 1;
        var summary = _worldRepository.LoadSaveSlotSummary(BuildSaveSlotPath(slotNumber), slotNumber);
        _saveDescriptionLineEdit.Text = summary.Exists ? summary.Description : string.Empty;
        _saveSlotSummaryLabel.Text = BuildSaveSlotSummaryText(summary);
    }

    private void RefreshOptionDialogText()
    {
        if (_optionDialog != null)
        {
            _optionDialog.Title = GetOptionDialogTitle();
        }

        if (_optionButton != null)
        {
            _optionButton.Text = GetOptionButtonText();
        }

        if (_optionSaveLoadButton != null)
        {
            _optionSaveLoadButton.Text = GetOptionSaveLoadButtonText();
        }

        if (_optionBgmToggleButton != null)
        {
            _optionBgmToggleButton.Text = GetAudioToggleButtonText(isBgm: true, _bgmEnabled);
        }

        if (_optionSfxToggleButton != null)
        {
            _optionSfxToggleButton.Text = GetAudioToggleButtonText(isBgm: false, _sfxEnabled);
        }

        if (_optionBgmVolumeSlider != null)
        {
            _optionBgmVolumeSlider.SetValueNoSignal(Math.Round(_bgmVolume * 100.0f));
            _optionBgmVolumeSlider.Editable = _bgmEnabled;
            _optionBgmVolumeSlider.TooltipText = GetBgmVolumeLabelText();
        }

        if (_optionBgmVolumeValueLabel != null)
        {
            _optionBgmVolumeValueLabel.Text = $"{Math.Round(_bgmVolume * 100.0f)}%";
        }

        if (_optionSfxVolumeSlider != null)
        {
            _optionSfxVolumeSlider.SetValueNoSignal(Math.Round(_sfxVolume * 100.0f));
            _optionSfxVolumeSlider.Editable = _sfxEnabled;
            _optionSfxVolumeSlider.TooltipText = GetSfxVolumeLabelText();
        }

        if (_optionSfxVolumeValueLabel != null)
        {
            _optionSfxVolumeValueLabel.Text = $"{Math.Round(_sfxVolume * 100.0f)}%";
        }

        if (_optionSaveSettingsButton != null)
        {
            _optionSaveSettingsButton.Text = GetSaveSettingsButtonText();
        }
    }

    private void RefreshSaveLoadDialogText()
    {
        if (_saveLoadDialog != null)
        {
            _saveLoadDialog.Title = GetSaveLoadDialogTitle();
        }

        if (_saveDescriptionLineEdit != null)
        {
            _saveDescriptionLineEdit.PlaceholderText = GetSaveDescriptionPlaceholder();
        }

        if (_saveSlotSaveButton != null)
        {
            _saveSlotSaveButton.Text = GetSaveButtonText();
        }

        if (_saveSlotLoadButton != null)
        {
            _saveSlotLoadButton.Text = GetLoadButtonText();
        }

        if (_saveSlotCloseButton != null)
        {
            _saveSlotCloseButton.Text = GetCloseButtonText();
        }

        if (_saveLoadConfirmDialog != null)
        {
            _saveLoadConfirmDialog.Title = GetSaveLoadConfirmTitle();
        }

        if (_saveLoadConfirmYesButton != null)
        {
            _saveLoadConfirmYesButton.Text = GetConfirmYesText();
        }

        if (_saveLoadConfirmNoButton != null)
        {
            _saveLoadConfirmNoButton.Text = GetConfirmNoText();
        }

        if (_saveLoadConfirmLabel != null)
        {
            _saveLoadConfirmLabel.Text = _pendingSaveLoadConfirmAction switch
            {
                SaveLoadConfirmAction.Save => GetSaveConfirmMessage(_selectedSaveSlotIndex + 1),
                SaveLoadConfirmAction.Load => GetLoadConfirmMessage(_selectedSaveSlotIndex + 1),
                _ => _saveLoadConfirmLabel.Text
            };
        }

        RefreshSelectedSaveSlotSummary();
    }

    private void ApplyAudioSettings()
    {
        GameAudioController.Instance?.SetBgmEnabled(_bgmEnabled);
        GameAudioController.Instance?.SetSfxEnabled(_sfxEnabled);
        GameAudioController.Instance?.SetBgmVolume(_bgmVolume);
        GameAudioController.Instance?.SetSfxVolume(_sfxVolume);
        SetBusMute(_sfxEnabled, "Sfx", "SFX");
    }

    public void ReapplyOptionSettings()
    {
        ApplyAudioSettings();
        RefreshOptionDialogText();
    }

    private static void SetBusMute(bool enabled, params string[] busNames)
    {
        foreach (var busName in busNames)
        {
            var busIndex = AudioServer.GetBusIndex(busName);
            if (busIndex >= 0)
            {
                AudioServer.SetBusMute(busIndex, !enabled);
            }
        }
    }

    private void LoadOptionSettings()
    {
        var resolvedPath = ProjectSettings.GlobalizePath(OptionSettingsPath);
        if (!File.Exists(resolvedPath))
        {
            return;
        }

        var json = File.ReadAllText(resolvedPath);
        var settings = JsonSerializer.Deserialize<OptionSettingsData>(json);
        if (settings == null)
        {
            return;
        }

        _bgmEnabled = settings.BgmEnabled;
        _sfxEnabled = settings.SfxEnabled;
        _bgmVolume = Mathf.Clamp(settings.BgmVolume, 0.0f, 1.0f);
        _sfxVolume = Mathf.Clamp(settings.SfxVolume, 0.0f, 1.0f);
    }

    private void SaveOptionSettings()
    {
        var resolvedPath = ProjectSettings.GlobalizePath(OptionSettingsPath);
        var directory = System.IO.Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var settings = new OptionSettingsData
        {
            BgmEnabled = _bgmEnabled,
            SfxEnabled = _sfxEnabled,
            BgmVolume = _bgmVolume,
            SfxVolume = _sfxVolume
        };
        File.WriteAllText(resolvedPath, JsonSerializer.Serialize(settings));
    }

    private string BuildSaveSlotPath(int slotNumber)
    {
        return $"user://saves/slot{slotNumber:00}.json";
    }

    private string BuildSaveSlotListText(SaveSlotSummary summary)
    {
        if (!summary.Exists)
        {
            return $"{GetSlotPrefix(summary.SlotIndex)} {GetEmptySlotText()}";
        }

        var description = string.IsNullOrWhiteSpace(summary.Description) ? GetNoDescriptionText() : summary.Description;
        return $"{GetSlotPrefix(summary.SlotIndex)} {description}";
    }

    private string BuildSaveSlotSummaryText(SaveSlotSummary summary)
    {
        if (!summary.Exists)
        {
            return _localization?.IsTraditionalChinese == true
                ? "此存檔格目前為空。\n可輸入描述後直接存檔。"
                : "This save slot is empty.\nEnter a description and save the game here.";
        }

        var savedTime = FormatSavedTime(summary.SavedAtUtc);
        var storyName = _localization?.IsTraditionalChinese == true
            ? (!string.IsNullOrWhiteSpace(summary.StoryNameZhHant) ? summary.StoryNameZhHant : summary.StoryNameEn)
            : (!string.IsNullOrWhiteSpace(summary.StoryNameEn) ? summary.StoryNameEn : summary.StoryNameZhHant);
        var description = string.IsNullOrWhiteSpace(summary.Description) ? GetNoDescriptionText() : summary.Description;
        return _localization?.IsTraditionalChinese == true
            ? $"存檔格：{summary.SlotIndex}\n描述：{description}\n劇本：{storyName}\n時間：{savedTime}\n進度：{summary.Year} 年 {summary.Month} 月"
            : $"Slot: {summary.SlotIndex}\nDescription: {description}\nStory: {storyName}\nSaved: {savedTime}\nProgress: Year {summary.Year}, Month {summary.Month}";
    }

    private string FormatSavedTime(string savedAtUtc)
    {
        if (DateTime.TryParse(savedAtUtc, out var dateTime))
        {
            return dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        return _localization?.IsTraditionalChinese == true ? "未知" : "Unknown";
    }

    private string GetOptionButtonText() => _localization?.IsTraditionalChinese == true ? "選項" : "Option";
    private string GetOptionDialogTitle() => _localization?.IsTraditionalChinese == true ? "選項" : "Options";
    private string GetOptionSaveLoadButtonText() => _localization?.IsTraditionalChinese == true ? "存檔 / 讀檔" : "Save / Load";
    private string GetSaveSettingsButtonText() => _localization?.IsTraditionalChinese == true ? "儲存設定" : "Save Settings";
    private string GetBgmVolumeLabelText() => _localization?.IsTraditionalChinese == true ? "BGM 音量" : "BGM Volume";
    private string GetSfxVolumeLabelText() => _localization?.IsTraditionalChinese == true ? "SFX 音量" : "SFX Volume";
    private string GetSaveLoadDialogTitle() => _localization?.IsTraditionalChinese == true ? "存檔 / 讀檔" : "Save / Load";
    private string GetSaveSlotListLabel() => _localization?.IsTraditionalChinese == true ? "存檔格" : "Save Slots";
    private string GetSaveDescriptionLabel() => _localization?.IsTraditionalChinese == true ? "描述" : "Description";
    private string GetSaveSummaryLabel() => _localization?.IsTraditionalChinese == true ? "存檔資訊" : "Save Details";
    private string GetSaveDescriptionPlaceholder() => _localization?.IsTraditionalChinese == true ? "輸入此存檔格描述" : "Enter a description for this slot";
    private string GetSaveButtonText() => _localization?.IsTraditionalChinese == true ? "存檔" : "Save";
    private string GetLoadButtonText() => _localization?.IsTraditionalChinese == true ? "讀檔" : "Load";
    private string GetCloseButtonText() => _localization?.IsTraditionalChinese == true ? "關閉" : "Close";
    private string GetEmptySlotText() => _localization?.IsTraditionalChinese == true ? "空白" : "Empty";
    private string GetNoDescriptionText() => _localization?.IsTraditionalChinese == true ? "未填描述" : "No Description";
    private string GetSlotPrefix(int slotIndex) => _localization?.IsTraditionalChinese == true ? $"存檔 {slotIndex}" : $"Slot {slotIndex}";
    private string GetAudioToggleButtonText(bool isBgm, bool enabled)
    {
        var label = isBgm
            ? (_localization?.IsTraditionalChinese == true ? "BGM" : "BGM")
            : (_localization?.IsTraditionalChinese == true ? "SFX" : "SFX");
        var state = enabled
            ? (_localization?.IsTraditionalChinese == true ? "開" : "On")
            : (_localization?.IsTraditionalChinese == true ? "關" : "Off");
        return $"{label}: {state}";
    }
    private string GetSaveSlotSavedMessage(int slotNumber) => _localization?.IsTraditionalChinese == true ? $"已存入存檔 {slotNumber}。" : $"Saved to slot {slotNumber}.";
    private string GetSaveSlotSaveFailedMessage(int slotNumber) => _localization?.IsTraditionalChinese == true ? $"存檔 {slotNumber} 失敗。" : $"Save to slot {slotNumber} failed.";
    private string GetSaveSlotMissingMessage(int slotNumber) => _localization?.IsTraditionalChinese == true ? $"存檔 {slotNumber} 為空。" : $"Save slot {slotNumber} is empty.";
    private string GetSaveSlotLoadedMessage(int slotNumber) => _localization?.IsTraditionalChinese == true ? $"已讀取存檔 {slotNumber}。" : $"Loaded slot {slotNumber}.";
    private string GetSaveLoadConfirmTitle() => _localization?.IsTraditionalChinese == true ? "確認" : "Confirm";
    private string GetConfirmYesText() => _localization?.IsTraditionalChinese == true ? "確認" : "Confirm";
    private string GetConfirmNoText() => _localization?.IsTraditionalChinese == true ? "取消" : "Cancel";
    private string GetSaveConfirmMessage(int slotNumber) => _localization?.IsTraditionalChinese == true ? $"確定要存入存檔 {slotNumber} 嗎？" : $"Save to slot {slotNumber}?";
    private string GetLoadConfirmMessage(int slotNumber) => _localization?.IsTraditionalChinese == true ? $"確定要讀取存檔 {slotNumber} 嗎？" : $"Load slot {slotNumber}?";
    private string GetOptionSettingsSavedMessage() => _localization?.IsTraditionalChinese == true ? "選項設定已儲存。" : "Option settings saved.";
}
