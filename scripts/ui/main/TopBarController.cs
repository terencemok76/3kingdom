using Godot;
using ThreeKingdom.Core;

namespace ThreeKingdom.UI;

internal sealed class TopBarController
{
    private readonly MainHudUiContext _context;
    // Keep the strategic top bar horizontally inside the map frame while aligning it to the top edge.
    private const float MapFrameContentInset = 44.0f;
    private const float TopBarTop = 20.0f;
    private const float TopBarWidth = 750.0f;
    private const float TopBarHeight = 80.0f;
    private const float RightControlInset = 30.0f;
    private const float RightControlGap = 10.0f;

    private ColorRect? _background;
    private HBoxContainer? _content;
    private Vector2 _defaultPosition;
    private Vector2 _position;
    private Vector2 _size;
    private Vector2 _contentOffset = new(260.0f, 52.0f);
    private Vector2 _designedContentSize = new(600.0f, 42.0f);
    private Vector2 _contentSize = new(600.0f, 42.0f);
    private Vector2 _endTurnOffset = new(681.0f, 52.0f);
    private Vector2 _endTurnSize = new(116.0f, 42.0f);
    private Vector2 _testButtonOffset = new(612.0f, 52.0f);
    private Vector2 _testButtonSize = new(60.0f, 42.0f);
    private Vector2 _optionOffset = new(813.0f, 52.0f);
    private Vector2 _optionSize = new(48.0f, 42.0f);
    private int _displayedFactionId = -1;
    private bool _displayingAiTurn;
    private bool _temporarilyHidden;
    private bool _languageButtonConnected;
    private bool _godModeButtonConnected;
    private bool _testButtonConnected;
    private bool _endTurnButtonConnected;

    public TopBarController(MainHudUiContext context)
    {
        _context = context;
    }

    public void Initialize()
    {
        ConnectButtons();
        ApplyButtonThemes();
    }

    public void Shutdown()
    {
        DisconnectButtons();
    }

    public void InitializePanelBehavior(ColorRect? background, HBoxContainer? content)
    {
        _background = background;
        _content = content;
        if (_background == null || _content == null)
        {
            return;
        }

        CaptureEditorLayoutOffsets();
        UpdateViewportLayoutMetrics();
        _position = _defaultPosition;
        _background.Color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        _background.MouseFilter = Control.MouseFilterEnum.Pass;
        _content.MouseFilter = Control.MouseFilterEnum.Pass;
        _content.AddThemeConstantOverride("separation", 10);
        ApplyButtonThemes();
        ApplyLayout();
    }

    public void RefreshViewportLayout()
    {
        if (_background == null || _content == null)
        {
            return;
        }

        UpdateViewportLayoutMetrics();
        _position = _defaultPosition;
        ApplyLayout();
    }

    public void RefreshText()
    {
        var localization = _context.Localization;
        var world = _context.World;
        if (localization == null || world == null)
        {
            return;
        }

        RefreshMonth();

        if (_context.PlayerFactionLabel != null)
        {
            var factionId = _displayedFactionId > 0 ? _displayedFactionId : _context.PlayerFactionId;
            var factionName = localization.GetFactionName(world, factionId);
            var turnOwnerLabel = _displayingAiTurn ? localization.T("ui.ai") : localization.T("ui.player");
            _context.PlayerFactionLabel.Text = localization.Format("fmt.label_value", turnOwnerLabel, factionName);
        }

        if (_context.StoryLabel != null)
        {
            _context.StoryLabel.Text = localization.IsTraditionalChinese
                ? (!string.IsNullOrWhiteSpace(world.StoryNameZhHant) ? world.StoryNameZhHant : world.StoryNameEn)
                : (!string.IsNullOrWhiteSpace(world.StoryNameEn) ? world.StoryNameEn : world.StoryNameZhHant);
        }

        if (_context.EndTurnButtonLabel != null)
        {
            _context.EndTurnButtonLabel.Text = localization.T("ui.end_turn");
        }
        else if (_context.EndTurnButton != null)
        {
            _context.EndTurnButton.Text = localization.T("ui.end_turn");
        }

        if (_context.GodModeButton != null)
        {
            _context.GodModeButton.Text = _context.BuildGodModeButtonText();
        }

        if (_context.TestButton != null)
        {
            _context.TestButton.Visible = _context.IsGodModeEnabled() && !_temporarilyHidden;
            _context.TestButton.Text = "Test";
        }

        if (_context.LanguageButton != null)
        {
            _context.LanguageButton.Text = localization.IsTraditionalChinese
                ? localization.T("ui.lang_btn_en")
                : localization.T("ui.lang_btn_zh");
        }
    }

