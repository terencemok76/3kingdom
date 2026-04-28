# Three Kingdoms Strategy (Godot 4.6.2, C#)
## Phase 1 Design Document

## Feature Freeze
- Freeze tag: `Phase1-Locked-v1`
- Lock date: `April 12, 2026`
- Completion milestone: `Phase1-Completed-v1`
- Completion date: `April 27, 2026`
- Completion status: core Phase 1 playable loop completed and verified
- Purpose: freeze Phase 1 implementation scope to control complexity and prevent feature drift

## Phase 1.5 Design Lock
- Design lock tag: `Phase1.5-Design-Locked-v1`
- Design lock date: `April 28, 2026`
- Purpose: freeze the Phase 1.5 feature design before implementation begins
- Status: design locked, implementation not started
- Locked feature areas:
- `Internal Affairs` / `內政` system replacing Phase 1 `Develop`
- Five internal affairs jobs: `Farm`, `Commercial`, `Defend`, `WaterControl`, `Construction`
- Multi-month job schedules, UI termination, and war/event interruption
- Internal affairs job experience, rank, official titles, and title buffs
- Battle experience, military rank, general titles, and title buffs
- Strategist experience, strategist rank, strategist titles, and strategy buffs
- Civil experience, civil rank, civil official titles, and governance buffs
- Six basic troop types and baseline matchup rules
- Item/equipment system with initial famous item set
- Population, succession, defection, riot, relief, and stability-event design direction
- Values still allowed to tune during implementation:
- Exact buff percentages
- Experience gain and rank thresholds
- Troop costs, upkeep, and matchup multipliers
- Item rarity, ownership, location, and stat bonuses
- AI priority weights and balancing constants

### In Scope (Phase 1 Locked)
- 2D desktop playable map loop
- Monthly turn flow (player -> AI -> month advance)
- Core city resources and ownership:
- `Gold`, `Food`, `Troops`, `Officers`, `Farm`, `Commercial`, `Defense`, `Loyalty`
- City selection and HUD city info display
- Commands available in UI:
- `Develop`, `Recruit`, `Move`, `Search`, `Merchant`, `Attack`
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

## 1.1 Scenario / Story
- Current Phase 1 story:
- `storyId`: `yellow_turban_rebellion`
- English name: `Yellow Turban Rebellion`
- Traditional Chinese name: `黃巾之亂`
- Start date: `184年 1月`
- Current implementation has one scenario/story, but each scenario file owns its own story metadata, start date, factions, `cityStarts`, and `factionStarts`
- Later versions can add more scenario files and a scenario selection flow without sharing one global startup setup

## 2. Phase 1 Goals
- Deliver a playable vertical slice with:
- 2D map containing connected cities
- Monthly turn progression
- City and officer simulation basics
- Player command system: `Develop`, `Recruit`, `Move`, `Search`, `Merchant`, `Attack`
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
- `Develop` evolves into the broader `Internal Affairs` / `內政` system
- Internal affairs assignment is introduced as a Phase 1.5 extension on top of the Phase 1 loop
- Adds city jobs, officer job title, officer rank, recurring schedules, and performance formulas
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
3. End-of-month resolution:
- Resolve scheduled `Develop` commands
- Resolve scheduled `Recruit` commands
- Resolve scheduled `Search` commands
- Resolve scheduled `Move` commands
- Resolve scheduled `Attack` commands into battle
- Apply food upkeep and loyalty drift checks if any
- Advance month
- Seasonal resource schedule:
- Collect city `Gold` income in **April** as one annual settlement
- Collect city `Food` income in **August** as one annual settlement
- Annual settlement uses the city's current monthly income formula multiplied by `12`

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
- Current Phase 1 officer attributes:
- `Name`
- `NameZhHant`
- `Role`
- `Belongs`
- `Sex`
- `BirthYear`
- `DeathYear`
- `Strength`
- `Intelligence`
- `Charm`
- `Leadership`
- `Politics`
- `Loyalty`
- `Ambition`
- `Combat`
- `RelationshipType`
- `CityId`
- Officers are assigned to exactly one city
- Display age is calculated from `currentYear - BirthYear`
- `DeathYear` is loaded from historical reference data for future rules
- Officers must be at least `18` years old at scenario start to join an initial city/faction
- Commands use either city aggregate power or a selected lead officer
- `Develop`, `Recruit`, and `Search` each require one assigned officer
- An officer assigned to any command in the current month cannot be assigned to another command until next month
- Officers can also be assigned to one internal affairs job schedule:
- `Farm` / `農業` (agriculture and food production)
- `Commercial` / `商業` (commerce and gold income)
- `Defend` / `防衛` (fortification and city security)
- `WaterControl` / `治水` (flood prevention, farming stability, and disaster resistance)
- `Construction` / `建設` (city infrastructure, defense works, and long-term development)
- Each active internal affairs job requires one assigned officer
- One officer can only be assigned to one active internal affairs job at the same time
- Internal affairs jobs can be scheduled for multiple months instead of being assigned every round
- A scheduled job can be terminated manually from UI
- A scheduled job can be interrupted automatically by war or other major events
- `RelationshipType` stores lightweight officer relationship links used by startup placement and future loyalty/event rules
- Deferred officer profile extensions (Phase 1.5+):
- `BodyStatus` (e.g. `Healthy`, `Injured`, `Sick`, `Exhausted`)
- expanded blood/family relationship systems beyond current `RelationshipType`

