# TankGame — Progress

Living status tracker. The full spec is `docs/research/development-plan.md`; this
file records what is actually done and what is next.

This `## Status` block is the live checklist the Command Center `/projects`
dashboard reads — keep it in sync with the detailed sections below.

## Status
- [x] M0 — scaffold, CI/CD, i18n, Sentry, secret-scan hook (T1–T12)
- [x] M1 — one tank, empty arena, moves and shoots (local)
- [x] S1 — extensibility foundation / entity spine (ADR-0010)
- [x] M2 — static labyrinth + destructible walls (ADR-0004)
- [x] Local-first combat arc — single-player vs AI, local 2-player co-op & versus (ADR-0011)
- [~] Local polish arc — no new art, no networking
- [~] M3 — networked multiplayer (secret-free scaffolding underway; live deploy needs Cloudflare Durable Object + secrets + two-device testing)

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
- **LP6 — tank↔tank body collision** ✅ tanks are circles of `CollisionRadius` and may not
  overlap: each axis of a tank's move is rejected if the new centre would come within a
  tank-diameter of another live tank (`Tank.OverlapsAnotherTank` over `World.Entities`). The
  mover stops; no momentum is transferred. Wall and tank blocks are tracked separately so
  bumping a tank never chips a wall behind it (push-to-demolish still needs a wall).
- **LP5 — AI vision-gated targeting** ✅ the enemy AI no longer seeks omnisciently through
  walls: `NearestVisibleEnemy` only acquires an enemy within `VisionRange` (≈18 tiles) with a
  clear line of sight, so an enemy that breaks line of sight (behind steel, and later a bush)
  is no longer hunted — the AI holds until it sees something. Foundation for the fog/stealth
  cluster. `VisionRange` is a tunable balance knob.
- **LP4 — bushes / hide spots** ✅ a new `IConcealment` Domain seam: a tank standing on a bush
  cell is hidden from the AI unless it is within `BushRevealRange` (≈1.5 tiles). `LevelMap`
  parses `b` as passable floor flagged concealing; `BushField` answers `ConcealsAt`;
  `AiInputSource` skips a far bushed target; `BushOverlay` draws translucent green patches
  (code-built, no art). `Battlefield01` gained four bush patches. Bushes never block movement
  or shots — concealment only.
