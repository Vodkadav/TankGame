<#
.SYNOPSIS
  Windows mirror of hooks/no-secrets-scan.sh. Scans staged additions/
  modifications for likely hardcoded secrets and exits non-zero on the
  first match. Patterns must match the bash hook — keep both in sync.
.NOTES
  Source of truth for patterns: C:/programming/Claude/rules/sec-no-hardcoded-secrets.md
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$patterns = @(
  @{ Label = 'password=';         Regex = 'password\s*=\s*"[^"]+"' }
  @{ Label = 'token=';            Regex = 'token\s*=\s*"[^"]{16,}"' }
  @{ Label = 'secret=';           Regex = 'secret\s*=\s*"[^"]{8,}"' }
  @{ Label = 'bearer-token';      Regex = 'Bearer\s+[A-Za-z0-9_\-\.=]{20,}' }
  @{ Label = 'aws-access-key';    Regex = 'AKIA[0-9A-Z]{16}' }
  @{ Label = 'github-token';      Regex = 'gh[pousr]_[A-Za-z0-9]{30,}' }
  @{ Label = 'base64-credential'; Regex = '(secret|token|password|key)[^A-Za-z0-9]\s*[:=]\s*"?[A-Za-z0-9+/]{40,}={0,2}"?' }
)

$staged = & git diff --cached --name-only --diff-filter=ACM
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not $staged) { exit 0 }

foreach ($file in $staged) {
  if ([string]::IsNullOrWhiteSpace($file)) { continue }
  if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { continue }
  $content = Get-Content -LiteralPath $file -Raw -ErrorAction SilentlyContinue
  if ($null -eq $content) { continue }
  foreach ($p in $patterns) {
    if ([regex]::IsMatch($content, $p.Regex, 'IgnoreCase')) {
      [Console]::Error.WriteLine("blocked: $file contains likely secret <$($p.Label)>")
      exit 1
    }
  }
}

exit 0
