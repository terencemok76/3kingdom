using Godot;

namespace ThreeKingdom.UI;

internal enum SaveLoadConfirmActionKind
{
    None,
    Save,
    Load
}

internal sealed class SaveLoadConfirmDialogController : FloatingOverlayController
{
    private readonly SystemUiContext _context;
    private Label? _confirmLabel;
    private Button? _yesButton;
    private Button? _noButton;
    private SaveLoadConfirmActionKind _pendingAction = SaveLoadConfirmActionKind.None;
    private int _pendingSlotNumber;
    private System.Action? _pendingConfirmedAction;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(380.0f, 180.0f);

    public SaveLoadConfirmDialogController(SystemUiContext context)
        : base(context, "res://scenes/ui/system/SaveLoadConfirmDialog.tscn")
    {
        _context = context;
    }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public void Cancel()
    {
        _pendingConfirmedAction = null;
        _pendingAction = SaveLoadConfirmActionKind.None;
        HideOverlay();
    }

    public void RefreshText()
    {
        if (!EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.GetSaveLoadConfirmTitle());
        if (_yesButton != null)
        {
            _yesButton.Text = _context.GetConfirmYesText();
        }

        if (_noButton != null)
        {
            _noButton.Text = _context.GetConfirmNoText();
        }

        if (_confirmLabel != null)
        {
            _confirmLabel.Text = _pendingAction switch
            {
                SaveLoadConfirmActionKind.Save => _context.GetSaveConfirmMessage(_pendingSlotNumber),
                SaveLoadConfirmActionKind.Load => _context.GetLoadConfirmMessage(_pendingSlotNumber),
                _ => _confirmLabel.Text
            };
        }
    }

    public void ShowSaveConfirmation(int slotNumber, System.Action confirmedAction)
    {
        Show(SaveLoadConfirmActionKind.Save, slotNumber, confirmedAction);
    }

    public void ShowLoadConfirmation(int slotNumber, System.Action confirmedAction)
    {
        Show(SaveLoadConfirmActionKind.Load, slotNumber, confirmedAction);
    }

    private void Show(SaveLoadConfirmActionKind action, int slotNumber, System.Action confirmedAction)
    {
        _pendingAction = action;
        _pendingSlotNumber = slotNumber;
        _pendingConfirmedAction = confirmedAction;
        RefreshText();
        ShowOverlay();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _confirmLabel = root.GetNodeOrNull<Label>("ConfirmLabel");
        _yesButton = root.GetNodeOrNull<Button>("ButtonRow/ConfirmYesButton");
        _noButton = root.GetNodeOrNull<Button>("ButtonRow/ConfirmNoButton");
        if (_yesButton != null)
        {
            _context.ApplyButtonTheme(_yesButton);
        }
        if (_noButton != null)
        {
            _context.ApplyButtonTheme(_noButton);
        }
        ConnectSignals();
    }

    private void ConnectSignals()
    {
        if (_signalsConnected)
        {
            return;
        }

        if (_yesButton != null)
        {
            _yesButton.Pressed += OnConfirmed;
        }
        if (_noButton != null)
        {
            _noButton.Pressed += OnCanceled;
        }
        _signalsConnected = true;
    }

    private void OnConfirmed()
    {
        HideOverlay();
        _pendingConfirmedAction?.Invoke();
        _pendingConfirmedAction = null;
        _pendingAction = SaveLoadConfirmActionKind.None;
    }

    private void OnCanceled()
    {
        Cancel();
    }
}
