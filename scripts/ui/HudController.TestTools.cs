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

    private void OnTestBattleEquipmentPressed()
    {
        if (_turnManager?.World == null || _selectedCity == null)
        {
            return;
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        if (_selectedCity.OwnerFactionId != playerFactionId)
        {
            AddLog(_localization?.IsTraditionalChinese == true
                ? "只有玩家城市可取得測試戰役裝備。"
                : "Test battle equipment can only be added to a player-owned city.");
            return;
        }

        _selectedCity.RamCount += 1;
        _selectedCity.LadderCount += 1;
        _selectedCity.CatapultCount += 1;
        _selectedCity.SupplyCartCount += 1;
        AddLog(_localization?.IsTraditionalChinese == true
            ? $"「{_selectedCity.NameZhHant}」獲得戰役裝備：補給車、衝車、雲梯、投石車各 1。"
            : $"{_selectedCity.Name} received one supply cart, ram, ladder, and catapult.",
            isPlayerRelated: true);
        _uiEventHub.PublishCityStateChanged(_selectedCity.Id, playerFactionId);
        RefreshSelectedCity();
    }

    private void OnTestEngineersPressed()
    {
        if (_turnManager?.World == null || _selectedCity == null)
        {
            return;
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        if (_selectedCity.OwnerFactionId != playerFactionId)
        {
            AddLog(_localization?.IsTraditionalChinese == true
                ? "只有玩家城市可取得測試工兵。"
                : "Test engineers can only be added to a player-owned city.");
            return;
        }

        _selectedCity.EngineerTroops += 300;
        AddLog(_localization?.IsTraditionalChinese == true
            ? $"「{_selectedCity.NameZhHant}」獲得工兵 +300。"
            : $"{_selectedCity.Name} received 300 engineers.",
            isPlayerRelated: true);
        _uiEventHub.PublishCityStateChanged(_selectedCity.Id, playerFactionId);
        RefreshSelectedCity();
    }

    private void OnTestBowWorkshopUpgradePressed() => UpgradeTestFacility(ConstructionProjectType.BowWorkshop);

    private void OnTestHorsePastureUpgradePressed() => UpgradeTestFacility(ConstructionProjectType.HorsePasture);

    private void UpgradeTestFacility(ConstructionProjectType projectType)
    {
        if (_turnManager?.World == null || _selectedCity == null)
        {
            return;
        }

        var playerFactionId = _turnManager.GetPlayerFactionId();
        if (_selectedCity.OwnerFactionId != playerFactionId)
        {
            AddLog(_localization?.T("log.test_player_city_only") ?? "Test facilities can only be upgraded in a player-owned city.");
            return;
        }

        var nextLevel = ConstructionRules.GetLevel(_selectedCity, projectType) + 1;
        if (projectType == ConstructionProjectType.BowWorkshop)
        {
            _selectedCity.BowWorkshopLevel = nextLevel;
            _selectedCity.BowWorkshopProgress = 0;
        }
        else
        {
            _selectedCity.HorsePastureLevel = nextLevel;
            _selectedCity.HorsePastureProgress = 0;
        }
        var facilityName = _localization?.T(projectType == ConstructionProjectType.BowWorkshop
            ? "ui.bow_workshop"
            : "ui.horse_pasture") ?? (projectType == ConstructionProjectType.BowWorkshop ? "Bow Workshop" : "Horse Pasture");
        var cityName = _localization?.GetCityName(_selectedCity) ?? _selectedCity.Name;
        AddLog(_localization?.Format("log.test_facility_upgraded", cityName, facilityName, nextLevel)
            ?? $"{cityName}'s {facilityName} reached Lv.{nextLevel}.",
            isPlayerRelated: true);
        _uiEventHub.PublishCityStateChanged(_selectedCity.Id, playerFactionId);
        RefreshSelectedCity();
    }

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
