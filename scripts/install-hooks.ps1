# Installs the TankGame git hooks by pointing git at the tracked hooks/ folder.
# Run once per fresh clone: pwsh scripts/install-hooks.ps1
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

git -C $repo config core.hooksPath hooks
Write-Host "core.hooksPath set to 'hooks' - pre-commit secret scan is active." -ForegroundColor Green
