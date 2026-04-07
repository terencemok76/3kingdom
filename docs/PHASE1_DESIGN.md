# Three Kingdoms Strategy (Godot 4.6.2, C#)
## Phase 1 Design Document

## 1. Overview
- Working title: `3Kingdom`
- Target: 2D desktop strategy game in Godot 4.6.2 using C#
- Inspiration: Romance of the Three Kingdoms I (early-era strategic flow), with simplified Phase 1 scope
- Core loop: Monthly turn-based empire management and expansion through city commands and AI faction turns

## 2. Phase 1 Goals
- Deliver a playable vertical slice with:
- 2D map containing connected cities
- Monthly turn progression
- City and officer simulation basics
- Player command system: `Develop`, `Recruit`, `Move`, `Search`, `Attack`
- Simple AI factions taking turns
- Win/lose conditions for basic campaign flow

## 3. Out of Scope (Phase 1)
- Diplomacy systems (alliances, treaties, marriage)
- Detailed battles/tactics maps
- Weather/disaster/random world events beyond basic search
- Officer skills/perks/equipment
- Save/load UI polish and advanced settings
- Audio/animation polish beyond minimal feedback

## 4. Core Gameplay Model
### 4.1 Time System
- Time unit: 1 month per round
- Turn order each month:
1. Player phase (issue commands to owned cities)
2. AI faction phases (one faction at a time)
3. End-of-month resolution (food upkeep, loyalty drift checks if any, month increment)

### 4.2 Factions and Cities
- World contains multiple factions and neutral cities
- Each city has:
- `Ruler` (faction owner)
- `Gold`
- `Food`
- `Troops`
- `Officers[]`
- Cities are connected by route graph (adjacency list)
- Move/Attack can only target directly connected cities in Phase 1

### 4.3 Officers
- Officer stats:
- `War`
- `Intelligence`
- `Charm`
- `Loyalty`
- Officers are assigned to exactly one city
- Commands use either city aggregate power or a selected lead officer

## 5. Player Commands (Phase 1 Rules)
## 5.1 Develop
- Purpose: Improve city economy
- Cost: small gold amount
- Effect: Increase city gold income potential (simplified immediate gold gain in Phase 1)
- Suggested formula:
- `gain = 20 + leadOfficer.Intelligence / 5 + random(0..10)`

## 5.2 Recruit
- Purpose: Convert resources into troops
- Cost: gold + food
- Effect: Add troops to city
- Suggested formula:
- `newTroops = 50 + leadOfficer.Charm / 4 + random(0..30)`

## 5.3 Move
- Purpose: Relocate troops and optionally officers between connected friendly cities
- Constraint: source and destination must be connected and same faction
- Effect: transfer selected troops/officers

## 5.4 Search
- Purpose: Find hidden officer or bonus resources
- Success chance scales with Intelligence + Charm
- Outcomes:
- Discover officer (joins city with medium loyalty)
- Find gold or food cache
- No result

## 5.5 Attack
- Purpose: Invade connected enemy/neutral city
- Resolution: lightweight numeric combat (no tactical subscene in Phase 1)
- Suggested power:
- `attackPower = attackingTroops * (1 + avgWar / 200.0)`
- `defensePower = defendingTroops * (1 + avgWar / 220.0)`
- Winner captures/holds city; casualties applied to both sides

## 6. AI Design (Phase 1)
### 6.1 AI Profile
- Simple heuristic AI, no long planning tree
- For each AI city each month, choose one action based on priority:
1. If adjacent enemy is weak and own troops sufficient: `Attack`
2. If low troops and enough resources: `Recruit`
3. If economy low: `Develop`
4. If adjacent friendly city needs troops: `Move`
5. Otherwise: `Search`

### 6.2 AI Constraints
- One command per city per month (matches player pacing)
- Use deterministic seed option for reproducible test runs

