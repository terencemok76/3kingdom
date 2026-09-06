using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static ThreeKingdom.Battle.BattleBalanceSettings;
using static ThreeKingdom.Battle.BattleUnitTypes;

namespace ThreeKingdom.Battle;

public partial class BattleSceneController
{
    private static string FormatLogTeamName(string teamName)
    {
        if (teamName.Contains("Attacker"))
        {
            return "A";
        }

        if (teamName.Contains("Defender"))
        {
            return "B";
        }

        return teamName;
    }

    private static string FormatLogUnit(BattleOccupantInfo unit)
    {
        if (string.IsNullOrWhiteSpace(unit.OfficerName))
        {
            return unit.DisplayName;
        }

        return $"{unit.OfficerName}/{unit.TroopType}";
    }

    private static string FormatMorale(BattleOccupantInfo unit)
    {
        return unit.Morale.HasValue ? unit.Morale.Value.ToString("N0") : "-";
    }

    private static string FormatWeaponAmmo(BattleOccupantInfo unit)
    {
        return unit.WeaponAmmo.HasValue && unit.MaxWeaponAmmo.HasValue
            ? $"{unit.WeaponAmmo.Value:N0}/{unit.MaxWeaponAmmo.Value:N0}"
            : "-";
    }

    private static string FormatWeaponAmmoLog(BattleOccupantInfo unit)
    {
        return unit.WeaponAmmo.HasValue && unit.MaxWeaponAmmo.HasValue
            ? $" (ammo {unit.WeaponAmmo.Value:N0}/{unit.MaxWeaponAmmo.Value:N0})"
            : string.Empty;
    }

    private static string FormatNormalAttackAmmoLog(BattleOccupantInfo unit, bool isWeakCloseAttack)
    {
        if (isWeakCloseAttack)
        {
            return unit.MaxWeaponAmmo.HasValue
                ? $" (weak close attack, ammo 0/{unit.MaxWeaponAmmo.Value:N0})"
                : " (weak close attack)";
        }

        return FormatWeaponAmmoLog(unit);
    }

    private static string FormatWoundedTroops(BattleOccupantInfo unit)
    {
        return unit.Category == CategoryUnit ? unit.WoundedTroops.ToString("N0") : "-";
    }

    private string BuildEmptyUnitMenuInfoText()
    {
        return string.Join(
            "\n",
            BattleFormat("ui.battle.menu_officer", "Officer: {0}", "-"),
            BattleFormat("ui.battle.menu_type", "Type: {0}", "-"),
            BattleFormat("ui.battle.menu_status", "Status: {0}", "-"),
            BattleFormat("ui.battle.menu_command", "Command: {0}", "-"),
            BattleFormat("ui.battle.menu_active_wounded", "Troops: {0:N0} ({1})", "-", "-"));
    }

    private string FormatUnitCategory(string category)
    {
        return category switch
        {
            CategoryUnit => BattleText("ui.battle.category_unit", "Unit"),
            CategorySiegeEngine => BattleText("ui.battle.category_siege_engine", "Siege Engine"),
            _ => category
        };
    }

    private string FormatTroopType(string troopType)
    {
        return troopType switch
        {
            TroopInfantry => BattleText("ui.battle.troop_infantry", "Infantry"),
            TroopSpearman => BattleText("ui.battle.troop_spearman", "Spearman"),
            TroopArcher => BattleText("ui.battle.troop_archer", "Archer"),
            TroopCavalry => BattleText("ui.battle.troop_cavalry", "Cavalry"),
            TroopCrossbow => BattleText("ui.battle.troop_crossbow", "Crossbow"),
            TroopGuard => BattleText("ui.battle.troop_guard", "Guard"),
            TroopWorker => BattleText("ui.battle.troop_worker", "Worker"),
            TroopRam => BattleText("ui.battle.troop_ram", "Ram"),
            TroopLadder => BattleText("ui.battle.troop_ladder", "Ladder"),
            TroopCatapult => BattleText("ui.battle.troop_catapult", "Catapult"),
            TroopSupplyCart => BattleText("ui.battle.troop_supply_cart", "Supply Cart"),
            _ => troopType
        };
    }

