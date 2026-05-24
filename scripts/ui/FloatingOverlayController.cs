using Godot;

namespace ThreeKingdom.UI;

internal interface IFloatingOverlayContext
{
    Control CreateOverlay(string scenePath, System.Action closeAction);
    void PopupDialog(Control? dialog);
    void CloseOverlay(System.Action closeAction);
    void BringOverlayToFront(CanvasItem? item);
}

internal abstract class FloatingOverlayController
{
    private readonly IFloatingOverlayContext _overlayContext;
    private readonly string _scenePath;
    private bool _chromeSignalsConnected;
    private bool _contentReady;
    private bool _hasPositionInitialized;
    private bool _isDragging;
    private bool _keepCenteredDuringInitialLayout;
    private int _pendingDeferredLayoutPasses;
    private Vector2 _dragOffset = Vector2.Zero;

    protected FloatingOverlayController(IFloatingOverlayContext overlayContext, string scenePath)
    {
        _overlayContext = overlayContext;
        _scenePath = scenePath;
    }

    protected Control? OverlayRoot { get; private set; }
    protected PanelContainer? OverlayPanel { get; private set; }
    protected VBoxContainer? OverlayContentRoot { get; private set; }
    protected Label? OverlayTitleLabel { get; private set; }
    public Control? OverlayControl => OverlayRoot;
    public bool IsOverlayVisible => OverlayRoot?.Visible == true;
    protected virtual bool AutoFitOverlayHeight => false;
    protected virtual bool AutoFitOverlayWidth => false;
    protected virtual Vector2 MinimumOverlaySize => OverlayPanel?.CustomMinimumSize ?? Vector2.Zero;

    protected void InitializeOverlay()
    {
        OverlayRoot = _overlayContext.CreateOverlay(_scenePath, HideOverlay);
        EnsureOverlayReady();
        _hasPositionInitialized = false;
        if (OverlayPanel != null)
        {
            OverlayPanel.Position = Vector2.Zero;
        }
        OverlayRoot.Hide();
    }

    protected void HideOverlay()
    {
        _isDragging = false;
        OverlayRoot?.Hide();
    }

    protected bool EnsureOverlayReady()
    {
        if (OverlayRoot == null)
        {
            return false;
        }

        OverlayRoot.MouseFilter = Control.MouseFilterEnum.Ignore;
        OverlayContentRoot = OverlayRoot.GetNodeOrNull<VBoxContainer>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot");
        OverlayPanel = OverlayRoot.GetNodeOrNull<PanelContainer>("CenterContainer/AdvisorDialogPanel");
        var centerContainer = OverlayRoot.GetNodeOrNull<Control>("CenterContainer");
        var titleBar = OverlayRoot.GetNodeOrNull<Control>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/TitleBarPanel/TitleBar");
        OverlayTitleLabel = OverlayRoot.GetNodeOrNull<Label>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/TitleBarPanel/TitleBar/TitleLabel");
        var closeButton = OverlayRoot.GetNodeOrNull<Button>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/TitleBarPanel/TitleBar/CloseButton");

        if (OverlayContentRoot == null || OverlayPanel == null || centerContainer == null || titleBar == null || closeButton == null)
        {
            GD.PushError($"Floating overlay chrome not found in '{_scenePath}'.");
            return false;
        }

        centerContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
        OverlayPanel.MouseFilter = Control.MouseFilterEnum.Stop;
        OverlayContentRoot.MouseFilter = Control.MouseFilterEnum.Stop;
        titleBar.MouseFilter = Control.MouseFilterEnum.Stop;

        if (!_chromeSignalsConnected)
        {
            closeButton.Pressed += OnClosePressed;
            titleBar.GuiInput += OnTitleBarGuiInput;
            OverlayPanel.GuiInput += OnOverlayPanelGuiInput;
            _chromeSignalsConnected = true;
        }

        if (!_contentReady)
        {
            OnOverlayContentReady(OverlayContentRoot);
            ApplyOverlayInputThemes(OverlayContentRoot);
            _contentReady = true;
        }

        return true;
    }

    protected void ShowOverlay()
    {
        if (!EnsureOverlayReady())
        {
            return;
        }

        _keepCenteredDuringInitialLayout = !_hasPositionInitialized;
        _overlayContext.PopupDialog(OverlayRoot);
        UpdateOverlayLayoutNow();
        _pendingDeferredLayoutPasses = 3;
        Callable.From(RefreshOverlayLayoutDeferred).CallDeferred();
    }

    protected void BringOverlayToFront()
    {
        _overlayContext.BringOverlayToFront(OverlayRoot);
    }

