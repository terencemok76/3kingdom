# Three Kingdoms Strategy (Godot 4.6.2, C#)
## Phase 1 Design Document

## Feature Freeze
- Freeze tag: `Phase1-Locked-v1`
- Lock date: `April 12, 2026`
- Purpose: freeze Phase 1 implementation scope to control complexity and prevent feature drift

### In Scope (Phase 1 Locked)
- 2D desktop playable map loop
- Monthly turn flow (player -> AI -> month advance)
- Core city resources and ownership:
- `Gold`, `Food`, `Troops`, `Officers`, `Farm`, `Commercial`, `Defense`, `Loyalty`
- City selection and HUD city info display
- Commands available in UI:
- `Develop`, `Recruit`, `Move`, `Search`, `Attack`
- Baseline AI turn execution
- Win/lose check for city control completion

### Deferred (Phase 1.5+ Backlog)
- Officer job assignment system and titles/rank
- Item/equipment system (`SpecialWeapon`, `SpecialItem`, `SpecialHorse`)
- Officer age/body status/blood relationship systems
- Population-capacity depth (city upgrade and full balancing)
- Succession, defection, riot full event chains and advanced stability simulation
- Advanced AI heuristics for jobs/items/succession

### Change Control Rule
- Any feature not listed under **In Scope (Phase 1 Locked)** is deferred by default
- New additions require explicit post-freeze approval and a new lock tag revision
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

## 3.1 Phase 1.5 Extension (Officer Job System)
- Officer assignment system is introduced as a Phase 1.5 extension on top of Phase 1 loop
- Adds city jobs, officer job title, officer rank, and performance formulas
- Keeps tactical battle/map scope unchanged

## 3.2 Phase 1.5 Extension (Item System)
- Adds officer equipment and special item assignment
- Item assignment affects officer attributes and therefore city/job/combat outcomes
- Item system is strategic-layer only in this phase (no battle scene equipment UI)

## 4. Core Gameplay Model
### 4.1 Time System
- Time unit: 1 month per round
- Turn order each month:
1. Player phase (issue commands to owned cities)
2. AI faction phases (one faction at a time)
3. End-of-month resolution (food upkeep, loyalty drift checks if any, month increment)
- Seasonal resource schedule:
- Collect city `Gold` income in **April**
- Collect city `Food` income in **August**

### 4.2 Factions and Cities
- World contains multiple factions and neutral cities
- Each city has:
- `Ruler` (faction owner)
- `Gold`
- `Food`
- `Troops`
- `Officers[]`
- `Farm`
- `Commercial`
- `Defense`
- `Loyalty`
- `CityType` (`Small`, `Medium`, `Large`)
- `Population`
- `MaxPopulation`
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
- Officers can also be assigned to one city job:
- `Farm` (agriculture)
- `Commercial` (commerce)
- `Defense` (fortification and city security)
- `Training` (soldier drill and combat readiness)
- Officer profile extensions:
- `Age`
- `BodyStatus` (e.g. `Healthy`, `Injured`, `Sick`, `Exhausted`)
- `BloodRelationship` links (with ruler and/or other officers)

### 4.4 Officer Job Title and Rank
- Add to officer profile:
- `JobType` (`None`, `Farm`, `Commercial`, `Defense`, `Training`)
- `JobTitle` (example: `Assistant`, `Director`, `Chief`)
- `Rank` (numeric or enum, e.g. 1-9 where lower number is higher rank)
- City supports one active officer per job in Phase 1.5 baseline
- Optional future expansion: deputy slots per job

### 4.5 Item Categories
- Item types:
- `SpecialWeapon`
- `SpecialItem`
- `SpecialHorse`
- Item ownership rules:
- Item belongs to one faction inventory or one officer
- One item can be assigned to only one officer at a time
- Officer equipment slot policy (Phase 1.5 baseline):
- Max 1 weapon + 1 horse + 1 special item per officer

## 5. Player Commands (Phase 1 Rules)
## 5.1 Develop
- Purpose: Improve city economy
- Cost: small gold amount
- Effect: Improve city development attributes directly in Phase 1
- Suggested formula:
- `gain = 20 + leadOfficer.Intelligence / 5 + random(0..10)`

## 5.2 Recruit
- Purpose: Convert resources into troops
- Cost: gold + food
- Effect: Add troops to city
- Suggested formula:
- `newTroops = 50 + leadOfficer.Charm / 4 + random(0..30)`
- Recruit extension (Phase 1.5):
- City has `Population`
- Recruit count depends on city `Population`, officer `Charm`, spent `Gold` hiring budget, and modifiers
- Heavy recruitment causes direct percentage decrease to city `Loyalty`
- Recruiting troops decreases city `Population` directly
- Recruitment cannot exceed available population constraints
- Population-to-troop conversion ratio: `1 population -> 1 troop`

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
- `defensePower = defendingTroops * (1 + Defense * 0.006)`
- Winner captures/holds city; casualties applied to both sides

