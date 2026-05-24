using System;
using System.Collections.Generic;
using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private const string EventPictureBasePath = "res://assets/event/";
    private const string EventSfxBasePath = "res://assets/sfx/events/";
    private const double EventOverlayFadeSeconds = 0.2;

    private sealed class PendingEventPresentation
    {
        public required MonthlyCityEventType EventType { get; init; }
        public required List<int> CityIds { get; init; }
        public required List<string> CityNames { get; init; }
        public required string PicturePath { get; init; }
        public required string SfxPath { get; init; }
        public required float DurationSeconds { get; init; }
        public required Color MarkerColor { get; init; }
    }

    private readonly Queue<PendingEventPresentation> _pendingEventPresentations = new();
    private Control? _eventPresentationOverlay;
    private PanelContainer? _eventPresentationPanel;
    private TextureRect? _eventPictureRect;
    private Label? _eventCaptionLabel;
    private PendingEventPresentation? _activeEventPresentation;
    private double _activeEventPresentationStartedAt;
    private double _activeEventPresentationEndsAt;
    private readonly List<Control> _temporarilyHiddenUiOverlays = new();
    private bool _eventPresentationUiSuppressed;

    private void InitializeEventPresentationUi()
    {
        _eventPresentationOverlay = GetNodeOrNull<Control>("Root/EventPresentationOverlay");
        _eventPresentationPanel = GetNodeOrNull<PanelContainer>("Root/EventPresentationOverlay/CenterContainer/EventPresentationPanel");
        _eventPictureRect = GetNodeOrNull<TextureRect>("Root/EventPresentationOverlay/CenterContainer/EventPresentationPanel/EventPresentationRoot/EventPictureRect");
        _eventCaptionLabel = GetNodeOrNull<Label>("Root/EventPresentationOverlay/CenterContainer/EventPresentationPanel/EventPresentationRoot/EventCaptionLabel");

        if (_eventPresentationOverlay != null)
        {
            _eventPresentationOverlay.Hide();
            _eventPresentationOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        if (_eventPresentationPanel != null)
        {
            var panelStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.06f, 0.06f, 0.08f, 0.94f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.67f, 0.55f, 0.32f, 0.92f),
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10,
                CornerRadiusBottomRight = 10,
                CornerRadiusBottomLeft = 10
            };
            _eventPresentationPanel.AddThemeStyleboxOverride("panel", panelStyle);
        }

        if (_eventCaptionLabel != null)
        {
            _eventCaptionLabel.HorizontalAlignment = HorizontalAlignment.Center;
        }
    }

    private void QueueMonthlyCityEventPresentations(IEnumerable<MonthlyCityEvent> cityEvents)
    {
        if (_turnManager?.World == null || _localization == null)
        {
            return;
        }

        _pendingEventPresentations.Clear();
        _mapController?.ClearMonthlyEventHighlights();
        var groupedPresentations = new Dictionary<MonthlyCityEventType, PendingEventPresentation>();
        var presentationOrder = new List<MonthlyCityEventType>();

        foreach (var cityEvent in cityEvents)
        {
            var city = _turnManager.World.GetCity(cityEvent.CityId);
            if (city == null)
            {
                continue;
            }

            if (!TryBuildEventPresentation(cityEvent, city, out var presentation))
            {
                continue;
            }

            _mapController?.HighlightCityEvent(
                city.Id,
                presentation.MarkerColor,
                BuildEventMapTag(presentation.EventType));

            if (!groupedPresentations.TryGetValue(cityEvent.EventType, out var existingPresentation))
            {
                groupedPresentations[cityEvent.EventType] = presentation;
                presentationOrder.Add(cityEvent.EventType);
                continue;
            }

            existingPresentation.CityIds.Add(city.Id);
            existingPresentation.CityNames.Add(_localization.GetCityName(city));
        }

        foreach (var eventType in presentationOrder)
        {
            if (groupedPresentations.TryGetValue(eventType, out var presentation))
            {
                _pendingEventPresentations.Enqueue(presentation);
            }
        }
    }

    private bool TryBuildEventPresentation(MonthlyCityEvent cityEvent, CityData city, out PendingEventPresentation presentation)
    {
        presentation = null!;
        if (_localization == null || !EventPresentationCatalog.TryGetDefinition(cityEvent.EventType, out var definition))
        {
            return false;
        }

        presentation = new PendingEventPresentation
        {
            EventType = cityEvent.EventType,
            CityIds = new List<int> { city.Id },
            CityNames = new List<string> { _localization.GetCityName(city) },
            PicturePath = BuildEventResourcePath(EventPictureBasePath, definition.Picture),
            SfxPath = BuildEventResourcePath(EventSfxBasePath, definition.Sound),
            DurationSeconds = Mathf.Max(definition.DurationSeconds, 1.2f),
            MarkerColor = Color.FromString(definition.MapMarkerColor, new Color("C98B2B"))
        };
        return true;
    }

    private static string BuildEventResourcePath(string basePath, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{basePath}{value}";
    }

    private void ProcessEventPresentation()
    {
        var now = Time.GetTicksMsec() / 1000.0;
        if (_activeEventPresentation == null)
        {
            TryStartNextEventPresentation(now);
            return;
        }

        if (now >= _activeEventPresentationEndsAt)
        {
            FinishActiveEventPresentation();
            TryStartNextEventPresentation(now);
            return;
        }

        UpdateActiveEventPresentationVisual(now);
    }

    private void TryStartNextEventPresentation(double now)
    {
        if (_activeEventPresentation != null || _pendingEventPresentations.Count == 0)
        {
            return;
        }

        SuppressNonEssentialUiForEventPresentation();

        _activeEventPresentation = _pendingEventPresentations.Dequeue();
        _activeEventPresentationStartedAt = now;
        _activeEventPresentationEndsAt = now + _activeEventPresentation.DurationSeconds;

        if (_eventPictureRect != null)
        {
            _eventPictureRect.Texture = ResourceLoader.Exists(_activeEventPresentation.PicturePath)
                ? ResourceLoader.Load<Texture2D>(_activeEventPresentation.PicturePath)
                : null;
        }

        if (_eventCaptionLabel != null)
        {
            _eventCaptionLabel.Text = BuildEventPresentationCaption(_activeEventPresentation);
        }

        _eventPresentationOverlay?.Show();
        _eventPresentationOverlay?.MoveToFront();
        _eventPresentationPanel?.MoveToFront();
        UpdateActiveEventPresentationVisual(now);

        if (!string.IsNullOrWhiteSpace(_activeEventPresentation.SfxPath))
        {
            GameAudioController.Instance?.PlayEventSfx(_activeEventPresentation.SfxPath);
        }
    }

    private void FinishActiveEventPresentation()
    {
        if (_activeEventPresentation == null)
        {
            return;
        }

        _eventPresentationOverlay?.Hide();
        if (_eventPresentationPanel != null)
        {
            _eventPresentationPanel.Modulate = Colors.White;
        }

        if (_eventPictureRect != null)
        {
            _eventPictureRect.Texture = null;
        }

        _activeEventPresentation = null;

        if (_pendingEventPresentations.Count == 0)
        {
            RestoreSuppressedUiAfterEventPresentation();
        }
    }

    private void UpdateActiveEventPresentationVisual(double now)
    {
        if (_activeEventPresentation == null || _eventPresentationPanel == null)
        {
            return;
        }

        var elapsed = now - _activeEventPresentationStartedAt;
        var remaining = _activeEventPresentationEndsAt - now;
        var fadeInAlpha = Mathf.Clamp((float)(elapsed / EventOverlayFadeSeconds), 0.0f, 1.0f);
        var fadeOutAlpha = Mathf.Clamp((float)(remaining / EventOverlayFadeSeconds), 0.0f, 1.0f);
        var alpha = Math.Min(fadeInAlpha, fadeOutAlpha);
        _eventPresentationPanel.Modulate = new Color(1.0f, 1.0f, 1.0f, alpha);
    }

    private string BuildEventPresentationCaption(PendingEventPresentation presentation)
    {
        if (_localization == null)
        {
            return $"{presentation.EventType} in {string.Join(", ", presentation.CityNames)}";
        }

        return _localization.Format(
            "ui.event_presentation.caption",
            BuildEventCityNamesText(presentation.CityNames),
            GetEventDisplayName(presentation.EventType));
    }

    private string BuildEventCityNamesText(IReadOnlyList<string> cityNames)
    {
        if (cityNames.Count == 0)
        {
            return string.Empty;
        }

        if (cityNames.Count == 1)
        {
            return cityNames[0];
        }

        var separator = _localization?.IsTraditionalChinese == true ? "、" : ", ";
        return string.Join(separator, cityNames);
    }

    private string BuildEventMapTag(MonthlyCityEventType eventType)
    {
        if (_localization == null)
        {
            return $"[{eventType}]";
        }

        return _localization.Format(
            "ui.event_presentation.map_tag",
            GetEventDisplayName(eventType));
    }

    private string GetEventDisplayName(MonthlyCityEventType eventType)
    {
        if (_localization == null)
        {
            return eventType.ToString();
        }

        var key = eventType switch
        {
            MonthlyCityEventType.Flooding => "event.name.flooding",
            MonthlyCityEventType.Drought => "event.name.drought",
            MonthlyCityEventType.Earthquake => "event.name.earthquake",
            MonthlyCityEventType.InsectDisaster => "event.name.insect_disaster",
            MonthlyCityEventType.Plague => "event.name.plague",
            MonthlyCityEventType.Rebellion => "event.name.rebellion",
            MonthlyCityEventType.Bandit => "event.name.bandit",
            MonthlyCityEventType.Snow => "event.name.snow",
            MonthlyCityEventType.Typhoon => "event.name.typhoon",
            MonthlyCityEventType.BumperHarvest => "event.name.bumper_harvest",
            MonthlyCityEventType.Fire => "event.name.fire",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(key) ? eventType.ToString() : _localization.T(key);
    }

    private void SuppressNonEssentialUiForEventPresentation()
    {
        if (_eventPresentationUiSuppressed)
        {
            return;
        }

        _temporarilyHiddenUiOverlays.Clear();
        CollectVisibleEventSuppressibleUiOverlays(_temporarilyHiddenUiOverlays);
        _mainHudUiController?.SetCityInfoTemporarilyHidden(true);

        foreach (var overlay in _temporarilyHiddenUiOverlays)
        {
            if (GodotObject.IsInstanceValid(overlay))
            {
                overlay.Hide();
            }
        }

        _eventPresentationUiSuppressed = _temporarilyHiddenUiOverlays.Count > 0;
    }

    private void RestoreSuppressedUiAfterEventPresentation()
    {
        if (!_eventPresentationUiSuppressed)
        {
            return;
        }

        foreach (var overlay in _temporarilyHiddenUiOverlays)
        {
            if (GodotObject.IsInstanceValid(overlay))
            {
                overlay.Show();
            }
        }

        _temporarilyHiddenUiOverlays.Clear();
        _mainHudUiController?.SetCityInfoTemporarilyHidden(false);
        _eventPresentationUiSuppressed = false;
    }

    private void CollectVisibleEventSuppressibleUiOverlays(List<Control> overlays)
    {
        _merchantUiController?.CollectVisibleDialogOverlays(overlays);
        _militaryUiController?.CollectVisibleDialogOverlays(overlays);
        _civilUiController?.CollectVisibleDialogOverlays(overlays);
        _personnelUiController?.CollectVisibleDialogOverlays(overlays);
        _advisorUiController?.CollectVisibleDialogOverlays(overlays);
        _diplomacyUiController?.CollectVisibleDialogOverlays(overlays);
        _internalAffairsUiController?.CollectVisibleDialogOverlays(overlays);
        _spyUiController?.CollectVisibleDialogOverlays(overlays);
        _systemUiController?.CollectVisibleDialogOverlays(overlays);
        _viewUiController?.CollectVisibleDialogOverlays(overlays);

        if (_selectOfficerDialog?.Visible == true)
        {
            overlays.Add(_selectOfficerDialog);
        }
    }
}
