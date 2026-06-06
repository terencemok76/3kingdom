using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private const string TestCaptiveOfficerNameEn = "Temp Captive";
    private const string TestCaptiveOfficerNameZhHant = "測試俘虜";

    private void OnTestCapturePressed()
    {
        if (_turnManager?.World == null || _selectedCity == null)
        {
            return;
        }

        var world = _turnManager.World;
        var playerFactionId = _turnManager.GetPlayerFactionId();
        if (_selectedCity.OwnerFactionId != playerFactionId)
        {
            AddLog(_localization?.IsTraditionalChinese == true
                ? "只有玩家城市可建立測試俘虜。"
                : "Test captives can only be created in a player-owned city.");
            return;
        }

        CleanupTemporaryDebugOfficers(world);
        var testOfficer = CreateTemporaryCapturedOfficer(world, _selectedCity, playerFactionId);
        world.PendingCapturedOfficerRecords.Add(new WorldState.PendingCapturedOfficerData
        {
            WinnerFactionId = playerFactionId,
            WinnerCityId = _selectedCity.Id,
            OfficerId = testOfficer.Id,
            IsTestOnly = true
        });

        var officerName = _localization?.IsTraditionalChinese == true
            ? (!string.IsNullOrWhiteSpace(testOfficer.NameZhHant) ? testOfficer.NameZhHant : testOfficer.Name)
            : (!string.IsNullOrWhiteSpace(testOfficer.Name) ? testOfficer.Name : testOfficer.NameZhHant);
        AddLog(_localization?.IsTraditionalChinese == true
            ? $"已建立測試俘虜「{officerName}」，並開啟俘虜處置視窗。"
            : $"Created test captive \"{officerName}\" and opened the captured-officer dialog.",
            isPlayerRelated: true);
        _uiEventHub.PublishCityStateChanged(_selectedCity.Id, playerFactionId);
        _militaryUiController?.ShowCapturedOfficerDialog();
    }

    private OfficerData CreateTemporaryCapturedOfficer(WorldState world, CityData jailCity, int captorFactionId)
    {
        var officerId = world.Officers.Count == 0 ? 1 : world.Officers.Max(officer => officer.Id) + 1;
        var officer = new OfficerData
        {
            Id = officerId,
            Name = $"{TestCaptiveOfficerNameEn} {officerId}",
            NameZhHant = $"{TestCaptiveOfficerNameZhHant}{officerId}",
            Role = "Common",
            Sex = "Male",
            BirthYear = Math.Max(1, world.Year - 26),
            Strength = 72,
            Intelligence = 76,
            Charm = 61,
            Leadership = 70,
            Politics = 58,
            Loyalty = 92,
            Ambition = 88,
            Combat = 74,
            RelationshipType = new Dictionary<string, string>(),
            CityId = 0,
            CaptiveFactionId = captorFactionId,
            JailedCityId = jailCity.Id,
            FreeOfficerStayMonths = 0,
            IsTemporaryDebugOfficer = true
        };
        world.Officers.Add(officer);
        return officer;
    }

    private static void CleanupTemporaryDebugOfficers(WorldState world)
    {
        var tempOfficerIds = world.Officers
            .Where(officer => officer.IsTemporaryDebugOfficer)
            .Select(officer => officer.Id)
            .ToHashSet();
        if (tempOfficerIds.Count == 0)
        {
            return;
        }

        foreach (var city in world.Cities)
        {
            city.OfficerIds.RemoveAll(tempOfficerIds.Contains);
        }

        foreach (var faction in world.Factions)
        {
            faction.OfficerIds.RemoveAll(tempOfficerIds.Contains);
            if (tempOfficerIds.Contains(faction.ChancellorOfficerId))
            {
                faction.ChancellorOfficerId = 0;
            }

            if (tempOfficerIds.Contains(faction.ChiefStrategistOfficerId))
            {
                faction.ChiefStrategistOfficerId = 0;
            }
        }

        world.InternalAffairsSchedules.RemoveAll(schedule => tempOfficerIds.Contains(schedule.OfficerId));
        world.PendingCommands.RemoveAll(command => command.OfficerIds.Any(tempOfficerIds.Contains));
        world.PendingCapturedOfficerRecords.RemoveAll(record => tempOfficerIds.Contains(record.OfficerId));

        foreach (var item in world.Items.Where(item => tempOfficerIds.Contains(item.EquippedOfficerId)))
        {
            item.EquippedOfficerId = 0;
        }

        world.Officers.RemoveAll(officer => tempOfficerIds.Contains(officer.Id));
    }
}
