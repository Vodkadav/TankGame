# ADR-0010: Entity spine — a world that owns, steps, and reaps entities

**Date:** 2026-06-03
**Status:** Accepted
**Deciders:** Solo developer + Claude Code (architect agent)

Promoted from `docs/adr/PROPOSAL-entity-spine.md` after the S1 slice (tickets S1-T1…T6)
landed. Numbered 0010 because ADR-0004…0009 are reserved by name for M2…M7 in
`docs/research/development-plan.md`; 0010 is the next free number beyond that block.

## Context

The roadmap (`docs/research/feature-roadmap.md` §3, system **S1**) names the entity/world
spine as "the single highest-leverage refactor" and the prerequisite for every droppable,
drone, turret, grenade, mine, and bouncing shell. The pain was concrete and lived in
`client/src/Presentation/Arena/ArenaScene.cs`: that one file was simultaneously the Godot
play scene **and** the gameplay composition root. It hand-instantiated the single `Tank`,
the `RectArena`, and the `TankView` in `_Ready`, and owned the fire-rate rule plus the
hand-instantiation of every `Projectile`/`ProjectileView` in `_Process`. The only way a new
live object could reach the screen was to add another hand-wiring branch — the roadmap's own
smell test (§1) flags any feature touching `ArenaScene.cs` as a missing abstraction.

A second smell: the **view owned the tick**. `TankView` and `ProjectileView` each called
`model.Step(dt)` themselves, so simulation advancement was entangled with Godot frame
callbacks and scattered across N view classes — no deterministic step order, no single place
to reap the dead, and no client-side sim counterpart for the future authoritative server.

Forces: the five-layer rules are non-negotiable (ADR-0001, enforced by NetArchTest); the
interface-first convention (ADR-0003) puts every contract as an `IServiceName` in Domain with
constructor-injected collaborators and hand-written fakes; a future entity must be able to
spawn *other* entities without creating a Domain→GameLogic dependency or a construction
cycle; and client/server parity is unsettled (the M3 authoritative TS `MatchSim`), so S1 must
not quietly become the authoritative-sim design.

## Decision

We will introduce a minimal **entity spine**: an `IEntity` and an `IWorld` contract in
`TankGame.Domain`, a `World` implementation in `TankGame.GameLogic`, and a spawn/despawn
event subscription in `ArenaScene` that maps entities to Godot views. Tick ownership moves
from the views into the world.

1. **`IEntity` (Domain) — extend, do not compose.** `ITank` and `IProjectile` **extend**
   `IEntity` (`Guid Id`, `Vector2 Position`, `bool IsAlive`, `void Step(float)`). A tank
   *is-a* world entity; composition would force forwarding members and buy nothing. `ITank`
   adds only `Rotation`/`TurretRotation`; `IProjectile` adds nothing (its body is empty).
   `Id` is a `Guid` assigned by the implementation at construction. We keep
   `System.Numerics.Vector2` — no Godot, no new value types.

2. **`IWorld` (Domain) + `World` (GameLogic).** The world owns a `List<IEntity>`, exposes a
   read-only insertion-ordered view, raises `EntitySpawned` synchronously on `Spawn`, and on
   `Step` advances every entity then reaps any whose `IsAlive` is false, raising
   `EntityDespawned` for each. `Step` **snapshots the live set before advancing**, so an
   entity that spawns a child mid-step does not corrupt iteration; the child joins the world
   immediately but is first stepped on the next tick. Stepping is insertion-ordered
   (deterministic — it matters for the eventual server-parity story).

3. **The future-spawner handle — inject `IWorld` at construction, not into `Step`.** A
   spawner receives `IWorld` through its constructor; `IEntity.Step(float)` keeps its
   single argument. This matches the project's established convention (`Tank(IInputSource,…)`,
   `Projectile(IArena,…)` take collaborators via the constructor, ADR-0003) and keeps `Step`
   boring for entities that never spawn. There is no Domain→GameLogic dependency because the
   entity depends on the `IWorld` *interface* (Domain), never the concrete `World`.

4. **`ArenaScene` shrinks to "build, subscribe, mirror".** It builds the `IArena`, the input
   source, and the `World`; spawns the initial `Tank` into the world; subscribes **once** to
   `EntitySpawned`/`EntityDespawned`; maps entity→view with a **type-switch** over the Domain
   interfaces (`ITank` → `TankView` + a following `Camera2D`; `IProjectile` →
   `ProjectileView`) backed by a `Dictionary<Guid, Node2D>` registry; and calls `world.Step`
   exactly once per `_Process`. Godot stays in Presentation — Domain/GameLogic only ever raise
   `Action<IEntity>`. Drawing a new kind of entity needs only a new switch arm.