### 4.4 Officer Job Title and Rank
- Add to officer profile:
- `JobType` (`None`, `Farm`, `Commercial`, `Defend`, `WaterControl`, `Construction`)
- `JobTitle` (example: `Assistant`, `Director`, `Chief`)
- `Rank` (numeric or enum, e.g. 1-9 where lower number is higher rank)
- City supports one active officer per internal affairs job in Phase 1.5 baseline
- Job schedule fields should track assigned officer, target city, remaining months, and active/terminated state
- Each completed monthly job tick grants job experience to the assigned officer
- When job experience reaches the required threshold, the officer increases rank in that job path
- Each official title grants a small job-specific buff
- Title buff and rank bonus stack, but final job output should be clamped for balance
- Optional future expansion: deputy slots per job

### 4.4.1 Internal Affairs Job Titles
- `Farm` / `Agriculture` titles:
- `司農` / `Minister of Agriculture`
- `屯田校尉` / `Commander of Agricultural Garrisons`
- `典農中郎將` / `Director of Farming`
- `勸農使` / `Commissioner of Agriculture`
- `農政官` / `Agricultural Officer`
- `Commercial` / `Commerce` titles:
- `度支尚書` / `Minister of Finance`
- `市令` / `Market Supervisor`
- `司市` / `Market Director`
- `商政官` / `Commerce Officer`
- `平準令` / `Price Stabilization Officer`
- `WaterControl` / `Water` titles:
- `都水使者` / `Chief of Waterworks`
- `河渠令` / `Director of River Works`
- `治河使` / `River Control Commissioner`
- `水衡都尉` / `Superintendent of Waterworks`
- `水利官` / `Waterworks Officer`
- `Construction` / `Infrastructure` titles:
- `將作大匠` / `Chief Engineer / Chief Architect`
- `工部尚書` / `Minister of Works`
- `營造官` / `Construction Officer`
- `將作監` / `Directorate of Construction`
- `修城校尉` / `Fortification Officer`
- `Defend` / `Garrison` titles:
- `鎮軍將軍` / `General Who Pacifies the Army`
- `護軍` / `Protector-General`
- `城防都尉` / `City Defense Commander`
- `守城校尉` / `Garrison Commander`
- `戍衛將軍` / `Defensive General`

### 4.4.2 Job Experience and Title Buffs
- Job experience is tracked per officer and per job type
- A month counts as completed when the schedule survives until month-end resolution and applies its job effect
- Terminated or interrupted schedules do not grant experience for the unfinished month
- Rank increase should unlock stronger titles or improve the active title buff
- Suggested title buff examples:
- Farm titles increase food output or farming growth
- Commercial titles increase gold income or merchant efficiency
- WaterControl titles reduce disaster/flood risk and improve farm stability
- Construction titles increase infrastructure growth and construction efficiency
- Defend titles increase defensive combat value and reduce siege losses

### 4.4.3 Battle Experience, Military Rank, and General Titles
- Officers who join battle gain battle experience
- Battle experience is separate from internal affairs job experience
- When battle experience reaches the required threshold, the officer increases military rank
- Military rank unlocks or improves general title buffs
- Each general title grants a battle-specific buff
- Suggested battle experience sources:
- Joining an attack or defense battle
- Winning a battle
- Surviving a difficult battle
- Capturing a city
- Defeating a stronger enemy force
- Suggested title buff categories:
- Attack power bonus
- Defense power bonus
- Casualty reduction
- Morale or combat stability bonus
- Siege/city capture bonus
- Troop command efficiency bonus

#### Top Tier General Titles
- `大將軍` / `General-in-Chief`
- `驃騎將軍` / `General of Agile Cavalry`
- `車騎將軍` / `General of Chariots and Cavalry`
- `衛將軍` / `General of the Guards`

#### Mid Tier Main Battle General Titles
- `前將軍` / `General of the Vanguard`
- `後將軍` / `General of the Rear`
- `左將軍` / `General of the Left`
- `右將軍` / `General of the Right`

