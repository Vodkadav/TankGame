#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0' }
<#
.SYNOPSIS
  Pester v5 scaffold-paths assertion for M0-T1.
  Asserts that every required directory and root file exists.
  Run: Invoke-Pester -Path scripts/test-scaffold.ps1 -PassThru
#>

Describe "M0-T1 monorepo scaffold" {

  BeforeAll {
    $script:Root = Split-Path $PSScriptRoot -Parent
  }

  Context "required directories" {
    $requiredDirs = @(
      "client",
      "client/src/Domain",
      "client/src/GameLogic",
      "client/src/Data",
      "client/src/Infrastructure",
      "client/src/Presentation",
      "client/tests",
      "server/worker",
      "server/supabase",
      "shared/protocol",
      "docs/adr",
      "docs/credits",
      "docs/licenses",
      "scripts"
    )

    It "directory '<dir>' exists" -TestCases ($requiredDirs | ForEach-Object { @{ dir = $_ } }) {
      Test-Path (Join-Path $script:Root $dir) -PathType Container | Should -BeTrue
    }
  }

  Context "required root files" {
    $requiredFiles = @(
      "README.md",
      "LICENSE",
      ".editorconfig",
      ".gitignore"
    )

    It "file '<file>' exists" -TestCases ($requiredFiles | ForEach-Object { @{ file = $_ } }) {
      Test-Path (Join-Path $script:Root $file) -PathType Leaf | Should -BeTrue
    }
  }

  Context ".gitkeep sentinels in empty directories" {
    $gitkeepDirs = @(
      "client/src/Domain",
      "client/src/GameLogic",
      "client/src/Data",
      "client/src/Infrastructure",
      "client/src/Presentation",
      "client/tests",
      "server/worker",
      "server/supabase",
      "shared/protocol",
      "docs/adr",
      "docs/credits",
      "docs/licenses"
    )

    It "'.gitkeep' exists in '<dir>'" -TestCases ($gitkeepDirs | ForEach-Object { @{ dir = $_ } }) {
      Test-Path (Join-Path $script:Root "$dir/.gitkeep") -PathType Leaf | Should -BeTrue
    }
  }
}
