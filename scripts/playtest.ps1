# Launch the latest TankGame build for a play test.
# Double-clicked via the "Play TankGame (latest)" desktop shortcut.
# Pulls the newest committed code (best-effort), rebuilds the C# client, then runs it.

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

Write-Host "=== TankGame play test ===" -ForegroundColor Cyan
Write-Host "Repo: $repo`n"

# 1. Fetch the latest committed version (fast-forward only; never clobbers local work).
# Run git where stderr is NOT a terminating error: a blocked or diverged pull writes to
# stderr, and under $ErrorActionPreference='Stop' that throws and aborts the launcher
# before the "play current working tree" fallback below can run.
Write-Host "Updating to latest version..." -ForegroundColor Yellow
& { $ErrorActionPreference = 'Continue'; git -C $repo pull --ff-only 2>&1 | Write-Host }
if ($LASTEXITCODE -ne 0) {
    Write-Host "Could not fast-forward (offline, or local changes present) - playing current working tree." -ForegroundColor DarkYellow
}

# 2. Resolve Godot 4.6 (.NET) - PATH shim first, then the known winget install.
$godot = (Get-Command godot -ErrorAction SilentlyContinue).Source
if (-not $godot) {
    $godot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe'
}
if (-not (Test-Path $godot)) {
    Write-Host "Godot not found. Install it or fix the path in scripts/playtest.ps1." -ForegroundColor Red
    Read-Host "Press Enter to close"
    exit 1
}

# 3. Build the latest C# (Godot.NET.Sdk emits to .godot/mono where the runtime loads it).
Write-Host "`nBuilding client..." -ForegroundColor Yellow
dotnet build (Join-Path $repo 'client\TankGame.csproj') -c Debug -v minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nBuild failed - see errors above." -ForegroundColor Red
    Read-Host "Press Enter to close"
    exit 1
}

# 4. Play.
Write-Host "`nLaunching TankGame..." -ForegroundColor Green
& $godot --path (Join-Path $repo 'client')
