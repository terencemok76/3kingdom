# AI Playtest Checklist

## 1. Purpose
- Verify current Phase 1 AI behavior works end-to-end across multiple months.
- Focus on AI command validity, month-end resolution, economy flow, and stability.

## 2. Current AI Rules Under Test
- If adjacent enemy city is weaker and current city has `target.Troops + 300` or more, AI schedules `Attack`.
- If no attack is chosen and adjacent friendly city is much weaker, AI schedules `Move`.
- AI may also perform these core actions in the same month:
- `Recruit` if troops are low enough and resources allow it
- `Develop` if gold allows it
- `Search` if not already used this month
- AI can do a military/logistics order plus core city actions in the same month.

## 3. Test Setup
- Start from a fresh game state.
- Use default scenario setup unless a specific edge case needs a custom setup.
- Keep log panel visible during all tests.
- Record results with:
- `Test`
- `Expected`
- `Actual`
- `PASS/FAIL`
- `Notes`

## 4. Core AI Tests

### 4.1 Basic Turn Execution
- End player turn once.
- Confirm each AI city produces one combined AI log entry.
- Expected:
- No freeze
- No empty/null command output
- No runtime error

### 4.2 AI Attack Scheduling
- Find an AI city adjacent to a weaker enemy city.
- End turn.
- Expected:
- AI schedules `Attack`
- Source city troops reduce immediately when attack is assigned
- Selected officers become unavailable for other pending `Move/Attack`

### 4.3 AI Move Scheduling
- Find an AI city with much higher troops than a connected friendly city.
- End turn.
- Expected:
- AI schedules `Move`
- Order appears to resolve only at month end
- Source and target city values update correctly after resolution

### 4.4 AI Recruit
- Use a state where AI city has:
- troops below threshold
- enough gold
- enough food
- End turn.
- Expected:
- AI schedules `Recruit`
- gold/food reduce immediately
- troops increase only at month end

### 4.5 AI Develop
- Use a state where AI city has enough gold.
- End turn.
- Expected:
- AI schedules `Develop`
- gold reduces immediately
- farm/commercial/defense/loyalty effects apply at month end

### 4.6 AI Search
- End turn with an AI city that has not searched this month.
- Expected:
- AI can run `Search`
- result is immediate
- same AI city does not search twice in same month

## 5. Month-End Resolution Tests

### 5.1 AI Attack Success
- Create or observe a case where AI attack should win.
- End turn.
- Expected:
- target city ownership changes
- attack officers stay in captured city
- carried gold/food enter captured city
- source city has already paid troops/gold/food at assignment time

### 5.2 AI Attack Failure
- Create or observe a case where AI attack should fail.
- End turn.
- Expected:
- target city remains with defender
- deployed officers return to source city
- surviving troops return to source city
- only partial gold/food refund occurs

### 5.3 AI Move Resolution
- End turn after AI has scheduled a move.
- Expected:
- move resolves after `Develop/Recruit`
- before `Attack`
- troops/gold/food/officers arrive at target city correctly

## 6. Economy Tests

### 6.1 April Gold
- Advance until entering `April`.
- Expected:
- annual gold settlement is applied
- AI cities receive gold correctly
- log remains valid

### 6.2 August Food
- Advance until entering `August`.
- Expected:
- annual food settlement is applied
- AI cities receive food correctly
- log remains valid

### 6.3 Upkeep and Desertion
- Observe an AI city with low food over multiple months.
- Expected:
- monthly upkeep reduces food
- if food is insufficient, troop desertion occurs
- loyalty penalty applies

## 7. Validity / Constraint Tests

### 7.1 Officer Locking
- After AI schedules `Move` or `Attack`, inspect same city next month if possible.
- Expected:
- assigned officers are not double-booked into another pending military order

### 7.2 Connected-City Constraint
- Observe AI decisions near disconnected or invalid targets.
- Expected:
- AI does not create invalid `Move`/`Attack` orders to non-connected cities

### 7.3 Same-Faction Attack Block
- Observe AI around friendly borders.
- Expected:
- AI never attacks same-faction city

## 8. Stability Tests

### 8.1 Multi-Month Soak
- Run 6 to 12 months by repeatedly ending player turn.
- Expected:
- no crash
- no invalid city ownership state
- no missing officer references
- no negative resource explosion outside intended rules

### 8.2 Faction Elimination
- Let one AI faction lose all cities.
- Expected:
- elimination log appears
- eliminated faction stops acting on later turns

### 8.3 Victory / Defeat Integrity
- Continue until strong domination state.
- Expected:
- win/lose conditions still trigger correctly after repeated AI turns

## 9. Known Current Scope
- This checklist tests current heuristic AI, not advanced strategy quality.
- A PASS means:
- AI acts legally
- AI resolves commands correctly
- AI does not corrupt state
- It does not mean AI is smart or balanced.
