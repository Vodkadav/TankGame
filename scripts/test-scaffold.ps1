<#
.SYNOPSIS
    M0-T1 scaffold verification — asserts every required monorepo path exists.
    Run with: Invoke-Pester scripts/test-scaffold.ps1
    Compatible with Pester v3 (Test-Path assertions) and Pester v5.
#>

$root = "C:\programmering\TankGame"

function Assert-PathExists([string]$relativePath) {
    $full = Join-Path $root $relativePath
    Test-Path $full | Should Be $true
}

function Assert-FileContains([string]$relativePath, [string]$pattern) {
    $full = Join-Path $root $relativePath
    (Get-Content $full -Raw) -match $pattern | Should Be $true
}

Describe "M0-T1 Monorepo scaffold" {

    Context "client/src layer directories" {
        It "client/src/Domain exists" {
            Assert-PathExists "client\src\Domain"
        }
        It "client/src/GameLogic exists" {
            Assert-PathExists "client\src\GameLogic"
        }
        It "client/src/Data exists" {
            Assert-PathExists "client\src\Data"
        }
        It "client/src/Infrastructure exists" {
            Assert-PathExists "client\src\Infrastructure"
        }
        It "client/src/Presentation exists" {
            Assert-PathExists "client\src\Presentation"
        }
    }

    Context "client/tests directory" {
        It "client/tests exists" {
            Assert-PathExists "client\tests"
        }
    }

    Context "server directories" {
        It "server/worker exists" {
            Assert-PathExists "server\worker"
        }
        It "server/supabase exists" {
            Assert-PathExists "server\supabase"
        }
    }

    Context "shared directory" {
        It "shared/protocol exists" {
            Assert-PathExists "shared\protocol"
        }
    }

    Context "docs directories" {
        It "docs/adr exists" {
            Assert-PathExists "docs\adr"
        }
        It "docs/credits exists" {
            Assert-PathExists "docs\credits"
        }
        It "docs/licenses exists" {
            Assert-PathExists "docs\licenses"
        }
    }

    Context "scripts directory" {
        It "scripts exists" {
            Assert-PathExists "scripts"
        }
    }

    Context ".github/workflows directory" {
        It ".github/workflows exists" {
            Assert-PathExists ".github\workflows"
        }
    }

    Context "root files" {
        It "README.md exists" {
            Assert-PathExists "README.md"
        }
        It "LICENSE exists" {
            Assert-PathExists "LICENSE"
        }
        It ".editorconfig exists" {
            Assert-PathExists ".editorconfig"
        }
        It ".gitignore exists" {
            Assert-PathExists ".gitignore"
        }
        It ".gitignore retains .claude/plans/ entry" {
            Assert-FileContains ".gitignore" ([regex]::Escape(".claude/plans/"))
        }
    }

    Context ".gitkeep sentinels in otherwise-empty directories" {
        # server/worker, client/src/Presentation and client/tests are populated by
        # sibling M0 slices, so they carry no .gitkeep — their existence is asserted above.
        It "client/src/Domain/.gitkeep exists" {
            Assert-PathExists "client\src\Domain\.gitkeep"
        }
        It "client/src/GameLogic/.gitkeep exists" {
            Assert-PathExists "client\src\GameLogic\.gitkeep"
        }
        It "client/src/Data/.gitkeep exists" {
            Assert-PathExists "client\src\Data\.gitkeep"
        }
        It "client/src/Infrastructure/.gitkeep exists" {
            Assert-PathExists "client\src\Infrastructure\.gitkeep"
        }
        It "server/supabase/.gitkeep exists" {
            Assert-PathExists "server\supabase\.gitkeep"
        }
        It "shared/protocol/.gitkeep exists" {
            Assert-PathExists "shared\protocol\.gitkeep"
        }
        It "docs/adr/.gitkeep exists" {
            Assert-PathExists "docs\adr\.gitkeep"
        }
        It "docs/credits/.gitkeep exists" {
            Assert-PathExists "docs\credits\.gitkeep"
        }
        It "docs/licenses/.gitkeep exists" {
            Assert-PathExists "docs\licenses\.gitkeep"
        }
        It ".github/workflows/.gitkeep exists" {
            Assert-PathExists ".github\workflows\.gitkeep"
        }
    }

    Context "LICENSE content" {
        It "LICENSE contains MIT text" {
            Assert-FileContains "LICENSE" "MIT License"
        }
        It "LICENSE contains copyright holder" {
            Assert-FileContains "LICENSE" "Daniel Machado"
        }
        It "LICENSE contains year 2026" {
            Assert-FileContains "LICENSE" "2026"
        }
    }
}