## 5.6 Assign Job (Phase 1.5)
- Purpose: assign or reassign officer to one city job
- Constraints:
- Officer must belong to selected city
- Each job has one active slot by default
- Reassignment does **not** consume city monthly action count
- Effect:
- Monthly city output and command effectiveness update based on officer job performance

## 5.7 Assign Item (Phase 1.5)
- Purpose: assign faction-owned item to officer in same faction
- Constraints:
- Officer must be alive/active and in faction roster
- Slot compatibility required (`SpecialWeapon` to weapon slot, etc.)
- Assignment/removal does **not** consume city monthly action count
- Effect:
- Officer attributes update immediately for strategy calculations
- UI and logs show resulting stat delta

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

### 6.3 AI Job Assignment (Phase 1.5)
- AI checks unfilled high-priority jobs first:
1. Low food: fill `Farm`
2. Low gold: fill `Commercial`
3. Border city: fill `Defense`
4. Low troop quality: fill `Training`
- AI favors officers whose best stat matches the job

## 7. Data Model (C#)
## 7.1 Domain Classes
- `OfficerData`
- Fields: `Id`, `Name`, `War`, `Intelligence`, `Charm`, `Loyalty`, `CityId`, `JobType`, `JobTitle`, `Rank`, `SkillTags`, `Age`, `BodyStatus`, `BloodRelationships`
- `CityData`
- Fields: `Id`, `Name`, `OwnerFactionId`, `Gold`, `Food`, `Troops`, `Farm`, `Commercial`, `Defense`, `Loyalty`, `OfficerIds`, `ConnectedCityIds`
- `FactionData`
- Fields: `Id`, `Name`, `IsPlayer`
- `WorldState`
- Fields: `Month`, `Year`, `Cities`, `Officers`, `Factions`, `RandomSeed`

## 7.3 Job Performance Model
- Each job has a base formula:
- `FarmOutput = BaseFarm * (1 + officer.Intelligence * 0.004 + RankBonus + SkillBonus)`
- `CommercialOutput = BaseGold * (1 + officer.Charm * 0.004 + RankBonus + SkillBonus)`
- `DefenseOutput = BaseDefense * (1 + officer.War * 0.003 + officer.Intelligence * 0.002 + RankBonus + SkillBonus)`
- `TrainingOutput = BaseTraining * (1 + officer.War * 0.004 + officer.Charm * 0.002 + RankBonus + SkillBonus)`
- Suggested rank bonus:
- `Rank 1-3: +0.12`
- `Rank 4-6: +0.06`
- `Rank 7-9: +0.02`
- Skill examples:
- `Agriculture`: +10% Farm job output
- `Trade`: +10% Commercial job output
- `Fortification`: +10% Defense job output
- `Drill`: +10% Training job output

## 7.4 Item Data Model
- `ItemData`
- Fields: `Id`, `Name`, `ItemType`, `WarBonus`, `IntelligenceBonus`, `CharmBonus`, `LoyaltyBonus`, `OwnerFactionId`, `EquippedOfficerId`, `Rarity`
- `OfficerData` extension:
- `EquippedWeaponItemId`
- `EquippedHorseItemId`
- `EquippedSpecialItemId`
- `GetEffectiveWar/Intelligence/Charm/Loyalty` should include item bonuses

## 7.5 Item Attribute Effects
- Effective stats used by game logic:
- `effectiveWar = baseWar + itemWarBonus`
- `effectiveIntelligence = baseIntelligence + itemIntelligenceBonus`
- `effectiveCharm = baseCharm + itemCharmBonus`
- `effectiveLoyalty = baseLoyalty + itemLoyaltyBonus`
- Suggested balancing caps:
- Effective stat clamp: `1..100`
- Suggested item rarity bands:
- `Common`: +1~3 total distributed bonus
- `Rare`: +4~7 total distributed bonus
- `Epic`: +8~12 total distributed bonus

## 7.6 Relationship and Condition Model
- `BloodRelationshipData`
- Fields: `TargetOfficerId`, `RelationType` (`Parent`, `Child`, `Sibling`, `Spouse`, `Kin`, `None`), `Strength`
- Ruler relationship shortcut:
- `RulerRelationType` and `RulerRelationStrength` may be cached on officer for fast checks
- `BodyStatus` model:
- Enum values: `Healthy`, `Injured`, `Sick`, `Exhausted`
- Optional duration field: `StatusRemainingMonths`
- Suggested strategy-layer effects:
- `Injured`: `War -10%`
- `Sick`: `Intelligence -10%`, `Charm -10%`
- `Exhausted`: all outputs/job performance `-8%`
- `Healthy`: no penalty
- Suggested blood-relationship effects:
- If assigned ruler has close kin relation: `Loyalty +5~10`
- Kin in same city gives small positive command stability bonus
- Opposing kin in enemy faction can reduce capture/defection chance (optional)