#### Directional Expedition General Titles
- `征東將軍` / `General Who Conquers the East`
- `征西將軍` / `General Who Conquers the West`
- `征南將軍` / `General Who Conquers the South`
- `征北將軍` / `General Who Conquers the North`
- `鎮東將軍` / `General Who Pacifies the East`
- `鎮南將軍` / `General Who Pacifies the South`
- `安西將軍` / `General Who Secures the West`

#### Low Tier Miscellaneous General Titles
- `裨將軍` / `Deputy General`
- `偏將軍` / `Subordinate General`
- `牙門將軍` / `Gate Guard General`
- `中郎將` / `General of the Household`

### 4.4.4 Strategist Titles
- Strategist titles represent the officer's planning, tactics, and advisory path
- Strategist rank is separate from internal affairs job rank and military/general rank
- Strategist experience can be gained from successful Search, battle participation as advisor, future tactics, and major strategy events
- Each strategist title grants a strategy-specific buff
- Suggested buff categories:
- Search success bonus
- Battle tactics or combat stability bonus
- Enemy casualty bonus or friendly casualty reduction
- Internal affairs planning bonus
- Diplomacy or event resolution bonus in later phases
- Strategist title list:
- `謀士` / `Tactician`
- `參軍` / `Military Advisor`
- `軍師` / `Strategist`
- `軍師中郎將` / `Chief Strategist`
- `大軍師` / `Grand Strategist`

### 4.4.5 Civil Official Titles
- Civil official titles represent governance, administration, and state authority
- Civil rank is separate from internal affairs job rank, military/general rank, and strategist rank
- Civil experience can be gained from governing cities, completing construction/internal affairs schedules, managing high-loyalty cities, future policy actions, and major administrative events
- Each civil title grants a governance-specific buff
- Suggested buff categories:
- City loyalty stability bonus
- Gold or food administration bonus
- Reduced command/resource waste
- Improved population growth or public order
- Better succession/faction stability in later phases
- High civil titles:
- `丞相` / `Chancellor`
- `司徒` / `Minister of the People`
- `司空` / `Minister of Works`
- `太尉` / `Grand Commandant`
- Central government titles:
- `尚書令` / `Director of Secretariat`
- `侍中` / `Palace Attendant`
- `中書令` / `Director of the Imperial Secretariat`
- Local government titles:
- `太守` / `Prefect`
- `刺史` / `Inspector`
- `州牧` / `Governor`

### 4.4.6 Basic Troop Types
- Phase 1 uses a single aggregate `Troops` value
- Phase 1.5 can split troops into six basic troop types:
- `Infantry` / `步兵`
- `Spearman` / `槍兵`
- `Cavalry` / `騎兵`
- `Archer` / `弓兵`
- `Crossbow` / `弩兵`
- `Siege` / `投石車`
- Each city should track troop counts by type in addition to, or instead of, aggregate total troops
- Attack and Move UI should eventually allow selecting troop amounts by type
- Combat resolver should calculate battle power from troop type counts, officer stats, military title buffs, city defense, and matchup modifiers
- Siege units are mainly for city attack and should be weaker in open-field defense
- Cavalry should be strong in attack and pursuit but more costly to recruit/upkeep
- Spearmen should be useful against cavalry
- Archers and crossbowmen should provide ranged attack value
- Infantry should be the balanced baseline unit
- Basic troop matchup table:
- `Infantry` counters `Archer`
- `Spearman` counters `Cavalry`
- `Cavalry` counters `Archer`
- `Archer` counters `Infantry`
- `Crossbow` counters `Cavalry`
- `Siege` counters city defense
- Matchup multipliers should be configured in data or balance constants, not hardcoded in UI

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

### 4.5.1 Initial Famous Items
- Famous weapons:
- `青龍偃月刀` / `Green Dragon Crescent Blade`
- `方天畫戟` / `Sky-Piercing Halberd`
- `丈八蛇矛` / `Serpent Spear`
- `雌雄雙股劍` / `Twin Swords of Fate`
- `青釭劍` / `Blue Steel Sword`
- `倚天劍` / `Heaven Reliant Sword`
- `七星寶刀` / `Seven Star Treasure Blade`
- `古錠刀` / `Ancient Ingot Blade`
- Famous horses:
- `赤兔` / `Red Hare`
- `的盧` / `Dilu`
- `絕影` / `Shadowless`
- `爪黃飛電` / `Yellow Claw Lightning`
- `烏騅` / `Wuzhui`
- Famous special items/books/seals:
- `孫子兵法` / `The Art of War`
- `孟德新書` / `Mengde's New Book`
- `太平要術` / `Essential Arts of Great Peace`
- `青囊書` / `Blue Bag Medical Manual`
- `傳國玉璽` / `Imperial Seal`
- Item names should be data-driven and localized through item data, not hardcoded in UI code
- Specific owners, discovery locations, rarity, and exact stat buffs should be configured in item data

