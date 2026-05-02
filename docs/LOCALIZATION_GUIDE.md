# Localization Guide

## Purpose

This project no longer uses a single `locale.json`.

All localization entries are split into multiple `*.locale.json` files under:

- `data/localization/`

The goal is to:

- reduce merge conflicts
- keep related strings together
- make new strings easier to place and review
- avoid one oversized localization file

## File Loading Rule

`LocalizationService` loads all files matching:

- `*.locale.json`

from:

- `res://data/localization`

Files are merged in filename order.

Recommended practice:

- keep filenames prefixed with numbers such as `01-`, `02-`, `03-`
- do not reintroduce `locale.json`

## Current File Layout

### `01-ui-general.locale.json`

Use for common UI labels shared across the game.

Examples:

- `ui.city`
- `ui.gold`
- `ui.food`
- `ui.none`
- `ui.yes`
- `ui.no`

### `02-ui-forms.locale.json`

Use for form labels, confirm buttons, warnings, and input-related UI text.

Examples:

- `ui.confirm_attack`
- `ui.confirm_hire_officer`
- `ui.attack_deployment_required_warning`
- `ui.hire_officer_gold_offer`

### `03-ui-view.locale.json`

Use for View dialog titles, filters, sorting labels, and empty-state messages.

Examples:

- `ui.view_title.officers_faction`
- `ui.view_title.cities_all`
- `ui.city_filter_self`
- `ui.sort_strength`

### `04-ui-officer.locale.json`

Use for officer detail fields and officer-related stat labels.

Examples:

- `ui.strength`
- `ui.intelligence`
- `ui.spy_rank`
- `ui.farm_experience`

### `05-role-status.locale.json`

Use for role and status keys.

Examples:

- `role.general`
- `role.strategist`
- `status.idle`
- `status.attack`

### `06-progression.locale.json`

Use for rank titles and progression-related naming.

Examples:

- `progression.general.rank1`
- `progression.spy.rank3`
- `progression.diplomacy.rank2`
- `progression.job.farm.rank4`

### `07-command-names.locale.json`

Use for command names themselves.

Examples:

- `command.personnel.hire_officer`
- `command.civil.relief`
- `command.internal_affairs.farm`

### `08-command-results.locale.json`

Use for command execution results, validation errors, and success/failure messages.

Examples:

- `cmd.attack.success`
- `cmd.attack.failed`
- `cmd.hire_officer.refused`
- `cmd.civil_relief.resolved`

### `09-formats.locale.json`

Use for reusable formatted templates.

Examples:

- `fmt.label_value`
- `fmt.year_month`
- `fmt.attack_deployment_summary`

### `10-logs.locale.json`

Use for log output shown in the log panel or progression logs.

Examples:

- `log.boot`
- `log.player_end_turn`
- `log.city_disaster`

### `11-items.locale.json`

Use for item categories and item rarity labels.

Examples:

- `item_type.weapon`
- `item_type.book`
- `item_rarity.rare`

### `12-troops.locale.json`

Use for troop type names.

Examples:

- `troop_type.infantry`
- `troop_type.cavalry`
- `troop_type.siege`

## Namespace Rules

### `ui.*`

Use for visible UI labels, buttons, warnings, and static interface text.

### `command.*`

Use for command names only.

This is for the name of an action shown in option lists, titles, or menu selections.

Examples:

- `command.personnel.give_bonus`
- `command.civil.investigate_people`

### `cmd.*`

Use for command results and execution feedback.

This includes:

- invalid input
- insufficient resources
- scheduled result
- success
- failure
- cancellation

### `fmt.*`

Use for strings that are intended to be formatted with arguments.

Examples:

- `fmt.label_value`
- `fmt.city_selected`

If the string is expected to use `string.Format(...)`, it should usually live under `fmt.*` or `cmd.*`.

### `log.*`

Use for log entries that are part of the log stream rather than button labels or command names.

### `role.*`

Use for officer role names.

### `status.*`

Use for officer or command status text.

### `progression.*`

Use for all rank titles, progression titles, and promotion naming.

### `item_type.*`, `item_rarity.*`, `troop_type.*`

Use for data taxonomy labels, not command text.

## How To Choose The Correct File

Use this decision order:

1. If it is a command name, place it in `07-command-names.locale.json`.
2. If it is a command result or error message, place it in `08-command-results.locale.json`.
3. If it is a reusable format string with placeholders, place it in `09-formats.locale.json`.
4. If it is a log entry, place it in `10-logs.locale.json`.
5. If it is a visible UI field/button/warning, place it in one of the `ui` files.
6. If it is a role, status, progression title, item type, item rarity, or troop type, place it in the corresponding domain file.

## Naming Style

Prefer:

- lowercase
- dot-separated namespaces
- specific names

Good examples:

- `ui.faction_owner`
- `ui.view_title.cities_self`
- `command.internal_affairs.disaster_prevention`
- `cmd.attack.cancelled`
- `fmt.attack_deployment_summary`

Avoid:

- mixed namespace styles for the same concept
- putting command names under `ui.*`
- putting execution results under `command.*`
- vague names like `ui.value1`

## Do Not Hardcode Chinese Text

All Chinese UI text must live in localization files.

Do not hardcode Traditional Chinese strings in C# UI code.

If a new feature needs text:

1. add the localization key first
2. place it into the correct `*.locale.json` file
3. reference it from code via `LocalizationService`

## When Adding New Features

Examples:

- Diplomacy action name:
  - `command.diplomacy.alliance`
- Diplomacy success/failure:
  - `cmd.diplomacy.alliance.success`
  - `cmd.diplomacy.alliance.rejected`
- Spy action name:
  - `command.spy.assassinate`
- Spy result:
  - `cmd.spy.assassinate.exposed`
- Spy detail UI label:
  - `ui.spy_experience`

## Future Refactor Guidance

If a file grows too large, split it by feature while preserving namespace clarity.

Example future split:

- `08-command-results-military.locale.json`
- `08-command-results-civil.locale.json`
- `08-command-results-personnel.locale.json`

If this happens:

- update `LocalizationService` only if file pattern rules change
- keep namespaces stable so code references do not need mass renaming
