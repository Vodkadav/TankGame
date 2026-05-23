#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0' }
<#
.SYNOPSIS
  Pester v5 tests for the M0-T10 pre-commit secret-scan hook.
  Verifies the bash hook (hooks/no-secrets-scan.sh) and the PowerShell mirror
  (scripts/hooks/no-secrets-scan.ps1) both block known secret patterns and
  allow clean files.

  Run: Invoke-Pester -Path scripts/tests/SecretScanHookTests.ps1 -Output Detailed
#>

Describe "M0-T10 pre-commit secret-scan hook" {

  BeforeAll {
    $script:RepoRoot   = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $script:BashHook   = Join-Path $script:RepoRoot 'hooks/no-secrets-scan.sh'
    $script:PwshHook   = Join-Path $script:RepoRoot 'scripts/hooks/no-secrets-scan.ps1'
    $script:Installer  = Join-Path $script:RepoRoot 'scripts/install-hooks.ps1'

    # Resolve bash from Git for Windows (Git Bash). Walk up the git.exe path
    # looking for a sibling bash.exe under bin/ or usr/bin/.
    $script:Bash = $null
    $gitCmd = Get-Command git -ErrorAction SilentlyContinue
    if ($gitCmd) {
      $dir = Split-Path $gitCmd.Source -Parent
      for ($i = 0; $i -lt 4 -and $dir -and -not $script:Bash; $i++) {
        foreach ($rel in @('bin/bash.exe', 'usr/bin/bash.exe')) {
          $candidate = Join-Path $dir $rel
          if (Test-Path $candidate) { $script:Bash = $candidate; break }
        }
        $dir = Split-Path $dir -Parent
      }
    }

    function New-ScratchRepo {
      $guid = [guid]::NewGuid().ToString('N').Substring(0, 12)
      $dir  = Join-Path $env:TEMP "tankgame-hooktest-$guid"
      New-Item -ItemType Directory -Path $dir -Force | Out-Null
      Push-Location $dir
      try {
        git init --quiet 2>&1 | Out-Null
        git config user.email "test@example.invalid"
        git config user.name  "Hook Test"
      } finally { Pop-Location }
      return $dir
    }

    function Remove-ScratchRepo {
      param([string]$Path)
      if ($Path -and (Test-Path $Path)) {
        Remove-Item -Recurse -Force $Path -ErrorAction SilentlyContinue
      }
    }

    function Stage-File {
      param([string]$Repo, [string]$Name, [string]$Content)
      $full = Join-Path $Repo $Name
      Set-Content -LiteralPath $full -Value $Content -NoNewline -Encoding utf8
      Push-Location $Repo
      try { git add -- $Name 2>&1 | Out-Null } finally { Pop-Location }
    }

    function Invoke-BashHook {
      param([string]$Repo)
      if (-not $script:Bash) { return $null }
      Push-Location $Repo
      try {
        # Convert Windows path to a form Git Bash understands.
        $hookPath = $script:BashHook -replace '\\', '/'
        & $script:Bash -c "'$hookPath'" 2>&1 | Out-Null
        return $LASTEXITCODE
      } finally { Pop-Location }
    }

    function Invoke-PwshHook {
      param([string]$Repo)
      Push-Location $Repo
      try {
        & pwsh -NoProfile -File $script:PwshHook 2>&1 | Out-Null
        return $LASTEXITCODE
      } finally { Pop-Location }
    }
  }

  Context "hook scripts exist" {
    It "bash hook 'hooks/no-secrets-scan.sh' exists" {
      Test-Path $script:BashHook -PathType Leaf | Should -BeTrue
    }
    It "pwsh hook 'scripts/hooks/no-secrets-scan.ps1' exists" {
      Test-Path $script:PwshHook -PathType Leaf | Should -BeTrue
    }
    It "installer 'scripts/install-hooks.ps1' exists" {
      Test-Path $script:Installer -PathType Leaf | Should -BeTrue
    }
  }

  Context "PowerShell hook (scripts/hooks/no-secrets-scan.ps1)" {

    It "exits 0 on a clean staged file" {
      $repo = New-ScratchRepo
      try {
        Stage-File -Repo $repo -Name 'clean.txt' -Content "Just some prose. Nothing sensitive here.`nNo credentials.`n"
        Invoke-PwshHook -Repo $repo | Should -Be 0
      } finally { Remove-ScratchRepo $repo }
    }

    It "blocks a staged file containing a password= assignment" {
      $repo = New-ScratchRepo
      try {
        # fake credential — test fixture only
        Stage-File -Repo $repo -Name 'bad.txt' -Content 'password = "abc12345"'
        Invoke-PwshHook -Repo $repo | Should -Not -Be 0
      } finally { Remove-ScratchRepo $repo }
    }

    It "blocks a staged file containing a token= assignment (16+ chars)" {
      $repo = New-ScratchRepo
      try {
        # fake credential — test fixture only (24 chars)
        Stage-File -Repo $repo -Name 'bad.cfg' -Content 'token = "AKIAIOSFODNN7EXAMPLE99"'
        Invoke-PwshHook -Repo $repo | Should -Not -Be 0
      } finally { Remove-ScratchRepo $repo }
    }

    It "blocks a staged file containing a Bearer token header" {
      $repo = New-ScratchRepo
      try {
        # fake credential — test fixture only
        Stage-File -Repo $repo -Name 'bad.http' -Content 'Authorization: Bearer abcdef1234567890ABCDEF1234567890'
        Invoke-PwshHook -Repo $repo | Should -Not -Be 0
      } finally { Remove-ScratchRepo $repo }
    }

    It "blocks a staged file containing a secret= assignment (8+ chars)" {
      $repo = New-ScratchRepo
      try {
        # fake credential — test fixture only
        Stage-File -Repo $repo -Name 'bad.env' -Content 'secret = "hunter2hunter2"'
        Invoke-PwshHook -Repo $repo | Should -Not -Be 0
      } finally { Remove-ScratchRepo $repo }
    }
  }

  Context "bash hook (hooks/no-secrets-scan.sh)" {

    It "exits 0 on a clean staged file" -Skip:(-not $script:Bash) {
      $repo = New-ScratchRepo
      try {
        Stage-File -Repo $repo -Name 'clean.txt' -Content "All clean here.`n"
        Invoke-BashHook -Repo $repo | Should -Be 0
      } finally { Remove-ScratchRepo $repo }
    }

    It "blocks a staged file containing a password= assignment" -Skip:(-not $script:Bash) {
      $repo = New-ScratchRepo
      try {
        # fake credential — test fixture only
        Stage-File -Repo $repo -Name 'bad.txt' -Content 'password = "abc12345"'
        Invoke-BashHook -Repo $repo | Should -Not -Be 0
      } finally { Remove-ScratchRepo $repo }
    }

    It "blocks a staged file containing a token= assignment (16+ chars)" -Skip:(-not $script:Bash) {
      $repo = New-ScratchRepo
      try {
        # fake credential — test fixture only
        Stage-File -Repo $repo -Name 'bad.cfg' -Content 'token = "AKIAIOSFODNN7EXAMPLE99"'
        Invoke-BashHook -Repo $repo | Should -Not -Be 0
      } finally { Remove-ScratchRepo $repo }
    }

    It "blocks a staged file containing a Bearer token header" -Skip:(-not $script:Bash) {
      $repo = New-ScratchRepo
      try {
        # fake credential — test fixture only
        Stage-File -Repo $repo -Name 'bad.http' -Content 'Authorization: Bearer abcdef1234567890ABCDEF1234567890'
        Invoke-BashHook -Repo $repo | Should -Not -Be 0
      } finally { Remove-ScratchRepo $repo }
    }
  }
}
