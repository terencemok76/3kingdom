# Officer Data Rules

This document records the current runtime rules for officer data fields. It is intended to describe actual behavior in the game code, especially where the original design notes have drifted.

## Optional Fields

The following fields may be omitted from `data/person/officer_data.json` without breaking deserialization:

- `role`
- `death_year`
- `belongs`

When omitted, the game falls back to default runtime behavior described below.

## Role

- `role` defaults to `Common`.
- Localized display for `Common` is:
  - Traditional Chinese: `一般`
  - English: `Common`
- Empty or missing `role` values are normalized to `Common`.
- Legacy role values are still normalized:
  - `Strategist` -> adds strategist appointment, base role becomes `Advisor`
  - `Governor` -> adds governor appointment, base role becomes `General`
  - `Lord` / `Ruler` -> normalized as ruler role / lord appointment behavior

## Death Year And Natural Death

- If `death_year` exists, it is still treated as a hard alive/dead cutoff.
- If `death_year` is missing, the officer is no longer effectively immortal.
- Officers without `death_year` now use age-based natural death chance during monthly advancement.

Current monthly natural death chances:

- Age below 50: `0%`
- Age `50-59`: `0.15%`
- Age `60-69`: `0.5%`
- Age `70-79`: `1.5%`
- Age `80-89`: `4%`
- Age `90+`: `8%`

Only officers without `death_year` use the probabilistic natural death rule.

## Join Age

- Officers become eligible to join from age `14`.
- This rule is used consistently in:
  - scenario/world loading
  - free officer visibility/movement
  - HUD/UI age eligibility checks

## Newly Eligible Officers

When an officer has just reached join age:

- they do **not** automatically appear immediately
- they first go through the normal free-officer appearance roll
- if they are selected to appear, relationship placement rules are applied first
- if not selected to appear, they remain hidden and follow the normal free-officer flow later

This prevents all newly eligible officers from appearing together at the start of a year.

## Relationship Placement

If a newly eligible officer has `relationship_type` entries:

- the game checks whether any related officer is alive
- the related officer must also currently have a valid city location
- if one or more related officers qualify, the newly eligible officer preferentially appears in one of those related officers' cities
- if no valid related officer city is found, normal random free-officer placement is used

Relationship matching currently uses officer names from `relationship_type` keys and compares against:

- `NameZhHant`
- `Name`

## Belongs

- `belongs` is optional and may be omitted from officer data
- omission does not break loading
- current gameplay dependency on `belongs` is limited
- the main remaining behavior is a search/discovery preference for some factions when selecting discoverable free officers

Because of this, removing `belongs` mainly reduces historical faction-bias preference in free-officer discovery, but does not break recruitment, officer loading, or basic faction ownership.

## View Officer Dialog

Current `View Officer` UI behavior:

- `Role` column shows base identity such as `Lord`, `Common`, `Advisor`, `General`
- `Appointments` column shows assigned posts such as `Governor`, `Strategist`, `Chancellor`, `Chief Strategist`
- `Lord` is displayed in `Role`, not duplicated in `Appointments`

The officer table now shows these core attributes:

- Age
- Loyalty
- Strength
- Intelligence
- Charm
- Leadership
- Politics
- Combat
