# Encoding Notes

This project should treat Chinese documents, localization files, and source files as UTF-8 by default.

## Rule

- Prefer UTF-8 when reading or writing `.md`, `.json`, `.cs`, and other text files that may contain Chinese.
- Do not rely on the default Windows PowerShell encoding when handling Chinese text.

## PowerShell

When reading or writing Chinese content in PowerShell, prefer explicit UTF-8:

```powershell
Get-Content -Encoding UTF8 .\path\to\file.md
Set-Content -Encoding UTF8 .\path\to\file.md $content
```

If terminal output shows mojibake, set the current PowerShell session to UTF-8 first:

```powershell
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001 > $null
```

## Why

- Some Windows PowerShell environments still default to Big5 or other legacy code pages.
- Files may be valid UTF-8 while terminal output still displays corrupted Chinese.
- Explicit UTF-8 handling reduces accidental document corruption when editing Chinese text.
