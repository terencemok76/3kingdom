using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class GameStartMenuController : CanvasLayer
{
    private const int SaveSlotCount = 10;
    private static readonly (string SheetPath, string MappingPath)[] PortraitSources =
    {
        ("res://assets/portrait/team1.png", "res://data/person/person_image_1.json"),
        ("res://assets/portrait/team2.png", "res://data/person/person_image_2.json"),
        ("res://assets/portrait/team3.png", "res://data/person/person_image_3.json"),
        ("res://assets/portrait/team4.png", "res://data/person/person_image_4.json")
    };

    public sealed record ScenarioEntry(string Path);

    private sealed class PortraitMappingEntry
    {
        [JsonPropertyName("charId")]
        public int CharId { get; set; }

        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("width")]
        public float Width { get; set; }

        [JsonPropertyName("height")]
        public float Height { get; set; }
    }

    private enum MenuScreen
    {
        Main,
        Story,
        Lord,
        Load
    }

    private LocalizationService? _localization;
    private WorldRepository? _worldRepository;
    private IReadOnlyList<ScenarioEntry> _scenarioEntries = Array.Empty<ScenarioEntry>();
    private readonly Dictionary<string, WorldState> _scenarioPreviewCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, Texture2D> _portraitTextures = new();
    private bool _languageSignalConnected;
    private string _selectedScenarioPath = string.Empty;
    private int _selectedLordFactionId = -1;
    private int _selectedLoadSlotIndex;

    private Control? _root;
    private Label? _titleLabel;
    private Label? _statusLabel;
    private VBoxContainer? _mainMenuPanel;
    private VBoxContainer? _storyPanel;
    private VBoxContainer? _lordPanel;
    private VBoxContainer? _loadPanel;
    private Button? _startGameButton;
    private Button? _loadGameButton;
    private Button? _optionButton;
    private Button? _storyConfirmButton;
    private Button? _storyBackButton;
    private ItemList? _storyList;
    private RichTextLabel? _storySummaryLabel;
    private Label? _storySectionLabel;
    private Label? _lordSectionLabel;
    private Label? _loadSectionLabel;
    private Label? _selectedStoryLabel;
    private ItemList? _lordList;
    private TextureRect? _lordPortraitRect;
    private Label? _lordPortraitPlaceholderLabel;
    private RichTextLabel? _lordSummaryLabel;
    private Button? _lordConfirmButton;
    private Button? _lordBackButton;
    private ItemList? _loadSlotList;
    private RichTextLabel? _loadSummaryLabel;
    private Button? _loadConfirmButton;
    private Button? _loadBackButton;
    private ColorRect? _optionDialogBackdrop;
    private CenterContainer? _optionDialogCenter;
    private Label? _optionDialogTitleLabel;
    private Button? _optionLanguageButton;
    private Button? _bgmToggleButton;
    private HSlider? _bgmVolumeSlider;
    private Label? _bgmVolumeValueLabel;
    private Button? _sfxToggleButton;
    private HSlider? _sfxVolumeSlider;
    private Label? _sfxVolumeValueLabel;
    private Button? _optionDialogCloseButton;

    public event Action<string, int>? StartGameConfirmed;
    public event Action<int>? LoadGameConfirmed;

    public override void _Ready()
    {
        _root = GetNodeOrNull<Control>("Root");
        _titleLabel = GetNodeOrNull<Label>("Root/CenterContainer/MenuPanel/MenuRoot/TitleLabel");
        _statusLabel = GetNodeOrNull<Label>("Root/CenterContainer/MenuPanel/MenuRoot/StatusLabel");
        _mainMenuPanel = GetNodeOrNull<VBoxContainer>("Root/CenterContainer/MenuPanel/MenuRoot/MainMenuPanel");
        _storyPanel = GetNodeOrNull<VBoxContainer>("Root/CenterContainer/MenuPanel/MenuRoot/StoryPanel");
        _lordPanel = GetNodeOrNull<VBoxContainer>("Root/CenterContainer/MenuPanel/MenuRoot/LordPanel");
        _loadPanel = GetNodeOrNull<VBoxContainer>("Root/CenterContainer/MenuPanel/MenuRoot/LoadPanel");
        _startGameButton = GetNodeOrNull<Button>("Root/CenterContainer/MenuPanel/MenuRoot/MainMenuPanel/StartGameButton");
        _loadGameButton = GetNodeOrNull<Button>("Root/CenterContainer/MenuPanel/MenuRoot/MainMenuPanel/LoadGameButton");
        _optionButton = GetNodeOrNull<Button>("Root/CenterContainer/MenuPanel/MenuRoot/MainMenuPanel/OptionButton");
        _storySectionLabel = GetNodeOrNull<Label>("Root/CenterContainer/MenuPanel/MenuRoot/StoryPanel/StoryLabel");
        _storyList = GetNodeOrNull<ItemList>("Root/CenterContainer/MenuPanel/MenuRoot/StoryPanel/StoryList");
        _storySummaryLabel = GetNodeOrNull<RichTextLabel>("Root/CenterContainer/MenuPanel/MenuRoot/StoryPanel/StorySummaryLabel");
        _storyBackButton = GetNodeOrNull<Button>("Root/CenterContainer/MenuPanel/MenuRoot/StoryPanel/StoryButtonRow/StoryBackButton");
        _storyConfirmButton = GetNodeOrNull<Button>("Root/CenterContainer/MenuPanel/MenuRoot/StoryPanel/StoryButtonRow/StoryConfirmButton");
        _lordSectionLabel = GetNodeOrNull<Label>("Root/CenterContainer/MenuPanel/MenuRoot/LordPanel/LordLabel");
        _selectedStoryLabel = GetNodeOrNull<Label>("Root/CenterContainer/MenuPanel/MenuRoot/LordPanel/SelectedStoryLabel");
        _lordList = GetNodeOrNull<ItemList>("Root/CenterContainer/MenuPanel/MenuRoot/LordPanel/LordList");
        _lordPortraitRect = GetNodeOrNull<TextureRect>("Root/CenterContainer/MenuPanel/MenuRoot/LordPanel/LordInfoRow/LordPortraitPanel/LordPortraitCenter/LordPortraitRect");
        _lordPortraitPlaceholderLabel = GetNodeOrNull<Label>("Root/CenterContainer/MenuPanel/MenuRoot/LordPanel/LordInfoRow/LordPortraitPanel/LordPortraitCenter/LordPortraitPlaceholder");
        _lordSummaryLabel = GetNodeOrNull<RichTextLabel>("Root/CenterContainer/MenuPanel/MenuRoot/LordPanel/LordInfoRow/LordSummaryLabel");
        _lordBackButton = GetNodeOrNull<Button>("Root/CenterContainer/MenuPanel/MenuRoot/LordPanel/LordButtonRow/LordBackButton");
        _lordConfirmButton = GetNodeOrNull<Button>("Root/CenterContainer/MenuPanel/MenuRoot/LordPanel/LordButtonRow/LordConfirmButton");
        _loadSectionLabel = GetNodeOrNull<Label>("Root/CenterContainer/MenuPanel/MenuRoot/LoadPanel/LoadLabel");
        _loadSlotList = GetNodeOrNull<ItemList>("Root/CenterContainer/MenuPanel/MenuRoot/LoadPanel/LoadSlotList");
        _loadSummaryLabel = GetNodeOrNull<RichTextLabel>("Root/CenterContainer/MenuPanel/MenuRoot/LoadPanel/LoadSummaryLabel");
        _loadBackButton = GetNodeOrNull<Button>("Root/CenterContainer/MenuPanel/MenuRoot/LoadPanel/LoadButtonRow/LoadBackButton");
        _loadConfirmButton = GetNodeOrNull<Button>("Root/CenterContainer/MenuPanel/MenuRoot/LoadPanel/LoadButtonRow/LoadConfirmButton");
        _optionDialogBackdrop = GetNodeOrNull<ColorRect>("Root/OptionDialogBackdrop");
        _optionDialogCenter = GetNodeOrNull<CenterContainer>("Root/OptionDialogCenter");
        _optionDialogTitleLabel = GetNodeOrNull<Label>("Root/OptionDialogCenter/OptionDialogPanel/OptionDialogRoot/OptionDialogTitleLabel");
        _optionLanguageButton = GetNodeOrNull<Button>("Root/OptionDialogCenter/OptionDialogPanel/OptionDialogRoot/OptionLanguageButton");
        _bgmToggleButton = GetNodeOrNull<Button>("Root/OptionDialogCenter/OptionDialogPanel/OptionDialogRoot/BgmAudioRow/BgmToggleButton");
        _bgmVolumeSlider = GetNodeOrNull<HSlider>("Root/OptionDialogCenter/OptionDialogPanel/OptionDialogRoot/BgmAudioRow/BgmVolumeSlider");
        _bgmVolumeValueLabel = GetNodeOrNull<Label>("Root/OptionDialogCenter/OptionDialogPanel/OptionDialogRoot/BgmAudioRow/BgmVolumeValueLabel");
        _sfxToggleButton = GetNodeOrNull<Button>("Root/OptionDialogCenter/OptionDialogPanel/OptionDialogRoot/SfxAudioRow/SfxToggleButton");
        _sfxVolumeSlider = GetNodeOrNull<HSlider>("Root/OptionDialogCenter/OptionDialogPanel/OptionDialogRoot/SfxAudioRow/SfxVolumeSlider");
        _sfxVolumeValueLabel = GetNodeOrNull<Label>("Root/OptionDialogCenter/OptionDialogPanel/OptionDialogRoot/SfxAudioRow/SfxVolumeValueLabel");
        _optionDialogCloseButton = GetNodeOrNull<Button>("Root/OptionDialogCenter/OptionDialogPanel/OptionDialogRoot/OptionDialogCloseButton");

        ConnectSignals();
        ApplyTheme();
        LoadPortraitData();
        Hide();
    }

    public void Initialize(LocalizationService localization, WorldRepository worldRepository, IReadOnlyList<ScenarioEntry> scenarioEntries)
    {
        if (_localization != null && _languageSignalConnected)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
            _languageSignalConnected = false;
        }

        _localization = localization;
        _worldRepository = worldRepository;
        _scenarioEntries = scenarioEntries;
        _scenarioPreviewCache.Clear();

        _localization.LanguageChanged += OnLanguageChanged;
        _languageSignalConnected = true;
        RefreshText();
        PopulateStoryList();
    }

    public override void _ExitTree()
    {
        if (_localization != null && _languageSignalConnected)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
            _languageSignalConnected = false;
        }
    }

    public void ShowMainMenu()
    {
        RefreshText();
        SetVisibleScreen(MenuScreen.Main);
        Visible = true;
    }

    public void HideMenu()
    {
        Visible = false;
    }

    private void ConnectSignals()
    {
        if (_startGameButton != null)
        {
            _startGameButton.Pressed += OnStartGamePressed;
        }

        if (_loadGameButton != null)
        {
            _loadGameButton.Pressed += OnLoadGamePressed;
        }

        if (_optionButton != null)
        {
            _optionButton.Pressed += OnOptionPressed;
        }

        if (_storyList != null)
        {
            _storyList.ItemSelected += OnStorySelected;
        }

        if (_storyBackButton != null)
        {
            _storyBackButton.Pressed += () => SetVisibleScreen(MenuScreen.Main);
        }

        if (_storyConfirmButton != null)
        {
            _storyConfirmButton.Pressed += OnStoryConfirmPressed;
        }

        if (_lordList != null)
        {
            _lordList.ItemSelected += OnLordSelected;
        }

        if (_lordBackButton != null)
        {
            _lordBackButton.Pressed += () => SetVisibleScreen(MenuScreen.Story);
        }

        if (_lordConfirmButton != null)
        {
            _lordConfirmButton.Pressed += OnLordConfirmPressed;
        }

        if (_loadSlotList != null)
        {
            _loadSlotList.ItemSelected += OnLoadSlotSelected;
        }

        if (_loadBackButton != null)
        {
            _loadBackButton.Pressed += () => SetVisibleScreen(MenuScreen.Main);
        }

        if (_loadConfirmButton != null)
        {
            _loadConfirmButton.Pressed += OnLoadConfirmPressed;
        }

        if (_optionLanguageButton != null)
        {
            _optionLanguageButton.Pressed += OnOptionLanguagePressed;
        }

        if (_bgmToggleButton != null)
        {
            _bgmToggleButton.Pressed += OnBgmTogglePressed;
        }

        if (_sfxToggleButton != null)
        {
            _sfxToggleButton.Pressed += OnSfxTogglePressed;
        }

        if (_bgmVolumeSlider != null)
        {
            _bgmVolumeSlider.ValueChanged += OnBgmVolumeChanged;
        }

        if (_sfxVolumeSlider != null)
        {
            _sfxVolumeSlider.ValueChanged += OnSfxVolumeChanged;
        }

        if (_optionDialogCloseButton != null)
        {
            _optionDialogCloseButton.Pressed += HideOptionDialog;
        }
    }

    private void ApplyTheme()
    {
        var panel = GetNodeOrNull<PanelContainer>("Root/CenterContainer/MenuPanel");
        if (panel != null)
        {
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.08f, 0.1f, 0.94f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.58f, 0.46f, 0.28f, 0.96f),
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10,
                CornerRadiusBottomLeft = 10,
                CornerRadiusBottomRight = 10,
                ContentMarginLeft = 18.0f,
                ContentMarginTop = 18.0f,
                ContentMarginRight = 18.0f,
                ContentMarginBottom = 18.0f
            });
        }

        var optionDialogPanel = GetNodeOrNull<PanelContainer>("Root/OptionDialogCenter/OptionDialogPanel");
        if (optionDialogPanel != null)
        {
            optionDialogPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.08f, 0.1f, 0.96f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.58f, 0.46f, 0.28f, 0.98f),
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10,
                CornerRadiusBottomLeft = 10,
                CornerRadiusBottomRight = 10,
                ContentMarginLeft = 18.0f,
                ContentMarginTop = 18.0f,
                ContentMarginRight = 18.0f,
                ContentMarginBottom = 18.0f
            });
        }

        foreach (var button in new[] { _startGameButton, _loadGameButton, _optionButton, _storyConfirmButton, _storyBackButton, _lordConfirmButton, _lordBackButton, _loadConfirmButton, _loadBackButton, _optionLanguageButton, _bgmToggleButton, _sfxToggleButton, _optionDialogCloseButton })
        {
            ApplyButtonTheme(button);
        }

        foreach (var list in new[] { _storyList, _lordList, _loadSlotList })
        {
            ApplyItemListTheme(list);
        }
    }

    private static void ApplyButtonTheme(Button? button)
    {
        if (button == null)
        {
            return;
        }

        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.82f, 0.74f, 0.56f, 1.0f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.45f, 0.35f, 0.2f, 1.0f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color(0.9f, 0.82f, 0.64f, 1.0f);
        var pressed = (StyleBoxFlat)normal.Duplicate();
        pressed.BgColor = new Color(0.72f, 0.62f, 0.43f, 1.0f);
        var disabled = (StyleBoxFlat)normal.Duplicate();
        disabled.BgColor = new Color(0.34f, 0.33f, 0.31f, 0.94f);
        disabled.BorderColor = new Color(0.42f, 0.39f, 0.34f, 1.0f);

        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("disabled", disabled);
        button.AddThemeColorOverride("font_color", new Color(0.16f, 0.12f, 0.08f, 1.0f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.16f, 0.12f, 0.08f, 1.0f));
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", new Color(0.7f, 0.68f, 0.64f, 1.0f));
    }

    private static void ApplyItemListTheme(ItemList? itemList)
    {
        if (itemList == null)
        {
            return;
        }

        var panel = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.12f, 0.92f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.48f, 0.39f, 0.24f, 0.92f)
        };
        var selected = new StyleBoxFlat
        {
            BgColor = new Color(0.46f, 0.38f, 0.24f, 0.94f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.62f, 0.5f, 0.3f, 1.0f)
        };

        itemList.AddThemeStyleboxOverride("panel", panel);
        itemList.AddThemeStyleboxOverride("focus", panel);
        itemList.AddThemeStyleboxOverride("selected", selected);
        itemList.AddThemeColorOverride("font_color", new Color(0.94f, 0.9f, 0.84f, 1.0f));
        itemList.AddThemeColorOverride("font_selected_color", Colors.White);
        itemList.AddThemeColorOverride("guide_color", new Color(0.44f, 0.37f, 0.25f, 0.5f));
    }

    private void RefreshText()
    {
        if (_localization == null)
        {
            return;
        }

        if (_titleLabel != null)
        {
            _titleLabel.Text = _localization.T("ui.main_menu");
        }

        if (_startGameButton != null)
        {
            _startGameButton.Text = _localization.T("ui.start_game");
        }

        if (_loadGameButton != null)
        {
            _loadGameButton.Text = _localization.T("ui.load_game");
        }

        if (_optionButton != null)
        {
            _optionButton.Text = _localization.T("ui.option");
        }

        if (_storySectionLabel != null)
        {
            _storySectionLabel.Text = _localization.T("ui.select_story");
        }

        if (_storyBackButton != null)
        {
            _storyBackButton.Text = _localization.T("ui.back");
        }

        if (_storyConfirmButton != null)
        {
            _storyConfirmButton.Text = _localization.T("ui.confirm_story");
        }

        if (_lordSectionLabel != null)
        {
            _lordSectionLabel.Text = _localization.T("ui.select_lord");
        }

        if (_lordBackButton != null)
        {
            _lordBackButton.Text = _localization.T("ui.back");
        }

        if (_lordConfirmButton != null)
        {
            _lordConfirmButton.Text = _localization.T("ui.start_campaign");
        }

        if (_loadSectionLabel != null)
        {
            _loadSectionLabel.Text = _localization.T("ui.load_game");
        }

        if (_loadBackButton != null)
        {
            _loadBackButton.Text = _localization.T("ui.back");
        }

        if (_loadConfirmButton != null)
        {
            _loadConfirmButton.Text = _localization.T("ui.load");
        }

        if (_optionDialogTitleLabel != null)
        {
            _optionDialogTitleLabel.Text = _localization.T("ui.options");
        }

        if (_optionLanguageButton != null)
        {
            _optionLanguageButton.Text = _localization.T("ui.option_language_toggle");
        }

        var settings = OptionSettingsStore.LoadOrDefault();

        if (_bgmToggleButton != null)
        {
            _bgmToggleButton.Text = BuildAudioToggleButtonText(_localization.T("ui.bgm_volume"), settings.BgmEnabled);
        }

        if (_bgmVolumeSlider != null)
        {
            _bgmVolumeSlider.SetValueNoSignal(Math.Round(settings.BgmVolume * 100.0f));
            _bgmVolumeSlider.Editable = settings.BgmEnabled;
            _bgmVolumeSlider.TooltipText = _localization.T("ui.bgm_volume");
        }

        if (_bgmVolumeValueLabel != null)
        {
            _bgmVolumeValueLabel.Text = $"{Math.Round(settings.BgmVolume * 100.0f)}%";
        }

        if (_sfxToggleButton != null)
        {
            _sfxToggleButton.Text = BuildAudioToggleButtonText(_localization.T("ui.sfx_volume"), settings.SfxEnabled);
        }

        if (_sfxVolumeSlider != null)
        {
            _sfxVolumeSlider.SetValueNoSignal(Math.Round(settings.SfxVolume * 100.0f));
            _sfxVolumeSlider.Editable = settings.SfxEnabled;
            _sfxVolumeSlider.TooltipText = _localization.T("ui.sfx_volume");
        }

        if (_sfxVolumeValueLabel != null)
        {
            _sfxVolumeValueLabel.Text = $"{Math.Round(settings.SfxVolume * 100.0f)}%";
        }

        if (_optionDialogCloseButton != null)
        {
            _optionDialogCloseButton.Text = _localization.T("ui.close");
        }

        if (_mainMenuPanel?.Visible == true)
        {
            SetStatusText(_localization.T("ui.main_menu_hint"));
        }

        RefreshStorySummary();
        RefreshLordSummary();
        RefreshLoadSummary();
    }

    private void OnLanguageChanged()
    {
        RefreshText();
        PopulateStoryList();
        if (_lordPanel?.Visible == true && !string.IsNullOrWhiteSpace(_selectedScenarioPath))
        {
            PopulateLordList(_selectedScenarioPath);
        }
        if (_loadPanel?.Visible == true)
        {
            PopulateLoadSlotList();
        }
    }

    private void OnStartGamePressed()
    {
        PopulateStoryList();
        SetVisibleScreen(MenuScreen.Story);
    }

    private void OnLoadGamePressed()
    {
        PopulateLoadSlotList();
        SetVisibleScreen(MenuScreen.Load);
    }

    private void OnOptionPressed()
    {
        ShowOptionDialog();
    }

    private void OnOptionLanguagePressed()
    {
        if (_localization == null)
        {
            return;
        }

        _localization.ToggleLanguage();
        SaveCurrentLanguageSetting();
    }

    private void OnBgmTogglePressed()
    {
        var settings = OptionSettingsStore.LoadOrDefault();
        settings.BgmEnabled = !settings.BgmEnabled;
        OptionSettingsStore.Save(settings);
        ApplyAudioSettings(settings);
        RefreshText();
    }

    private void OnSfxTogglePressed()
    {
        var settings = OptionSettingsStore.LoadOrDefault();
        settings.SfxEnabled = !settings.SfxEnabled;
        OptionSettingsStore.Save(settings);
        ApplyAudioSettings(settings);
        RefreshText();
    }

    private void OnBgmVolumeChanged(double value)
    {
        var settings = OptionSettingsStore.LoadOrDefault();
        settings.BgmVolume = Mathf.Clamp((float)(value / 100.0), 0.0f, 1.0f);
        OptionSettingsStore.Save(settings);
        ApplyAudioSettings(settings);
        RefreshText();
    }

    private void OnSfxVolumeChanged(double value)
    {
        var settings = OptionSettingsStore.LoadOrDefault();
        settings.SfxVolume = Mathf.Clamp((float)(value / 100.0), 0.0f, 1.0f);
        OptionSettingsStore.Save(settings);
        ApplyAudioSettings(settings);
        RefreshText();
    }

    private void PopulateStoryList()
    {
        if (_storyList == null || _worldRepository == null || _localization == null)
        {
            return;
        }

        _storyList.Clear();
        _selectedScenarioPath = string.Empty;

        for (var index = 0; index < _scenarioEntries.Count; index += 1)
        {
            var path = _scenarioEntries[index].Path;
            var world = LoadScenarioPreview(path);
            if (world == null)
            {
                continue;
            }

            var text = GetStoryDisplayName(world);
            _storyList.AddItem(text);
            _storyList.SetItemMetadata(_storyList.ItemCount - 1, path);
        }

        if (_storyList.ItemCount > 0)
        {
            _storyList.Select(0);
            OnStorySelected(0);
        }
        else
        {
            RefreshStorySummary();
        }
    }

    private void OnStorySelected(long index)
    {
        if (_storyList == null || index < 0 || index >= _storyList.ItemCount)
        {
            return;
        }

        _selectedScenarioPath = _storyList.GetItemMetadata((int)index).AsString();
        RefreshStorySummary();
        if (_storyConfirmButton != null)
        {
            _storyConfirmButton.Disabled = string.IsNullOrWhiteSpace(_selectedScenarioPath);
        }
    }

    private void OnStoryConfirmPressed()
    {
        if (string.IsNullOrWhiteSpace(_selectedScenarioPath))
        {
            return;
        }

        PopulateLordList(_selectedScenarioPath);
        SetVisibleScreen(MenuScreen.Lord);
    }

    private void PopulateLordList(string scenarioPath)
    {
        if (_lordList == null || _localization == null)
        {
            return;
        }

        _selectedLordFactionId = -1;
        _lordList.Clear();
        var world = LoadScenarioPreview(scenarioPath);
        if (world == null)
        {
            RefreshLordSummary();
            return;
        }

        if (_selectedStoryLabel != null)
        {
            _selectedStoryLabel.Text = _localization.Format("fmt.selected_story", GetStoryDisplayName(world));
        }

        foreach (var faction in world.Factions)
        {
            var cityCount = 0;
            foreach (var city in world.Cities)
            {
                if (city.OwnerFactionId == faction.Id)
                {
                    cityCount += 1;
                }
            }

            var text = _localization.Format("fmt.lord_list_item",
                _localization.GetFactionName(world, faction.Id),
                cityCount,
                faction.OfficerIds.Count);
            _lordList.AddItem(text);
            _lordList.SetItemMetadata(_lordList.ItemCount - 1, faction.Id);
        }

        if (_lordList.ItemCount > 0)
        {
            _lordList.Select(0);
            OnLordSelected(0);
        }
        else
        {
            RefreshLordSummary();
        }
    }

    private void OnLordSelected(long index)
    {
        if (_lordList == null || index < 0 || index >= _lordList.ItemCount)
        {
            return;
        }

        _selectedLordFactionId = _lordList.GetItemMetadata((int)index).AsInt32();
        RefreshLordSummary();
        if (_lordConfirmButton != null)
        {
            _lordConfirmButton.Disabled = _selectedLordFactionId <= 0 || string.IsNullOrWhiteSpace(_selectedScenarioPath);
        }
    }

    private void OnLordConfirmPressed()
    {
        if (string.IsNullOrWhiteSpace(_selectedScenarioPath) || _selectedLordFactionId <= 0)
        {
            return;
        }

        StartGameConfirmed?.Invoke(_selectedScenarioPath, _selectedLordFactionId);
    }

    private void PopulateLoadSlotList()
    {
        if (_loadSlotList == null || _worldRepository == null)
        {
            return;
        }

        _loadSlotList.Clear();
        _selectedLoadSlotIndex = 0;

        for (var slotNumber = 1; slotNumber <= SaveSlotCount; slotNumber += 1)
        {
            var summary = _worldRepository.LoadSaveSlotSummary(BuildSaveSlotPath(slotNumber), slotNumber);
            _loadSlotList.AddItem(BuildSaveSlotListText(summary));
            _loadSlotList.SetItemMetadata(_loadSlotList.ItemCount - 1, slotNumber);
        }

        if (_loadSlotList.ItemCount > 0)
        {
            _loadSlotList.Select(0);
            OnLoadSlotSelected(0);
        }
        else
        {
            RefreshLoadSummary();
        }
    }

    private void OnLoadSlotSelected(long index)
    {
        if (_loadSlotList == null || index < 0 || index >= _loadSlotList.ItemCount)
        {
            return;
        }

        _selectedLoadSlotIndex = (int)index;
        RefreshLoadSummary();
    }

    private void OnLoadConfirmPressed()
    {
        if (_loadSlotList == null || _worldRepository == null)
        {
            return;
        }

        var slotNumber = _selectedLoadSlotIndex + 1;
        var summary = _worldRepository.LoadSaveSlotSummary(BuildSaveSlotPath(slotNumber), slotNumber);
        if (!summary.Exists)
        {
            return;
        }

        LoadGameConfirmed?.Invoke(slotNumber);
    }

    private void RefreshStorySummary()
    {
        if (_storySummaryLabel == null || _localization == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedScenarioPath))
        {
            _storySummaryLabel.Text = _localization.T("ui.select_story_hint");
            return;
        }

        var world = LoadScenarioPreview(_selectedScenarioPath);
        if (world == null)
        {
            _storySummaryLabel.Text = _localization.T("ui.story_load_failed");
            return;
        }

        _storySummaryLabel.Text = _localization.Format(
            "fmt.story_summary",
            GetStoryDisplayName(world),
            world.Year,
            world.Month,
            world.Factions.Count);
    }

    private void RefreshLordSummary()
    {
        if (_lordSummaryLabel == null || _localization == null)
        {
            return;
        }

        var world = string.IsNullOrWhiteSpace(_selectedScenarioPath) ? null : LoadScenarioPreview(_selectedScenarioPath);
        if (world == null || _selectedLordFactionId <= 0)
        {
            RefreshLordPortrait(null, string.Empty);
            _lordSummaryLabel.Text = _localization.T("ui.select_lord_hint");
            return;
        }

        var faction = world.GetFaction(_selectedLordFactionId);
        if (faction == null)
        {
            RefreshLordPortrait(null, string.Empty);
            _lordSummaryLabel.Text = _localization.T("ui.select_lord_hint");
            return;
        }

        var ruler = world.GetOfficer(faction.RulerOfficerId);
        var cityCount = 0;
        foreach (var city in world.Cities)
        {
            if (city.OwnerFactionId == faction.Id)
            {
                cityCount += 1;
            }
        }

        var rulerName = ruler != null ? _localization.GetOfficerName(ruler) : _localization.T("ui.unknown");
        RefreshLordPortrait(ruler, rulerName);
        _lordSummaryLabel.Text = _localization.Format(
            "fmt.lord_summary",
            _localization.GetFactionName(world, faction.Id),
            rulerName,
            cityCount,
            faction.OfficerIds.Count);
    }

    private void RefreshLoadSummary()
    {
        if (_loadSummaryLabel == null || _worldRepository == null)
        {
            return;
        }

        var slotNumber = _selectedLoadSlotIndex + 1;
        var summary = _worldRepository.LoadSaveSlotSummary(BuildSaveSlotPath(slotNumber), slotNumber);
        _loadSummaryLabel.Text = BuildSaveSlotSummaryText(summary);
        if (_loadConfirmButton != null)
        {
            _loadConfirmButton.Disabled = !summary.Exists;
        }
    }

    private void SetVisibleScreen(MenuScreen screen)
    {
        HideOptionDialog();

        if (_mainMenuPanel != null)
        {
            _mainMenuPanel.Visible = screen == MenuScreen.Main;
        }

        if (_storyPanel != null)
        {
            _storyPanel.Visible = screen == MenuScreen.Story;
        }

        if (_lordPanel != null)
        {
            _lordPanel.Visible = screen == MenuScreen.Lord;
        }

        if (_loadPanel != null)
        {
            _loadPanel.Visible = screen == MenuScreen.Load;
        }

        if (_localization == null)
        {
            return;
        }

        var statusKey = screen switch
        {
            MenuScreen.Main => "ui.main_menu_hint",
            MenuScreen.Story => "ui.select_story_hint",
            MenuScreen.Lord => "ui.select_lord_hint",
            MenuScreen.Load => "ui.load_game_hint",
            _ => "ui.main_menu_hint"
        };
        SetStatusText(_localization.T(statusKey));
    }

    private void SetStatusText(string text)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = text;
        }
    }

    private void ShowOptionDialog()
    {
        if (_optionDialogBackdrop != null)
        {
            _optionDialogBackdrop.Visible = true;
        }

        if (_optionDialogCenter != null)
        {
            _optionDialogCenter.Visible = true;
        }
    }

    private void HideOptionDialog()
    {
        if (_optionDialogBackdrop != null)
        {
            _optionDialogBackdrop.Visible = false;
        }

        if (_optionDialogCenter != null)
        {
            _optionDialogCenter.Visible = false;
        }
    }

    private void SaveCurrentLanguageSetting()
    {
        if (_localization == null)
        {
            return;
        }

        var settings = OptionSettingsStore.LoadOrDefault();
        settings.Language = _localization.CurrentLanguage;
        OptionSettingsStore.Save(settings);
    }

    private void ApplyAudioSettings(OptionSettingsData settings)
    {
        GameAudioController.Instance?.SetBgmEnabled(settings.BgmEnabled);
        GameAudioController.Instance?.SetSfxEnabled(settings.SfxEnabled);
        GameAudioController.Instance?.SetBgmVolume(settings.BgmVolume);
        GameAudioController.Instance?.SetSfxVolume(settings.SfxVolume);
        SetBusMute(settings.SfxEnabled, "Sfx", "SFX");
    }

    private string BuildAudioToggleButtonText(string label, bool enabled)
    {
        var onText = _localization?.T("ui.on") ?? "On";
        var offText = _localization?.T("ui.off") ?? "Off";
        return $"{label}: {(enabled ? onText : offText)}";
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

    private WorldState? LoadScenarioPreview(string path)
    {
        if (_worldRepository == null)
        {
            return null;
        }

        if (_scenarioPreviewCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var world = _worldRepository.LoadScenario(path);
        if (world != null)
        {
            _scenarioPreviewCache[path] = world;
        }

        return world;
    }

    private string GetStoryDisplayName(WorldState world)
    {
        if (_localization == null)
        {
            return world.StoryNameZhHant;
        }

        return _localization.IsTraditionalChinese
            ? (!string.IsNullOrWhiteSpace(world.StoryNameZhHant) ? world.StoryNameZhHant : world.StoryNameEn)
            : (!string.IsNullOrWhiteSpace(world.StoryNameEn) ? world.StoryNameEn : world.StoryNameZhHant);
    }

    private string BuildSaveSlotPath(int slotNumber)
    {
        return $"user://saves/slot{slotNumber:00}.json";
    }

    private string BuildSaveSlotListText(SaveSlotSummary summary)
    {
        if (_localization == null)
        {
            return $"Slot {summary.SlotIndex}";
        }

        var description = !summary.Exists
            ? _localization.T("ui.empty")
            : (string.IsNullOrWhiteSpace(summary.Description) ? _localization.T("ui.no_description") : summary.Description);
        return _localization.Format("fmt.save_slot_list_item", _localization.Format("fmt.save_slot_prefix", summary.SlotIndex), description);
    }

    private string BuildSaveSlotSummaryText(SaveSlotSummary summary)
    {
        if (_localization == null)
        {
            return string.Empty;
        }

        if (!summary.Exists)
        {
            return _localization.T("fmt.save_slot_empty_summary");
        }

        var storyName = _localization.IsTraditionalChinese
            ? (!string.IsNullOrWhiteSpace(summary.StoryNameZhHant) ? summary.StoryNameZhHant : summary.StoryNameEn)
            : (!string.IsNullOrWhiteSpace(summary.StoryNameEn) ? summary.StoryNameEn : summary.StoryNameZhHant);
        var description = string.IsNullOrWhiteSpace(summary.Description) ? _localization.T("ui.no_description") : summary.Description;
        return _localization.Format(
            "fmt.save_slot_summary",
            summary.SlotIndex,
            description,
            storyName,
            FormatSavedTime(summary.SavedAtUtc),
            summary.Year,
            summary.Month);
    }

    private string FormatSavedTime(string savedAtUtc)
    {
        if (DateTime.TryParse(savedAtUtc, out var dateTime))
        {
            return dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        return _localization?.T("ui.unknown") ?? "Unknown";
    }

    private void RefreshLordPortrait(OfficerData? ruler, string rulerName)
    {
        if (_lordPortraitRect != null)
        {
            _lordPortraitRect.Texture = ruler != null ? BuildOfficerPortraitTexture(ruler.Id) : null;
        }

        if (_lordPortraitPlaceholderLabel != null)
        {
            var hasPortrait = _lordPortraitRect?.Texture != null;
            _lordPortraitPlaceholderLabel.Visible = !hasPortrait;
            _lordPortraitPlaceholderLabel.Text = string.IsNullOrWhiteSpace(rulerName)
                ? "Portrait"
                : $"Portrait\n{rulerName}";
        }
    }

    private void LoadPortraitData()
    {
        _portraitTextures.Clear();
        foreach (var portraitSource in PortraitSources)
        {
            var portraitSheetTexture = ResourceLoader.Load<Texture2D>(portraitSource.SheetPath);
            if (portraitSheetTexture == null || !FileAccess.FileExists(portraitSource.MappingPath))
            {
                continue;
            }

            using var file = FileAccess.Open(portraitSource.MappingPath, FileAccess.ModeFlags.Read);
            var rawText = file.GetAsText();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                continue;
            }

            var parsedEntries = JsonSerializer.Deserialize<List<PortraitMappingEntry>>(rawText);
            if (parsedEntries == null)
            {
                continue;
            }

            foreach (var entry in parsedEntries.Where(static entry => entry.CharId > 0 && entry.Width > 0 && entry.Height > 0))
            {
                _portraitTextures[entry.CharId] = new AtlasTexture
                {
                    Atlas = portraitSheetTexture,
                    Region = new Rect2(entry.X, entry.Y, entry.Width, entry.Height)
                };
            }
        }
    }

    private Texture2D? BuildOfficerPortraitTexture(int officerId)
    {
        return _portraitTextures.TryGetValue(officerId, out var portraitTexture)
            ? portraitTexture
            : null;
    }
}
