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
    protected virtual bool AutoFitOverlayHeight => false;
    protected virtual bool AutoFitOverlayWidth => false;
    protected virtual Vector2 MinimumOverlaySize => OverlayPanel?.CustomMinimumSize ?? Vector2.Zero;

    protected void InitializeOverlay()
    {
        OverlayRoot = _overlayContext.CreateOverlay(_scenePath, HideOverlay);
        EnsureOverlayReady();
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

        OverlayContentRoot = OverlayRoot.GetNodeOrNull<VBoxContainer>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot");
        OverlayPanel = OverlayRoot.GetNodeOrNull<PanelContainer>("CenterContainer/AdvisorDialogPanel");
        var titleBar = OverlayRoot.GetNodeOrNull<Control>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/TitleBarPanel/TitleBar");
        OverlayTitleLabel = OverlayRoot.GetNodeOrNull<Label>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/TitleBarPanel/TitleBar/TitleLabel");
        var closeButton = OverlayRoot.GetNodeOrNull<Button>("CenterContainer/AdvisorDialogPanel/AdvisorDialogRoot/TitleBarPanel/TitleBar/CloseButton");

        if (OverlayContentRoot == null || OverlayPanel == null || titleBar == null || closeButton == null)
        {
            GD.PushError($"Floating overlay chrome not found in '{_scenePath}'.");
            return false;
        }

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

        _overlayContext.PopupDialog(OverlayRoot);
        ApplyOverlaySize();
        EnsureOverlayPosition();
        BringOverlayToFront();
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

    protected T? GetOverlayContentNode<T>(string path) where T : class
        => OverlayContentRoot?.GetNodeOrNull<T>(path);

    protected abstract void OnOverlayContentReady(VBoxContainer root);

    private void OnClosePressed()
    {
        _overlayContext.CloseOverlay(HideOverlay);
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

        if (!_hasPositionInitialized || current.X > maxX || current.Y > maxY)
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
        ApplyOverlaySize();
        EnsureOverlayPosition();
        BringOverlayToFront();

        if (_pendingDeferredLayoutPasses > 0)
        {
            Callable.From(RefreshOverlayLayoutDeferred).CallDeferred();
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
        }
    }
}