## 5. Player Commands (Phase 1 Rules)
## 5.1 Develop
- Purpose: Improve city economy
- Cost: small gold amount
- Officer requirement: assign exactly `1` officer to execute the command
- Flow:
- During command phase, player/AI assigns a `Develop` order to the city
- Assigned officer becomes unavailable for other commands for the rest of the month
- Assigned develop does **not** resolve immediately in the same command step
- At end-of-month resolution of the current round, scheduled develop orders resolve before recruit, search, move, and attack
- Effect: Improve city development attributes when month-end resolution runs
- Suggested formula:
- `gain = 20 + leadOfficer.Intelligence / 5 + random(0..10)`
- Phase 1.5 transition:
- `Develop` is replaced by `Internal Affairs` / `內政`
- Player chooses a specific internal affairs job: `Farm`, `Commercial`, `Defend`, `WaterControl`, or `Construction`
- Player chooses assigned officer and planned duration in months
- Job continues month by month until its duration ends, player terminates it, or war/event interruption cancels it
- Monthly effect is applied at end-of-month while the schedule is active

## 5.2 Recruit
- Purpose: Convert resources into troops
- Cost: gold + food
- Officer requirement: assign exactly `1` officer to execute the command
- Flow:
- During command phase, player/AI assigns a `Recruit` order to the city
- Assigned officer becomes unavailable for other commands for the rest of the month
- Assigned recruit does **not** resolve immediately in the same command step
- At end-of-month resolution of the current round, scheduled recruit orders resolve after develop, before search/move/attack
- Effect: Add troops to city when month-end resolution runs
- Suggested formula:
- `newTroops = 50 + leadOfficer.Charm / 4 + random(0..30)`
- Recruit extension (Phase 1.5):
- City has `Population`
- Recruit count depends on city `Population`, officer `Charm`, spent `Gold` hiring budget, and modifiers
- Recruit UI can choose which troop type to recruit
- Different troop types can have different gold/food/population cost and upkeep
- Heavy recruitment causes direct percentage decrease to city `Loyalty`
- Recruiting troops decreases city `Population` directly
- Recruitment cannot exceed available population constraints
- Population-to-troop conversion ratio: `1 population -> 1 troop`

## 5.3 Move
- Purpose: Transfer troops, gold, food, and officers between connected friendly cities
- Constraint: source and destination must be connected and same faction
- Flow:
- During command phase, player/AI assigns a `Move` order from source city to connected friendly target city
- Player UI can choose:
- `target city`
- `troops` by type in Phase 1.5
- `gold`
- `food`
- `officers`
- Assigned move does **not** resolve immediately in the same command step
- At end-of-month resolution of the current round, scheduled moves resolve after develop/recruit and before scheduled attacks
- Effect:
- Transfer selected `Troops`
- Transfer selected `Gold`
- Transfer selected `Food`
- Transfer selected `Officers`

## 5.4 Search
- Purpose: Find hidden officer or bonus resources
- Officer requirement: assign exactly `1` officer to execute the command
- Success chance scales with Intelligence + Charm
- Flow:
- During command phase, player/AI assigns a `Search` order to the city
- Assigned officer becomes unavailable for other commands for the rest of the month
- Assigned search does **not** resolve immediately in the same command step
- At end-of-month resolution of the current round, scheduled search orders resolve after develop/recruit and before move/attack
- Outcomes:
- Discover officer (joins city with medium loyalty)
- Find gold or food cache
- No result
- Hidden officers under age `18` cannot be discovered or recruited by Search

## 5.5 Merchant
- Purpose: Trade city `Gold` and `Food` immediately through merchant exchange
- Flow:
- Player opens `Merchant` dialog from HUD
- Player chooses trade mode:
- `Buy Food`
- `Sell Food`
- Player enters `Food` amount
- Dialog shows preview of resulting `Gold` gain/loss before confirmation
- Trade resolves immediately in the same command step
- Current implementation:
- Fixed rate `100 Food <-> 10 Gold`
- Trade amount must be multiple of `100 Food`
- `Buy Food`: spend `Gold`, gain `Food`
- `Sell Food`: spend `Food`, gain `Gold`
- Merchant is currently not blocked by the city core-action limit