- **LP3 — fog of war (local)** ✅ a dark `CanvasModulate` over the field with a `PointLight2D`
  per player-team ally that follows them (`SetUpFog` in `ArenaScene`), so the team sees a lit
  circle (radius ≈ AI fire range) and the rest is dark. The light texture is a code-built
  radial `GradientTexture2D` (no art). On in one-player and co-op (allies share the reveal),
  off in versus (one shared screen can't fairly fog rivals). `FogAmbient`/`FogLightRadius` are
  tunable; no wall shadows yet (a soft circular reveal). Visual darkness/radius awaits a
  playtest pass. **Headline vision cluster (battlefield + fog + bushes) complete.**
- **LP8 — score / kill tracking** ✅ a pure-C# `ScoreBoard` tallies kills per team, fed by a new
  `CombatResolver.TankKilled` event that fires with the shooter's team the moment a shot's damage
  destroys a tank (once per death, since the dead are skipped thereafter). A `ScoreOverlay` HUD
  (top-right, EN/ES/DK `hud.score` "Score {0} - {1}") mirrors the board live, and the game-over
  screen shows the final tally. First match-flow item — no art, no networking.
- **LP9 — lives / respawn** ✅ each tank gets `lives` (ctor, default 1 = no respawn; `ArenaScene`
  uses `StartingLives` 3 uniformly). A downed tank (Hp 0) stays *in the match* and revives at its
  spawn at full health after `Tank.RespawnDelay` (2 s), spending one life; once lives run out the
  world reaps it. `IEntity.IsAlive` on a tank now means "in the match" (Hp > 0 **or** a life left);
  combat targeting/kill-credit, tank–tank collision, AI target acquisition, the fog light, and the
  `TankView` (hidden while down) all key off `Hp > 0` (tangible right now). `MatchTracker` keeps a
  respawning team alive via `IsAlive`. Kills still credit on every destruction (LP8 intact).
- **LP10 — best-of-N rounds** ✅ a pure-C# `SeriesTracker` (`roundsToWin`, default best-of-three)
  tallies round wins and reports the match winner. `GameSetup` carries the `Series` across the
  per-round scene reloads and `StartNewMatch` resets it (title screen and "Play again"). When a
  round is decided `ArenaScene` records it, then the round-end panel shows the round outcome, the
  round kill score, and the running round tally (`hud.rounds` EN/ES/DK); the button reads
  "Next round" (`hud.next_round`) mid-series — reloading with the series intact — and "Play again"
  once the match is won, starting a fresh series.
- **S4 #11 — stats + timed status effects** ✅ (`docs/adr/0012-stats-and-status-effects.md`) a
  pure-C# `Stats` block (base value per `StatKind` ∘ a list of expiring `StatusEffect` modifiers)
  replaces the tank's raw `_speed`/`_fireInterval` fields. `Current(kind) = (base + Σ AddFlat) ×
  Π Mult` over live effects; `Stats.Step` ages them in real time. `Tank.ApplyEffect(StatusEffect)`
  is the single entry point — powerups/traps (#12) become "apply an effect", not new mechanics.
  The `Tank` ctor is unchanged (seeds `Stats` from the same args). Foundation only; over-shield /
  damage-over-time (which touch `MaxHp`/`IDamageable`) are deferred per the ADR.
- **S4 #12 — pickups (stat-based)** ✅ a new `IPowerup`/`PowerupKind` Domain seam + `Powerup`
  GameLogic entity: each `Step` it scans the world and the first live tank within `PickupRadius`
  collects it (`Tank.ApplyEffect` + the powerup expires). `PowerupView` draws a code-built
  coloured diamond keyed by kind (blue speed / orange rapid-fire — no art). `ArenaScene` spawns a
  **speed-boost** (×1.6, 6 s) and a **rapid-fire** (fire interval ×0.5, 6 s) pickup at mid-field
  floor cells via the entity-spine type-switch. One pickup per round (a round reload re-lays
  them); no respawn yet. **Shield / repair deferred** — they need the `MaxHp`/health-modifier
  path ADR-0012 deferred.

- **S2 #13 — weapon-behaviour strategy + raycast normals** ✅
  (`docs/adr/0013-weapon-behaviour-strategy.md`). Two parts: (a) **raycast normals** (#76) —
  `RaycastHit` gained `Normal` (unit normal of the struck face, back along the ray), derived from
  the `GridArena` DDA axis / the `RectArena` exited wall, so a reflection is `dir − 2·(dir·n)·n`;
  (b) **behaviour strategy** — `Projectile` now holds a `ProjectileState` (data) + an
  `IProjectileBehaviour` (motion/impact) and just delegates `Step`; `StraightBehaviour` is the
  prior logic (one shared instance). The new `behaviour` ctor arg defaults to straight, so every
  call site is untouched and the refactor is behaviour-preserving. New ammo = a new behaviour only
  when the motion is genuinely new.
- **S2 #14 — bouncing & spread shells via ammo crates** ✅ `BouncingBehaviour` reflects off the
  hit `Normal` (`v − 2(v·n)n`), decrementing a per-shot bounce budget, then lands like a straight
  shot when spent. `IWeapon` firing strategy: `BehaviourWeapon` (one shot, a behaviour factory),
  `SpreadWeapon` (N straight pellets fanned about the aim). `Tank` gains a default straight weapon
  plus `LoadAmmo(weapon, shots)`; it fires the special weapon while ammo lasts, then reverts. The
  `Powerup` was generalised to carry an `IPickupEffect` (`StatusEffectPickup` | `AmmoPickup`), so
  ammo **crates reuse the S4 pickup entity/view**; `ArenaScene` lays a purple bouncing crate and a
  pink spread crate (5 shots each). **Piercing deferred** — it needs pierce-aware combat or
  wall-skip geometry (a separate decision).
- **Health path — shield & repair pickups** ✅ resolves the `MaxHp`/health-modifier deferral from
  ADR-0012. `Tank` gains `Heal(amount)` (restores Hp clamped at `MaxHp`, no-op on a downed tank) and
  an over-shield pool: `Shield` (now on `ITank`), `AddShield(amount)` (stacks, uncapped), and
  `TakeDamage` spends shield before hit points. Two new `IPickupEffect`s (`RepairPickup`,
  `ShieldPickup`) reuse the S4 pickup entity/view; `PowerupKind` gains `Repair` (green) / `Shield`
  (cyan); `TankView` draws a cyan over-shield bar above the health bar (hidden when unshielded).
  `ArenaScene` lays a repair (+2 Hp) and shield (+3) pickup. Shield is local-combat only for now
  (not in the net `TankState`); `NetTank.Shield` defaults to 0.
- **Pickup respawn** ✅ a collected pickup now returns to the field after a delay instead of being
  gone for the round. `Powerup` gained a `respawnDelay` ctor arg (default 0 = one-shot, unchanged):
  with a positive delay the entity stays in the world and toggles an `IsAvailable` flag (added to
  `IPowerup`) — collected → dormant for the delay → available again at the same spot — rather than
  spawn/despawn churn. `PowerupView` became a per-frame mirror that hides the shape while dormant.
  `ArenaScene` uses a 12 s respawn delay for all field pickups. One-shot collection (the prior
  behaviour) is preserved for `respawnDelay` 0.
- **S9 — damage + K/D meters (HUD)** ✅ first S9 slice. `CombatResolver` gains a `CombatHit`
  record (`ShooterTeam`, `VictimTeam`, `Amount`, `Killed`) and a `Hit` event raised on every landed
  shot — richer than the kill-only `TankKilled` the `ScoreBoard` still uses. A pure-C# `MeterBoard`
  tallies per-team damage dealt, kills, and deaths from it; `MetersOverlay` (top-left, EN/ES/DK
  `hud.meters` "Damage L-R  K/D L-R") mirrors it live. Single-responsibility split: `ScoreBoard`
  owns the round-winning kill score (top-right), `MeterBoard` owns the performance meters. (The
  layer guard's `IsCompilerGenerated` was hardened to ignore Roslyn's `<>`-named params-span buffer,
  emitted by the 6-arg `string.Format`.)
- **S2 — piercing ammo** ✅ completes the S2 ammo trio (bouncing/spread/piercing), resolving the
  ADR-0013 deferral. A shared pierce budget lives on `ProjectileState.Pierce`, decremented by both
  collision paths: `PiercingBehaviour` punches through a destructible (brick) wall and stops on
  steel/permanent or when the budget runs out (`RaycastHit` gained `Destructible`, default false;
  `GridArena` flags brick); the `CombatResolver` passes a piercing shot through one tank — tracking
  already-hit tanks (`Projectile.HasHit`/`RegisterTankHit`) so it damages each once — and stops it on
  the next. Spec: pierce one target (tank or brick), the next stops it and takes damage, steel always
  stops. `PiercingWeapon` seeds the budget; `PowerupKind.PiercingAmmo` (yellow crate, 5 shots) laid
  at a mid-field floor cell. Ordinary shots (budget 0) are unchanged.
- **S8 — procedural arena generation** ✅ (`docs/adr/0014-procedural-arena-generation.md`) the local
  battlefield is generated, not hand-authored. `LevelMap` gains a `FromCells` producer beside `Parse`;
  `ArenaGenerator.Generate(ArenaGenParams)` returns a **seeded, validated** `GeneratedArena` — scattered
  brick/steel/bush in a steel border, **valid by construction**: the generator **places its own spawns
  and one cell per pickup** (spread out, locked to floor before scattering), fills unreachable floor
  pockets with steel (every floor cell reachable), keeps the interior ≥80% open, bounded retries then a
  fallback. `GameSetup.ArenaSeed` is rolled per match by `StartNewMatch` (fixed by default for tests),
  and **`GameSetup.ArenaWidth/Height` make the size adjustable** — the scene hard-codes no cell and the
  two-player camera fits whatever size was generated. **Local only** — `NetArenaScene` keeps the shared
  `Battlefield01`. Deferred: theming, a title-screen size control, versus symmetry.
- **S8 — arena theming (seam)** ✅ first theming slice. `ArenaTheme` (Presentation) is a swappable
  palette = ground colour + wall tint; `GameSetup.Theme` selects it (defaults to `Sandy`, the owner's
  reference look). `ArenaScene` paints a `Ground` Polygon2D (negative ZIndex) under the field in
  `Theme.Ground` and applies `Theme.WallTint` to the `WallGridView.Modulate`, so a theme recolours
  whatever wall art is loaded — **source-agnostic, not blocked on the art pass**. Ships `Sandy` +
  `Slate` so the swap is real and tested. Deferred: a title-screen theme picker; biome/ground sprites
  (art pass).
- **Art-pass prep — tank team tinting (seam)** ✅ `TeamPalette` (Presentation) is the single source of
  the per-team `Sprite2D.Modulate` tint (friendly = white/as-authored, enemy = reddened), and
  `TankView.ApplyTeamTint(isEnemy)` owns applying it. Both `ArenaScene` and `NetArenaScene` now call it
  instead of each hardcoding the same red `Color`, so one neutral tank texture will read as either side
  once the tintable art lands.
- **S9 — match modifiers** ✅ (`docs/adr/0015-match-modifiers.md`) the first S9 *gameplay* slice:
  "everyone starts with effect X". `MatchModifier` (GameLogic) carries a list of `StartingEffects`
  applied to every tank at spawn via `ApplyTo(Tank)`; reuses the ADR-0012 stats machinery, so no new
  combat code. `MatchModifier.Permanent` builds an infinite-duration effect (`Stats.Step` never expires
  one whose remaining time stays positive); `None` (default) is a no-op and `Blitz` (everyone faster +
  rapid-firing) is a real, tested preset. `GameSetup.Modifier` is the match-level seam (beside Mode /
  Series / Seed / size / Theme); `ArenaScene.SpawnTank` applies it uniformly to player, P2, and AI.
  Local only — `NetArenaScene` (authoritative) untouched. Deferred: a title-screen selector, the
  mine/oil-slick trap modifiers (need art + hazard entities), NPC-animal XP.
- **Art-pass prep — asset catalogue (swap seam)** ✅ `AssetCatalogue` (Presentation) is the single
  source of truth for every sprite `res://` path (`Active` set, `Default` = the current placeholders).
  `TankView`, `ProjectileView`, and `WallGridView` now load their textures from the catalogue in code,
  and the embedded textures were stripped from `TankView.tscn`/`ProjectileView.tscn`, so nothing
  hardcodes a path in a scene or a view. Swapping one asset is `Active = Default with { … }`; swapping a
  whole set is one assignment — ready to repoint at the imported **Kenney CC0** pack (owner picked the
  hybrid: Kenney base sprites + generated glowing pickup discs). Behaviour-preserving (still the
  placeholder art).

Test counts on `main`: GameLogic 187, Domain 32, Infrastructure 12, Architecture 6, 65 GoDotTest
scene tests.

**Owner ask (2026-06-04): map variety + progression — both now under way.** Captured in
`docs/research/feature-roadmap.md` as two systems. **S8 arena generation & theming** — the
**generation** slice is done (ADR-0014, generated battlefield above) and the **theming seam** is in
(`ArenaTheme` ground/wall palette above); still deferred: biome/ground **sprites** (art pass), a
title-screen theme + size picker. **S9 progression/meters/match-modifiers** — the **damage + K/D meters**
slice (HUD) and the **match-modifier framework** ("everyone starts with effect X", ADR-0015) are done;
still deferred: cosmetic-only unlocks, the trap modifiers (mine / oil-slick) and shootable NPC-animal-XP,
and the XP layer (post-systems content). Each remaining slice gets its own ADR before build.

### M3 — 2-player real-time via a single Durable Object (scaffolding underway)

Still gated on developer involvement for the **live** milestone (Cloudflare account + Durable
Object deploy + GitHub Actions secrets + two-device testing). The **secret-free scaffolding** is
being built autonomously ahead of that:

- **M3-T1 — `IMatchTransport` + `INetClock` (Domain)** ✅ the client↔server seam:
  `SendInput(InputFrame)` up, `SnapshotReceived` down; `INetClock` (monotonic `Now` + server
  `TickRateHz`). Contract tests on hand-written loopback/clock fakes. "Client sends intent, server
  resolves outcome" — the network analogue of the `IInputSource` seam (ADR-0011).
- **M3-T2 — wire protocol (both languages)** ✅ `TankGame.Domain.Net`: `InputFrame` /
  `SnapshotFrame` / `TankState` / `WallDelta` + `ProtocolCodec` (hand-rolled little-endian binary),
  and its TypeScript mirror `server/worker/src/protocol/codec.ts`. Roundtrip tests in **both**
  languages plus **identical canonical byte-vector** tests (the cross-language parity anchor — if
  either side changes the layout, both fail).
- **M3-T3 — `MatchRoom` Durable Object skeleton** ✅ `server/worker/src/MatchRoom.ts`: accepts the
  WebSocket upgrade via the hibernation API (`state.acceptWebSocket`) and relays each peer's frame
  to the others; the Worker routes `/room/:code` to one DO per code (`idFromName`). Vitest +
  Miniflare test connects two sockets and asserts the host's input echoes to the guest. `wrangler.toml`
  gains the `MATCH_ROOM` DO binding + a `new_sqlite_classes` migration. **Deployed live** — CI's
  `deploy.yml` ran `wrangler deploy` on merge; `…workers.dev/room/TEST01` answers `426`.
- **M3-T4 — lobby routes** ✅ `server/worker/src/lobby.ts`: `POST /lobby` allocates a 6-char code
  (unambiguous alphabet, collision-retry) mapped to its DO id in `LOBBY_KV`; `POST /lobby/:code/join`
  validates the code and returns the `/room/:code` WS URL (404 unknown). `LOBBY_KV` binding wired in
  `wrangler.toml` (namespace created by the developer). Vitest: pure-function collision-retry/store/
  join + Miniflare route round-trip. Worker suite 17 tests green.
- **M3-T5 (core) — authoritative `MatchSim`** ✅ `server/worker/src/sim/`: a pure, deterministic
  TypeScript MVP of the match — `map.ts` ports Battlefield01 (materials + brick HP, OOB steel);
  `matchSim.ts` runs 2 tanks (per-slot input → axis-separated wall-collided movement, turret aim,
  rate-limited firing), projectiles (travel → brick chip/break with `WallDelta`s, enemy-tank
  damage), and `snapshotFor(slot)` (per-client `ackSeq`). **Anti-cheat clamps live here**: move
  magnitude ≤ 1 and a server-enforced fire interval, so a tampered client cannot move/shoot faster.
  Spawn cells are injectable for tests. 12 new Vitest cases (worker suite 29).
- **M3-T5 (wiring) — `MatchRoom` runs the sim** ✅ the DO replaced its relay with the authoritative
  loop: assigns each joiner a slot (host 0 / guest 1, stored via `serializeAttachment` to survive
  hibernation), decodes `InputFrame`s into `sim.applyInput`, and a 20 Hz `setInterval` steps the sim
  and sends each client its own `snapshotFor(slot)`. The loop runs only while players are connected
  (empty room → stop + drop sim → hibernate); a third joiner gets `503`. Miniflare integration test:
  two sockets join, an input frame moves the host's tank, and a broadcast snapshot reflects it.
  Worker suite 30 tests. **Server side of M3 is functionally complete** — remaining is the client
  (T6 `WebSocketTransport`, T7 prediction, T8 `TEST01` join UI, T9 strings) + T11 alarm.
- **M3-T6 — client `WebSocketTransport`** ✅ `client/src/Infrastructure/Net/`: `WebSocketTransport`
  implements `IMatchTransport` over an `IMatchSocket` byte seam — `SendInput` encodes via
  `ProtocolCodec` and `Poll()` decodes each inbound message up as `SnapshotReceived`. Kept free of
  Godot so it unit-tests against a fake socket (xUnit: send-encodes, poll-raises-per-message,
  empty-poll-silent); `GodotWebSocket` wraps Godot's `WebSocketPeer` as the live socket (untested
  wiring — the framing is covered by the transport + codec tests). Infrastructure suite 11 tests.