    private string FormatOfficerName(string officerName)
    {
        return officerName switch
        {
            "Xiahou Yuan" => BattleText("ui.battle.officer_xiahou_yuan", "Xiahou Yuan"),
            "Zhang He" => BattleText("ui.battle.officer_zhang_he", "Zhang He"),
            "Dong Zhuo" => BattleText("ui.battle.officer_dong_zhuo", "Dong Zhuo"),
            "Li Jue" => BattleText("ui.battle.officer_li_jue", "Li Jue"),
            "Guo Si" => BattleText("ui.battle.officer_guo_si", "Guo Si"),
            "Cao Hong" => BattleText("ui.battle.officer_cao_hong", "Cao Hong"),
            "Cao Chun" => BattleText("ui.battle.officer_cao_chun", "Cao Chun"),
            "" => "-",
            _ => officerName
        };
    }

    private string FormatMarkerName(string officerName, string displayName, string troopType)
    {
        if (!string.IsNullOrWhiteSpace(officerName) &&
            !string.Equals(officerName, "Worker", StringComparison.OrdinalIgnoreCase))
        {
            return FormatOfficerName(officerName);
        }

        return string.IsNullOrWhiteSpace(troopType)
            ? displayName
            : FormatTroopType(troopType);
    }

    private void RefreshMarkerNamePlates()
    {
        foreach (var occupant in _occupantsByGrid.Values.SelectMany(static occupants => occupants))
        {
            if (occupant.Marker != null)
            {
                occupant.Marker.SetupNamePlate(FormatMarkerName(occupant.OfficerName, occupant.DisplayName, occupant.TroopType));
            }
        }
    }

    private void RefreshOfficerPortrait()
    {
        if (_officerPortrait == null)
        {
            return;
        }

        if (_selectedUnit == null || !IsGeneralCountedPiece(_selectedUnit.Category, _selectedUnit.OfficerName))
        {
            _officerPortrait.Texture = null;
            _officerPortrait.Visible = false;
            return;
        }

        var texture = GetOfficerPortraitTexture(_selectedUnit.OfficerName);
        _officerPortrait.Texture = texture;
        _officerPortrait.Visible = texture != null;
    }

    private Texture2D? GetOfficerPortraitTexture(string officerName)
    {
        if (_officerPortraitTextures.TryGetValue(officerName, out var cachedTexture))
        {
            return cachedTexture;
        }

        if (!BattleOfficerPortraitCatalog.TryGet(officerName, out var definition))
        {
            return null;
        }

        var sheetTexture = GD.Load<Texture2D>(definition.SheetPath);
        if (sheetTexture == null)
        {
            return null;
        }

        var portraitTexture = new AtlasTexture
        {
            Atlas = sheetTexture,
            Region = definition.Region
        };
        _officerPortraitTextures[officerName] = portraitTexture;
        return portraitTexture;
    }

    private string FormatStrategyAvailability(BattleOccupantInfo unit)
    {
        if (unit.Marker != null && _strategyUsedByMarkerThisTurn.Contains(unit.Marker))
        {
            return BattleText("ui.battle.already_used_this_turn", "Already used this turn");
        }

        var strategyAction = ResolveStrategyAction(unit, _selectedUnitGrid);
        if (strategyAction != BattleStrategyAction.Extinguish && !HasStrategyPlans(unit.TeamName))
        {
            return BattleText("ui.battle.no_strategy_plan", "No strategy plan");
        }

        return strategyAction switch
        {
            BattleStrategyAction.Extinguish => BattleFormat("ui.battle.extinguish_ready", "Extinguish Ready (Energy {0})", ExtinguishFireEnergyCost),
            BattleStrategyAction.Fire => BattleText("ui.battle.fire_ready", "Fire Ready"),
            BattleStrategyAction.Mental => BattleText("ui.battle.mess_calm_ready", "Mess / Calm Ready"),
            _ => BattleText("ui.battle.unavailable", "Unavailable")
        };
    }


    private string FormatBattleStatus(BattleOccupantInfo unit)
    {
        var statuses = new List<string>();
        if (unit.IsHidden)
        {
            statuses.Add(BattleText("ui.battle.status_hidden", "Hidden"));
        }

        if (IsMessed(unit))
        {
            statuses.Add(BattleFormat("ui.battle.status_mess_turns", "Mess ({0} turns)", unit.MessTurns));
        }

        if (unit.HasAttackedThisTurn && !unit.IsGuarding)
        {
            statuses.Add(BattleText("ui.battle.status_attacked", "Attacked (cannot move)"));
        }
        if (unit.IsGuarding)
        {
            var nextReductionPercent = GetGuardDamageReductionRatio(unit.GuardDamageReductionCount) * 100.0f;
            statuses.Add(BattleFormat(
                unit.GuardCounterAvailable ? "ui.battle.status_guard" : "ui.battle.status_guard_counter_used",
                unit.GuardCounterAvailable ? "Guard (next reduction {0:0.#}%)" : "Guard (counter used; next reduction {0:0.#}%)",
                nextReductionPercent));
        }

        return statuses.Count == 0 ? BattleText("ui.battle.status_normal", "Normal") : string.Join(", ", statuses);
    }