## 5.6 Attack
- Purpose: Invade connected enemy/neutral city
- Flow:
- During command phase, player/AI assigns an `Attack` order from source city to connected target city
- Player UI can choose:
- `target city`
- `troops` by type in Phase 1.5
- `gold`
- `food`
- `officers`
- On `Confirm Attack`, UI validates the request before submitting:
- at least `1` officer must be selected
- `troops` must be greater than `0`
- `troops` cannot exceed the source city's currently available troops after earlier same-month attack assignments
- if validation fails, the attack dialog stays open and shows an inline warning message
- When attack is assigned, selected `troops` are reserved from the source city immediately so city UI updates at once
- When attack is assigned, selected `gold` and `food` are consumed from the source city immediately
- Assigned attack does **not** resolve immediately in the same command step
- At end-of-month resolution of the current round, scheduled attacks resolve into battle after develop/recruit/move
- Resolution: lightweight numeric combat (no tactical subscene in Phase 1)
- Suggested power:
- `attackPower = attackingTroops * (1 + avgStrength / 200.0)`
- `defensePower = defendingTroops * (1 + Defense * 0.006)`
- If attack succeeds:
- Target city changes ownership
- Selected attack `gold` and `food` are brought into the captured city
- Selected attack officers are transferred from the source city and remain in the captured city
- If attack fails:
- Attack officers return to the source city
- Surviving attack troops return to the source city
- Carried `gold` and `food` are only partially returned to the source city
- Current implementation: `50%` returned, `50%` lost
- Winner/holder applies casualties to both sides

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
1. If adjacent enemy is weak and own troops sufficient: assign `Attack`
2. If low troops and enough resources: `Recruit`
3. If economy low: `Develop`
4. If adjacent friendly city needs support: assign `Move` (troops / gold / food)
5. If food stock must be adjusted immediately: `Merchant`
6. Otherwise: `Search`

### 6.2 AI Constraints
- Each city can take one core city action per month: `Develop`, `Recruit`, or `Search`
- `Move` and `Attack` are scheduled military/logistics orders and are not blocked by the core city-action limit as long as they target valid connected cities
- `Merchant` is an immediate economy action and is not blocked by the core city-action limit
- Use deterministic seed option for reproducible test runs

### 6.3 AI Job Assignment (Phase 1.5)
- AI checks unfilled high-priority jobs first:
1. Low food: fill `Farm`
2. Low gold: fill `Commercial`
3. Border city or expected attack risk: fill `Defend`
4. Low farming stability or high disaster risk: fill `WaterControl`
5. Long-term growth target or weak infrastructure: fill `Construction`
- AI favors officers whose best stat matches the job
- AI prefers multi-month schedules but may terminate or reassign jobs when war pressure changes

## 7. Data Model (C#)
## 7.1 Domain Classes
- `OfficerData`
- Fields: `Id`, `Name`, `NameZhHant`, `Role`, `Belongs`, `Sex`, `BirthYear`, `DeathYear`, `Strength`, `Intelligence`, `Charm`, `Leadership`, `Politics`, `Loyalty`, `Ambition`, `Combat`, `RelationshipType`, `CityId`
- Phase 1.5 job extension fields: current active job id/reference, per-job experience, per-job rank, per-job title
- Phase 1.5 military extension fields: battle experience, military rank, general title, unlocked general title buffs
- Phase 1.5 strategist extension fields: strategist experience, strategist rank, strategist title, unlocked strategist title buffs
- Phase 1.5 civil extension fields: civil experience, civil rank, civil title, unlocked civil title buffs
- `CityData`
- Fields: `Id`, `Name`, `OwnerFactionId`, `Gold`, `Food`, `Troops`, `Farm`, `Commercial`, `Defense`, `Loyalty`, `OfficerIds`, `ConnectedCityIds`
- Phase 1.5 troop extension fields: troop counts by type (`Infantry`, `Spearman`, `Cavalry`, `Archer`, `Crossbow`, `Siege`)
- `FactionData`
- Fields: `Id`, `NameEn`, `NameZhHant`, `RulerOfficerId`, `OfficerIds`, `IsPlayer`
- `WorldState`
- Fields: `StoryId`, `StoryNameEn`, `StoryNameZhHant`, `Month`, `Year`, `Cities`, `Officers`, `Factions`, `CityStarts`, `FactionStarts`, `RandomSeed`
- Phase 1.5 should add active internal affairs schedules, or store them under city/faction state if easier for save/load

## 7.2 Internal Affairs Schedule Data
- `InternalAffairsScheduleData`
- Fields:
- `Id`
- `CityId`
- `OfficerId`
- `JobType`
- `RemainingMonths`
- `TotalMonths`
- `StartedYear`
- `StartedMonth`
- `State` (`Active`, `Terminated`, `Interrupted`, `Completed`)
- `InterruptedReason`
- Constraint: one officer can only have one active schedule
- Constraint: each city can only have one active officer per job type in Phase 1.5 baseline

