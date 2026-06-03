# TankGame — Progress

Living status tracker. The full spec is `docs/research/development-plan.md`; this
file records what is actually done and what is next.

## Current status: **M1 + S1 + M2 + local-first combat arc complete; local polish arc in progress** (2026-06-03)

The CI/CD pipeline is live and every merge to `main` builds, tests, and deploys. M1's core
loop ships; the S1 entity spine (ADR-0010) is done; M2 (ADR-0004) ships the destructible maze
with tank↔wall collision + push-to-demolish; and the **local-first combat arc** (ADR-0011) is
complete — single-player vs AI, plus local 2-player co-op and versus chosen from a title
screen, with health, win/lose, and restart. The networked M3 is deferred (needs a Cloudflare
Durable Object + secrets + two-device testing). See the arc section and "Deferred: M3" below.

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

## Done: M2 — static labyrinth + destructible walls — ADR-0004

The empty `RectArena` box is replaced by a hand-authored maze of brick (3 hits, then breaks
to floor) and steel (indestructible) walls. Design + as-built record:
`docs/adr/0004-wallgrid-data-model.md`.

- **M2-T1 — `IWallGrid` (Domain)** ✅ tile-space grid contract (`GetCell`/`IsBlocked`/
  `DamageCell`/`CellChanged`); `WallCell(CellMaterial, Hp)`; out-of-bounds reads as steel.
- **M2-T3 — `WallGrid` (GameLogic)** ✅ 2D-array impl; brick chips and breaks to floor,
  steel/floor immune; `FromMaterials` factory fills brick at `DefaultBrickHp` (3).
- **M2-T5 — projectile↔wall collision** ✅ `GridArena` (Amanatides–Woo DDA) finds the first
  blocked cell; `RaycastFirstHit` stays a pure query and impact damage goes through a new
  `IArena.DamageAt`; `Projectile` carries a damage value and chips the struck brick.
- **M2-T2 — placeholder wall atlas** ✅ `Walls.png` (4×32px: brick intact/cracked/rubble +
  steel), generated by `scripts/gen_wall_atlas.py`; damage states baked as frames.
- **M2-T4 — `WallGridView` (Presentation)** ✅ a `TileMapLayer` that mirrors the grid and
  re-tiles a cell on `CellChanged`; builds its atlas `TileSet` in code.
- **M2-T6 — maze + wiring** ✅ `MazeDefinition` parses a text map; `Maze01` is the
  hand-authored 28×16 labyrinth; `ArenaScene` loads it into a `WallGrid`/`GridArena`, renders
  it, and spawns the tank at the maze spawn cell.
- **M2-T7 — brick-counter overlay + i18n** ✅ `m2.bricks_destroyed` (EN/ES/DK) and a
  `BrickCounterOverlay` that counts cells breaking to floor.
- **M2-T8 — ADR-0004** ✅ records the wall-grid model, the pure-query/explicit-damage split,
  and the text-map maze decision.

- **Post-M2 — tank↔wall collision + push-to-demolish** ✅ `IArena.IsBlocked(point)` +
  axis-separated movement in `Tank.Step` (the tank stops at walls and slides along them);
  steel blocks permanently, brick blocks until cleared. **Driving into a brick wall and
  holding** chips it once per `Tank.PushInterval` (≈1.2 s to break) through the same HP/crack
  frames as gunfire, then the tank rolls through. Requested by the developer after M2. (Dust
  particle on the break deferred to the art pass.)

---

## Done: Local-first combat arc — ADR-0011 (networked M3 deferred)

After playtesting M2, the developer chose to grow the game locally before networking:
**single-player vs computer adversaries → local 2-player → networked later.** Design +
MP-safety rationale + as-built record: `docs/adr/0011-local-first-combat.md`. The guarantee:
`IInputSource` is the universal "intent" seam (human / AI / future network player all drive a
tank the same way) and combat is a deterministic GameLogic pass — so this arc does not make
networked MP harder.

- **C1 — tank health + death** ✅ `IDamageable` (Hp/MaxHp/TakeDamage); HP-driven `IsAlive`
  (resolves the S1 stub); dead tanks reaped.
- **C2 — teams + projectile ownership** ✅ `Team` on `ITank`/`IProjectile`; a shot never
  hurts its own team.
- **C3 — projectile↔tank combat pass** ✅ `ICombatResolver`/`CombatResolver`: enemy shots
  damage tanks and expire; the `World` runs it each step between advancing and reaping.
- **C4 — computer adversaries** ✅ `AiInputSource` (seek/aim/fire-with-LOS) drives three
  enemy tanks, tinted red. Single-player vs AI playable.
- **C5 — local 2-player** ✅ `Player2InputSource` (arrows + drive-aim + click/Enter);
  `GameMode` (1P / co-op / versus) via a title screen; mode-aware spawning, camera, and
  win/lose text. **Co-op and versus playable.** (C5a input, C5b modes, C5c title screen.)
- **C6 — HUD + round flow** ✅ `MatchTracker` (last team standing); game-over overlay
  (win/lose/draw, EN/ES/DK) + Play-again restart; scene-level camera that survives player
  death; per-tank health bars. Proposal promoted to **ADR-0011**.

Test counts on `main`: GameLogic 78, Domain 22, Infrastructure 8, Architecture 6, 26 GoDotTest
scene tests.

## In progress: Local polish arc (no new art, no networking)

A curated cut of `feature-roadmap.md` that ships now with programmer-drawn placeholder visuals
and runs on one device — the battlefield/cover, fog of war, hide spots, and match-flow items.
Catalogue and ordering: `docs/research/local-backlog.md`.

- **LP1 — open battlefield** ✅ replaced the dense `Maze01` labyrinth with `Battlefield01`,
  an open arena of scattered brick (destructible) and steel (permanent) cover. The maze
  parser/loader generalised to `LevelMap`; the "no orphan wall" test flipped to the new
  intent — interior is ≥80% open floor and every floor cell is reachable from the spawn
  (no walled-off pockets), while single-cell cover is now allowed. Camera/spawns unchanged
  (still 28×16).

Test counts on `main`: GameLogic 80, Domain 22, Infrastructure 8, Architecture 6, 26 GoDotTest
scene tests.

### Deferred: M3 — 2-player real-time via a single Durable Object

Still planned (`development-plan.md` M3), but **after** the local-first arc. Needs developer
involvement (Cloudflare Durable Object + secrets + two-device testing), so it is not
autonomous; the intent seam and deterministic combat above are what keep it tractable.
