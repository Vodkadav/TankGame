<#
.SYNOPSIS
    M0-T10 verification — the no-secrets scanner rejects hardcoded secrets.
    Run with: Invoke-Pester scripts/test-secret-scan.ps1
#>

Describe "M0-T10 no-secrets-scan" {

    BeforeAll {
        $repo = Split-Path -Parent $PSScriptRoot
        $script:scanner = (Join-Path $repo 'hooks/no-secrets-scan.sh') -replace '\\', '/'
        $script:bash = (Get-Command bash -ErrorAction SilentlyContinue).Source
        if (-not $script:bash) {
            $script:bash = 'C:/Program Files/Git/bin/bash.exe'
        }

        function Invoke-Scan([string]$content) {
            $tmp = New-TemporaryFile
            Set-Content -Path $tmp -Value $content -NoNewline
            $unix = $tmp.FullName -replace '\\', '/'
            & $script:bash $script:scanner $unix 2>$null | Out-Null
            $code = $LASTEXITCODE
            Remove-Item $tmp -Force
            return $code
        }
    }

    # Fixtures are assembled from fragments so these source lines do not themselves
    # trip the pre-commit hook when this test file is committed.
    It "rejects a file containing a hardcoded password" {
        $line = 'var ' + 'pass' + 'word = "abc12345"'
        (Invoke-Scan $line) | Should -Be 1
    }

    It "rejects a GitHub personal access token" {
        $line = 'gh' + 'p_' + ('a' * 36)
        (Invoke-Scan $line) | Should -Be 1
    }

    It "passes a clean file" {
        (Invoke-Scan 'var greeting = "hello world"') | Should -Be 0
    }
}