## 7.2.1 Troop Type Data
- `TroopType`
- Values: `Infantry`, `Spearman`, `Cavalry`, `Archer`, `Crossbow`, `Siege`
- City troop storage can use a dictionary keyed by `TroopType`
- Pending move and attack commands should carry troop counts by type
- Aggregate troop count can be derived from all troop type counts for UI summary, upkeep, and legacy compatibility

## 7.3 Job Performance Model
- Each job has a base formula:
- `FarmOutput = BaseFarm * (1 + officer.Intelligence * 0.004 + RankBonus + SkillBonus)`
- `CommercialOutput = BaseGold * (1 + officer.Charm * 0.004 + RankBonus + SkillBonus)`
- `DefendOutput = BaseDefense * (1 + officer.Strength * 0.003 + officer.Intelligence * 0.002 + RankBonus + SkillBonus)`
- `WaterControlOutput = BaseWaterControl * (1 + officer.Politics * 0.004 + officer.Intelligence * 0.002 + RankBonus + SkillBonus)`
- `ConstructionOutput = BaseConstruction * (1 + officer.Politics * 0.003 + officer.Charm * 0.002 + RankBonus + SkillBonus)`
- Suggested rank bonus:
- `Rank 1-3: +0.12`
- `Rank 4-6: +0.06`
- `Rank 7-9: +0.02`
- Skill examples:
- `Agriculture`: +10% Farm job output
- `Trade`: +10% Commercial job output
- `Fortification`: +10% Defend job output
- `Hydrology`: +10% WaterControl job output
- `Architecture`: +10% Construction job output

## 7.4 Item Data Model
- `ItemData`
- Fields: `Id`, `NameEn`, `NameZhHant`, `ItemType`, `StrengthBonus`, `IntelligenceBonus`, `CharmBonus`, `LeadershipBonus`, `PoliticsBonus`, `CombatBonus`, `LoyaltyBonus`, `OwnerFactionId`, `OwnerCityId`, `EquippedOfficerId`, `Rarity`
- `ItemType` values: `SpecialWeapon`, `SpecialHorse`, `SpecialItem`
- `OwnerFactionId` stores faction inventory ownership
- `OwnerCityId` can store unclaimed/searchable city location
- `EquippedOfficerId` stores active equipment owner when assigned
- `OfficerData` extension:
- `EquippedWeaponItemId`
- `EquippedHorseItemId`
- `EquippedSpecialItemId`
- `GetEffectiveStrength/Intelligence/Charm/Leadership/Politics/Combat/Loyalty` should include item bonuses

## 7.5 Item Attribute Effects
- Effective stats used by game logic:
- `effectiveStrength = baseStrength + itemStrengthBonus`
- `effectiveIntelligence = baseIntelligence + itemIntelligenceBonus`
- `effectiveCharm = baseCharm + itemCharmBonus`
- `effectiveLeadership = baseLeadership + itemLeadershipBonus`
- `effectivePolitics = basePolitics + itemPoliticsBonus`
- `effectiveCombat = baseCombat + itemCombatBonus`
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
- `Injured`: `Strength -10%`
- `Sick`: `Intelligence -10%`, `Charm -10%`
- `Exhausted`: all outputs/job performance `-8%`
- `Healthy`: no penalty
- Suggested blood-relationship effects:
- If assigned ruler has close kin relation: `Loyalty +5~10`
- Kin in same city gives small positive command stability bonus
- Opposing kin in enemy faction can reduce capture/defection chance (optional)

## 7.7 Aging, Death, Defection, Riot Rules
- Officer and ruler aging:
- Officer age is calculated from `currentYear - BirthYear`
- `DeathYear` is loaded as historical reference data; automatic death handling is reserved for a later phase
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

- `phase1_scenario.json` contains the current story metadata, start year/month, factions, `cityStarts`, and `factionStarts`
- There is no separate `scenario_setup.json`; scenario setup belongs inside each scenario file

## 10. UI/UX Flow (Phase 1)
1. Player clicks city on map
2. HUD shows city data and available commands
3. Player picks command, enters required values (for example troops / gold / food / officers / target city / trade mode)
4. Each city can only use one core city action per month: `Develop`, `Recruit`, or `Search`
5. `Move` and `Attack` can still be assigned in the same month if they target valid connected cities
6. `Merchant` resolves immediately and updates city resources at once
7. `Develop`, `Recruit`, `Move`, and `Attack` are registered as scheduled actions for end-of-month resolution
8. `Search` resolves immediately and consumes that city's core monthly action
9. When all player cities have acted or player ends turn, AI turns run automatically
10. End-of-month develop, recruit, move, attack, upkeep, and month advance apply
11. Invalid `Attack` input keeps the attack dialog open and displays an inline warning instead of submitting the order

