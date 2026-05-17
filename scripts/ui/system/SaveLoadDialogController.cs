using Godot;

namespace ThreeKingdom.UI;

internal sealed class SaveLoadDialogController
{
    private const int SaveSlotCount = 10;

    private readonly SystemUiContext _context;
    private readonly SaveLoadConfirmDialogController _confirmDialogController;
    private Window? _dialog;
    private ItemList? _slotList;
    private LineEdit? _descriptionLineEdit;
    private RichTextLabel? _summaryLabel;
    private Button? _saveButton;
    private Button? _loadButton;
    private Button? _closeButton;
    private int _selectedSlotIndex;
    private bool _signalsConnected;

    public SaveLoadDialogController(SystemUiContext context, SaveLoadConfirmDialogController confirmDialogController)
    {
        _context = context;
        _confirmDialogController = confirmDialogController;
    }

    public void Initialize()
    {
        _dialog = _context.SaveLoadDialog;
        EnsureWidgets();
    }

    public void Hide() => _dialog?.Hide();

    public void Show()
    {
        PopulateSaveSlotList();
        RefreshText();
        _context.PopupDialog(_dialog);
    }

    public void RefreshText()
    {
        if (_dialog == null)
        {
            return;
        }

        _dialog.Title = _context.GetSaveLoadDialogTitle();
        SetLabelText("SlotListLabel", _context.GetSaveSlotListLabel());
        SetLabelText("DescriptionLabel", _context.GetSaveDescriptionLabel());
        SetLabelText("SummaryTitleLabel", _context.GetSaveSummaryLabel());

        if (_descriptionLineEdit != null)
        {
            _descriptionLineEdit.PlaceholderText = _context.GetSaveDescriptionPlaceholder();
        }

        if (_saveButton != null)
        {
            _saveButton.Text = _context.GetSaveButtonText();
        }

        if (_loadButton != null)
        {
            _loadButton.Text = _context.GetLoadButtonText();
        }

        if (_closeButton != null)
        {
            _closeButton.Text = _context.GetCloseButtonText();
        }

        RefreshSelectedSaveSlotSummary();
    }

    private void EnsureWidgets()
    {
        var root = _dialog?.GetNodeOrNull<VBoxContainer>("SaveLoadDialogRoot");
        if (root == null)
        {
            return;
        }

        _slotList = root.GetNodeOrNull<ItemList>("SlotList");
        _descriptionLineEdit = root.GetNodeOrNull<LineEdit>("DescriptionLineEdit");
        _summaryLabel = root.GetNodeOrNull<RichTextLabel>("SummaryLabel");
        _saveButton = root.GetNodeOrNull<Button>("ButtonRow/SaveSlotButton");
        _loadButton = root.GetNodeOrNull<Button>("ButtonRow/LoadSlotButton");
        _closeButton = root.GetNodeOrNull<Button>("ButtonRow/CloseSlotButton");

        if (_saveButton != null)
        {
            _context.ApplyButtonTheme(_saveButton);
        }
        if (_loadButton != null)
        {
            _context.ApplyButtonTheme(_loadButton);
        }
        if (_closeButton != null)
        {
            _context.ApplyButtonTheme(_closeButton);
        }
        ConnectSignals();
    }

    private void ConnectSignals()
    {
        if (_signalsConnected)
        {
            return;
        }

        if (_slotList != null)
        {
            _slotList.ItemSelected += OnSlotSelected;
        }
        if (_saveButton != null)
        {
            _saveButton.Pressed += OnSavePressed;
        }
        if (_loadButton != null)
        {
            _loadButton.Pressed += OnLoadPressed;
        }
        if (_closeButton != null)
        {
            _closeButton.Pressed += OnClosePressed;
        }
        _signalsConnected = true;
    }

    private void OnSlotSelected(long index)
    {
        _selectedSlotIndex = (int)index;
        RefreshSelectedSaveSlotSummary();
    }

    private void OnSavePressed()
    {
        _confirmDialogController.ShowSaveConfirmation(_selectedSlotIndex + 1, PerformSave);
    }

    private void OnLoadPressed()
    {
        _confirmDialogController.ShowLoadConfirmation(_selectedSlotIndex + 1, PerformLoad);
    }

    private void OnClosePressed()
    {
        _dialog?.Hide();
    }

    private void PerformSave()
    {
        if (_context.WorldRepository == null || _context.TurnManager?.World == null || _descriptionLineEdit == null)
        {
            return;
        }

        var slotNumber = _selectedSlotIndex + 1;
        var description = _descriptionLineEdit.Text?.Trim() ?? string.Empty;
        var saved = _context.WorldRepository.SaveGame(_context.BuildSaveSlotPath(slotNumber), _context.TurnManager.World, description, slotNumber);
        PopulateSaveSlotList();
        SelectSaveSlot(slotNumber - 1);
        _context.AddLog(saved ? _context.GetSaveSlotSavedMessage(slotNumber) : _context.GetSaveSlotSaveFailedMessage(slotNumber), isPlayerRelated: true);
    }

    private void PerformLoad()
    {
        if (_context.WorldRepository == null)
        {
            return;
        }

        var slotNumber = _selectedSlotIndex + 1;
        var loadedWorld = _context.WorldRepository.LoadSavedGame(_context.BuildSaveSlotPath(slotNumber));
        if (loadedWorld == null)
        {
            _context.AddLog(_context.GetSaveSlotMissingMessage(slotNumber), isPlayerRelated: true);
            return;
        }

        _context.ApplyLoadedWorld(loadedWorld);
        PopulateSaveSlotList();
        SelectSaveSlot(slotNumber - 1);
        _context.AddLog(_context.GetSaveSlotLoadedMessage(slotNumber), isPlayerRelated: true);
    }

    private void PopulateSaveSlotList()
    {
        if (_slotList == null || _context.WorldRepository == null)
        {
            return;
        }

        var previousIndex = _selectedSlotIndex;
        _slotList.Clear();
        for (var slotNumber = 1; slotNumber <= SaveSlotCount; slotNumber += 1)
        {
            var summary = _context.WorldRepository.LoadSaveSlotSummary(_context.BuildSaveSlotPath(slotNumber), slotNumber);
            var itemIndex = _slotList.ItemCount;
            _slotList.AddItem(_context.BuildSaveSlotListText(summary));
            _slotList.SetItemMetadata(itemIndex, slotNumber);
        }

        SelectSaveSlot(previousIndex);
    }

    private void SelectSaveSlot(int index)
    {
        if (_slotList == null || _slotList.ItemCount == 0)
        {
            return;
        }

        if (index < 0 || index >= _slotList.ItemCount)
        {
            index = 0;
        }

        _selectedSlotIndex = index;
        _slotList.Select(index);
        RefreshSelectedSaveSlotSummary();
    }

    private void RefreshSelectedSaveSlotSummary()
    {
        if (_summaryLabel == null || _descriptionLineEdit == null || _context.WorldRepository == null)
        {
            return;
        }

        var slotNumber = _selectedSlotIndex + 1;
        var summary = _context.WorldRepository.LoadSaveSlotSummary(_context.BuildSaveSlotPath(slotNumber), slotNumber);
        _descriptionLineEdit.Text = summary.Exists ? summary.Description : string.Empty;
        _summaryLabel.Text = _context.BuildSaveSlotSummaryText(summary);
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = _dialog?.GetNodeOrNull<Label>($"SaveLoadDialogRoot/{nodeName}");
        if (label != null)
        {
            label.Text = text;
        }
    }
}
