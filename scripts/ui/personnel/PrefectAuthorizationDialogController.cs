using System.Linq;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class PrefectAuthorizationDialogController : FloatingOverlayController
{
    private readonly PersonnelUiContext _context;
    private OptionButton? _authorizationOption;
    private Label? _summaryLabel;
    private Button? _confirmButton;
    private bool _signalsConnected;
    protected override Vector2 MinimumOverlaySize => new(500.0f, 220.0f);

    public PrefectAuthorizationDialogController(PersonnelUiContext context)
        : base(context, "res://scenes/ui/personnel/PrefectAuthorizationDialog.tscn")
    {
        _context = context;
    }

    public void Initialize()
    {
        InitializeOverlay();
    }

    public void Hide() => HideOverlay();

    public void Show()
    {
        if (_context.SelectedCity == null || _context.TurnManager?.World == null || _context.Localization == null)
        {
            return;
        }

        Populate();
        RefreshText();
        ShowOverlay();
    }

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization.T("command.personnel.prefect_authorization"));
        SetLabelText("AuthorizationLabel", _context.Localization.T("ui.prefect_authorization_mode"));
        if (_confirmButton != null)
        {
            _confirmButton.Text = _context.Localization.T("ui.confirm_personnel");
        }

        UpdateSummary();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _authorizationOption = root.GetNodeOrNull<OptionButton>("AuthorizationOption");
        _summaryLabel = root.GetNodeOrNull<Label>("SummaryLabel");
        _confirmButton = root.GetNodeOrNull<Button>("ConfirmRow/ConfirmButton");
        if (_confirmButton != null)
        {
            _context.ApplyCommandButtonTheme(_confirmButton);
        }

        if (!_signalsConnected)
        {
            if (_authorizationOption != null)
            {
                _authorizationOption.ItemSelected += _ => UpdateSummary();
            }

            if (_confirmButton != null)
            {
                _confirmButton.Pressed += OnConfirmPressed;
            }

            _signalsConnected = true;
        }
    }

    private void Populate()
    {
        if (_authorizationOption == null || _context.Localization == null)
        {
            return;
        }

        _authorizationOption.Clear();
        AddAuthorizationOption(PrefectAuthorizationType.None);
        AddAuthorizationOption(PrefectAuthorizationType.Half);
        AddAuthorizationOption(PrefectAuthorizationType.Full);

        var city = _context.SelectedCity;
        var selectedIndex = 0;
        if (city != null)
        {
            selectedIndex = city.PrefectAuthorizationType switch
            {
                PrefectAuthorizationType.Half => 1,
                PrefectAuthorizationType.Full => 2,
                _ => 0
            };
        }

        if (_authorizationOption.ItemCount > selectedIndex)
        {
            _authorizationOption.Select(selectedIndex);
        }

        UpdateSummary();
    }

    private void AddAuthorizationOption(PrefectAuthorizationType authorizationType)
    {
        if (_authorizationOption == null || _context.Localization == null)
        {
            return;
        }

        _authorizationOption.AddItem(GetAuthorizationDisplayName(authorizationType));
        _authorizationOption.SetItemMetadata(_authorizationOption.ItemCount - 1, (int)authorizationType);
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>(nodeName);
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void OnConfirmPressed()
    {
        var city = _context.SelectedCity;
        var turnManager = _context.TurnManager;
        var commandResolver = _context.CommandResolver;
        if (city == null || turnManager == null || commandResolver == null)
        {
            return;
        }

        var result = commandResolver.ExecuteAuthorizePrefect(
            turnManager.GetPlayerFactionId(),
            city.Id,
            GetSelectedAuthorizationType());
        _context.AddLog(_context.GetLocalizedResultMessage(result), isPlayerRelated: true);
        if (result.Success)
        {
            _context.UiEventHub.PublishCityStateChanged(city.Id, city.OwnerFactionId);
        }

        HideOverlay();
    }

    private void UpdateSummary()
    {
        if (_summaryLabel == null || _context.Localization == null)
        {
            return;
        }

        var city = _context.SelectedCity;
        var world = _context.TurnManager?.World;
        if (city == null || world == null)
        {
            _summaryLabel.Text = string.Empty;
            return;
        }

        var prefect = GetCityPrefect(world, city);
        var prefectName = prefect != null
            ? _context.Localization.GetOfficerName(prefect)
            : _context.Localization.T("ui.unassigned");

        var currentMode = GetAuthorizationDisplayName(city.PrefectAuthorizationType);
        var selectedMode = GetAuthorizationDisplayName(GetSelectedAuthorizationType());

        _summaryLabel.Text =
            $"{_context.Localization.T("ui.prefect_authorization_prefect")}: {prefectName}\n" +
            $"{_context.Localization.T("ui.prefect_authorization_current")}: {currentMode}\n" +
            $"{_context.Localization.T("ui.prefect_authorization_pending")}: {selectedMode}";
    }

    private PrefectAuthorizationType GetSelectedAuthorizationType()
    {
        if (_authorizationOption == null || _authorizationOption.ItemCount == 0 || _authorizationOption.Selected < 0)
        {
            return PrefectAuthorizationType.None;
        }

        var metadata = _authorizationOption.GetItemMetadata(_authorizationOption.Selected);
        if (metadata.VariantType == Variant.Type.Int)
        {
            return (PrefectAuthorizationType)metadata.AsInt32();
        }

        return PrefectAuthorizationType.None;
    }

    private string GetAuthorizationDisplayName(PrefectAuthorizationType authorizationType)
    {
        if (_context.Localization == null)
        {
            return authorizationType.ToString();
        }

        return authorizationType switch
        {
            PrefectAuthorizationType.None => _context.Localization.T("ui.prefect_authorization.none"),
            PrefectAuthorizationType.Half => _context.Localization.T("ui.prefect_authorization.half"),
            PrefectAuthorizationType.Full => _context.Localization.T("ui.prefect_authorization.full"),
            _ => authorizationType.ToString()
        };
    }

    private static OfficerData? GetCityPrefect(WorldState world, CityData city)
    {
        return city.OfficerIds
            .Select(world.GetOfficer)
            .FirstOrDefault(officer =>
                officer != null &&
                OfficerAppointmentRules.HasAppointment(officer, OfficerAppointmentRules.Governor));
    }
}