## 7. Data Model (C#)
## 7.1 Domain Classes
- `OfficerData`
- Fields: `Id`, `Name`, `War`, `Intelligence`, `Charm`, `Loyalty`, `CityId`
- `CityData`
- Fields: `Id`, `Name`, `OwnerFactionId`, `Gold`, `Food`, `Troops`, `OfficerIds`, `ConnectedCityIds`
- `FactionData`
- Fields: `Id`, `Name`, `IsPlayer`
- `WorldState`
- Fields: `Month`, `Year`, `Cities`, `Officers`, `Factions`, `RandomSeed`

## 7.2 Runtime Services
- `TurnManager`: controls monthly sequence and phase transitions
- `CommandResolver`: validates and executes player/AI commands
- `CombatResolver`: resolves attack outcomes
- `AiController`: selects AI commands
- `WorldRepository`: loads initial scenario data

## 8. Scene Architecture (Godot)
- Root scene: `Main.tscn`
- Child scenes:
- `MapScene.tscn` (city nodes, route lines, input hitboxes)
- `HUD.tscn` (month display, city panel, command panel, log panel)
- Optional modal scenes:
- `CityDetailPanel.tscn`
- `CommandDialog.tscn`

## 9. Suggested Folder Structure
```text
res://
  scenes/
    main/Main.tscn
    map/MapScene.tscn
    ui/HUD.tscn
    ui/CityDetailPanel.tscn
    ui/CommandDialog.tscn
  scripts/
    core/
      GameBootstrap.cs
      TurnManager.cs
      CommandResolver.cs
      CombatResolver.cs
      AiController.cs
      WorldRepository.cs
    data/
      OfficerData.cs
      CityData.cs
      FactionData.cs
      WorldState.cs
      CommandModels.cs
    map/
      MapController.cs
      CityNode.cs
      RouteRenderer.cs
    ui/
      HudController.cs
      CommandPanel.cs
      CityInfoPanel.cs
      LogPanel.cs
  data/
    scenarios/
      phase1_scenario.json
```

## 10. UI/UX Flow (Phase 1)
1. Player clicks city on map
2. HUD shows city data and available commands
3. Player picks command, enters required values (troops target city etc.)
4. Command executes, log updates
5. When all player cities have acted or player ends turn, AI turns run automatically
6. Month advances and upkeep applies

## 11. Resource Economy (Initial Balance)
- Monthly base city income:
- Gold `+50`
- Food `+80`
- Monthly troop upkeep:
- Food cost `troops / 20` (rounded down)
- If food below 0, troop desertion applies

## 12. Victory and Defeat (Phase 1)
- Victory: player faction controls all cities
- Defeat: player controls zero cities
- Optional soft target: control N cities by month M (for scenario checks)

## 13. Implementation Milestones
### M1: Project Skeleton
- Create scenes, folders, C# files, and game bootstrap
- Load mock scenario into memory

### M2: Map + Selection + HUD
- Render city nodes and route lines
- City click selection and info panel

### M3: Command Execution
- Implement Develop, Recruit, Move, Search, Attack with validation
- Add battle and log output

### M4: Turn & AI
- Monthly flow, AI actions, upkeep, win/lose checks

### M5: Stabilization
- Balancing pass
- Basic bug fix and polish pass

## 14. Testing Checklist (Phase 1)
- City cannot move/attack to non-connected target
- Recruit blocked when resources insufficient
- Attack can capture city and transfer ownership
- AI turn executes without freezing
- Month increments and upkeep applied consistently
- Win/lose triggers correctly

## 15. Risks and Mitigations
- Risk: Early formulas create snowballing
- Mitigation: Centralize constants and tune quickly
- Risk: Turn state bugs from mixed UI/game logic
- Mitigation: Keep `TurnManager` authoritative; UI only sends intents
- Risk: AI stalls (no valid actions)
- Mitigation: Always include fallback `Search` or `Pass`

## 16. Phase 1 Deliverable Definition
- Playable loop from start to end condition
- All required commands functional
- AI factions take full monthly turns
- Code organized by structure above, with clear separation between data, core logic, map, and UI
