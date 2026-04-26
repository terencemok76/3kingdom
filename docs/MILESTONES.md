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
- Officer job assignment system
- Item/equipment system
- Advanced officer profile systems such as body status and deeper blood relationships
- Succession, defection, riot, relief, and advanced stability event chains
- Population-capacity depth and advanced balancing
