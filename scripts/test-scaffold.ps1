#Requires -Modules Pester
<#
.SYNOPSIS
  Pester v5 scaffold-paths assertion for M0-T1.
  Asserts that every required directory and root file exists.
  Run: Invoke-Pester -Path scripts/test-scaffold.ps1 -PassThru
#>

param(
  [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

Describe "M0-T1 monorepo scaffold" {

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

    foreach ($dir in $requiredDirs) {
      It "directory '$dir' exists" {
        Test-Path (Join-Path $Root $dir) -PathType Container | Should -BeTrue
      }
    }
  }

  Context "required root files" {
    $requiredFiles = @(
      "README.md",
      "LICENSE",
      ".editorconfig",
      ".gitignore"
    )

    foreach ($file in $requiredFiles) {
      It "file '$file' exists" {
        Test-Path (Join-Path $Root $file) -PathType Leaf | Should -BeTrue
      }
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

    foreach ($dir in $gitkeepDirs) {
      It "'.gitkeep' exists in '$dir'" {
        Test-Path (Join-Path $Root "$dir/.gitkeep") -PathType Leaf | Should -BeTrue
      }
    }
  }
}
