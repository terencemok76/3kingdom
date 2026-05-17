using Godot;

namespace ThreeKingdom.UI;

internal enum SaveLoadConfirmActionKind
{
    None,
    Save,
    Load
}

internal sealed class SaveLoadConfirmDialogController
{
    private readonly SystemUiContext _context;
    private Window? _dialog;
    private Label? _confirmLabel;
    private Button? _yesButton;
    private Button? _noButton;
    private SaveLoadConfirmActionKind _pendingAction = SaveLoadConfirmActionKind.None;
    private int _pendingSlotNumber;
    private System.Action? _pendingConfirmedAction;
    private bool _signalsConnected;

    public SaveLoadConfirmDialogController(SystemUiContext context)
    {
        _context = context;
    }

    public void Initialize()
    {
        _dialog = _context.SaveLoadConfirmDialog;
        EnsureWidgets();
    }

    public void Hide() => _dialog?.Hide();

    public void Cancel()
    {
        _pendingConfirmedAction = null;
        _pendingAction = SaveLoadConfirmActionKind.None;
        _dialog?.Hide();
    }

    public void RefreshText()
    {
        if (_dialog == null)
        {
            return;
        }

        _dialog.Title = _context.GetSaveLoadConfirmTitle();
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
        _context.PopupDialog(_dialog);
    }

    private void EnsureWidgets()
    {
        var root = _dialog?.GetNodeOrNull<VBoxContainer>("ConfirmRoot");
        if (root == null)
        {
            return;
        }

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
        _dialog?.Hide();
        _pendingConfirmedAction?.Invoke();
        _pendingConfirmedAction = null;
        _pendingAction = SaveLoadConfirmActionKind.None;
    }

    private void OnCanceled()
    {
        Cancel();
    }
}
