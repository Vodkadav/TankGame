# TankGame — Progress

Living status tracker. The full spec is `docs/research/development-plan.md`; this
file records what is actually done and what is next.

## Current status: **M1 + S1 complete** (2026-06-03) — starting M2

The CI/CD pipeline is live and every merge to `main` builds, tests, and deploys. M1's core
loop ships (drive + shoot + instructions overlay) and the S1 entity spine (ADR-0010) is
done — the world owns and steps every entity, nothing hand-spawns. Next work is **M2**
(static labyrinth + destructible walls) — see "Next" below.

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

## M1 complete — one tank, empty arena, moves and shoots (local)

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
- **T8 — instructions overlay** ✅ `ArenaScene` builds a screen-space `CanvasLayer` + `Label`
  ("WASD to move, mouse to aim, click to fire") wired to the `m1.instructions` key; EN/ES/DK
  strings in `strings.csv`; GoDotTest forces each locale and asserts the rendered line.
- M1 done. (Tank↔wall collision is deferred to M2's wall grid; M1's tank roams the open arena.)

---

## Done: extensibility foundation (roadmap S1) — ADR-0010

The expansion wishlist (new ammo types, tank upgrades, smart walls/doors, wormholes,
droppables, drones, points of interest, hide spots) is catalogued in
`docs/research/feature-roadmap.md`, which maps every idea to seven foundational system
abstractions (**S1–S7**). Building the systems first makes the content cheap to add.

**S1 — entity spine** ✅ **complete** (design + as-built record:
`docs/adr/0010-entity-spine.md`): a `World` that owns and `Step`s many entities with
spawn/despawn events, ending the `ArenaScene` hand-spawning. Settled decisions:
`ITank`/`IProjectile` **extend** `IEntity`; spawners get `IWorld` **injected at
construction**; **the world is the single tick owner** and the views are pure mirrors.

- **S1-T1 — `IEntity` + `IWorld` contracts** ✅ Domain interfaces (`Id`/`Position`/
  `IsAlive`/`Step`; `Entities`/`Spawn`/`EntitySpawned`/`EntityDespawned`/`Step`) with
  `EntityContractTests` + `WorldContractTests` (pure C#, no Godot). Scoped to contracts
  only — `ITank`/`IProjectile` adopt `: IEntity` in S1-T3/T4 alongside the impl changes.
- **S1-T2 — `World` impl (`GameLogic`)** ✅ owns entities in insertion order; `Spawn`
  adds + raises `EntitySpawned`; `Step` advances every entity, then reaps the dead and
  raises `EntityDespawned`. `Step` snapshots the live set first, so a mid-step spawn (the
  future-spawner seam) cannot corrupt iteration and the child is deferred to the next tick.
  `WorldTests` (7) verify it against the S1-T1 contract semantics.
- **S1-T4 — `Projectile` → `IEntity`** ✅ (done before T3: T3's `Tank` spawns a
  `Projectile` into `IWorld`, which needs `Projectile` to be an `IEntity` — the proposal
  listed T3→T4 but the spawn dependency runs the other way). `IProjectile` extends
  `IEntity` (empty body); `Projectile` assigns a stable `Guid Id`. `ProjectileTests` add
  Id-assigned/stable/unique.
- **S1-T3 — `Tank` → `IEntity` + owns the fire rule** ✅ `ITank` extends `IEntity` (adds
  only `Rotation`/`TurretRotation`); `Tank` assigns a `Guid Id`, is always alive (no health
  until S3), and takes an injected `IWorld` + `IArena`. `Step` spawns a `Projectile` into
  the world when `Fire` is held and the cooldown has elapsed — the rule moved out of
  `ArenaScene._Process`. `ArenaScene` now builds the `World`, subscribes once to
  `EntitySpawned`/`EntityDespawned` to map spawned projectiles to `ProjectileView`s, and
  calls `world.Step` per frame. **`ProjectileView` became a pure mirror here** (pulled
  forward from T5) because the world owning the projectile tick is what reaps the dead and
  avoids a `world.Entities` leak. `TankTests` cover the migrated fire rule (spawn, rate
  limit, aim) + `Id`/`IsAlive`; `ProjectileViewTests` rewritten mirror-only.
- **S1-T5 — world owns the tick; views are pure mirrors** ✅ the initial tank is spawned via
  `world.Spawn` (no hand-wiring); `ArenaScene` maps entity→view with a type-switch
  (`ITank` → `TankView` + `Camera2D`; `IProjectile` → `ProjectileView`) over a
  `Dictionary<Guid,Node2D>` registry, and frees a view when the world reaps its entity.
  `_Process` calls `world.Step` once. `TankView` became a pure mirror. `TankViewTests`
  rewritten mirror-only; `ArenaSceneTests` stays green and now exercises the whole
  spawn-event→factory pipeline.
- **S1-T6 — promote to a numbered ADR** ✅ the proposal is promoted to
  `docs/adr/0010-entity-spine.md` (Accepted; numbered 0010 because ADR-0004…0009 are
  reserved by name for M2…M7). Full NetArchTest suite green; roadmap §7 S1 row links the ADR.

S1 was the systems-foundation slice (not a numbered milestone in `development-plan.md`).
**M2 — static labyrinth + destructible walls** is the next numbered milestone and the next
work.

---

## Next: M2 — static labyrinth + destructible walls

Per `docs/research/development-plan.md` M2. Replaces the open `RectArena` with a tile wall
grid the tank collides against and projectiles can damage. The S1 entity spine makes the
destructible-wall content cheap. See the M2 ticket table in the development plan (M2-T1…T8,
including ADR-0004 on the `IWallGrid` interface + tile-state model).