## 10.1 UI Additions for Jobs (Phase 1.5)
1. `Develop` button becomes `Internal Affairs` / `內政`
2. City panel shows job slots (`Farm`, `Commercial`, `Defend`, `WaterControl`, `Construction`)
3. Each slot displays assigned officer, title, rank, remaining months, estimated monthly contribution, and schedule state
4. Player can open an internal affairs dialog to assign officer, choose job, and choose duration in months
5. Player can terminate an active job schedule from UI
6. War or major event interruption can cancel an active job schedule and free the assigned officer
7. Officer detail panel shows current internal affairs job, remaining months, title, rank, and skill tags

## 10.2 UI Additions for Items (Phase 1.5)
1. Officer panel displays equipment slots (`Weapon`, `Horse`, `Special`)
2. Item inventory panel lists owned items with stat bonuses and rarity
3. Assign/Remove item actions show before/after effective stat preview
4. City/officer tooltip can show active item bonuses in compact form

## 10.3 UI Additions for Officer Profile (Phase 1.5)
1. Officer card shows calculated age, `BodyStatus`, and relationship summary
2. Relationship panel lists kin links and ruler relation badge
3. Body status icon and color indicator shown in city officer list
4. Tooltips explain active penalties/bonuses from status and relationship

## 11. Resource Economy (Initial Balance)
- Monthly base city income:
- Gold `+50`
- Food `+80`
- Merchant exchange:
- Fixed rate `100 Food <-> 10 Gold`
- Merchant trade is immediate
- Merchant dialog shows preview of resulting gold gain/loss before confirmation
- Monthly troop upkeep:
- Food cost `troops / 40` (rounded down)
- If food below 0, troop desertion applies

## 11.1 Job-driven Economy (Phase 1.5)
- `Farm` job adds food growth bonus before upkeep
- `Commercial` job adds gold growth bonus
- `Defend` job increases defender effective defense and lowers siege loss
- `WaterControl` job improves farming stability and reduces future flood/disaster impact
- `Construction` job improves long-term city infrastructure and can raise development ceilings in later phases
- Low city `Loyalty` applies output penalties and increases riot risk
- Seasonal settlement:
- Gold is collected from cities in April as annual accumulated settlement
- Food is collected from cities in August as annual accumulated settlement
- Current implementation model:
- `April Gold = monthlyGoldIncome * 12`
- `August Food = monthlyFoodIncome * 12`
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
- Replace `Develop` with `Internal Affairs` / `內政`
- Extend officer data or schedule data with `JobType`, `JobTitle`, `Rank`, `SkillTags`, assigned city, remaining months, and schedule state
- Implement assign/reassign/terminate flow
- Apply monthly job performance formulas
- Add UI display and AI assignment heuristics

### M7: Item System (Phase 1.5)
- Add `ItemData` model and initial item database
- Implement item assignment/removal flow
- Apply item bonuses to effective officer attributes
- Add inventory and officer equipment UI
- Integrate item-aware AI evaluation

### M8: Officer Profile Extensions (Phase 1.5)
- Add officer `BirthYear`, `DeathYear`, `BodyStatus`, and `BloodRelationship` data structures
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
- Each city can only use one core city action per month for player and AI: `Develop`, `Recruit`, or `Search`
- Develop order is queued during command phase and resolves in end-of-month step
- Recruit order is queued during command phase and resolves in end-of-month step
- City cannot move/attack to non-connected target
- Recruit blocked when resources insufficient
- Search resolves immediately and still consumes the city core monthly action
- Merchant resolves immediately and updates city gold/food at once
- Merchant UI supports `Buy Food` and `Sell Food`
- Merchant dialog previews expected gold/food change while trade amount changes
- Move order is queued during command phase and resolves in end-of-month step
- Move can still be assigned even if the city already used `Develop`, `Recruit`, or `Search` this month, as long as the target city is valid
- Move can transfer troops, gold, food, and officers to connected friendly city after resolution
- Attack order is queued during command phase and resolves in end-of-month battle step
- Attack can still be assigned even if the city already used `Develop`, `Recruit`, or `Search` this month, as long as the target city is valid
- Attack can capture city and transfer ownership after battle resolution
- Attack player UI supports choosing target city, troops, gold, food, and officers
- Attack player UI validates that at least one officer is selected and that requested troops are above `0` and do not exceed currently available troops
- Attack validation failure keeps the dialog open and shows an inline warning message
- Attack reserves selected troops immediately when the order is assigned, so source city troop count updates at once
- Attack consumes carried gold/food immediately when order is assigned
- Attack success transfers carried gold/food and selected officers into the captured city
- Attack failure returns officers and surviving troops to source city and refunds only part of carried gold/food
- AI turn executes without freezing
- Month increments and upkeep applied consistently
- Win/lose triggers correctly