## 7.7 Aging, Death, Defection, Riot Rules
- Officer and ruler aging:
- `Age` increases by 1 each in-game year
- Starting from threshold age (suggested `>= 60`), monthly death check applies
- Suggested death chance by age:
- `60-69`: low chance
- `70-79`: medium chance
- `80+`: high chance
- On ruler death:
- Trigger succession assignment flow:
- Succession resolves immediately in the same month
- Player faction: player can assign any officer in the same faction as next ruler
- AI faction: selection priority is `blood relationship` > `high loyalty` > `high rank`
- If no officer can become ruler, faction ends immediately
- If faction has no cities, faction ends immediately
- Recalculate faction stability and officer loyalty modifiers after succession

- Officer defection / leaving:
- If officer `Loyalty` is below threshold (suggested `< 40`), monthly defection check
- Possible outcomes:
- Leaves service (becomes unaffiliated/hidden)
- Joins another faction (priority: related ruler, neighboring strong faction, scripted bias)
- Defection chance should be reduced by strong blood ties and high city loyalty

- City loyalty riot:
- If city `Loyalty` is below threshold (suggested `< 35`), monthly riot check
- Riot effects:
- One-time loss of `1%` city `Gold`, `Food`, and `Troops` (rounded down, minimum 1 if value > 0)
- Optional command lock penalty for next month (balancing toggle)
- Repeated low loyalty across months increases riot probability

- Citizen relief policy:
- Player/AI can give `Gold` and/or `Food` to citizens to increase city `Loyalty`
- Suggested operation name: `Relief`
- Relief consumes resources immediately and applies instant city loyalty gain
- Fixed rule:
- `+10%` city loyalty per `1000 Food`
- `+10%` city loyalty per `100 Gold`

- Recruitment and loyalty:
- `Recruit` action applies city loyalty pressure (loyalty reduction)
- Recruit action causes direct percentage decrease to city loyalty
- Low loyalty from repeated recruitment can increase riot risk indirectly

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

## 10.1 UI Additions for Jobs (Phase 1.5)
1. City panel shows job slots (`Farm`, `Commercial`, `Defense`, `Training`)
2. Each slot displays assigned officer, title, rank, estimated monthly contribution
3. Officer detail panel shows current job, title, rank, and skill tags
4. Player can open `Assign Job` dialog and switch officer assignment

## 10.2 UI Additions for Items (Phase 1.5)
1. Officer panel displays equipment slots (`Weapon`, `Horse`, `Special`)
2. Item inventory panel lists owned items with stat bonuses and rarity
3. Assign/Remove item actions show before/after effective stat preview
4. City/officer tooltip can show active item bonuses in compact form

## 10.3 UI Additions for Officer Profile (Phase 1.5)
1. Officer card shows `Age`, `BodyStatus`, and relationship summary
2. Relationship panel lists kin links and ruler relation badge
3. Body status icon and color indicator shown in city officer list
4. Tooltips explain active penalties/bonuses from status and relationship

## 11. Resource Economy (Initial Balance)
- Monthly base city income:
- Gold `+50`
- Food `+80`
- Monthly troop upkeep:
- Food cost `troops / 20` (rounded down)
- If food below 0, troop desertion applies

## 11.1 Job-driven Economy (Phase 1.5)
- `Farm` job adds food growth bonus before upkeep
- `Commercial` job adds gold growth bonus
- `Defense` job increases defender effective defense and lowers siege loss
- `Training` job raises monthly troop readiness (used by combat modifier, morale proxy, or recruit quality)
- Low city `Loyalty` applies output penalties and increases riot risk
- Seasonal settlement:
- Gold is collected from cities in April
- Food is collected from cities in August
- Population model:
- City population increases once per year
- Population is capped by `MaxPopulation`
- `MaxPopulation` is determined by `CityType` in Phase 1.5 baseline
- City upgrade can increase capacity later (handled in later phase)
- City type set is fixed to:
- `Small`
- `Medium`
- `Large`
- Annual growth formula depends on:
- `Farm`
- `Commercial`
- `Loyalty`

## 12. Victory and Defeat (Phase 1)
- Victory: player faction controls all cities
- Defeat: player controls zero cities
- Optional soft target: control N cities by month M (for scenario checks)
- If one faction occupies all cities, game ends immediately

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

### M6: Officer Jobs and Rank (Phase 1.5)
- Extend officer data with `JobType`, `JobTitle`, `Rank`, `SkillTags`
- Implement assign/reassign flow
- Apply monthly job performance formulas
- Add UI display and AI assignment heuristics

