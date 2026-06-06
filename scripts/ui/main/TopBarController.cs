using Godot;
using ThreeKingdom.Core;

namespace ThreeKingdom.UI;

internal sealed class TopBarController
{
    private readonly MainHudUiContext _context;
    private const float FloatingPanelViewportMargin = 8.0f;
    private const float FloatingPanelTopClamp = 8.0f;
    private const float TopBarWidth = 720.0f;
    private const float TopBarHeight = 42.0f;

    private ColorRect? _background;
    private HBoxContainer? _content;
    private Vector2 _defaultPosition;
    private Vector2 _position;
    private Vector2 _size;
    private readonly Vector2 _contentOffset = new(12.0f, 6.0f);
    private Vector2 _contentSize;
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

        _defaultPosition = _background.Position;
        _position = _background.Position;
        var viewportSize = _background.GetViewportRect().Size;
        _size = new Vector2(Mathf.Min(TopBarWidth, viewportSize.X - (FloatingPanelViewportMargin * 2.0f)), TopBarHeight);
        if (_size.X < 520.0f)
        {
            _size.X = Mathf.Max(420.0f, _background.Size.X);
        }

        _contentSize = new Vector2(_size.X - 24.0f, 28.0f);
        _background.Color = new Color(0.06f, 0.06f, 0.08f, 0.9f);
        _background.MouseFilter = Control.MouseFilterEnum.Pass;
        _content.MouseFilter = Control.MouseFilterEnum.Pass;
        _content.AddThemeConstantOverride("separation", 10);
        ApplyButtonThemes();
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
            var factionName = localization.GetFactionName(world, _context.PlayerFactionId);
            _context.PlayerFactionLabel.Text = localization.FormatPlayerFaction(factionName);
        }

        if (_context.StoryLabel != null)
        {
            _context.StoryLabel.Text = localization.IsTraditionalChinese
                ? (!string.IsNullOrWhiteSpace(world.StoryNameZhHant) ? world.StoryNameZhHant : world.StoryNameEn)
                : (!string.IsNullOrWhiteSpace(world.StoryNameEn) ? world.StoryNameEn : world.StoryNameZhHant);
        }

        if (_context.EndTurnButton != null)
        {
            _context.EndTurnButton.Text = localization.T("ui.end_turn");
        }

        if (_context.GodModeButton != null)
        {
            _context.GodModeButton.Text = _context.BuildGodModeButtonText();
        }

        if (_context.TestButton != null)
        {
            _context.TestButton.Visible = _context.IsGodModeEnabled();
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
        if (_context.MonthLabel == null || localization == null || world == null)
        {
            return;
        }

        _context.MonthLabel.Text = localization.FormatYearMonth(world.Year, world.Month);
    }

    public Vector2 GetPosition() => _position;

    public Vector2 GetSize() => _size;

    public void SetDraggedPosition(Vector2 targetPosition)
    {
        _position = ClampPosition(targetPosition, _size.X, _size.Y);
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
    }

    public void BringToFront()
    {
        _context.MoveToFront(_background);
        _context.MoveToFront(_content);
    }

    public void ApplyLoadedSettings(float x, float y)
    {
        _position = new Vector2(x, y);
    }

    public void RestoreDefaultLayout()
    {
        _position = _defaultPosition;
        ApplyLayout();
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
        ApplySharedButtonTheme(_context.TestButton);
        ApplySharedButtonTheme(_context.EndTurnButton);
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

        target.CustomMinimumSize = source.CustomMinimumSize;
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

        var maxX = Mathf.Max(FloatingPanelViewportMargin, viewportSize.X - width - FloatingPanelViewportMargin);
        var maxY = Mathf.Max(FloatingPanelTopClamp, viewportSize.Y - height - FloatingPanelViewportMargin);
        return new Vector2(
            Mathf.Clamp(position.X, FloatingPanelViewportMargin, maxX),
            Mathf.Clamp(position.Y, FloatingPanelTopClamp, maxY));
    }
}
