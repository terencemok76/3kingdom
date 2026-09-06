using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattlePresentationSettings;
using static ThreeKingdom.Battle.BattleResourcePaths;
using static ThreeKingdom.Battle.BattleUnitTypes;
using static ThreeKingdom.Battle.BattleUnitVisualCatalog;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private static void ApplyMoveAnimation(BattleOccupantInfo occupant, BattleSpriteDirection direction, Vector2 destinationPosition, Vector2[]? pathPositions = null, BattleSpriteDirection[]? pathDirections = null, Color?[]? pathModulates = null, Action? onComplete = null)
    {
        if (occupant.Marker == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopInfantry)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, GetMoveScenePathArray(pathDirections, GetInfantryMoveScene), pathModulates, GetScaledMoveDuration(InfantryMoveAnimationDurationSeconds, pathPositions), GetInfantryMoveScene(direction), GetInfantryIdleScene(direction), onComplete);
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopSpearman)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, GetMoveScenePathArray(pathDirections, GetSpearmanMoveScene), pathModulates, GetScaledMoveDuration(SpearmanMoveAnimationDurationSeconds, pathPositions), GetSpearmanMoveScene(direction), GetSpearmanIdleScene(direction), onComplete);
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopRam)
        {
            var carIdleScene = GetCarIdleScene(direction);
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, null, pathModulates, GetScaledMoveDuration(CarMoveAnimationDurationSeconds, pathPositions), carIdleScene, carIdleScene, onComplete);
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopSupplyCart)
        {
            var supplyCarIdleScene = GetSupplyCarIdleScene(direction);
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, null, pathModulates, GetScaledMoveDuration(CarMoveAnimationDurationSeconds, pathPositions), supplyCarIdleScene, supplyCarIdleScene, onComplete);
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopLadder)
        {
            var carLadderIdleScene = GetCarLadderIdleScene(direction);
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, null, pathModulates, GetScaledMoveDuration(CarMoveAnimationDurationSeconds, pathPositions), carLadderIdleScene, carLadderIdleScene, onComplete);
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopArcher)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, GetMoveScenePathArray(pathDirections, GetArcherMoveScene), pathModulates, GetScaledMoveDuration(ArcherMoveAnimationDurationSeconds, pathPositions), GetArcherMoveScene(direction), GetArcherIdleScene(direction), onComplete);
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopCavalry)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, null, pathModulates, GetScaledMoveDuration(CavalryMoveAnimationDurationSeconds, pathPositions), GetCavalryMoveScene(direction), GetCavalryIdleScene(direction), onComplete);
            return;
        }

        if (occupant.Category == CategoryUnit && occupant.TroopType == TroopWorker)
        {
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, GetMoveScenePathArray(pathDirections, GetWorkerMoveScene), pathModulates, GetScaledMoveDuration(InfantryMoveAnimationDurationSeconds, pathPositions), GetWorkerMoveScene(direction), GetWorkerIdleScene(direction), onComplete);
            return;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopCatapult)
        {
            var catapultIdleScene = GetCatapultIdleScene(direction);
            MoveMarker(occupant.Marker, destinationPosition, pathPositions, null, pathModulates, GetScaledMoveDuration(CatapultMoveAnimationDurationSeconds, pathPositions), catapultIdleScene, catapultIdleScene, onComplete);
            return;
        }

        occupant.Marker.Position = destinationPosition;
        onComplete?.Invoke();
    }

    private static void MoveMarker(BattlePieceMarker marker, Vector2 destinationPosition, Vector2[]? pathPositions, string[]? pathMoveScenePaths, Color?[]? pathModulates, double duration, string moveScenePath, string idleScenePath, Action? onComplete = null)
    {
        if (pathPositions is { Length: > 0 })
        {
            if (pathMoveScenePaths is { Length: > 0 })
            {
                marker.MoveAlong(pathPositions, duration, pathMoveScenePaths, idleScenePath, onComplete, pathModulates);
                return;
            }

            marker.MoveAlong(pathPositions, duration, moveScenePath, idleScenePath, onComplete, pathModulates);
            return;
        }

        marker.MoveTo(destinationPosition, duration, moveScenePath, idleScenePath, onComplete);
    }

    private static double GetScaledMoveDuration(double baseDuration, Vector2[]? pathPositions)
    {
        return baseDuration * Math.Max(1, pathPositions?.Length ?? 1);
    }

    private static string[]? GetMoveScenePathArray(BattleSpriteDirection[]? directions, Func<BattleSpriteDirection, string> getMoveScene)
    {
        if (directions is not { Length: > 0 })
        {
            return null;
        }

        var scenePaths = new string[directions.Length];
        for (var index = 0; index < directions.Length; index++)
        {
            scenePaths[index] = getMoveScene(directions[index]);
        }

        return scenePaths;
    }

    private double ApplyAttackAnimation(BattleOccupantInfo occupant, BattleSpriteDirection direction)
    {
        if (occupant.Marker == null)
        {
            return 0.0;
        }

        if (occupant.Category == CategorySiegeEngine && occupant.TroopType == TroopCatapult)
        {
            occupant.Marker.PlayAction(
                GetCatapultAttackScene(direction),
                GetCatapultIdleScene(direction),
                CatapultAttackAnimationDurationSeconds);
            return CatapultAttackAnimationDurationSeconds;
        }

        if (occupant.Category != CategoryUnit)
        {
            return 0.0;
        }

        if (occupant.TroopType == TroopInfantry)
        {
            occupant.Marker.PlayAction(
                GetInfantryAttackScene(direction),
                GetInfantryIdleScene(direction),
                InfantryAttackAnimationDurationSeconds);
            return InfantryAttackAnimationDurationSeconds;
        }

        if (occupant.TroopType == TroopSpearman)
        {
            occupant.Marker.PlayAction(
                GetSpearmanAttackScene(direction),
                GetSpearmanIdleScene(direction),
                SpearmanAttackAnimationDurationSeconds);
            return SpearmanAttackAnimationDurationSeconds;
        }

        if (occupant.TroopType == TroopArcher)
        {
            occupant.Marker.PlayAction(
                GetArcherAttackScene(direction),
                GetArcherIdleScene(direction),
                ArcherAttackAnimationDurationSeconds);
            return ArcherAttackAnimationDurationSeconds;
        }

        if (occupant.TroopType == TroopCavalry)
        {
            occupant.Marker.PlayAction(
                GetCavalryAttackScene(direction),
                GetCavalryIdleScene(direction),
                CavalryAttackAnimationDurationSeconds);
            return CavalryAttackAnimationDurationSeconds;
        }

        if (occupant.TroopType == TroopWorker)
        {
            occupant.Marker.PlayAction(
                GetWorkerAttackScene(direction),
                GetWorkerIdleScene(direction),
                WorkerAttackAnimationDurationSeconds);
            return WorkerAttackAnimationDurationSeconds;
        }

        return 0.0;
    }

    private double ApplyTargetHurtAnimation(BattleGridKey attackerGrid, BattleGridKey targetGrid, BattleOccupantInfo? attacker = null)
    {
        if (IsClosedGateStructureTarget(targetGrid))
        {
            return 0.0;
        }

        if (!_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            return 0.0;
        }

        var target = attacker == null
            ? GetAttackTarget(targetOccupants)
            : GetAttackTargetForAttack(targetOccupants, attacker.TeamName, targetGrid);
        if (target?.Marker == null)
        {
            return 0.0;
        }

        if (target.Category == CategorySiegeEngine)
        {
            return 0.0;
        }

        var hurtDirection = GetInfantryDirection(attackerGrid.Grid, targetGrid.Grid);
        if (target.TroopType == TroopSpearman)
        {
            target.Marker.PlayAction(
                GetSpearmanHurtScene(hurtDirection),
                GetSpearmanIdleScene(target.FacingDirection),
                SpearmanHurtAnimationDurationSeconds);
            return SpearmanHurtAnimationDurationSeconds;
        }

        if (target.TroopType == TroopArcher)
        {
            target.Marker.PlayAction(
                GetArcherHurtScene(hurtDirection),
                GetArcherIdleScene(target.FacingDirection),
                ArcherHurtAnimationDurationSeconds);
            return ArcherHurtAnimationDurationSeconds;
        }

        if (target.TroopType == TroopCavalry)
        {
            target.Marker.PlayAction(
                GetCavalryHurtScene(hurtDirection),
                GetCavalryIdleScene(target.FacingDirection),
                CavalryHurtAnimationDurationSeconds);
            return CavalryHurtAnimationDurationSeconds;
        }

        if (target.TroopType == TroopWorker)
        {
            target.Marker.PlayAction(
                GetWorkerHurtScene(hurtDirection),
                GetWorkerIdleScene(target.FacingDirection),
                WorkerHurtAnimationDurationSeconds);
            return WorkerHurtAnimationDurationSeconds;
        }

        target.Marker.PlayAction(
            GetInfantryHurtScene(hurtDirection),
            GetInfantryIdleScene(target.FacingDirection),
            InfantryHurtAnimationDurationSeconds);
        return InfantryHurtAnimationDurationSeconds;
    }

    private double GetTargetHurtAnimationDuration(BattleGridKey attackerGrid, BattleGridKey targetGrid, BattleOccupantInfo attacker)
    {
        if (IsClosedGateStructureTarget(targetGrid) ||
            !_occupantsByGrid.TryGetValue(targetGrid, out var targetOccupants))
        {
            return 0.0;
        }

        var target = GetAttackTargetForAttack(targetOccupants, attacker.TeamName, targetGrid);
        if (target?.Marker == null || target.Category == CategorySiegeEngine)
        {
            return 0.0;
        }

        return target.TroopType switch
        {
            TroopSpearman => SpearmanHurtAnimationDurationSeconds,
            TroopArcher => ArcherHurtAnimationDurationSeconds,
            TroopCavalry => CavalryHurtAnimationDurationSeconds,
            TroopWorker => WorkerHurtAnimationDurationSeconds,
            _ => InfantryHurtAnimationDurationSeconds
        };
    }

    private async void PlayTargetHurtAnimationAfterDelay(double delaySeconds, BattleGridKey attackerGrid, BattleGridKey targetGrid, BattleOccupantInfo attacker)
    {
        if (delaySeconds > 0.0)
        {
            await ToSignal(GetTree().CreateTimer(delaySeconds), SceneTreeTimer.SignalName.Timeout);
        }

        ApplyTargetHurtAnimation(attackerGrid, targetGrid, attacker);
    }

    private async void PlayAttackProjectileAfterDelay(double delaySeconds, BattleGridKey sourceGrid, BattleGridKey targetGrid, BattleOccupantInfo attacker)
    {
        if (delaySeconds > 0.0)
        {
            await ToSignal(GetTree().CreateTimer(delaySeconds), SceneTreeTimer.SignalName.Timeout);
        }

        if (IsArrowProjectileAttacker(attacker))
        {
            PlayArrowProjectileEffect(sourceGrid, targetGrid);
        }
        else if (attacker.Category == CategorySiegeEngine && attacker.TroopType == TroopCatapult)
        {
            PlayCatapultProjectileEffect(sourceGrid, targetGrid);
        }
    }

    private async void ResolveProjectileAttackImpactAfterDelay(
        double delaySeconds,
        BattleGridKey attackerGrid,
        BattleGridKey targetGrid,
        BattleOccupantInfo attacker,
        int damage,
        double hurtAnimationDuration)
    {
        if (delaySeconds > 0.0)
        {
            await ToSignal(GetTree().CreateTimer(delaySeconds), SceneTreeTimer.SignalName.Timeout);
        }

        ApplyTargetHurtAnimation(attackerGrid, targetGrid, attacker);
        ApplyAttackDamage(attacker, targetGrid, hurtAnimationDuration, damage, attackerGrid);
    }


    private void ShowDamagePopup(BattleGridKey targetGrid, int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        ShowBattlePopup(
            targetGrid,
            $"-{damage:N0}",
            new Color(1.0f, 0.10f, 0.06f, 1.0f),
            new Color(0.05f, 0.02f, 0.01f, 0.95f),
            new Vector2(-18.0f, -88.0f),
            DamagePopupDurationSeconds,
            22);
    }

    private void ShowRepairPopup(BattleGridKey targetGrid, int repairAmount)
    {
        if (repairAmount <= 0)
        {
            return;
        }

        ShowBattlePopup(
            targetGrid,
            $"+{repairAmount:N0}",
            new Color(0.18f, 0.95f, 0.34f, 1.0f),
            new Color(0.01f, 0.08f, 0.03f, 0.95f),
            new Vector2(-18.0f, -88.0f),
            DamagePopupDurationSeconds,
            22);
    }

    private void ShowMoralePopup(BattleGridKey targetGrid, int moraleDelta, double initialDelaySeconds = 0.0)
    {
        if (moraleDelta == 0)
        {
            return;
        }

        if (initialDelaySeconds > 0.0)
        {
            ShowMoralePopupAfterDelay(targetGrid, moraleDelta, initialDelaySeconds);
            return;
        }

        var sign = moraleDelta > 0 ? "+" : "-";
        var fontColor = moraleDelta > 0
            ? new Color(0.42f, 0.82f, 1.0f, 1.0f)
            : new Color(1.0f, 0.72f, 0.18f, 1.0f);
        var outlineColor = moraleDelta > 0
            ? new Color(0.01f, 0.07f, 0.13f, 0.95f)
            : new Color(0.12f, 0.06f, 0.01f, 0.95f);
        ShowBattlePopup(
            targetGrid,
            BattleFormat("ui.battle.morale_popup", "Morale {0}{1}", sign, Math.Abs(moraleDelta)),
            fontColor,
            outlineColor,
            new Vector2(-30.0f, -112.0f),
            MoralePopupDurationSeconds,
            20);
    }

    private async void ShowMoralePopupAfterDelay(BattleGridKey targetGrid, int moraleDelta, double delaySeconds)
    {
        await ToSignal(GetTree().CreateTimer(delaySeconds), SceneTreeTimer.SignalName.Timeout);
        if (!GodotObject.IsInstanceValid(this))
        {
            return;
        }

        ShowMoralePopup(targetGrid, moraleDelta);
    }

    private void ShowHireOfficerPopup(BattleGridKey targetGrid)
    {
        ShowBattlePopup(
            targetGrid,
            BattleText("ui.battle.hire_success_popup", "Hired"),
            new Color(1.0f, 0.88f, 0.28f, 1.0f),
            new Color(0.12f, 0.07f, 0.01f, 0.95f),
            new Vector2(-34.0f, -138.0f),
            HireOfficerEffectDurationSeconds,
            22,
            HireOfficerPopupDelaySeconds);
    }

    private void LoadOfficerSpeechCatalog()
    {
        _officerSpeechEntries.Clear();
        if (!Godot.FileAccess.FileExists(OfficerSpeechCatalogPath))
        {
            return;
        }

        try
        {
            var catalog = JsonSerializer.Deserialize<BattleOfficerSpeechCatalog>(
                Godot.FileAccess.GetFileAsString(OfficerSpeechCatalogPath),
                OfficerSpeechJsonOptions);
            if (catalog == null)
            {
                return;
            }

            _officerSpeechEntries.AddRange(catalog.Entries.Where(entry =>
                !string.IsNullOrWhiteSpace(entry.Event) &&
                !string.IsNullOrWhiteSpace(entry.Persona) &&
                entry.Keys.Count > 0));
        }
        catch (JsonException exception)
        {
            GD.PushWarning($"Unable to load officer battle speech catalog: {exception.Message}");
        }
    }

    private async void ShowOpeningOfficerSpeechAfterDelay()
    {
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        if (!GodotObject.IsInstanceValid(this) || _isBattleFinished)
        {
            return;
        }

        var speakers = _occupantsByGrid.Values
            .SelectMany(static occupants => occupants)
            .Where(occupant => IsGeneralCountedPiece(occupant.Category, occupant.OfficerName))
            .ToList();
        var attackerSpeakers = speakers
            .Where(occupant => occupant.TeamName.Contains("Attacker", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var defenderSpeakers = speakers
            .Where(occupant => occupant.TeamName.Contains("Defender", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (attackerSpeakers.Count > 0)
        {
            TryShowOfficerSpeech(attackerSpeakers[_officerSpeechRandom.Next(attackerSpeakers.Count)], BattleOfficerSpeechEvent.Opening);
        }

        if (defenderSpeakers.Count > 0)
        {
            await ToSignal(GetTree().CreateTimer(OfficerSpeechDurationSeconds + 0.2), SceneTreeTimer.SignalName.Timeout);
            if (GodotObject.IsInstanceValid(this) && !_isBattleFinished)
            {
                TryShowOfficerSpeech(defenderSpeakers[_officerSpeechRandom.Next(defenderSpeakers.Count)], BattleOfficerSpeechEvent.Opening);
            }
        }
    }

    private void TryShowTerrainSpeech(BattleOccupantInfo occupant, BattleGridKey grid)
    {
        if (_mapData == null)
        {
            return;
        }

        var speechEvent = _mapData.GetCell(grid.X, grid.Y).Terrain switch
        {
            BattleTerrainType.Forest => BattleOfficerSpeechEvent.TerrainForest,
            BattleTerrainType.Hill => BattleOfficerSpeechEvent.TerrainHill,
            BattleTerrainType.Bridge => BattleOfficerSpeechEvent.TerrainBridge,
            BattleTerrainType.Swamp => BattleOfficerSpeechEvent.TerrainSwamp,
            _ => (BattleOfficerSpeechEvent?)null
        };
        if (speechEvent.HasValue)
        {
            TryShowOfficerSpeech(occupant, speechEvent.Value);
        }
    }

    private void TryShowOfficerSpeech(BattleOccupantInfo occupant, BattleOfficerSpeechEvent speechEvent)
    {
        if (_officerSpeechOverlay == null ||
            _officerSpeechTeamNameLabel == null ||
            _officerSpeechNameLabel == null ||
            _officerSpeechTextLabel == null ||
            !IsGeneralCountedPiece(occupant.Category, occupant.OfficerName) ||
            _officerSpeechEntries.Count == 0)
        {
            return;
        }

        var eventName = GetOfficerSpeechEventName(speechEvent);
        var persona = GetOfficerSpeechPersona(occupant.OfficerName);
        var candidates = _officerSpeechEntries
            .Where(entry => entry.Event.Equals(eventName, StringComparison.OrdinalIgnoreCase) &&
                            entry.Persona.Equals(persona, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var entry = candidates[_officerSpeechRandom.Next(candidates.Count)];
        var now = Time.GetTicksMsec();
        if (_officerSpeechLastShownAt.TryGetValue(occupant.OfficerName, out var lastShownAt) &&
            now - lastShownAt < OfficerSpeechCooldownMilliseconds &&
            speechEvent != BattleOfficerSpeechEvent.Retreat)
        {
            return;
        }

        if (_officerSpeechOverlay.Visible && entry.Priority < _activeOfficerSpeechPriority)
        {
            return;
        }

        var key = entry.Keys[_officerSpeechRandom.Next(entry.Keys.Count)];
        _officerSpeechTeamNameLabel.Text = FormatTeamName(occupant.TeamName);
        _officerSpeechNameLabel.Text = FormatOfficerName(occupant.OfficerName);
        _officerSpeechTextLabel.Text = BattleText(key, key);
        if (_officerSpeechPortrait != null)
        {
            _officerSpeechPortrait.Texture = GetOfficerPortraitTexture(occupant.OfficerName);
            _officerSpeechPortrait.Visible = _officerSpeechPortrait.Texture != null;
        }

        _officerSpeechLastShownAt[occupant.OfficerName] = now;
        _activeOfficerSpeechPriority = entry.Priority;
        var speechSerial = ++_officerSpeechSerial;
        _officerSpeechOverlay.Visible = true;
        _officerSpeechOverlay.MoveToFront();
        HideOfficerSpeechAfterDelay(speechSerial);
    }

    private async void HideOfficerSpeechAfterDelay(int speechSerial)
    {
        await ToSignal(GetTree().CreateTimer(OfficerSpeechDurationSeconds), SceneTreeTimer.SignalName.Timeout);
        if (GodotObject.IsInstanceValid(this) && speechSerial == _officerSpeechSerial && _officerSpeechOverlay != null)
        {
            _officerSpeechOverlay.Visible = false;
            _activeOfficerSpeechPriority = 0;
        }
    }

    private static string GetOfficerSpeechEventName(BattleOfficerSpeechEvent speechEvent)
    {
        return speechEvent switch
        {
            BattleOfficerSpeechEvent.TerrainForest => "terrain_forest",
            BattleOfficerSpeechEvent.TerrainHill => "terrain_hill",
            BattleOfficerSpeechEvent.TerrainBridge => "terrain_bridge",
            BattleOfficerSpeechEvent.TerrainSwamp => "terrain_swamp",
            _ => speechEvent.ToString().ToLowerInvariant()
        };
    }

    private static string GetOfficerSpeechPersona(string officerName)
    {
        var intelligence = GetOfficerTacticalIntelligence(officerName);
        var combat = GetOfficerBattleAttribute(officerName);
        if (intelligence >= 84)
        {
            return "tactician";
        }

        if (combat >= 84)
        {
            return "vanguard";
        }

        return officerName is "Cao Hong" or "Guo Si" ? "steadfast" : "ambitious";
    }

    private async void ShowRetreatNotice(BattleOccupantInfo retreatingUnit)
    {
        if (_retreatNotice == null || _retreatNoticeLabel == null)
        {
            return;
        }

        var noticeSerial = ++_retreatNoticeSerial;
        var officerName = string.IsNullOrWhiteSpace(retreatingUnit.OfficerName)
            ? retreatingUnit.DisplayName
            : retreatingUnit.OfficerName;
        _retreatNoticeLabel.Text = $"{officerName} / {FormatTroopType(retreatingUnit.TroopType)}\n{BattleText("ui.battle.retreat", "Retreat")}";
        _retreatNotice.Visible = true;
        _retreatNotice.MoveToFront();
        TryShowOfficerSpeech(retreatingUnit, BattleOfficerSpeechEvent.Retreat);

        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
        if (GodotObject.IsInstanceValid(this) && noticeSerial == _retreatNoticeSerial)
        {
            _retreatNotice.Visible = false;
        }
    }

    private async void ShowOfficerCaptureNotice(BattleOccupantInfo capturedOfficer)
    {
        if (_officerCaptureNotice == null || _officerCaptureNoticeLabel == null)
        {
            return;
        }

        var noticeSerial = ++_officerCaptureNoticeSerial;
        _officerCaptureNoticeLabel.Text = BattleFormat(
            "ui.battle.officer_captured",
            "{0} officer {1} has been captured!",
            FormatTeamName(capturedOfficer.TeamName),
            FormatOfficerName(capturedOfficer.OfficerName));
        _officerCaptureNotice.Visible = true;
        _officerCaptureNotice.MoveToFront();

        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);
        if (GodotObject.IsInstanceValid(this) && noticeSerial == _officerCaptureNoticeSerial)
        {
            _officerCaptureNotice.Visible = false;
        }
    }

    private async void ShowTurnBanner()
    {
        if (_turnBanner == null || _turnBannerLabel == null)
        {
            return;
        }

        var bannerSerial = ++_turnBannerSerial;
        _turnBannerLabel.Text = BattleFormat(
            "ui.battle.turn_banner",
            "{0} Turn",
            FormatTeamName(GetCurrentTurnSideName()));
        _turnBanner.Visible = true;
        _turnBanner.MoveToFront();

        await ToSignal(GetTree().CreateTimer(TurnBannerDurationSeconds), SceneTreeTimer.SignalName.Timeout);
        if (GodotObject.IsInstanceValid(this) && bannerSerial == _turnBannerSerial)
        {
            _turnBanner.Visible = false;
        }
    }

    private void ShowBattlePopup(
        BattleGridKey targetGrid,
        string text,
        Color fontColor,
        Color outlineColor,
        Vector2 offset,
        double durationSeconds,
        int fontSize,
        double initialDelaySeconds = 0.0)
    {
        if (_battleDepthLayer == null)
        {
            return;
        }

        var popup = new Label
        {
            Text = text,
            Position = GetMarkerPosition(targetGrid) + offset,
            ZIndex = 500,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = initialDelaySeconds > 0.0
                ? new Color(fontColor.R, fontColor.G, fontColor.B, 0.0f)
                : fontColor
        };
        popup.AddThemeColorOverride("font_color", fontColor);
        popup.AddThemeColorOverride("font_outline_color", outlineColor);
        popup.AddThemeConstantOverride("outline_size", 4);
        popup.AddThemeFontSizeOverride("font_size", fontSize);
        _battleDepthLayer.AddChild(popup);

        var tween = popup.CreateTween();
        if (initialDelaySeconds > 0.0)
        {
            tween.TweenInterval(initialDelaySeconds);
            tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(popup))
                {
                    popup.Modulate = fontColor;
                }
            }));
        }

        tween.SetParallel(true);
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(popup, "position", popup.Position + new Vector2(0.0f, -34.0f), durationSeconds);
        tween.TweenProperty(popup, "modulate:a", 0.0f, durationSeconds);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => popup.QueueFree()));
    }

    private double PlayHireOfficerEffect(BattleGridKey sourceGrid, BattleGridKey targetGrid)
    {
        if (_battleDepthLayer == null)
        {
            return 0.0;
        }

        var sourcePosition = GetMarkerPosition(sourceGrid) + new Vector2(0.0f, -42.0f);
        var targetPosition = GetMarkerPosition(targetGrid) + new Vector2(0.0f, -34.0f);
        var link = new Line2D
        {
            Width = 4.0f,
            DefaultColor = new Color(1.0f, 0.76f, 0.24f, 1.0f),
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
            ZIndex = 518
        };
        link.AddPoint(sourcePosition);
        link.AddPoint(sourcePosition.Lerp(targetPosition, 0.5f) + new Vector2(0.0f, -28.0f));
        link.AddPoint(targetPosition);
        _battleDepthLayer.AddChild(link);

        var linkTween = link.CreateTween();
        linkTween.TweenProperty(link, "modulate:a", 0.92f, HireOfficerEffectDurationSeconds * 0.18);
        linkTween.TweenInterval(HireOfficerEffectDurationSeconds * 0.36);
        linkTween.TweenProperty(link, "modulate:a", 0.0f, HireOfficerEffectDurationSeconds * 0.28);
        linkTween.TweenCallback(Callable.From(() => link.QueueFree()));

        var ring = new Line2D
        {
            Width = 5.0f,
            DefaultColor = new Color(1.0f, 0.86f, 0.28f, 1.0f),
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
            Position = targetPosition + new Vector2(0.0f, 16.0f),
            ZIndex = 519
        };
        ring.AddPoint(new Vector2(0.0f, -22.0f));
        ring.AddPoint(new Vector2(34.0f, 0.0f));
        ring.AddPoint(new Vector2(0.0f, 22.0f));
        ring.AddPoint(new Vector2(-34.0f, 0.0f));
        ring.AddPoint(new Vector2(0.0f, -22.0f));
        _battleDepthLayer.AddChild(ring);

        var ringTween = ring.CreateTween();
        ringTween.SetParallel(true);
        ringTween.TweenProperty(ring, "modulate:a", 0.95f, HireOfficerEffectDurationSeconds * 0.16);
        ringTween.TweenProperty(ring, "scale", new Vector2(1.35f, 1.35f), HireOfficerEffectDurationSeconds * 0.62);
        ringTween.SetParallel(false);
        ringTween.TweenProperty(ring, "modulate:a", 0.0f, HireOfficerEffectDurationSeconds * 0.22);
        ringTween.TweenCallback(Callable.From(() => ring.QueueFree()));

        var motes = new[]
        {
            new Vector2(-24.0f, -8.0f),
            new Vector2(-9.0f, -18.0f),
            new Vector2(10.0f, -15.0f),
            new Vector2(25.0f, -5.0f)
        };
        for (var index = 0; index < motes.Length; index++)
        {
            var mote = new ColorRect
            {
                Color = new Color(1.0f, 0.72f, 0.18f, 0.92f),
                Position = targetPosition + motes[index],
                Size = new Vector2(7.0f, 7.0f),
                PivotOffset = new Vector2(3.5f, 3.5f),
                Rotation = index * 0.42f,
                ZIndex = 520,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _battleDepthLayer.AddChild(mote);

            var moteTween = mote.CreateTween();
            moteTween.SetParallel(true);
            moteTween.TweenProperty(mote, "position", mote.Position + new Vector2(0.0f, -26.0f - index * 3.0f), HireOfficerEffectDurationSeconds * 0.72);
            moteTween.TweenProperty(mote, "rotation", mote.Rotation + 2.8f, HireOfficerEffectDurationSeconds * 0.72);
            moteTween.SetParallel(false);
            moteTween.TweenProperty(mote, "modulate:a", 0.0f, HireOfficerEffectDurationSeconds * 0.2);
            moteTween.TweenCallback(Callable.From(() => mote.QueueFree()));
        }

        ShowHireOfficerPopup(targetGrid);
        return HireOfficerEffectDurationSeconds;
    }

    private double PlayDropStoneEffect(BattleGridKey sourceGrid, BattleGridKey targetGrid)
    {
        if (_battleDepthLayer == null)
        {
            return 0.0;
        }

        var sourcePosition = GetMarkerPosition(sourceGrid) + new Vector2(0.0f, -16.0f);
        var targetPosition = GetMarkerPosition(targetGrid) + new Vector2(0.0f, -20.0f);
        var offsets = new[]
        {
            new Vector2(-12.0f, -6.0f),
            new Vector2(4.0f, -14.0f),
            new Vector2(14.0f, -4.0f)
        };

        for (var index = 0; index < offsets.Length; index++)
        {
            var stone = new ColorRect
            {
                Color = new Color(0.32f, 0.27f, 0.20f, 1.0f),
                Position = sourcePosition + offsets[index],
                Size = new Vector2(10.0f, 8.0f),
                PivotOffset = new Vector2(5.0f, 4.0f),
                Rotation = index * 0.55f,
                ZIndex = 520,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _battleDepthLayer.AddChild(stone);

            var tween = stone.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(stone, "position", targetPosition + offsets[index] * 0.45f, DropStoneEffectDurationSeconds);
            tween.TweenProperty(stone, "rotation", stone.Rotation + 4.0f + index, DropStoneEffectDurationSeconds);
            tween.TweenProperty(stone, "scale", new Vector2(1.35f, 1.35f), DropStoneEffectDurationSeconds);
            tween.SetParallel(false);
            tween.TweenCallback(Callable.From(() => stone.QueueFree()));
        }

        var impact = new ColorRect
        {
            Color = new Color(1.0f, 0.70f, 0.22f, 1.0f),
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
            Position = targetPosition - new Vector2(22.0f, 3.0f),
            Size = new Vector2(44.0f, 6.0f),
            PivotOffset = new Vector2(22.0f, 3.0f),
            Rotation = 0.35f,
            ZIndex = 519,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _battleDepthLayer.AddChild(impact);

        var impactTween = impact.CreateTween();
        impactTween.TweenInterval(DropStoneEffectDurationSeconds * 0.72);
        impactTween.SetParallel(true);
        impactTween.TweenProperty(impact, "modulate:a", 1.0f, 0.06);
        impactTween.TweenProperty(impact, "scale", new Vector2(1.5f, 1.5f), 0.18);
        impactTween.SetParallel(false);
        impactTween.TweenProperty(impact, "modulate:a", 0.0f, 0.18);
        impactTween.TweenCallback(Callable.From(() => impact.QueueFree()));

        return DropStoneEffectDurationSeconds;
    }

    private double PlayPourOilEffect(BattleGridKey sourceGrid, BattleGridKey targetGrid)
    {
        if (_battleDepthLayer == null)
        {
            return 0.0;
        }

        var sourcePosition = GetMarkerPosition(sourceGrid) + new Vector2(4.0f, -12.0f);
        var targetPosition = GetMarkerPosition(targetGrid) + new Vector2(0.0f, -16.0f);
        var streamOffsets = new[] { -8.0f, 3.0f, 12.0f };
        foreach (var offsetX in streamOffsets)
        {
            var oilStream = new ColorRect
            {
                Color = new Color(0.96f, 0.31f, 0.05f, 0.92f),
                Position = sourcePosition + new Vector2(offsetX, 0.0f),
                Size = new Vector2(7.0f, 18.0f),
                PivotOffset = new Vector2(3.5f, 9.0f),
                Rotation = 0.38f,
                ZIndex = 520,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _battleDepthLayer.AddChild(oilStream);

            var streamTween = oilStream.CreateTween();
            streamTween.SetParallel(true);
            streamTween.TweenProperty(oilStream, "position", targetPosition + new Vector2(offsetX * 0.45f, 0.0f), PourOilEffectDurationSeconds * 0.7);
            streamTween.TweenProperty(oilStream, "scale", new Vector2(1.45f, 1.8f), PourOilEffectDurationSeconds * 0.7);
            streamTween.SetParallel(false);
            streamTween.TweenProperty(oilStream, "modulate:a", 0.0f, PourOilEffectDurationSeconds * 0.3);
            streamTween.TweenCallback(Callable.From(() => oilStream.QueueFree()));
        }

        var oilSplash = new ColorRect
        {
            Color = new Color(1.0f, 0.58f, 0.08f, 1.0f),
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
            Position = targetPosition - new Vector2(26.0f, 7.0f),
            Size = new Vector2(52.0f, 14.0f),
            PivotOffset = new Vector2(26.0f, 7.0f),
            Rotation = -0.18f,
            ZIndex = 519,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _battleDepthLayer.AddChild(oilSplash);

        var splashTween = oilSplash.CreateTween();
        splashTween.TweenInterval(PourOilEffectDurationSeconds * 0.58);
        splashTween.SetParallel(true);
        splashTween.TweenProperty(oilSplash, "modulate:a", 0.95f, 0.08);
        splashTween.TweenProperty(oilSplash, "scale", new Vector2(1.35f, 1.5f), 0.2);
        splashTween.SetParallel(false);
        splashTween.TweenProperty(oilSplash, "modulate:a", 0.0f, 0.2);
        splashTween.TweenCallback(Callable.From(() => oilSplash.QueueFree()));

        return PourOilEffectDurationSeconds;
    }

    private double PlayCatapultProjectileEffect(BattleGridKey sourceGrid, BattleGridKey targetGrid)
    {
        if (_battleDepthLayer == null)
        {
            return 0.0;
        }

        var sourcePosition = GetMarkerPosition(sourceGrid) + new Vector2(0.0f, -20.0f);
        var targetPosition = GetMarkerPosition(targetGrid) + new Vector2(0.0f, -18.0f);
        var arcPeakPosition = sourcePosition.Lerp(targetPosition, 0.5f) + new Vector2(0.0f, -84.0f);
        _catapultStoneTexture ??= GD.Load<Texture2D>(CatapultStoneTexturePath);
        if (_catapultStoneTexture == null)
        {
            return 0.0;
        }

        var projectile = new Sprite2D
        {
            Texture = _catapultStoneTexture,
            Position = sourcePosition,
            Rotation = 0.25f,
            Scale = new Vector2(0.68f, 1.18f),
            ZIndex = 520
        };
        _battleDepthLayer.AddChild(projectile);

        var projectileMovementTween = projectile.CreateTween();
        projectileMovementTween.TweenProperty(projectile, "position", arcPeakPosition, CatapultProjectileEffectDurationSeconds * 0.5);
        projectileMovementTween.TweenProperty(projectile, "position", targetPosition, CatapultProjectileEffectDurationSeconds * 0.5);
        projectileMovementTween.TweenCallback(Callable.From(() => projectile.QueueFree()));

        var projectileRotationTween = projectile.CreateTween();
        projectileRotationTween.SetParallel(true);
        projectileRotationTween.TweenProperty(projectile, "rotation", projectile.Rotation + 8.0f, CatapultProjectileEffectDurationSeconds);
        projectileRotationTween.TweenProperty(projectile, "scale", new Vector2(0.92f, 1.48f), CatapultProjectileEffectDurationSeconds);

        var impact = new ColorRect
        {
            Color = new Color(1.0f, 0.76f, 0.30f, 1.0f),
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
            Position = targetPosition - new Vector2(32.0f, 5.0f),
            Size = new Vector2(64.0f, 10.0f),
            PivotOffset = new Vector2(32.0f, 5.0f),
            Rotation = 0.18f,
            ZIndex = 519,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _battleDepthLayer.AddChild(impact);

        var impactTween = impact.CreateTween();
        impactTween.TweenInterval(CatapultProjectileEffectDurationSeconds * 0.78);
        impactTween.SetParallel(true);
        impactTween.TweenProperty(impact, "modulate:a", 1.0f, 0.06);
        impactTween.TweenProperty(impact, "scale", new Vector2(1.6f, 1.6f), 0.16);
        impactTween.SetParallel(false);
        impactTween.TweenProperty(impact, "modulate:a", 0.0f, 0.16);
        impactTween.TweenCallback(Callable.From(() => impact.QueueFree()));

        return CatapultProjectileEffectDurationSeconds;
    }

    private double PlayArrowProjectileEffect(BattleGridKey sourceGrid, BattleGridKey targetGrid)
    {
        if (_battleDepthLayer == null)
        {
            return 0.0;
        }

        var sourcePosition = GetMarkerPosition(sourceGrid) + new Vector2(0.0f, -22.0f);
        var targetPosition = GetMarkerPosition(targetGrid) + new Vector2(0.0f, -18.0f);
        var travel = targetPosition - sourcePosition;
        if (travel.LengthSquared() < 1.0f)
        {
            return 0.0;
        }

        var direction = travel.Normalized();
        var arrow = new Node2D
        {
            Position = sourcePosition,
            Rotation = travel.Angle(),
            ZIndex = 521
        };
        var shaft = new Line2D
        {
            Width = 2.0f,
            DefaultColor = new Color("e7d6a3")
        };
        shaft.AddPoint(new Vector2(-16.0f, 0.0f));
        shaft.AddPoint(new Vector2(8.0f, 0.0f));
        var arrowhead = new Polygon2D
        {
            Polygon = [new Vector2(14.0f, 0.0f), new Vector2(4.0f, -5.0f), new Vector2(4.0f, 5.0f)],
            Color = new Color("b67635")
        };
        arrow.AddChild(shaft);
        arrow.AddChild(arrowhead);
        _battleDepthLayer.AddChild(arrow);

        var tween = arrow.CreateTween();
        tween.TweenProperty(arrow, "position", targetPosition - direction * 14.0f, ArrowProjectileEffectDurationSeconds);
        tween.TweenCallback(Callable.From(() => arrow.QueueFree()));
        return ArrowProjectileEffectDurationSeconds;
    }

    private static bool IsArrowProjectileAttacker(BattleOccupantInfo occupant)
    {
        return occupant.Category == CategoryUnit &&
               (occupant.TroopType == TroopArcher || occupant.TroopType == TroopCrossbow);
    }

    private async void DestroyOccupantAfterDelay(
        BattleGridKey grid,
        BattleOccupantInfo occupant,
        double delaySeconds,
        BattleOccupantInfo? destroyer = null,
        bool captureOfficer = true)
    {
        if (delaySeconds > 0.0)
        {
            await ToSignal(GetTree().CreateTimer(delaySeconds), SceneTreeTimer.SignalName.Timeout);
        }

        if (!IsOccupantAtGrid(grid, occupant))
        {
            return;
        }

        if (occupant.TroopType == TroopSupplyCart)
        {
            ApplySupplyCartDestroyedMoralePenalty(occupant);
        }

        RemoveOccupant(grid, occupant);
        if (captureOfficer && IsGeneralCountedPiece(occupant.Category, occupant.OfficerName))
        {
            AppendBattleLog(occupant, "Capture", $"{FormatOfficerName(occupant.OfficerName)} is captured after the battle team is destroyed.");
            ShowOfficerCaptureNotice(occupant);
        }
        if (destroyer != null)
        {
            TryShowOfficerSpeech(destroyer, BattleOfficerSpeechEvent.Destroy);
        }
        RefreshBattleDepthLayerOrder();
        RefreshOccludedUnitSilhouettes();
        ConfigureHud();
    }


    private async void RefreshOccludedUnitSilhouettesAfterDelay(double durationSeconds)
    {
        if (durationSeconds > 0.0)
        {
            await ToSignal(GetTree().CreateTimer(durationSeconds), SceneTreeTimer.SignalName.Timeout);
        }

        RefreshOccludedUnitSilhouettes();
    }

    private static string GetInitialInfantryDirectionScene(string teamName)
    {
        _ = teamName;
        return InfantryIdleSouthEastScenePath;
    }

    private static BattleSpriteDirection GetInfantryDirection(Vector2I sourceGrid, Vector2I destinationGrid)
    {
        var delta = destinationGrid - sourceGrid;
        if (delta == Vector2I.Zero)
        {
            return BattleSpriteDirection.SouthEast;
        }

        if (delta.Y == 0)
        {
            return delta.X > 0
                ? BattleSpriteDirection.SouthEast
                : BattleSpriteDirection.NorthWest;
        }

        if (delta.X == 0)
        {
            return delta.Y > 0
                ? BattleSpriteDirection.SouthWest
                : BattleSpriteDirection.NorthEast;
        }

        if (delta.X > 0)
        {
            return delta.Y > 0
                ? BattleSpriteDirection.SouthEast
                : BattleSpriteDirection.NorthEast;
        }

        if (delta.X < 0)
        {
            return delta.Y > 0
                ? BattleSpriteDirection.SouthWest
                : BattleSpriteDirection.NorthWest;
        }

        return delta.Y > 0
            ? BattleSpriteDirection.SouthWest
            : BattleSpriteDirection.NorthEast;
    }



}