5. **The fire/cooldown rule moves into `Tank`.** The `_fireCooldown`/`FireInterval`/
   `FireProjectile()` that lived in `ArenaScene._Process` were a gameplay rule in the
   composition root. `Tank` gains an injected `IWorld` + `IArena`; on a `Step` where `Fire`
   is held and the cooldown has elapsed, `Tank.Step` spawns a `Projectile` into the world.
   The cooldown becomes tank state. (An `IProjectileFactory` seam to keep `Tank` from knowing
   `Projectile` concretely is deferred to S2.)

6. **Tick ownership moves out of the views.** Because `world.Step` advances the models,
   `TankView`/`ProjectileView` stop calling `model.Step` and become pure mirrors (read
   position/rotation, write the node); despawn is the `EntityDespawned` path, not a
   view-initiated `QueueFree`.

### As built — two forced deviations from the proposal's ticket order

- **S1-T4 ran before S1-T3.** `Tank.Step` spawns a `Projectile` into `IWorld`, which requires
  `Projectile` to be an `IEntity` first. The proposal listed T3→T4, but the spawn dependency
  runs the other way, so `Projectile`'s migration shipped first.
- **`ProjectileView`-as-mirror was pulled forward from S1-T5 into S1-T3.** Once `Tank` spawns
  projectiles into the world, the world must own the projectile tick so it can reap the dead;
  otherwise dead projectiles accumulate in `world.Entities` (reaped only on `Step`) — a leak,
  since stepping them via the world while a view also steps them would double-step. The
  riskier half of the inversion (moving the **tank** into the world and making `TankView` a
  mirror, which touches the playable feel and `ArenaSceneTests`) stayed isolated in S1-T5.

### Client/server parity

S1 is **client-side structure only**. `World` is a *client-prediction container* — what
reconciliation will later correct, not the source of truth. The M3+ authoritative simulation
is a separate TypeScript `MatchSim` in a Cloudflare Durable Object; the parity question (one
language-neutral definition set + shared test vectors vs two hand-coded sims) is deferred to
its own ADR before M5 content. `IWorld`/`IEntity` are kept deliberately thin (spawn, step,
reap, events) and free of any networking, reconciliation, or snapshot concept.

## Consequences

**Easier:**

- New live objects (mines, drones, turrets, extra projectiles) appear on screen with **zero
  `ArenaScene` edits** beyond one switch arm — construct the entity, `world.Spawn(it)`. The
  roadmap's core S1 promise, delivered.
- One deterministic, insertion-ordered tick (`world.Step`) replaces N view-owned `Step`
  calls; there is a single place to reap the dead and (later, S3) run a combat pass.
- The fire-rate rule is unit-testable in `TankTests` against the world, instead of living
  untested in a Godot `_Process`. Views became pure mirrors with no game rules.

**Harder / costs:**

- A one-time inversion of tick ownership touched both views and their tests.
- `ITank` gained `Id` and `IsAlive` it did not need; `IsAlive` is a placeholder (always true)
  until S3 gives a tank real health — an intentional stub, not dead code.
- Spawning is now an event hop (`Spawn` → `EntitySpawned` → factory → `Bind`) rather than a
  direct `AddChild`. Accepted — it is exactly the decoupling we want.

**Forecloses / defers:**

- No DI container is introduced; `ArenaScene` still hand-wires the world and the initial
  entity. If the spawner graph outgrows hand-wiring, **introducing a DI container** is the
  revisit trigger.
- S1 does **not** build S2's `IProjectileBehaviour`, S3's `IDamageable`/combat pass, or the
  S5 event bus. The seams are named (the world's reap step is where S3's combat pass slots in;
  construction-time `IWorld` injection is how S2 spawners will work) but unbuilt.

**Revisit triggers:**

- The **client/server parity ADR** (before M5 content) — if it mandates a shared definition
  format, `World` may need to consume those definitions; S1's thinness keeps that cheap.
- Introducing a **DI container** (spawner graph outgrows hand-wiring).
- **S3 health/damage** — when a tank gets real HP, `ITank.IsAlive` stops being a stub and the
  world's reap step grows a combat pass; revisit whether reap and combat are one pass or two.
- Any future entity that must spawn things and cannot get `IWorld` at construction time (e.g.
  a deserialized entity) — revisit the §3 inject-vs-pass decision for that case only.