    protected void SetOverlayTitleText(string text)
    {
        if (OverlayTitleLabel != null)
        {
            OverlayTitleLabel.Text = text;
        }
    }

    protected void UpdateOverlayLayoutNow()
    {
        ApplyOverlaySize();
        if (OverlayRoot?.Visible == true)
        {
            EnsureOverlayPosition();
            BringOverlayToFront();
        }
    }

    protected T? GetOverlayContentNode<T>(string path) where T : class
        => OverlayContentRoot?.GetNodeOrNull<T>(path);

    protected void ApplyInputThemeToSubtree(Node root)
    {
        ApplyOverlayInputThemes(root);
    }

    protected abstract void OnOverlayContentReady(VBoxContainer root);
    protected virtual void OnOverlayCloseRequested() => HideOverlay();

    private void OnClosePressed()
    {
        _overlayContext.CloseOverlay(OnOverlayCloseRequested);
    }

    private void EnsureOverlayPosition()
    {
        if (OverlayRoot == null || OverlayPanel == null)
        {
            return;
        }

        var viewportSize = OverlayRoot.GetViewportRect().Size;
        if (viewportSize.X <= 0 || viewportSize.Y <= 0)
        {
            return;
        }

        var panelSize = OverlayPanel.Size;
        if (panelSize.X <= 0 || panelSize.Y <= 0)
        {
            panelSize = OverlayPanel.CustomMinimumSize;
        }

        var current = OverlayPanel.Position;
        var maxX = Mathf.Max(0.0f, viewportSize.X - panelSize.X);
        var maxY = Mathf.Max(0.0f, viewportSize.Y - panelSize.Y);

        if (_keepCenteredDuringInitialLayout || !_hasPositionInitialized || current.X > maxX || current.Y > maxY)
        {
            OverlayPanel.Position = new Vector2(
                Mathf.Max(0.0f, (viewportSize.X - panelSize.X) * 0.5f),
                Mathf.Max(0.0f, (viewportSize.Y - panelSize.Y) * 0.5f));
            _hasPositionInitialized = true;
            return;
        }

        OverlayPanel.Position = new Vector2(
            Mathf.Clamp(current.X, 0.0f, maxX),
            Mathf.Clamp(current.Y, 0.0f, maxY));
    }

    private void ApplyOverlaySize()
    {
        if (OverlayPanel == null || OverlayContentRoot == null)
        {
            return;
        }

        var minimumSize = MinimumOverlaySize;
        var currentSize = OverlayPanel.Size;
        var targetWidth = minimumSize.X > 0.0f
            ? minimumSize.X
            : currentSize.X;
        var targetHeight = minimumSize.Y > 0.0f
            ? minimumSize.Y
            : currentSize.Y;

        if (AutoFitOverlayWidth || AutoFitOverlayHeight)
        {
            var contentSize = OverlayContentRoot.GetCombinedMinimumSize();
            var style = OverlayPanel.GetThemeStylebox("panel");
            var horizontalMargins = style?.GetMargin(Side.Left) + style?.GetMargin(Side.Right) ?? 0.0f;
            var verticalMargins = style?.GetMargin(Side.Top) + style?.GetMargin(Side.Bottom) ?? 0.0f;

            if (AutoFitOverlayWidth)
            {
                targetWidth = Mathf.Max(minimumSize.X, contentSize.X + horizontalMargins);
            }

            if (AutoFitOverlayHeight)
            {
                targetHeight = Mathf.Max(minimumSize.Y, contentSize.Y + verticalMargins);
            }
        }

        OverlayPanel.CustomMinimumSize = new Vector2(targetWidth, targetHeight);
        OverlayPanel.Size = new Vector2(targetWidth, targetHeight);
    }

    private void RefreshOverlayLayoutDeferred()
    {
        if (OverlayRoot == null || !OverlayRoot.Visible || _pendingDeferredLayoutPasses <= 0)
        {
            return;
        }

        _pendingDeferredLayoutPasses -= 1;
        UpdateOverlayLayoutNow();

        if (_pendingDeferredLayoutPasses > 0)
        {
            Callable.From(RefreshOverlayLayoutDeferred).CallDeferred();
            return;
        }

        _keepCenteredDuringInitialLayout = false;
    }

    private static void ApplyOverlayInputThemes(Node root)
    {
        foreach (var optionButton in EnumerateDescendants<OptionButton>(root))
        {
            ApplyOptionButtonTheme(optionButton);
        }

        foreach (var spinBox in EnumerateDescendants<SpinBox>(root))
        {
            ApplySpinBoxTheme(spinBox);
        }

        foreach (var lineEdit in EnumerateDescendants<LineEdit>(root))
        {
            ApplyLineEditTheme(lineEdit);
        }

        foreach (var tree in EnumerateDescendants<Tree>(root))
        {
            ApplyTreeTheme(tree);
        }
    }

