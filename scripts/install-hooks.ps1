<#
.SYNOPSIS
  Installs the TankGame pre-commit secret-scan hook into .git/hooks/.
  Copies hooks/no-secrets-scan.sh into .git/hooks/pre-commit, preserves
  the shebang, and sets the executable bit on POSIX shells. Idempotent —
  overwrites any existing pre-commit hook.
.NOTES
  Run from the repo root (or any subdirectory — the script resolves the
  repo root via `git rev-parse --show-toplevel`). M0-T10.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
  throw "install-hooks: not inside a git working tree."
}
$repoRoot = $repoRoot.Trim()

$source = Join-Path $repoRoot 'hooks/no-secrets-scan.sh'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
  throw "install-hooks: source hook missing at $source"
}

$gitDir = (& git rev-parse --git-dir 2>$null).Trim()
if ([System.IO.Path]::IsPathRooted($gitDir)) {
  $hooksDir = Join-Path $gitDir 'hooks'
} else {
  $hooksDir = Join-Path (Join-Path $repoRoot $gitDir) 'hooks'
}
if (-not (Test-Path -LiteralPath $hooksDir -PathType Container)) {
  New-Item -ItemType Directory -Path $hooksDir -Force | Out-Null
}

$dest = Join-Path $hooksDir 'pre-commit'
Copy-Item -LiteralPath $source -Destination $dest -Force

# Best-effort chmod for POSIX shells; harmless on plain Windows.
$chmod = Get-Command chmod -ErrorAction SilentlyContinue
if ($chmod) { & chmod +x $dest 2>$null | Out-Null }

Write-Host "installed: pre-commit hook -> $dest"
