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
            _localization?.IsTraditionalChinese == true ? "快速存檔" : "Quick Save",
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

    private void ApplyLoadedWorld(WorldState loadedWorld)
    {
        _turnManager?.Initialize(loadedWorld);
        _gameEnded = false;
        _pendingTargetCommand = CommandType.Pass;
        _pendingOfficerCommand = CommandType.Pass;
        _pendingDefenseCommand = null;
        _pendingDiplomacyProposalCommand = null;
        _pendingSuccessionFactionId = -1;
        _pendingNonAttackResolutionQueue.Clear();
        _pendingAttackResolutionQueue.Clear();
        _isResolvingEndTurn = false;
        _attackDiplomacyWarningAcknowledgedTargetCityId = -1;
        _attackDialogContextCity = null;
        _attackDialogMode = AttackDialogMode.Attack;
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
        _merchantDialog?.Hide();
        _militaryDialog?.Hide();
        _personnelDialog?.Hide();
        _personnelBonusDialog?.Hide();
        _assignRoleDialog?.Hide();
        _fireOfficerDialog?.Hide();
        _requestItemDialog?.Hide();
        _hireOfficerDialog?.Hide();
        _civilDialog?.Hide();
        _civilReliefDialog?.Hide();
        _internalAffairsDialog?.Hide();
        _moveDialog?.Hide();
        _attackDialog?.Hide();
        _diplomacyDialog?.Hide();
        _diplomacyProposalDialog?.Hide();
        _spyDialog?.Hide();
        _optionDialog?.Hide();
        _saveLoadDialog?.Hide();
        _successionDialog?.Hide();
        _officerListDialog?.Hide();
        _officerDetailDialog?.Hide();
    }
}