    public void RefreshMonth()
    {
        var localization = _context.Localization;
        var world = _context.World;
        if (localization == null || world == null)
        {
            return;
        }

        if (_context.DateLabel != null)
        {
            _context.DateLabel.Text = localization.IsTraditionalChinese
                ? $"{world.Year}年 {world.Month}月"
                : $"{world.Year} / M{world.Month}";
        }
    }

    public Vector2 GetPosition() => _position;

    public Vector2 GetSize() => _size;

    public void SetDraggedPosition(Vector2 targetPosition)
    {
        _position = _defaultPosition;
        ApplyLayout();
    }

    public void ApplyLayout()
    {
        if (_background == null || _content == null)
        {
            return;
        }

        _position = ClampPosition(_position, _size.X, _size.Y);
        _background.Position = _position;
        _background.Size = _size;
        _content.Position = _position + _contentOffset;
        _content.Size = _contentSize;
        if (_context.EndTurnButton != null)
        {
            _context.EndTurnButton.Position = _position + GetEndTurnOffset();
            _context.EndTurnButton.Size = _endTurnSize;
            _context.EndTurnButton.Visible = !_temporarilyHidden;
        }
        if (_context.TestButton != null)
        {
            _context.TestButton.Position = _position + GetTestButtonOffset();
            _context.TestButton.Size = _testButtonSize;
            _context.TestButton.Visible = _context.IsGodModeEnabled() && !_temporarilyHidden;
        }
        if (_context.OptionButton != null)
        {
            _context.OptionButton.Position = _position + GetOptionOffset();
            _context.OptionButton.Size = _optionSize;
            _context.OptionButton.Visible = !_temporarilyHidden;
        }
        _background.Visible = !_temporarilyHidden;
        _content.Visible = !_temporarilyHidden;
    }

    public void SetTemporarilyHidden(bool hidden)
    {
        _temporarilyHidden = hidden;
        ApplyLayout();
    }

    public void SetTurnState(int factionId, bool isAiTurn, bool buttonsEnabled)
    {
        _displayedFactionId = factionId;
        _displayingAiTurn = isAiTurn;
        SetTurnButtonsEnabled(buttonsEnabled);
        RefreshText();
    }

    private void SetTurnButtonsEnabled(bool enabled)
    {
        if (_context.EndTurnButton != null)
        {
            _context.EndTurnButton.Disabled = !enabled;
        }

        if (_context.LanguageButton != null)
        {
            _context.LanguageButton.Disabled = !enabled;
        }

        if (_context.GodModeButton != null)
        {
            _context.GodModeButton.Disabled = !enabled;
        }

        if (_context.TestButton != null)
        {
            _context.TestButton.Disabled = !enabled;
        }
    }

    public void BringToFront()
    {
        _context.MoveToFront(_background);
        _context.MoveToFront(_content);
        _context.MoveToFront(_context.TestButton);
        _context.MoveToFront(_context.EndTurnButton);
        _context.MoveToFront(_context.OptionButton);
    }

    public void ApplyLoadedSettings(float x, float y)
    {
        _position = _defaultPosition;
    }

    public void RestoreDefaultLayout()
    {
        _position = _defaultPosition;
        ApplyLayout();
    }

    private void UpdateViewportLayoutMetrics()
    {
        if (_background == null)
        {
            return;
        }

        var viewportSize = _background.GetViewportRect().Size;
        _size = new Vector2(Mathf.Min(TopBarWidth, viewportSize.X - (MapFrameContentInset * 2.0f)), TopBarHeight);
        _defaultPosition = new Vector2(
            Mathf.Max(MapFrameContentInset, (viewportSize.X - _size.X) * 0.5f),
            TopBarTop);

        var availableContentWidth = Mathf.Max(120.0f, _size.X - _contentOffset.X - 28.0f);
        _contentSize = new Vector2(Mathf.Min(_designedContentSize.X, availableContentWidth), _designedContentSize.Y);
    }

    private void CaptureEditorLayoutOffsets()
    {
        if (_background == null || _content == null)
        {
            return;
        }

        _contentOffset = _content.Position - _background.Position;
        _designedContentSize = _content.Size;
        _contentSize = _designedContentSize;
        if (_context.EndTurnButton != null)
        {
            _endTurnOffset = _context.EndTurnButton.Position - _background.Position;
            _endTurnSize = _context.EndTurnButton.Size;
        }

        if (_context.TestButton != null)
        {
            _testButtonOffset = _context.TestButton.Position - _background.Position;
            _testButtonSize = _context.TestButton.Size;
        }

        if (_context.OptionButton != null)
        {
            _optionOffset = _context.OptionButton.Position - _background.Position;
            _optionSize = _context.OptionButton.Size;
        }
    }

