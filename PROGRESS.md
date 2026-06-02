# TankGame — Progress

Living status tracker. The full spec is `docs/research/development-plan.md`; this
file records what is actually done and what is next.

## Current status: **M0 complete** (2026-06-02)

The CI/CD pipeline is live and every merge to `main` builds, tests, and deploys.

### Live surfaces

| Surface | State |
|---------|-------|
| Worker `tankgame-worker.vodkadav.workers.dev/healthz` | ✅ 200 (Sentry-wrapped) |
| Cloudflare Pages landing page | ✅ deployed (per `CF_PAGES_PROJECT`) |
| Android debug APK | ✅ published to the rolling [`canary`](https://github.com/Vodkadav/TankGame/releases/tag/canary) GitHub Release (~79 MB, arm64-v8a) |

### M0 tickets (all done)

T1 monorepo scaffold · T2 Worker skeleton (`/healthz`) · T3 Godot C# client skeleton ·
T4 NetArchTest five-layer rules · T5 `ci.yml` (lint, arch, build + **headless GoDotTest**, worker) ·
T6 `deploy.yml` (Worker + Pages + **Android APK release**) · T7 i18n (en/es/dk) ·
T8 ADR-0001 (layered architecture) · T9 ADR-0002 (monorepo + CI/CD) ·
T10 pre-commit secret-scan hook · T11 Sentry in Worker · T12 Sentry in Godot client.

### Deferrals resolved

- **Android APK export** — built via the .NET→Android Gradle custom-build path; debug-signed
  with an in-job keystore (no secret). Required enabling `gradle_build/use_gradle_build`,
  dropping `armeabi-v7a` (.NET targets arm64/x86_64), and ETC2/ASTC VRAM compression.
- **GoDotTest scene-test runner** — `Bootstrap.cs` `--run-tests` entry runs the scene tests
  headless inside Godot and gates CI (GoDotTest 2.0.34, `Godot.NET.Sdk` 4.6.2).

## In progress: M1 — one tank, empty arena, moves and shoots (local)

The web→Android plan revision is done, so M1 is unblocked (target: Android APK + desktop
dev build; 60 fps on the Galaxy A56).

- **T1 — Domain interfaces** ✅ `ITank`, `IProjectile`, `IInputSource`, `IArena` (pure C#,
  `System.Numerics`, no Godot) with xUnit contract tests in `client/tests/Domain/`.
- **T3 — `Tank` (GameLogic)** ✅ moves at constant speed, faces movement, aims turret;
  9 xUnit tests in `client/tests/GameLogic/`.
- **T4 — `Projectile` (GameLogic)** ✅ travels straight, dies on `IArena` hit (snaps to point).
- **T10 — ADR-0003** ✅ interface-first rule + `IServiceName` lives in Domain.
- **T2 — tank sprite** ✅ **placeholder** body+turret PNGs (PIL-generated; to be replaced by
  Kenney CC0 — see `docs/credits/assets.md`).
- **T5 — `TankView`** ✅ Node2D binding an `ITank`: node follows position, Body/Turret
  sprites rotate with chassis/aim; GoDotTest verifies load + model mirroring.
- **T7 — `KeyboardMouseInputSource`** ✅ WASD move, mouse aim (vs viewport centre),
  click/space fire; pure `ReadMove`/`ComputeAim` helpers with xUnit tests.
- **T9 — drivable wiring** ✅ `Arena.tscn`/`ArenaScene` builds the input source, a `Tank`,
  a `TankView`, and a following `Camera2D`. App-init (Sentry, translations) moved to `Bootstrap`; the M0
  boot-label scene (`Main.tscn`/`MainScene`) is retired (Sentry test now targets
  `SentryBootstrap`). **The tank is drivable** — launch via the desktop shortcut.
- **T4-view / T6 — firing + projectile + walls** ✅ `RectArena` (analytic rectangle
  raycast, GameLogic) bounds the arena; firing (rate-limited) spawns a `Projectile` +
  `ProjectileView` from the turret that travels and despawns on a wall hit; the arena
  draws its wall border. **The tank moves and shoots** — M1's core loop.
- Remaining M1: instructions overlay (T8, i18n). (Tank↔wall collision is deferred to M2's
  wall grid; M1's tank roams the open arena.)
