namespace ThreeKingdom.UI;

public sealed class PersonnelUiController
{
    private readonly PersonnelUiContext _context;
    private readonly PersonnelCommandDialogController _commandDialogController;
    private readonly PersonnelBonusDialogController _bonusDialogController;
    private readonly AssignRoleDialogController _assignRoleDialogController;
    private readonly PrefectAuthorizationDialogController _prefectAuthorizationDialogController;
    private readonly FireOfficerDialogController _fireOfficerDialogController;
    private readonly RequestItemDialogController _requestItemDialogController;
    private readonly HireOfficerDialogController _hireOfficerDialogController;
    private readonly SuccessionDialogController _successionDialogController;

    public PersonnelUiController(HudController owner)
    {
        _context = new PersonnelUiContext(owner);
        _bonusDialogController = new PersonnelBonusDialogController(_context);
        _assignRoleDialogController = new AssignRoleDialogController(_context);
        _prefectAuthorizationDialogController = new PrefectAuthorizationDialogController(_context);
        _fireOfficerDialogController = new FireOfficerDialogController(_context);
        _requestItemDialogController = new RequestItemDialogController(_context);
        _hireOfficerDialogController = new HireOfficerDialogController(_context);
        _successionDialogController = new SuccessionDialogController(_context);
        _commandDialogController = new PersonnelCommandDialogController(
            _context,
            _bonusDialogController.Show,
            _assignRoleDialogController.Show,
            _prefectAuthorizationDialogController.Show,
            _fireOfficerDialogController.Show,
            _requestItemDialogController.Show,
            _hireOfficerDialogController.Show);
    }

    public int PendingSuccessionFactionId
    {
        get => _successionDialogController.PendingFactionId;
        set => _successionDialogController.PendingFactionId = value;
    }

    public void Initialize()
    {
        _commandDialogController.Initialize();
        _bonusDialogController.Initialize();
        _assignRoleDialogController.Initialize();
        _prefectAuthorizationDialogController.Initialize();
        _fireOfficerDialogController.Initialize();
        _requestItemDialogController.Initialize();
        _hireOfficerDialogController.Initialize();
        _successionDialogController.Initialize();
    }

    public void HideDialogs()
    {
        _commandDialogController.Hide();
        _bonusDialogController.Hide();
        _assignRoleDialogController.Hide();
        _prefectAuthorizationDialogController.Hide();
        _fireOfficerDialogController.Hide();
        _requestItemDialogController.Hide();
        _hireOfficerDialogController.Hide();
        _successionDialogController.Hide();
    }

    public void RefreshText()
    {
        _commandDialogController.RefreshText();
        _bonusDialogController.RefreshText();
        _assignRoleDialogController.RefreshText();
        _prefectAuthorizationDialogController.RefreshText();
        _fireOfficerDialogController.RefreshText();
        _requestItemDialogController.RefreshText();
        _hireOfficerDialogController.RefreshText();
    }

    public bool HasPendingPlayerSuccession() => _successionDialogController.HasPendingPlayerSuccession();

    public void ShowPersonnelDialog() => _commandDialogController.Show();

    public void ShowSuccessionDialog() => _successionDialogController.Show();
}