### M7: Item System (Phase 1.5)
- Add `ItemData` model and initial item database
- Implement item assignment/removal flow
- Apply item bonuses to effective officer attributes
- Add inventory and officer equipment UI
- Integrate item-aware AI evaluation

### M8: Officer Profile Extensions (Phase 1.5)
- Add officer `Age`, `BodyStatus`, and `BloodRelationship` data structures
- Apply condition penalties and relationship-based loyalty modifiers
- Add profile UI and relationship/status indicators
- Add monthly status tick/update handling

### M9: Stability Events (Phase 1.5)
- Implement aging and mortality checks for officer/ruler
- Implement low-loyalty officer defection/leave flow
- Implement low city loyalty riot event flow
- Add succession and faction stability resolution for ruler death
- Add seasonal economy collection flow (April gold, August food)
- Add population-aware recruit formula and loyalty-percentage reduction
- Add annual population growth and max population cap handling
- Add fixed event execution order in month resolver

## 14. Testing Checklist (Phase 1)
- City cannot move/attack to non-connected target
- Recruit blocked when resources insufficient
- Attack can capture city and transfer ownership
- AI turn executes without freezing
- Month increments and upkeep applied consistently
- Win/lose triggers correctly

## 14.1 Testing Checklist (Phase 1.5 Jobs)
- Officer can be assigned to each of 4 jobs
- Reassign updates city output next month
- Rank bonus applies correctly in formula
- Skill bonus applies only to matching job
- Defense and Training effects impact combat calculations
- AI fills empty critical jobs when possible

## 14.2 Testing Checklist (Phase 1.5 Items)
- Item can be assigned only to valid slot and valid officer
- Removing/swapping item updates effective attributes immediately
- Effective attributes are used by job formulas and combat formulas
- Multiple item bonuses stack correctly and respect stat clamp
- AI does not equip duplicate item to multiple officers
- Save/load preserves item ownership and equipment state

## 14.3 Testing Checklist (Phase 1.5 Officer Profile)
- Officer `Age`, `BodyStatus`, and relationship data load correctly
- Body status penalties apply to effective attributes and job output
- Relationship bonuses/penalties apply consistently to loyalty logic
- UI correctly reflects status/age/relationship changes
- Save/load preserves status durations and relationship links

## 14.4 Testing Checklist (Phase 1.5 Stability Events)
- Officer/ruler age increments annually and mortality checks trigger at configured ages
- Ruler death triggers valid succession and game state remains stable
- Succession is resolved in the same month
- Player can assign next ruler for player faction after ruler death
- AI assigns next ruler for AI factions after ruler death
- If no valid officer exists, faction ends correctly
- If faction has no cities, faction ends correctly
- Low officer loyalty can trigger leave/defection with expected probabilities
- Defection respects relationship and city/faction modifiers
- Low city loyalty can trigger riot and correctly apply one-time 1% loss to gold/food/troops
- Relief command increases city loyalty and consumes resources correctly
- Relief follows fixed ratio (`+10%` per `1000 Food` or per `100 Gold`)
- Recruit command reduces city loyalty according to configured rule
- Recruit output depends on population, charm, hiring gold, and modifiers
- Recruit uses fixed conversion `1 population -> 1 troop`
- Recruit reduces city population and does not go below zero
- Annual population growth respects max population cap
- Assign Job and Assign Item do not consume city monthly action count

## 14.5 Testing Checklist (Faction End and Item Ownership)
- When faction ends, officers leave faction affiliation correctly
- Officer-equipped items remain with officer after faction end
- Non-owned/unclaimed items remain in city and are not auto-transferred
- Other faction ruler can hire unaffiliated officer and inherit equipped items
- City-stored items require search/discovery flow before assignment

## 17. Fixed Rules (Locked Defaults)
- Recruit conversion:
- `1 population -> 1 troop`
- City type:
- Use only `Small`, `Medium`, `Large`
- Annual population growth:
- Growth formula must include `Farm`, `Commercial`, `Loyalty`
- Monthly event execution order:
1. Player command phase
2. AI command phase
3. Recruit loyalty penalty and command-side immediate effects
4. Relief effects (loyalty updates)
5. Riot checks and riot one-time losses
6. Officer defection/leave checks
7. Officer/ruler death checks
8. Same-month ruler succession resolution
9. Faction-end checks (no ruler or no cities)
10. Seasonal economy collection (April gold, August food)
11. Upkeep and post-income adjustments
12. Month advance
- Faction end and ownership:
- On faction end, officers leave faction
- Officer items stay with officer
- Items not owned by officers remain in city as searchable assets
- Other factions can hire those officers later

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




