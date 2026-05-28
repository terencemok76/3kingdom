using Godot;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private const string QuickSavePath = "user://saves/quicksave.json";

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        if (keyEvent.Keycode == Key.F5)
        {
            QuickSaveGame();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (keyEvent.Keycode == Key.F9)
        {
            QuickLoadGame();
            GetViewport().SetInputAsHandled();
        }
    }

    private void QuickSaveGame()
    {
        if (_worldRepository == null || _turnManager?.World == null)
        {
            return;
        }

        var saved = _worldRepository.SaveGame(
            QuickSavePath,
            _turnManager.World,
            _localization?.T("fmt.quick_save_name") ?? "Quick Save",
            0);
        AddLog(saved
            ? _localization?.T("log.quick_save_success") ?? "Quick save completed."
            : _localization?.T("log.quick_save_failed") ?? "Quick save failed.",
            isPlayerRelated: true);
    }

    private void QuickLoadGame()
    {
        if (_worldRepository == null || _turnManager == null)
        {
            return;
        }

        var loadedWorld = _worldRepository.LoadSavedGame(QuickSavePath);
        if (loadedWorld == null)
        {
            AddLog(_localization?.T("log.quick_load_missing") ?? "Quick save file not found.", isPlayerRelated: true);
            return;
        }

        ApplyLoadedWorld(loadedWorld);
        AddLog(_localization?.T("log.quick_load_success") ?? "Quick load completed.", isPlayerRelated: true);
    }

    internal void ApplyLoadedWorld(WorldState loadedWorld)
    {
        _turnManager?.Initialize(loadedWorld);
        _gameEnded = false;
        _pendingTargetCommand = CommandType.Pass;
        _pendingOfficerCommand = CommandType.Pass;
        if (_diplomacyUiController != null)
        {
            _diplomacyUiController.PendingProposalCommand = null;
        }
        if (_personnelUiController != null)
        {
            _personnelUiController.PendingSuccessionFactionId = -1;
        }
        _pendingNonAttackResolutionQueue.Clear();
        _pendingAttackResolutionQueue.Clear();
        _isResolvingEndTurn = false;
        _militaryUiController?.ResetAttackDialogState();
        _selectedCity = null;

        HideTransientDialogs();
        ResetAliveFactionSnapshot();

        if (_mapController != null && _localization != null)
        {
            _mapController.BindWorld(loadedWorld, _localization);
        }

        AutoSelectPlayerCityForNewRound();
        if (_selectedCity != null)
        {
            _mapController?.SelectCityById(_selectedCity.Id);
        }

        RefreshAllText();
        EvaluateWinLose();
        _mapController?.RefreshVisuals();
    }

    private void HideTransientDialogs()
    {
        _targetCityMenu?.Hide();
        _merchantUiController?.HideDialogs();
        _militaryUiController?.HideDialogs();
        _personnelUiController?.HideDialogs();
        _advisorUiController?.HideDialogs();
        _civilUiController?.HideDialogs();
        _internalAffairsUiController?.HideDialogs();
        _diplomacyUiController?.HideDialogs();
        _spyUiController?.HideDialogs();
        _systemUiController?.HideDialogs();
        _viewUiController?.HideDialogs();
    }
}