    private static void ApplyOptionButtonTheme(OptionButton optionButton)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.14f, 0.92f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.62f, 0.53f, 0.36f, 0.95f),
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            ContentMarginLeft = 8.0f,
            ContentMarginTop = 4.0f,
            ContentMarginRight = 8.0f,
            ContentMarginBottom = 4.0f
        };
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BorderColor = new Color(0.78f, 0.68f, 0.47f, 1.0f);
        hover.BgColor = new Color(0.15f, 0.15f, 0.17f, 0.96f);
        var pressed = (StyleBoxFlat)normal.Duplicate();
        pressed.BorderColor = new Color(0.83f, 0.72f, 0.5f, 1.0f);
        pressed.BgColor = new Color(0.18f, 0.17f, 0.14f, 0.98f);
        var disabled = (StyleBoxFlat)normal.Duplicate();
        disabled.BorderColor = new Color(0.42f, 0.39f, 0.33f, 0.9f);
        disabled.BgColor = new Color(0.1f, 0.1f, 0.11f, 0.78f);
        var focus = (StyleBoxFlat)hover.Duplicate();
        focus.BorderColor = new Color(0.88f, 0.76f, 0.53f, 1.0f);

        optionButton.AddThemeStyleboxOverride("normal", normal);
        optionButton.AddThemeStyleboxOverride("hover", hover);
        optionButton.AddThemeStyleboxOverride("pressed", pressed);
        optionButton.AddThemeStyleboxOverride("disabled", disabled);
        optionButton.AddThemeStyleboxOverride("focus", focus);
        optionButton.AddThemeColorOverride("font_color", new Color(0.93f, 0.89f, 0.82f, 1.0f));
        optionButton.AddThemeColorOverride("font_hover_color", Colors.White);
        optionButton.AddThemeColorOverride("font_pressed_color", Colors.White);
        optionButton.AddThemeColorOverride("font_disabled_color", new Color(0.68f, 0.64f, 0.57f, 1.0f));
        optionButton.AddThemeColorOverride("font_focus_color", Colors.White);
        optionButton.Modulate = Colors.White;
    }

    private static void ApplySpinBoxTheme(SpinBox spinBox)
    {
        var lineEdit = spinBox.GetLineEdit();
        if (lineEdit != null)
        {
            ApplyLineEditTheme(lineEdit);
        }

        spinBox.CustomMinimumSize = new Vector2(Mathf.Max(spinBox.CustomMinimumSize.X, 96.0f), spinBox.CustomMinimumSize.Y);
        foreach (Node child in spinBox.GetChildren())
        {
            if (child is Button button)
            {
                ApplySpinBoxButtonTheme(button);
            }
        }
    }

    private static void ApplySpinBoxButtonTheme(Button button)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.14f, 0.96f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.62f, 0.53f, 0.36f, 0.95f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color(0.18f, 0.17f, 0.14f, 0.98f);
        hover.BorderColor = new Color(0.82f, 0.7f, 0.46f, 1.0f);
        var pressed = (StyleBoxFlat)hover.Duplicate();
        pressed.BgColor = new Color(0.24f, 0.21f, 0.16f, 1.0f);

        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f, 1.0f));
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
    }

    private static void ApplyLineEditTheme(LineEdit lineEdit)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.12f, 0.94f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.62f, 0.53f, 0.36f, 0.95f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8.0f,
            ContentMarginTop = 4.0f,
            ContentMarginRight = 8.0f,
            ContentMarginBottom = 4.0f
        };
        var focus = (StyleBoxFlat)normal.Duplicate();
        focus.BorderColor = new Color(0.88f, 0.76f, 0.53f, 1.0f);
        focus.BgColor = new Color(0.12f, 0.12f, 0.14f, 0.98f);
        var readOnly = (StyleBoxFlat)normal.Duplicate();
        readOnly.BorderColor = new Color(0.46f, 0.43f, 0.35f, 0.9f);
        readOnly.BgColor = new Color(0.08f, 0.08f, 0.1f, 0.84f);

        lineEdit.AddThemeStyleboxOverride("normal", normal);
        lineEdit.AddThemeStyleboxOverride("focus", focus);
        lineEdit.AddThemeStyleboxOverride("read_only", readOnly);
        lineEdit.AddThemeColorOverride("font_color", new Color(0.95f, 0.91f, 0.84f, 1.0f));
        lineEdit.AddThemeColorOverride("font_selected_color", Colors.White);
        lineEdit.AddThemeColorOverride("selection_color", new Color(0.58f, 0.45f, 0.24f, 0.92f));
        lineEdit.AddThemeColorOverride("caret_color", new Color(0.95f, 0.82f, 0.56f, 1.0f));
        lineEdit.AddThemeColorOverride("font_placeholder_color", new Color(0.72f, 0.68f, 0.61f, 0.8f));
    }

    private static void ApplyTreeTheme(Tree tree)
    {
        var panel = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.1f, 0.9f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.44f, 0.37f, 0.25f, 0.95f)
        };
        var focus = (StyleBoxFlat)panel.Duplicate();
        focus.BorderColor = new Color(0.85f, 0.72f, 0.48f, 1.0f);
        var selected = new StyleBoxFlat
        {
            BgColor = new Color(0.46f, 0.38f, 0.24f, 0.94f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.67f, 0.53f, 0.28f, 1.0f)
        };
        var selectedFocus = (StyleBoxFlat)selected.Duplicate();
        selectedFocus.BgColor = new Color(0.56f, 0.46f, 0.28f, 0.96f);
        var titleNormal = new StyleBoxFlat
        {
            BgColor = new Color(0.14f, 0.13f, 0.11f, 0.98f),
            BorderWidthBottom = 1,
            BorderColor = new Color(0.48f, 0.39f, 0.24f, 0.96f)
        };
        var titleHover = (StyleBoxFlat)titleNormal.Duplicate();
        titleHover.BgColor = new Color(0.19f, 0.17f, 0.14f, 1.0f);
        var titlePressed = (StyleBoxFlat)titleNormal.Duplicate();
        titlePressed.BgColor = new Color(0.28f, 0.23f, 0.16f, 1.0f);

        tree.AddThemeStyleboxOverride("panel", panel);
        tree.AddThemeStyleboxOverride("focus", focus);
        tree.AddThemeStyleboxOverride("selected", selected);
        tree.AddThemeStyleboxOverride("selected_focus", selectedFocus);
        tree.AddThemeStyleboxOverride("title_button_normal", titleNormal);
        tree.AddThemeStyleboxOverride("title_button_hover", titleHover);
        tree.AddThemeStyleboxOverride("title_button_pressed", titlePressed);
        tree.AddThemeColorOverride("font_color", new Color(0.95f, 0.91f, 0.84f, 1.0f));
        tree.AddThemeColorOverride("font_hovered_color", Colors.White);
        tree.AddThemeColorOverride("font_selected_color", Colors.White);
        tree.AddThemeColorOverride("font_hovered_selected_color", Colors.White);
        tree.AddThemeColorOverride("title_button_color", new Color(0.95f, 0.91f, 0.84f, 1.0f));
        tree.AddThemeColorOverride("title_button_hover_color", Colors.White);
        tree.AddThemeColorOverride("title_button_pressed_color", Colors.White);
        tree.AddThemeColorOverride("guide_color", new Color(0.44f, 0.37f, 0.25f, 0.55f));
    }

    private static System.Collections.Generic.IEnumerable<T> EnumerateDescendants<T>(Node root) where T : class
    {
        if (root is T rootMatch)
        {
            yield return rootMatch;
        }

        foreach (Node child in root.GetChildren())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in EnumerateDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (OverlayRoot == null || OverlayPanel == null)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                BringOverlayToFront();
                _isDragging = true;
                _keepCenteredDuringInitialLayout = false;
                _hasPositionInitialized = true;
                _dragOffset = mouseButton.GlobalPosition - OverlayPanel.GlobalPosition;
            }
            else
            {
                _isDragging = false;
            }

            return;
        }

        if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            var viewportSize = OverlayRoot.GetViewportRect().Size;
            var panelSize = OverlayPanel.Size;
            var target = mouseMotion.GlobalPosition - _dragOffset;
            OverlayPanel.Position = new Vector2(
                Mathf.Clamp(target.X, 0.0f, Mathf.Max(0.0f, viewportSize.X - panelSize.X)),
                Mathf.Clamp(target.Y, 0.0f, Mathf.Max(0.0f, viewportSize.Y - panelSize.Y)));
        }
    }

    private void OnOverlayPanelGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.ButtonIndex == MouseButton.Left &&
            mouseButton.Pressed)
        {
            BringOverlayToFront();
            OverlayPanel?.AcceptEvent();
            return;
        }

        if (@event is InputEventMouseMotion)
        {
            OverlayPanel?.AcceptEvent();
        }
    }
}