    private Vector2 GetOptionOffset()
    {
        return new Vector2(
            Mathf.Max(0.0f, _size.X - RightControlInset - _optionSize.X),
            _optionOffset.Y);
    }

    private Vector2 GetEndTurnOffset()
    {
        var optionOffset = GetOptionOffset();
        return new Vector2(
            Mathf.Max(0.0f, optionOffset.X - RightControlGap - _endTurnSize.X),
            _endTurnOffset.Y);
    }

    private Vector2 GetTestButtonOffset()
    {
        var endTurnOffset = GetEndTurnOffset();
        return new Vector2(
            Mathf.Max(0.0f, endTurnOffset.X - RightControlGap - _testButtonSize.X),
            _testButtonOffset.Y);
    }

    public void PopulateSettings(OptionSettingsData settings)
    {
        settings.TopBarX = _position.X;
        settings.TopBarY = _position.Y;
    }

    private void ConnectButtons()
    {
        if (_context.LanguageButton != null && !_languageButtonConnected)
        {
            _context.LanguageButton.Pressed += OnLanguageButtonPressed;
            _languageButtonConnected = true;
        }

        if (_context.GodModeButton != null && !_godModeButtonConnected)
        {
            _context.GodModeButton.Pressed += OnGodModeButtonPressed;
            _godModeButtonConnected = true;
        }

        if (_context.TestButton != null && !_testButtonConnected)
        {
            _context.TestButton.Pressed += OnTestButtonPressed;
            _testButtonConnected = true;
        }

        if (_context.EndTurnButton != null && !_endTurnButtonConnected)
        {
            _context.EndTurnButton.Pressed += OnEndTurnButtonPressed;
            _endTurnButtonConnected = true;
        }
    }

    private void ApplyButtonThemes()
    {
        ApplySharedButtonTheme(_context.LanguageButton);
        ApplySharedButtonTheme(_context.GodModeButton);
    }

    private void ApplySharedButtonTheme(Button? button)
    {
        if (button == null || _context.ViewButton == null)
        {
            return;
        }

        CopyButtonTheme(_context.ViewButton, button);
    }

    private static void CopyButtonTheme(Button source, Button target)
    {
        var designedMinimumSize = target.CustomMinimumSize;

        foreach (var name in new[] { "normal", "hover", "pressed", "disabled", "focus" })
        {
            var style = source.GetThemeStylebox(name);
            if (style != null)
            {
                target.AddThemeStyleboxOverride(name, style);
            }
        }

        foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_disabled_color", "font_focus_color" })
        {
            if (source.HasThemeColorOverride(name))
            {
                target.AddThemeColorOverride(name, source.GetThemeColor(name));
            }
        }

        target.CustomMinimumSize = new Vector2(
            Mathf.Max(source.CustomMinimumSize.X, designedMinimumSize.X),
            Mathf.Max(source.CustomMinimumSize.Y, designedMinimumSize.Y));
    }

    private void DisconnectButtons()
    {
        if (_context.LanguageButton != null && _languageButtonConnected)
        {
            _context.LanguageButton.Pressed -= OnLanguageButtonPressed;
            _languageButtonConnected = false;
        }

        if (_context.GodModeButton != null && _godModeButtonConnected)
        {
            _context.GodModeButton.Pressed -= OnGodModeButtonPressed;
            _godModeButtonConnected = false;
        }

        if (_context.TestButton != null && _testButtonConnected)
        {
            _context.TestButton.Pressed -= OnTestButtonPressed;
            _testButtonConnected = false;
        }

        if (_context.EndTurnButton != null && _endTurnButtonConnected)
        {
            _context.EndTurnButton.Pressed -= OnEndTurnButtonPressed;
            _endTurnButtonConnected = false;
        }
    }

    private void OnLanguageButtonPressed()
    {
        _context.ToggleLanguage();
    }

    private void OnGodModeButtonPressed()
    {
        _context.ToggleGodMode();
    }

    private void OnTestButtonPressed()
    {
        _context.OpenTestDialog();
    }

    private void OnEndTurnButtonPressed()
    {
        _context.EndTurn();
    }

    private Vector2 ClampPosition(Vector2 position, float width, float height)
    {
        if (_background == null)
        {
            return position;
        }

        var viewportSize = _background.GetViewportRect().Size;
        if (viewportSize == Vector2.Zero)
        {
            return position;
        }

        var maxX = Mathf.Max(MapFrameContentInset, viewportSize.X - width - MapFrameContentInset);
        var maxY = Mathf.Max(TopBarTop, viewportSize.Y - height);
        return new Vector2(
            Mathf.Clamp(position.X, MapFrameContentInset, maxX),
            Mathf.Clamp(position.Y, TopBarTop, maxY));
    }
}
