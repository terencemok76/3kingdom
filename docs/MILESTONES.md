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
