using System.Collections.Generic;
using System.Linq;
using Godot;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class AdvisorDialogController : FloatingOverlayController
{
    private sealed class AdviceEntry
    {
        public required string SpeakerName { get; init; }
        public required string SpeakerRole { get; init; }
        public required string DateText { get; init; }
        public required string AdviceText { get; init; }
        public required int Year { get; init; }
        public required int Month { get; init; }
        public int OfficerId { get; init; }

        public string HistoryText => $"{SpeakerName}（{SpeakerRole}） | {DateText} | {AdviceText}";
        public string SpeechText => $"{SpeakerName}（{SpeakerRole}）: {AdviceText}";
    }

    private readonly AdvisorUiContext _context;
    private readonly List<AdviceEntry> _adviceHistory = new();
    private Button? _askChancellorButton;
    private Button? _askChiefStrategistButton;
    private Button? _askLocalOfficerButton;
    private TextureRect? _portraitRect;
    private Label? _portraitPlaceholder;
    private RichTextLabel? _speechLabel;
    private ItemList? _adviceHistoryList;
    private bool _signalsConnected;

    public AdvisorDialogController(AdvisorUiContext context)
        : base(context, "res://scenes/ui/advisor/AdvisorDialog.tscn")
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

        RefreshText();
        ShowOverlay();
    }

    public void RefreshText()
    {
        if (_context.Localization == null || !EnsureOverlayReady())
        {
            return;
        }

        SetOverlayTitleText(_context.Localization.T("ui.advisor_menu"));
        SetLabelText("AdviceButtonsLabel", _context.Localization.T("ui.ask_for_advice"));
        SetLabelText("AdviceHistoryLabel", _context.Localization.T("ui.past_advice"));

        if (_askChancellorButton != null)
        {
            _askChancellorButton.Text = _context.Localization.T("ui.call_chancellor");
        }

        if (_askChiefStrategistButton != null)
        {
            _askChiefStrategistButton.Text = _context.Localization.T("ui.call_chief_strategist");
        }

        if (_askLocalOfficerButton != null)
        {
            _askLocalOfficerButton.Text = _context.Localization.T("ui.call_local_officer");
        }

        UpdateAdvisorButtonStates();
        RefreshAdviceHistoryList();
    }

    protected override void OnOverlayContentReady(VBoxContainer root)
    {
        _askChancellorButton = root.GetNodeOrNull<Button>("AdviceButtonsRow/AskChancellorButton");
        _askChiefStrategistButton = root.GetNodeOrNull<Button>("AdviceButtonsRow/AskChiefStrategistButton");
        _askLocalOfficerButton = root.GetNodeOrNull<Button>("AdviceButtonsRow/AskLocalOfficerButton");
        _portraitRect = root.GetNodeOrNull<TextureRect>("ActiveAdvicePanel/ActiveAdviceRow/PortraitPanel/PortraitCenter/PortraitStack/PortraitRect");
        _portraitPlaceholder = root.GetNodeOrNull<Label>("ActiveAdvicePanel/ActiveAdviceRow/PortraitPanel/PortraitCenter/PortraitStack/PortraitPlaceholder");
        _speechLabel = root.GetNodeOrNull<RichTextLabel>("ActiveAdvicePanel/ActiveAdviceRow/SpeechLabel");
        _adviceHistoryList = root.GetNodeOrNull<ItemList>("AdviceHistoryList");

        ApplyButtonThemes();
        ConnectAdvisorSignals();
    }

    private void SetLabelText(string nodeName, string text)
    {
        var label = GetOverlayContentNode<Label>(nodeName);
        if (label != null)
        {
            label.Text = text;
        }
    }

    private void ConnectAdvisorSignals()
    {
        if (_signalsConnected)
        {
            return;
        }

        if (_askChancellorButton != null)
        {
            _askChancellorButton.Pressed += OnAskChancellorPressed;
        }

        if (_askChiefStrategistButton != null)
        {
            _askChiefStrategistButton.Pressed += OnAskChiefStrategistPressed;
        }

        if (_askLocalOfficerButton != null)
        {
            _askLocalOfficerButton.Pressed += OnAskLocalOfficerPressed;
        }

        if (_adviceHistoryList != null)
        {
            _adviceHistoryList.ItemSelected += OnAdviceHistorySelected;
        }

        _signalsConnected = true;
    }

    private void ApplyButtonThemes()
    {
        if (_askChancellorButton != null)
        {
            _context.ApplyCommandButtonTheme(_askChancellorButton);
        }

        if (_askChiefStrategistButton != null)
        {
            _context.ApplyCommandButtonTheme(_askChiefStrategistButton);
        }

        if (_askLocalOfficerButton != null)
        {
            _context.ApplyCommandButtonTheme(_askLocalOfficerButton);
        }
    }

    private void UpdateAdvisorButtonStates()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var faction = world != null && city != null ? world.GetFaction(city.OwnerFactionId) : null;
        var hasChancellor = faction != null && faction.ChancellorOfficerId > 0;
        var hasChiefStrategist = faction != null && faction.ChiefStrategistOfficerId > 0;

        if (_askChancellorButton != null)
        {
            _askChancellorButton.Disabled = !hasChancellor;
        }

        if (_askChiefStrategistButton != null)
        {
            _askChiefStrategistButton.Disabled = !hasChiefStrategist;
        }
    }

    private void RefreshAdviceHistoryList()
    {
        if (_adviceHistoryList == null)
        {
            return;
        }

        _adviceHistoryList.Clear();
        foreach (var entry in _adviceHistory)
        {
            _adviceHistoryList.AddItem(entry.HistoryText);
        }

        if (_adviceHistoryList.ItemCount > 0)
        {
            _adviceHistoryList.Select(0);
            UpdateActiveAdviceDisplay(_adviceHistory[0]);
            return;
        }

        UpdateActiveAdviceDisplay(null);
    }

    private void UpdateActiveAdviceDisplay(AdviceEntry? entry)
    {
        if (_speechLabel != null)
        {
            _speechLabel.Text = entry?.SpeechText ?? (_context.Localization?.T("ui.no_advice") ?? "No advice.");
        }

        if (_portraitRect != null)
        {
            _portraitRect.Texture = entry != null && entry.OfficerId > 0
                ? _context.BuildOfficerPortraitTexture(entry.OfficerId)
                : null;
        }

        if (_portraitPlaceholder != null)
        {
            var hasPortrait = _portraitRect?.Texture != null;
            _portraitPlaceholder.Visible = !hasPortrait;
            _portraitPlaceholder.Text = entry != null
                ? $"{_context.GetPortraitLabel()}\n{entry.SpeakerName}"
                : _context.GetPortraitLabel();
        }
    }

    private void AddAdviceEntry(string speakerName, string speakerRole, string advice, int officerId = 0)
    {
        var localization = _context.Localization;
        var world = _context.TurnManager?.World;
        if (localization == null || world == null)
        {
            return;
        }

        var existingEntry = _adviceHistory.FirstOrDefault(entry =>
            entry.Year == world.Year &&
            entry.Month == world.Month &&
            entry.SpeakerName == speakerName &&
            entry.AdviceText == advice);
        if (existingEntry != null)
        {
            UpdateActiveAdviceDisplay(existingEntry);
            return;
        }

        var newEntry = new AdviceEntry
        {
            SpeakerName = speakerName,
            SpeakerRole = speakerRole,
            DateText = localization.FormatYearMonth(world.Year, world.Month),
            AdviceText = advice,
            Year = world.Year,
            Month = world.Month,
            OfficerId = officerId
        };

        _adviceHistory.Insert(0, newEntry);
        RefreshAdviceHistoryList();
        UpdateActiveAdviceDisplay(newEntry);
    }

    private void OnAdviceHistorySelected(long index)
    {
        if (index < 0 || index >= _adviceHistory.Count)
        {
            return;
        }

        UpdateActiveAdviceDisplay(_adviceHistory[(int)index]);
    }

    private void OnAskChancellorPressed()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var localization = _context.Localization;
        if (world == null || city == null || localization == null)
        {
            return;
        }

        var faction = world.GetFaction(city.OwnerFactionId);
        var chancellor = faction != null ? world.GetOfficer(faction.ChancellorOfficerId) : null;
        var speaker = chancellor != null ? localization.GetOfficerName(chancellor) : localization.T("ui.chancellor");
        AddAdviceEntry(speaker, localization.T("ui.chancellor"), BuildChancellorComment(city), chancellor?.Id ?? 0);
    }

    private void OnAskChiefStrategistPressed()
    {
        var world = _context.TurnManager?.World;
        var city = _context.SelectedCity;
        var localization = _context.Localization;
        if (world == null || city == null || localization == null)
        {
            return;
        }

        var faction = world.GetFaction(city.OwnerFactionId);
        var chiefStrategist = faction != null ? world.GetOfficer(faction.ChiefStrategistOfficerId) : null;
        var speaker = chiefStrategist != null ? localization.GetOfficerName(chiefStrategist) : localization.T("ui.chief_strategist");
        AddAdviceEntry(speaker, localization.T("ui.chief_strategist"), BuildChiefStrategistComment(city), chiefStrategist?.Id ?? 0);
    }

    private void OnAskLocalOfficerPressed()
    {
        var city = _context.SelectedCity;
        var localization = _context.Localization;
        if (city == null || localization == null)
        {
            return;
        }

        var officer = FindBestLocalAdvisor(city);
        if (officer == null)
        {
            AddAdviceEntry(localization.T("ui.local_place"), localization.T("ui.local_place"), localization.T("ui.no_advice"));
            return;
        }

        AddAdviceEntry(localization.GetOfficerName(officer), localization.GetOfficerRole(officer), BuildLocalOfficerComment(city, officer), officer.Id);
    }

    private OfficerData? FindBestLocalAdvisor(CityData city)
    {
        var world = _context.TurnManager?.World;
        if (world == null)
        {
            return null;
        }

        OfficerData? bestOfficer = null;
        var bestScore = int.MinValue;
        foreach (var officerId in city.OfficerIds)
        {
            var officer = world.GetOfficer(officerId);
            if (officer == null || _context.IsFactionRuler(world, officer))
            {
                continue;
            }

            var score = GetLocalAdvisorScore(city, officer);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestOfficer = officer;
        }

        return bestOfficer;
    }

    private static int GetLocalAdvisorScore(CityData city, OfficerData officer)
    {
        var needsDomesticAdvice = city.Loyalty < 70 || city.Gold < 600 || city.Food < 900 || city.Population < 40000;
        var needsMilitaryAdvice = city.Defense < 60 || city.Troops < 1800;
        if (needsDomesticAdvice)
        {
            return officer.Politics * 3 + officer.Intelligence * 2 + officer.Charm;
        }

        if (needsMilitaryAdvice)
        {
            return officer.Strength * 3 + officer.Intelligence * 2 + officer.Leadership;
        }

        return officer.Intelligence * 3 + officer.Politics * 2 + officer.Charm;
    }

    private string BuildChancellorComment(CityData city)
    {
        var localization = _context.Localization;
        var world = _context.TurnManager?.World;
        if (localization == null || world == null)
        {
            return string.Empty;
        }

        var faction = world.GetFaction(city.OwnerFactionId);
        if (faction == null || faction.ChancellorOfficerId <= 0)
        {
            return localization.T("ui.advisor_comment_no_chancellor");
        }

        if (city.Loyalty < 65)
        {
            return localization.T("ui.advisor_comment_chancellor_loyalty");
        }

        if (city.Food < 900)
        {
            return localization.T("ui.advisor_comment_chancellor_food");
        }

        if (city.Gold < 500)
        {
            return localization.T("ui.advisor_comment_chancellor_gold");
        }

        if (city.Population < 40000)
        {
            return localization.T("ui.advisor_comment_chancellor_population");
        }

        return localization.T("ui.advisor_comment_chancellor_balanced");
    }

    private string BuildChiefStrategistComment(CityData city)
    {
        var localization = _context.Localization;
        var world = _context.TurnManager?.World;
        if (localization == null || world == null)
        {
            return string.Empty;
        }

        var faction = world.GetFaction(city.OwnerFactionId);
        if (faction == null || faction.ChiefStrategistOfficerId <= 0)
        {
            return localization.T("ui.advisor_comment_no_chief_strategist");
        }

        var hasEnemyBorder = city.ConnectedCityIds
            .Select(world.GetCity)
            .Where(target => target != null)
            .Cast<CityData>()
            .Any(target => target.OwnerFactionId > 0 && target.OwnerFactionId != city.OwnerFactionId);
        if (hasEnemyBorder)
        {
            var strongestEnemy = city.ConnectedCityIds
                .Select(world.GetCity)
                .Where(target => target != null && target.OwnerFactionId > 0 && target.OwnerFactionId != city.OwnerFactionId)
                .Cast<CityData>()
                .OrderByDescending(target => target.Troops)
                .FirstOrDefault();
            if (strongestEnemy != null && city.Troops >= strongestEnemy.Troops + 600)
            {
                return localization.T("ui.advisor_comment_chief_strategist_attack");
            }

            if (city.Defense < 60 || city.Troops < 1600)
            {
                return localization.T("ui.advisor_comment_chief_strategist_defense");
            }

            return localization.T("ui.advisor_comment_chief_strategist_border");
        }

        if (city.Loyalty < 70)
        {
            return localization.T("ui.advisor_comment_chief_strategist_loyalty");
        }

        return localization.T("ui.advisor_comment_chief_strategist_layout");
    }

    private string BuildLocalOfficerComment(CityData city, OfficerData officer)
    {
        var localization = _context.Localization;
        var world = _context.TurnManager?.World;
        if (localization == null || world == null)
        {
            return string.Empty;
        }

        if (officer.Politics >= officer.Intelligence && officer.Politics >= officer.Strength)
        {
            if (officer.Politics < 60)
            {
                return localization.T("ui.no_advice");
            }

            if (city.Loyalty < 70)
            {
                return localization.T("ui.advice_local_politics_loyalty");
            }

            if (city.Gold < 600)
            {
                return localization.T("ui.advice_local_politics_gold");
            }

            return localization.T("ui.advice_local_politics_balanced");
        }

        if (officer.Intelligence >= officer.Strength)
        {
            if (officer.Intelligence < 60)
            {
                return localization.T("ui.no_advice");
            }

            var hasEnemyBorder = city.ConnectedCityIds
                .Select(world.GetCity)
                .Where(target => target != null)
                .Cast<CityData>()
                .Any(target => target.OwnerFactionId > 0 && target.OwnerFactionId != city.OwnerFactionId);
            return hasEnemyBorder
                ? localization.T("ui.advice_local_intelligence_border")
                : localization.T("ui.advice_local_intelligence_prepare");
        }

        if (officer.Strength < 60)
        {
            return localization.T("ui.no_advice");
        }

        if (city.Troops < 1800 || city.Defense < 60)
        {
            return localization.T("ui.advice_local_strength_defense");
        }

        return localization.T("ui.advice_local_strength_attack");
    }
}