- **M3-T8 — hardcoded `TEST01` join flow** ✅ `NetworkSession` (Presentation) joins via a swappable
  `TransportFactory` (default builds the live `WebSocketTransport`/`GodotWebSocket` to
  `wss://…workers.dev/room/TEST01`); the title screen gains a "Join TEST01" button (`title.join_test`
  in en/es/dk) that calls `NetworkSession.Join` and disables itself. GoDotTest exercises the click
  path against a mock transport (button-present + press-joins-TEST01-and-stores-active). Scene suite
  38 tests. The networked play-scene swap (rendering remote tanks from snapshots) waits on the T7
  prediction wiring.
- **M3-T7 — client prediction + reconciliation** ✅ `client/src/GameLogic/PredictedTank.cs`: the
  local tank `Predict`s each input immediately (no input lag) and buffers it; on a snapshot,
  `Reconcile` snaps this slot to the server's authoritative transform/health, drops every input the
  server has acked (`Seq ≤ AckSeq`), and replays the rest — so a correct prediction is invisible and
  a wrong one is pulled smoothly to truth. The movement model **mirrors the server sim** byte-for-step
  (200 u/s, axis-separated 24 u leading-edge wall collision, unit-magnitude clamp) so replayed inputs
  reproduce the server path; pure C#, no Godot/transport (the caller wires `SnapshotReceived` →
  `Reconcile`). 10 deterministic xUnit cases (predict-advances/clamps/turret, reconcile snap/replay/
  discard/correction/wall/wrong-slot, full transport loop). GameLogic suite 146 tests.
