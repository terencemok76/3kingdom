# Project Milestones

## Phase1-Completed-v1
- Date: `April 27, 2026`
- Status: completed
- Scope baseline: `Phase1-Locked-v1`
- Verification:
- `dotnet build 3kingdom.sln` passed with `0 warning / 0 error`
- AI regression harness passed with `PASS=17 FAIL=0`

### Completed Scope
- Playable 2D map loop with city selection and HUD updates
- Monthly player -> AI -> month advance flow
- Player commands: `Develop`, `Recruit`, `Move`, `Search`, `Merchant`, `Attack`
- Command scheduling and end-of-month resolution for queued commands
- Seasonal economy: annual gold in April and annual food in August
- Troop upkeep, basic loyalty pressure, and city resource updates
- Lightweight attack resolution with ownership transfer
- AI faction command selection and multi-month stability coverage
- Scenario-contained story metadata, city starts, and faction starts
- Officer data loading with birth/death year, calculated age, and age-gated startup/search
- View UI for city officers, faction officers, and city information
- Core localization flow through `data/localization/locale.json`

### Deferred To Phase 1.5+
- Internal Affairs / `內政` system replacing the Phase 1 `Develop` command
- Officer job assignment schedules for `Farm`, `Commercial`, `Defend`, `WaterControl`, and `Construction`
- Multi-month job duration, UI termination, and war/event interruption handling
- Battle experience, military rank, general titles, and title buffs
- Strategist experience, strategist rank, strategist titles, and strategy buffs
- Civil experience, civil rank, civil official titles, and governance buffs
- Six basic troop types: `Infantry`, `Spearman`, `Cavalry`, `Archer`, `Crossbow`, `Siege`
- Item/equipment system
- Initial famous item set: named weapons, horses, books, medical text, and imperial seal
- Advanced officer profile systems such as body status and deeper blood relationships
- Succession, defection, riot, relief, and advanced stability event chains
- Population-capacity depth and advanced balancing

## Phase1.5-Design-Locked-v1
- Date: `April 28, 2026`
- Status: design locked
- Scope baseline: follows `Phase1-Completed-v1`
- Purpose: freeze Phase 1.5 system design before implementation begins

### Locked Design Scope
- Internal Affairs / `內政` replaces Phase 1 `Develop`
- Internal affairs jobs: `Farm`, `Commercial`, `Defend`, `WaterControl`, `Construction`
- Officers can only hold one active internal affairs job at a time
- Internal affairs jobs support multi-month schedules, UI termination, and war/event interruption
- Job experience increases job rank and unlocks/improves official job titles
- Battle experience increases military rank and unlocks/improves general titles
- Strategist experience increases strategist rank and unlocks/improves strategist titles
- Civil experience increases civil rank and unlocks/improves civil official titles
- Six basic troop types: `Infantry`, `Spearman`, `Cavalry`, `Archer`, `Crossbow`, `Siege`
- Baseline troop matchup rules are locked
- Item system includes famous weapons, horses, books, medical text, and imperial seal
- Population, succession, defection, riot, relief, and stability systems remain in Phase 1.5 design scope

### Tunable During Implementation
- Exact buff percentages
- Experience gain and rank thresholds
- Troop costs, upkeep, and matchup multipliers
- Item rarity, owner, location, and stat bonuses
- AI priority weights and balancing constants

## Phase2-Design-Locked-v1
- Date: `May 10, 2026`
- Status: design locked
- Scope baseline: follows `Phase1.5-Completed-v1`
- Purpose: freeze Phase 2 system design before further implementation continues

### Locked Design Scope
- Unique faction central posts:
  - `Chancellor`
  - `Chief Strategist`
- Player appointment flow through a dedicated `Advisor` UI entry
- Advisor comment display in city information
- Faction-level persistence for advisor posts
- Automatic cleanup when the holder dies, is dismissed, defects, is hired away, or becomes ruler
- Basic AI appointment rules

### Intentionally Deferred Within Phase 2
- Full court hierarchy
- Multi-layer advisor trees
- Political jealousy / faction struggle simulation
- Dedicated advisor event chains
- Complex strategy AI driven by advisor personalities
- Full stat buffs before the UI / data model stabilizes