## 14.1 Testing Checklist (Phase 1.5 Jobs)
- Officer can be assigned to each internal affairs job
- Each active job requires one officer
- One officer cannot be assigned to more than one active internal affairs job
- Job can be scheduled for multiple months
- Remaining months decrease correctly after each month-end resolution
- Player can terminate a job schedule from UI
- War or major event interruption can cancel a job schedule and free the officer
- Completed monthly job ticks grant experience
- Interrupted or terminated unfinished months do not grant experience
- Experience threshold increases rank
- Rank increase unlocks or improves the correct job title
- Each job title applies the correct title buff
- Reassign updates city output next month
- Rank bonus applies correctly in formula
- Skill bonus applies only to matching job
- Defend effects impact combat calculations
- WaterControl effects impact farming stability or disaster resistance
- Construction effects impact long-term city development
- AI fills empty critical jobs when possible

## 14.1.1 Testing Checklist (Phase 1.5 Battle Rank)
- Officers who join attack battles gain battle experience
- Officers who join defense battles gain battle experience
- Battle experience is not mixed with internal affairs job experience
- Winning, difficult survival, city capture, or defeating stronger enemy can grant bonus battle experience
- Experience threshold increases military rank
- Military rank unlocks or improves the correct general title
- Each general title applies the correct battle buff
- General title buffs affect combat calculations consistently
- Battle title data displays correctly in officer detail UI

## 14.1.2 Testing Checklist (Phase 1.5 Strategist Rank)
- Officers can gain strategist experience from successful Search or future strategy/tactics actions
- Strategist experience is not mixed with internal affairs job experience or battle experience
- Experience threshold increases strategist rank
- Strategist rank unlocks or improves the correct strategist title
- Each strategist title applies the correct strategy buff
- Strategist buffs affect Search, tactics, or advisory calculations consistently
- Strategist title data displays correctly in officer detail UI

## 14.1.3 Testing Checklist (Phase 1.5 Civil Rank)
- Officers can gain civil experience from governance, internal affairs, policy, or administrative events
- Civil experience is not mixed with internal affairs job experience, battle experience, or strategist experience
- Experience threshold increases civil rank
- Civil rank unlocks or improves the correct civil title
- Each civil title applies the correct governance buff
- Civil buffs affect city loyalty, economy, population, public order, or faction stability consistently
- Civil title data displays correctly in officer detail UI

## 14.1.4 Testing Checklist (Phase 1.5 Troop Types)
- Cities can store all six troop type counts
- Aggregate troop count matches the sum of all troop type counts
- Recruit can add a selected troop type
- Move can transfer selected troop types between friendly connected cities
- Attack can deploy selected troop types
- Combat resolver includes troop type counts in battle power
- Troop type matchup modifiers apply consistently
- Infantry counter against Archer is applied
- Spearman counter against Cavalry is applied
- Cavalry counter against Archer is applied
- Archer counter against Infantry is applied
- Crossbow counter against Cavalry is applied
- Siege counter against city defense is applied
- Siege units contribute strongly to city attack but are weak outside that role
- Upkeep can be calculated per troop type or from aggregate fallback
- UI displays troop type counts clearly in city, move, attack, and recruit dialogs

## 14.2 Testing Checklist (Phase 1.5 Items)
- Item can be assigned only to valid slot and valid officer
- Removing/swapping item updates effective attributes immediately
- Effective attributes are used by job formulas and combat formulas
- Multiple item bonuses stack correctly and respect stat clamp
- Famous weapons, horses, and special items load from item data
- Localized item names display correctly in Chinese and English
- Unclaimed item city locations can be searched/discovered if configured
- AI does not equip duplicate item to multiple officers
- Save/load preserves item ownership and equipment state

## 14.3 Testing Checklist (Phase 1.5 Officer Profile)
- Officer `BirthYear`, `DeathYear`, `BodyStatus`, and relationship data load correctly
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
3. Resolve scheduled `Develop` commands
4. Resolve scheduled `Recruit` commands
5. Resolve scheduled `Move` commands
6. Resolve scheduled `Attack` commands into battle
7. Recruit loyalty penalty and other command-side month-end effects
8. Relief effects (loyalty updates)
9. Riot checks and riot one-time losses
10. Officer defection/leave checks
11. Officer/ruler death checks
12. Same-month ruler succession resolution
13. Faction-end checks (no ruler or no cities)
14. Seasonal economy collection (April annual gold, August annual food)
15. Upkeep and post-income adjustments
16. Month advance
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