- **M3 play-scene groundwork — slot discovery + transport pump** ✅ (toward the net play scene): a
  server→client message now carries a leading **kind byte** (`MSG_WELCOME` / `MSG_SNAPSHOT`); the DO
  sends a `WelcomeFrame { slot }` once on connect so a client learns whether it is host (0) or guest
  (1), and tags each snapshot. `encodeWelcome`/`EncodeWelcome` exist in both codecs with a
  cross-language byte-vector parity anchor. `IMatchTransport` gains `Poll()` (pump the socket each
  frame) + `WelcomeReceived`; `WebSocketTransport.Poll` dispatches inbound messages on the kind byte.
  Worker suite 33, Domain 32, Infrastructure 12.
- **M3 networked play scene** ✅ `client/src/Presentation/Arena/NetArenaScene.cs` (+ `NetArena.tscn`):
  the client half of the authoritative match. Loads the shared `Battlefield01`; on the welcome it
  adopts its slot and starts a `PredictedTank`; each `_Process`/`Tick` pumps the transport, sends the
  local intent (`InputFrame`, seq++), predicts the local tank, and follows it with the camera; each
  snapshot reconciles the local tank and mirrors every other slot straight from its `TankState`;
  `WallDelta`s apply to the grid via the new absolute `WallGrid.SetCell`. `NetTank` (GameLogic) is a
  mutable `ITank` view-model so the existing `TankView` renders network state unchanged. The title
  "Join TEST01" button now enters the scene (guarded so the GoDotTest click-path doesn't swap the
  runner's scene). GoDotTest drives welcome/snapshot/Tick against a fake transport (slot adopt, remote
  mirror, local reconcile, per-frame input send, wall-delta apply); scene suite 43, GameLogic 148.