    private static string FormatWorkerWorkAction(WorkerWorkAction action, bool removedWoodFence = false)
    {
        return action switch
        {
            WorkerWorkAction.WoodFence => removedWoodFence ? "removes wood fence" : "installs wood fence",
            _ => "works on bridge"
        };
    }

    private static string FormatGrid(Vector2I? grid)
    {
        return grid.HasValue ? $"({grid.Value.X}, {grid.Value.Y})" : "-";
    }

    private static string FormatGrid(BattleGridKey? gridKey, Vector2I? fallbackGrid)
    {
        if (gridKey.HasValue)
        {
            return gridKey.Value.ToString();
        }

        return FormatGrid(fallbackGrid);
    }

    private string BuildInfoText()
    {
        if (!_selectedGrid.HasValue || _mapData == null)
        {
            return BattleText("ui.battle.tile_info", "Tile Info") + "\n" +
                   BattleFormat("ui.battle.coordinate", "Coordinate: {0}", "-") + "\n" +
                   BattleText("ui.battle.inspect_hint", "Click a tile to inspect terrain, structure, deployment zone, and units.");
        }

        var grid = _selectedGrid.Value;
        var cell = _mapData.GetCell(grid.X, grid.Y);
        var builder = new StringBuilder();
        builder.AppendLine(BattleText("ui.battle.tile_info", "Tile Info"));
        builder.AppendLine(BattleFormat("ui.battle.coordinate", "Coordinate: {0}", FormatGrid(_selectedGridKey, _selectedGrid)));
        builder.AppendLine(BattleFormat("ui.battle.terrain", "Terrain: {0}", FormatTerrain(cell.Terrain)));
        builder.AppendLine(BattleFormat("ui.battle.structure", "Structure: {0}", FormatStructure(cell.Structure)));
        if (cell.Structure != BattleStructureType.None)
        {
            builder.AppendLine(BattleFormat("ui.battle.facing", "Facing: {0}", FormatStructureFacing(cell.StructureFacing)));
        }
        if (cell.Structure == BattleStructureType.Gate)
        {
            builder.AppendLine(BattleFormat("ui.battle.gate_segment", "Gate Segment: {0}", FormatGateSegment(cell.GateSegment)));
        }
        if (cell.HasStructureHealth)
        {
            var durability = GetDisplayStructureDurability(grid, cell);
            builder.AppendLine(BattleFormat("ui.battle.durability", "Durability: {0}/{1}", durability.Current, durability.Max));
            builder.AppendLine(BattleFormat("ui.battle.status", "Status: {0}", cell.IsBroken ? BattleText("ui.battle.broken", "Broken") : BattleText("ui.battle.intact", "Intact")));
        }
        if (cell.HasBridgeHealth)
        {
            builder.AppendLine(BattleFormat("ui.battle.bridge_hp", "Bridge HP: {0}/{1}", cell.BridgeHealth, cell.BridgeMaxHealth));
            var bridgeStatus = cell.IsBridgeUnderConstruction
                ? BattleText("ui.battle.under_construction", "Under Construction")
                : BattleBridgeSystem.IsHeavilyDamaged(cell)
                    ? BattleText("ui.battle.heavily_damaged", "Heavily Damaged")
                    : cell.IsBridgeDamaged
                        ? BattleText("ui.battle.damaged", "Damaged")
                        : BattleText("ui.battle.complete", "Complete");
            builder.AppendLine(BattleFormat("ui.battle.bridge_status", "Bridge Status: {0}", bridgeStatus));
            var bridgePassage = cell.IsBridgeUnderConstruction
                ? BattleText("ui.battle.bridge_passage_none", "Impassable")
                : cell.IsWoodenBridge || BattleBridgeSystem.IsHeavilyDamaged(cell)
                    ? BattleText("ui.battle.bridge_passage_regular_only", "Regular Troops Only")
                    : BattleText("ui.battle.bridge_passage_all", "All Units");
            builder.AppendLine(BattleFormat("ui.battle.bridge_passage", "Bridge Passage: {0}", bridgePassage));
        }

        builder.AppendLine(BattleFormat("ui.battle.deployment", "Deployment: {0}", FormatDeploymentZone(cell.DeploymentZone)));
        builder.AppendLine(BattleFormat("ui.battle.height", "Height: {0}", cell.HeightLevel));
        builder.AppendLine(BattleFormat("ui.battle.blocks_move", "Blocks Move: {0}", IsCellBlockingMovement(cell) ? BattleText("ui.battle.yes", "Yes") : BattleText("ui.battle.no", "No")));
        if (cell.ProvidesBuildingCover)
        {
            var coverStatus = IsBuildingCoverActive(_selectedGridKey)
                ? BattleFormat("ui.battle.damage_reduction", "Damage -{0}%", Mathf.RoundToInt(BuildingCoverDamageReduction * 100.0f))
                : BattleText("ui.battle.building_cover_disabled_fire", "Disabled while burning");
            builder.AppendLine(BattleFormat("ui.battle.building_defense", "Building Defense: {0}", coverStatus));
        }
        if (cell.IsDefenseOutpost)
        {
            var owner = cell.DefenseOutpostOwner == BattleOutpostOwner.Defender ? "Defender (Blue)" : cell.DefenseOutpostOwner == BattleOutpostOwner.Attacker ? "Attacker (Red)" : "None";
            builder.AppendLine($"Defense Outpost: {owner}");
        }
        if (_activeFireByGrid.TryGetValue(ToGroundGridKey(grid), out var fireState))
        {
            builder.AppendLine(BattleFormat("ui.battle.fire_burning", "Fire: Burning ({0} turn left)", fireState.RemainingTurns));
        }
        else
        {
            builder.AppendLine(BattleFormat("ui.battle.fire", "Fire: {0}", BattleText("ui.battle.none", "None")));
        }
        if (cell.Structure == BattleStructureType.Gate)
        {
            builder.AppendLine(BattleFormat("ui.battle.gate", "Gate: {0}", cell.IsGateOpen ? BattleText("ui.battle.open", "Open") : BattleText("ui.battle.closed", "Closed")));
        }

        builder.AppendLine(BattleText("ui.battle.occupants", "Occupants"));

        var occupantsAtGrid = GetOccupantsAtSelectedGrid(grid)
            .Where(entry => IsVisibleToCurrentTurnSide(entry.Occupant))
            .ToList();
        if (occupantsAtGrid.Count > 0)
        {
            foreach (var (gridKey, occupant) in occupantsAtGrid)
            {
                var hpText = occupant.Category == CategorySiegeEngine
                    ? BattleFormat("ui.battle.inline_hp", " HP {0}/{1}", occupant.HitPoints, occupant.MaxHitPoints)
                    : BattleFormat("ui.battle.inline_unit_stats", " Active {0:N0}/{1:N0} Wounded {2} Morale {3}", occupant.TroopCount, occupant.MaxHitPoints, FormatWoundedTroops(occupant), FormatMorale(occupant));
                var ammoText = occupant.MaxWeaponAmmo.HasValue
                    ? BattleFormat("ui.battle.inline_ammo", " Ammo {0}", FormatWeaponAmmo(occupant))
                    : string.Empty;
                var statusText = occupant.IsHidden || IsMessed(occupant)
                    ? BattleFormat("ui.battle.inline_status", " Status {0}", FormatBattleStatus(occupant))
                    : string.Empty;
                builder.AppendLine($"- {FormatUnitCategory(occupant.Category)}: {occupant.DisplayName} [{occupant.ShortLabel}] L{gridKey.Level}{hpText}{ammoText}{statusText}");
            }
        }
        else
        {
            builder.AppendLine($"- {BattleText("ui.battle.none", "None")}");
        }

        if (_selectedUnit != null && _selectedUnitGrid.HasValue)
        {
            builder.AppendLine(BattleText("ui.battle.selected_piece", "Selected Piece"));
            builder.AppendLine($"- {_selectedUnit.DisplayName} [{_selectedUnit.ShortLabel}]");
            builder.AppendLine(BattleFormat("ui.battle.list_category", "- Category: {0}", FormatUnitCategory(_selectedUnit.Category)));
            builder.AppendLine(BattleFormat("ui.battle.list_grid", "- Grid: {0}", $"({_selectedUnitGrid.Value.X}, {_selectedUnitGrid.Value.Y}, L{_selectedUnitGrid.Value.Level})"));
            builder.AppendLine(BattleFormat("ui.battle.list_status", "- Status: {0}", FormatBattleStatus(_selectedUnit)));
            builder.AppendLine(BattleFormat("ui.battle.list_move_range", "- Move Range: {0}/{1}", _selectedUnit.RemainingMoveRange, GetTeamMoveRangeCap(_selectedUnit)));
            builder.AppendLine(BattleFormat("ui.battle.list_energy", "- Energy: {0}/{1}", _selectedUnit.Energy, GetTeamEnergyCap(_selectedUnit.TeamName)));
            if (_selectedUnit.MaxWeaponAmmo.HasValue)
            {
                builder.AppendLine(BattleFormat("ui.battle.list_weapon_ammo", "- Weapon Ammo: {0}", FormatWeaponAmmo(_selectedUnit)));
            }

            var effectiveAttackRange = GetEffectiveAttackRange(_selectedUnit, _selectedUnitGrid);
            var attackRangeText = effectiveAttackRange == _selectedUnit.AttackRange
                ? _selectedUnit.AttackRange.ToString()
                : BattleFormat("ui.battle.effective_value", "{0} (effective {1})", _selectedUnit.AttackRange, effectiveAttackRange);
            builder.AppendLine(BattleFormat("ui.battle.list_attack_range", "- Attack Range: {0}", attackRangeText));
            if (_selectedUnit.Category == CategorySiegeEngine)
            {
                builder.AppendLine(BattleFormat("ui.battle.list_hp", "- HP: {0}/{1}", _selectedUnit.HitPoints, _selectedUnit.MaxHitPoints));
            }
            else
            {
                builder.AppendLine(BattleFormat("ui.battle.list_active_troops", "- Active Troops: {0:N0}/{1:N0}", _selectedUnit.TroopCount, _selectedUnit.MaxHitPoints));
                builder.AppendLine(BattleFormat("ui.battle.list_wounded_troops", "- Wounded Troops: {0}", FormatWoundedTroops(_selectedUnit)));
                builder.AppendLine(BattleFormat("ui.battle.list_morale", "- Morale: {0}", FormatMorale(_selectedUnit)));
            }

            builder.AppendLine(BattleFormat("ui.battle.list_reachable_tiles", "- Reachable Tiles: {0}", _movableGrids.Count));
            builder.AppendLine(BattleFormat("ui.battle.list_attackable_tiles", "- Attackable Tiles: {0}", _attackableGrids.Count));
            builder.AppendLine(BattleFormat("ui.battle.list_workable_tiles", "- Workable Tiles: {0}", _workableGrids.Count));
            builder.AppendLine(BattleFormat("ui.battle.list_strategy_targets", "- Strategy Targets: {0}", _strategyTargetGrids.Count));
            builder.AppendLine(BattleFormat("ui.battle.list_duel_targets", "- Duel Targets: {0}", _duelTargetGrids.Count));
            builder.AppendLine(BattleFormat("ui.battle.list_strategy", "- Strategy: {0}", FormatStrategyAvailability(_selectedUnit)));
            if (_selectedUnit.TroopType == TroopSupplyCart)
            {
                builder.AppendLine(BattleFormat("ui.battle.list_supply", "- Supply: {0}", HasSupplyTargets() ? BattleText("ui.battle.ready", "Ready") : BattleText("ui.battle.unavailable", "Unavailable")));
            }
            builder.AppendLine(BattleFormat("ui.battle.list_command_state", "- Command State: {0}", FormatCommandMode(_commandMode)));
            builder.AppendLine(BattleFormat("ui.battle.list_current_turn", "- Current Turn: {0}", FormatTeamName(GetCurrentTurnSideName())));
        }

        return builder.ToString().TrimEnd();
    }

    private (int Current, int Max) GetDisplayStructureDurability(Vector2I grid, BattleCellData cell)
    {
        if (cell.Structure != BattleStructureType.Gate || _mapData == null)
        {
            return (cell.StructureHealth, cell.StructureMaxHealth);
        }

        var group = GetConnectedGateGroup(grid);
        if (group.Count == 0)
        {
            return (cell.StructureHealth, cell.StructureMaxHealth);
        }

        var current = group
            .Select(gateGrid => _mapData.GetCell(gateGrid.X, gateGrid.Y).StructureHealth)
            .Min();
        return (current, BattleCellData.GateMaxHealth);
    }


}