## Phase2-Completed-v1
- Date: `May 25, 2026`
- Status: completed
- Scope baseline: `Phase2-Design-Locked-v1`
- Verification:
- `dotnet build ThreeKingdom.csproj` passed with `0 warning / 0 error`
- AI regression harness passed with `PASS=266 FAIL=0`

### Completed Scope
- Unique faction central posts:
  - `Chancellor`
  - `Chief Strategist`
- City-level prefect appointment system using current appointment data model
- Prefect authorization modes:
  - `None`
  - `Half`
  - `Full`
- Authorized monthly city plan storage and execution flow built on top of existing `InternalAffairsSchedule`
- Authorized plan controls:
  - `Pause`
  - `Resume`
  - `Cancel Current Month`
  - `Terminate`
- Prefect reassignment / dismissal flow and generic officer appointment clearing
- Automatic prefect refill when the prior prefect leaves, dies, is fired, is hired away, or loses city eligibility
- Faction and city UI coverage for:
  - advisor appointment
  - prefect authorization
  - city info display
  - internal affairs visibility
  - log filtering / clearing
- Grouped monthly event presentation with:
  - event picture
  - event sound
  - city map marker
  - temporary UI suppression during playback
- AI appointment baseline:
  - `Chancellor`
  - `Chief Strategist`
  - `Prefect`
- AI regression harness coverage updated for appointment behavior

### Deferred To Phase 3+
- Personality-driven prefect autonomy
- Rich central comment / fallback speaker logic
- Buff system for central posts and prefects
- Fog of war and foreign court intelligence visibility
- Advisor / prefect event chains
- Deeper AI governance mode selection beyond the current `None / Full` baseline

## Phase3-Started-v1
- Date: `May 25, 2026`
- Status: superseded by `Phase3-Completed-v1`
- Scope baseline: follows `Phase2-Completed-v1`
- Purpose: begin Phase 3 work on personality-driven governance, richer advisor systems, and higher-level strategic simulation layers deferred from Phase 2.

### Initial Focus
- Personality-driven prefect autonomy and governance style
- Richer central comment quality and fallback speaker behavior
- Buff system design and implementation
- Fog of war / intelligence visibility for foreign courts and central posts
- Advisor / prefect event chain foundations

## Phase3-Completed-v1
- Date: `June 7, 2026`
- Status: completed
- Scope baseline:
  - `docs/PHASE3_DESIGN_ZH.md`
  - practical close-out tracked in `docs/PHASE3_REMAINING_CHECKLIST.md`
- Verification:
- Core player-facing scope landed and accepted for close-out:
  - facilities / construction points / recruit unlocks / siege engines
  - prisoner management flow and regression checklist
  - View refresh and live-localization support
  - map marker polish: city icons, flags, selected arrow, generated road rendering

### Completed Scope
- City facility system:
  - `BowWorkshop`
  - `SiegeWorkshop`
  - `HorsePasture`
- Construction points flow and facility upgrade progression
- Siege engine inventory and build progression:
  - `Ram`
  - `Catapult`
  - `Ladder`
- Recruit cost / unlock model tied to facility and resource state
- Attack / Move UI support for siege engine allocation and transfer
- View / city info display for facilities and siege engines
- Live localization refresh for major dynamic command dialogs
- Captured officer flow:
  - post-battle handling
  - prisoner management
  - transfer via `Move`
  - monthly escape handling
  - View integration
- Map presentation polish:
  - city image markers
  - faction flags
  - selected arrow marker
  - generated curved road rendering

### Deferred Beyond Phase 3
- faction-level `Technology`
- siege engine system v2:
  - durability
  - transport capacity
  - defender-side siege devices
- deeper AI construction heuristics beyond current baseline and targeted fixes
- further View redraw / interaction polish if later regressions appear

## Phase4-Started-v1
- Date: `June 7, 2026`
- Status: in progress
- Scope baseline: follows `Phase3-Completed-v1`
- Purpose: begin focused battle-system work after Phase 3 city / economy / prisoner / map presentation line is closed.

### Initial Focus
- Battle resolution structure and clearer combat phases
- Troop-type interaction rebalance
- Siege / defense / siege-engine combat integration pass
- Battle UI readability and deployment clarity
- AI battle decision baseline improvement