- **M3-T9 — connection-status UI (EN/ES/DK)** ✅ `NetStatusOverlay` (a screen-space banner) shows
  `net.connecting` until the welcome, `net.connected` once welcomed, and `net.player2_joined` when the
  opponent first appears in a snapshot; `NetArenaScene` drives it. Strings added in all three locales;
  GoDotTest forces each locale and asserts the three render, plus a scene test that the status
  progresses connecting → connected → joined. Scene suite 47.
- **M3-T11 — request-budget alarm** ✅ `server/worker/src/budget.ts`: a daily cron (`crons = ["0 6 * * *"]`)
  reads this month's Durable Object request usage and posts a Sentry warning at **80%** of the
  free-tier budget (ADR-0005 §4) — the remedy is to refuse new lobbies, never to pay. The decision
  logic (`overBudget`, `checkRequestBudget`, `monthStart`) is pure and unit-tested against stubbed
  analytics/alerter (6 Vitest cases, worker suite 39); the live Cloudflare GraphQL Analytics client is
  thin wiring that no-ops without a token (so the local/headless scheduled run is safe). `CF_API_TOKEN`
  is a deploy-time read-only Analytics secret.
  **The whole autonomous half of M3 is now done (T1–T9, T11).** The only thing left is the
  developer-gated **two-device playtest** (`docs/setup/m3-go-live.md` Step 4) — the M3 definition of done.

The intent seam and deterministic GameLogic combat from the local arc are what keep this
tractable. Still **not fully autonomous** — the deploy/secrets/devices remain the developer's.
