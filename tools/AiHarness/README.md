# AI Regression Harness

This tool is the project's logic-level regression test harness for current Phase 1 AI behavior.

## What it tests
- AI attack scheduling
- AI attack immediate troop reservation
- AI move scheduling
- AI `Recruit / Develop / Search`
- AI immediate resource cost application
- AI attack month-end capture flow
- April annual gold settlement
- August annual food settlement
- upkeep shortage handling
- multi-month soak stability

## How to run

From the project root:

```powershell
dotnet run --project tools\AiHarness\AiHarness.csproj
```

Or use the helper script:

```powershell
powershell -ExecutionPolicy Bypass -File tools\AiHarness\run-ai-regression.ps1
```

## Expected result

Successful run example:

```text
AI TEST SUMMARY: PASS=16 FAIL=0
```

If any test fails, the process exits with code `1`.

## Notes
- This harness is for logic/regression coverage, not UI coverage.
- It is intentionally deterministic where possible, but some AI/search behavior can still include bounded randomness.
- The main Godot project excludes `tools\AiHarness\*.cs` from normal game compilation, so this tool does not affect runtime builds.
