# PROPOSAL — S1 Entity Spine (world owns and steps many entities)

**Filename:** `docs/adr/PROPOSAL-entity-spine.md`

**Date:** 2026-06-02
**Status:** Proposed (un-numbered; ADR-0001..0009 reserved by `docs/research/development-plan.md` for M0..M7; promotes to the next free ADR number at S1 execution)
**Deciders:** Solo developer + Claude Code (architect agent)

## Context

The roadmap (`docs/research/feature-roadmap.md` §3, S1) names the entity/world spine as "the single highest-leverage refactor" and the prerequisite for every droppable, drone, turret, grenade, mine, and bouncing shell. This proposal formalizes **S1 only** — the world container, the entity contract, and spawn/despawn events. It deliberately does not pull in S2–S7 (weapon behaviour, health/damage, stats, event bus, vision, terrain), only noting where their seams attach.

The pain is concrete and lives in `client/src/Presentation/Arena/ArenaScene.cs`. That one file is simultaneously the Godot play scene **and** the gameplay composition root, and it hand-wires every live object:

- `_Ready` hand-instantiates the single `Tank`, the `RectArena`, and one `TankView`, then `Bind`s them.
- `_Process` owns a `_fireCooldown` field and the fire-rate gameplay rule (`FireInterval = 0.3`), and on each fire `FireProjectile()` hand-instantiates a `Projectile` plus a `ProjectileView` and `AddChild`s it into the scene tree.

So the only way a new live object can appear on screen today is to add another hand-instantiation branch to `ArenaScene`. The roadmap's own rule of thumb (§1) is that any feature touching `ArenaScene.cs` signals a missing abstraction. There is no collection that owns "all the things," no place that advances them uniformly, and no event a renderer can subscribe to in order to learn "a new entity exists, draw it."

A second structural smell: the **view currently owns the tick**. `TankView._Process` and `ProjectileView.Advance` each call `model.Step(dt)` themselves (`TankView.cs:35`, `ProjectileView.cs:29`) and then mirror the model onto the node. Simulation advancement is therefore entangled with Godot frame callbacks and scattered across N view classes. For a single tank and ad-hoc projectiles this is survivable; for a world of dozens of entities it means stepping order is non-deterministic, there is no single place to reap the dead, and the future server sim (M3+) has no client-side counterpart whose stepping it could mirror.

Forces:

- **Layer rules are non-negotiable** (`docs/adr/0001-layered-architecture.md`, enforced by `client/tests/Architecture/LayerRulesTests.cs` via NetArchTest). Domain depends on nothing (not even Godot); GameLogic depends only on Domain; Presentation is the composition root and may not touch `Data`. Every production type must reside in one of the five known namespaces (`EveryProductionType_LivesInOneOfTheFiveKnownNamespaces`).
- **Interface-first** (`docs/adr/0003-interface-first-services.md`): the contract is an `IServiceName` in `TankGame.Domain`, the implementation lives in the owning layer, dependencies are constructor-injected, and tests use hand-written fakes — no mocking framework.
- **The future-spawner problem.** A future entity (a weapon, a cluster shell, a turret) needs to spawn *other* entities during its own `Step`. The naive fix — give the entity a reference to the world — risks either a Domain→GameLogic dependency (if `World` is the concrete type the entity holds) or a construction-time cycle (the world holds the entity, the entity holds the world). The design must give a spawner a handle to the world **without** breaking the layer rules and **without** building S2's weapon system now.
- **Client/server parity is unsettled** (`feature-roadmap.md` §4; the M3 authoritative TS `MatchSim`). S1 must not quietly become the authoritative simulation design. Today the world is a *client-prediction container*; the server sim is a separate, later, language-neutral concern.
- **Existing tests must stay green.** `TankTests`, `ProjectileTests` (pure xUnit against `ITank`/`IProjectile`), `ProjectileViewTests`, `TankViewTests`, and `ArenaSceneTests` (GoDotTest scene tests asserting the scene wires a `TankView` + `Camera2D` and draws walls) all encode current behaviour the refactor must preserve.

## Decision

We will introduce a minimal **entity spine**: an `IEntity` contract and an `IWorld` contract in `TankGame.Domain`, a `World` implementation in `TankGame.GameLogic`, and a spawn-event subscription in `ArenaScene` that maps entities to Godot views. We will move the tick ownership from the views into the world.

### 1. `IEntity` (Domain) — extend, do not compose

`ITank` and `IProjectile` already expose exactly the entity surface (`Position`, `Step`; `IProjectile` already has `IsAlive`). We will **make `ITank` and `IProjectile` extend `IEntity`**, rather than compose an entity inside them.

Rationale: composition (`IEntity Entity { get; }` on a tank) would force every caller and every view to reach through a property to get position, duplicate `Position`/`Step` as forwarding members, and buy nothing — a tank *is-a* world entity, not a thing that *has-an* entity. Extension keeps the existing `ITank.Position`/`Step` signatures byte-identical, so `TankTests`/`ProjectileTests` and the views compile unchanged. The only genuinely new members are `Id` (identity for the view lookup and despawn) and, for `ITank`, `IsAlive` (a tank has none today; default it to always-alive until S3 introduces health — see Consequences).

```csharp
// client/src/Domain/IEntity.cs
namespace TankGame.Domain;

using System;
using System.Numerics;

/// <summary>The minimal contract every world object satisfies. The world owns a
/// collection of these, advances them, and reaps the dead. Pure C# — no Godot.</summary>
public interface IEntity
{
    Guid Id { get; }
    Vector2 Position { get; }
    bool IsAlive { get; }
    void Step(float deltaSeconds);
}

// ITank now:       public interface ITank : IEntity { float Rotation {get;} float TurretRotation {get;} }
//   (Position/Step already present; add Guid Id and bool IsAlive)
// IProjectile now: public interface IProjectile : IEntity { }
//   (Position/IsAlive/Step already present; add Guid Id — interface body becomes empty)
```

`Id` is a `Guid` assigned by the implementation in its constructor (`Guid.NewGuid()`), matching the roadmap sketch. We keep `System.Numerics.Vector2` — no new value types, no Godot.

### 2. `IWorld` (Domain) + `World` (GameLogic)

```csharp
// client/src/Domain/IWorld.cs
namespace TankGame.Domain;

using System;
using System.Collections.Generic;

/// <summary>Owns a collection of entities, advances them each tick, and reaps the
/// dead. Spawns and deaths are events the Presentation layer subscribes to once.</summary>
public interface IWorld
{
    IReadOnlyCollection<IEntity> Entities { get; }

    /// <summary>Adds an entity and raises <see cref="EntitySpawned"/> synchronously.</summary>
    void Spawn(IEntity entity);

    event Action<IEntity> EntitySpawned;
    event Action<IEntity> EntityDespawned;

    /// <summary>Advances every live entity by <paramref name="deltaSeconds"/>, then
    /// removes any whose <see cref="IEntity.IsAlive"/> went false, raising
    /// <see cref="EntityDespawned"/> for each. Single tick owner for the sim.</summary>
    void Step(float deltaSeconds);
}
```

`World` (in `TankGame.GameLogic`) holds a `List<IEntity>`, exposes a read-only view, and on `Step`: snapshots the live set, calls `Step(dt)` on each, then partitions into survivors and dead, raising `EntityDespawned` for each reaped entity. Iterating a snapshot (not the live list) is what makes mid-step spawning safe (see below). Stepping order is insertion order — deterministic, which matters for the eventual server-parity story.

### 3. The future-spawner handle — inject the world at construction, not into `Step`

Decision: **a spawner receives `IWorld` through its constructor; `IEntity.Step(float dt)` keeps its single-argument signature.**

```csharp
// Illustrative, S2 — NOT built now. Shows the seam only.
public sealed class Turret : IEntity
{
    private readonly IWorld _world;            // injected once, at construction
    public Turret(IWorld world, Vector2 pos) { _world = world; Position = pos; }
    public void Step(float dt) { /* … _world.Spawn(new Projectile(...)); … */ }
}
```

Trade-off considered — **pass `IWorld` into `Step(dt, IWorld)`** vs **inject at construction**:

- *Pass-into-Step* keeps entities stateless about the world and makes the data flow explicit at the call site, but it (a) widens `IEntity.Step`, which today is `Step(float)` and is called identically by `TankView`/`ProjectileView` — every view and every existing test would have to thread a world argument through, and (b) forces even entities that never spawn (the tank, a straight projectile) to accept a world they ignore.
- *Inject-at-construction* matches the project's established convention exactly: `Tank(IInputSource, …)` and `Projectile(IArena, …)` already take their collaborators through the constructor (ADR-0003). A spawner takes `IWorld` the same way. It is constructed by whoever already holds the world (the composition root, or a future spawner factory), so there is no cycle: the world does not construct its entities; the composition root constructs an entity *with* the world, then calls `world.Spawn(entity)`.

There is **no Domain→GameLogic dependency** either way, because the entity depends on the `IWorld` *interface* (Domain), never on the concrete `World` (GameLogic) — identical to how `Projectile` depends on `IArena`, not `RectArena`. Recommendation: **inject at construction.** It is the smaller diff, the established pattern, and it keeps `Step` boring. Entities that do not spawn simply do not take an `IWorld`.

For S1 itself, nothing spawns mid-step except the tank firing (see §5) — so this is largely a documented seam, exercised minimally now and load-bearing for S2.

### 4. Presentation: `ArenaScene` shrinks to "build, subscribe, mirror"

`ArenaScene` becomes: build the `IArena`, the input source, and a `World`; spawn the initial `Tank` into the world; subscribe **once** to `world.EntitySpawned`/`EntityDespawned`; and in `_Process` call `world.Step(dt)` exactly once, then let each view mirror its model. The fire/cooldown rule leaves the scene entirely (see §5).

The entity→view mapping must not leak Godot into Domain/GameLogic. We keep it boring with a **type-switch view factory in Presentation** plus a **`Dictionary<Guid, Node2D>` registry** owned by the scene:

```csharp
// Presentation only — Godot lives here, never below.
private readonly Dictionary<Guid, Node2D> _views = new();

private void OnSpawned(IEntity e)
{
    Node2D view = e switch
    {
        ITank t       => Instance<TankView>("res://.../TankView.tscn",       v => v.Bind(t)),
        IProjectile p => Instance<ProjectileView>("res://.../ProjectileView.tscn", v => v.Bind(p)),
        _ => throw new NotSupportedException($"No view registered for {e.GetType().Name}")
    };
    _views[e.Id] = view;
    AddChild(view);
}

private void OnDespawned(IEntity e)
{
    if (_views.Remove(e.Id, out var view)) view.QueueFree();
}
```

The `switch` on Domain interfaces (`ITank`/`IProjectile`) is the lookup mechanism — Presentation already references those interfaces (the views do today). Domain and GameLogic never learn that views exist; they only raise `Action<IEntity>`. This is the same "scene subscribes once → map to a view" shape the roadmap (§3, S1) and the fog-of-war proposal's renderer-reacts-to-server pattern both assume.

**Tick ownership moves out of the views.** Because `world.Step(dt)` now advances the models, `TankView`/`ProjectileView` must stop calling `model.Step(dt)` themselves and become pure mirrors (read `Position`/`Rotation`, write the node; `QueueFree` is replaced by the `EntityDespawned` path). This is the one behavioural change the view tests will feel (see §6).

### 5. Migration of the fire/cooldown rule

The `_fireCooldown` field, `FireInterval`, and `FireProjectile()` currently in `ArenaScene._Process` are a **gameplay rule sitting in the composition root** — exactly the smell S1 removes. They move into GameLogic. The minimal landing for S1: the **`Tank` becomes the spawner**. `Tank` gains an injected `IWorld` and an `IArena`. On a `Step` where `input.Fire` is true and an internal cooldown has elapsed, `Tank.Step` calls `_world.Spawn(new Projectile(...))`. The cooldown becomes tank state (`_fireCooldown`), advanced by the same `dt`. (Keeping `Tank` from knowing `Projectile` concretely — an `IProjectileFactory` seam — is deferred to S2; for S1, `Tank` constructing a `Projectile` is acceptable and unit-tested.)

This is the first real exercise of the construction-time `IWorld` injection from §3, and it deletes `_Process`'s gameplay branch and `FireProjectile()` from `ArenaScene` entirely. The scene's `_Process` collapses to `_world.Step((float)delta)`.

### 6. Regression tests that must stay green

- `TankTests`, `ProjectileTests` (xUnit, pure): `Position`/`Step`/`IsAlive` semantics are unchanged by extending `IEntity`. `TankTests` gains coverage for the migrated fire rule (firing spawns into a fake `IWorld`; cooldown gates the rate) — the fire-rate assertion moves here from being untested scene logic. Existing assertions stay green; `Tank`/`Projectile` constructors gain parameters, so those tests' construction lines update (mechanical).
- `ProjectileViewTests`, `TankViewTests`: today they call the view's `Advance`/`UpdateFromModel` and assert the node mirrors a stepped model. After tick ownership moves to the world, the views no longer step — these tests change to: step the *model* (or the world), then assert the view mirrors it. This is the load-bearing test change; it is expected and is part of S1-T5/T3/T4, not a regression.
- `ArenaSceneTests` (GoDotTest): still asserts the scene instances a `TankView` with a `Camera2D` child and draws walls. The initial tank is now spawned via `world.Spawn` → `EntitySpawned` → view, so the `TankView` still appears as a child — assertion holds. Walls are unchanged.

### 7. NetArchTest compliance

| Type | Namespace / Layer | Rule satisfied |
|---|---|---|
| `IEntity` | `TankGame.Domain` | Domain depends on nothing; uses only `System`/`System.Numerics`. |
| `IWorld` | `TankGame.Domain` | Same. `Action<IEntity>` and `IReadOnlyCollection<IEntity>` are BCL + Domain. |
| `World` | `TankGame.GameLogic` | Depends only on Domain (`IEntity`, `IWorld`). No Godot. |

No rule breaks. `ITank`/`IProjectile` extending `IEntity` stays within Domain. The entity→view `switch` and the `Dictionary<Guid, Node2D>` live in `ArenaScene` (Presentation) — Godot stays in Presentation, satisfying `Presentation_DoesNotDependOnData` and the Domain/GameLogic "no Godot" rules. Every new type lands in a known namespace, satisfying `EveryProductionType_LivesInOneOfTheFiveKnownNamespaces`. No new dependency crosses a layer boundary in the wrong direction, so no boundary-exception ADR is required.

### 8. Client/server parity note

S1 is **client-side structure only** and must not be read as the authoritative-sim design. The `World` introduced here is a **client-prediction container**: it owns the entities the local client renders and predicts between server snapshots. From M3 on, the authoritative simulation is a separate TypeScript `MatchSim` inside a Cloudflare Durable Object (`feature-roadmap.md` §4), and the parity question — one language-neutral set of definitions + a shared test-vector suite vs two hand-coded sims — is explicitly deferred to its own ADR before M5 content. S1 therefore keeps `IWorld`/`IEntity` deliberately thin (spawn, step, reap, events) and free of any networking, reconciliation, or snapshot concept, so that when the server design lands it can mirror or diverge from this client container without S1 having pre-committed the wire model. The client `World` is what reconciliation will *correct*; it is not the source of truth.

## Consequences

**Easier:**

- New live objects (mines, drones, turrets, extra projectiles) appear on screen with **zero `ArenaScene` edits** — construct the entity, `world.Spawn(it)`, and add one `switch` arm in the view factory. This is the roadmap's core S1 promise delivered.
- One deterministic, insertion-ordered tick (`world.Step`) replaces N view-owned `Step` calls; there is a single place to reap the dead and (later, S3) run a combat pass.
- The fire-rate rule becomes unit-testable in `TankTests` against a fake `IWorld`, instead of living untested in a Godot `_Process`.
- Views become pure mirrors with no game rules — cleaner separation, and the view tests get simpler (mirror-only).

**Harder / costs:**

- A one-time inversion of tick ownership touches `TankView`/`ProjectileView` and their tests; this is the riskiest part of the migration and is isolated into its own tickets.
- `ITank` gains `Id` and `IsAlive` it did not need; `IsAlive` is a placeholder (always true) until S3 gives a tank real health. Documented as an intentional stub, not dead code.
- One more indirection: spawning is now an event hop (`Spawn` → `EntitySpawned` → factory → `Bind`) rather than a direct `AddChild`. Accepted — it is exactly the decoupling we want.

**Forecloses / deliberately defers:**

- No DI container is introduced; the composition root (`ArenaScene`) still hand-wires the world and initial entities. If the spawner graph grows past hand-wiring, **introducing a DI container** is the revisit trigger (consistent with the ADR-0001/0003 composition-root note).
- S1 does **not** build S2's `IProjectileBehaviour`, S3's `IDamageable`/combat pass, or the S5 event bus. The seams are named (the world's reap step is where S3's combat pass slots in; construction-time `IWorld` injection is how S2 spawners will work) but unbuilt.

**Revisit triggers:**

- The **client/server parity ADR** (before M5 content) — if it mandates a shared definition format, `World` may need to consume those definitions; S1's thinness is what keeps that cheap.
- Introducing a **DI container** (spawner graph outgrows hand-wiring).
- **S3 health/damage** — when a tank gets real HP, `ITank.IsAlive` stops being a stub and the world's reap step grows a combat pass; revisit whether reap and combat are one pass or two.
- Any future entity that must spawn things and cannot get `IWorld` at construction time (e.g. a deserialized entity) — revisit the §3 inject-vs-pass decision for that case only.

## TDD ticket breakdown

Interface-first (ADR-0003): the architect defines the Domain contracts first; **S1-T1 blocks everything else.** Each ticket is test-first (Red → Green → Refactor).

| ID | Description (test-first) | Owner | Size | Depends |
|---|---|---|---|---|
| **S1-T1** | Define `IEntity` + `IWorld` in `TankGame.Domain`; make `ITank`/`IProjectile` extend `IEntity`. Write **contract tests** (`EntityContractTests`, `WorldContractTests`) against the interfaces using hand-written fakes (a `StubEntity` whose `IsAlive` is scriptable) — assert `Spawn` raises `EntitySpawned`, `Entities` exposes spawned, the contracts compile against fakes. No impl yet. | @architect | S | — |
| **S1-T2** | Implement `World` in `TankGame.GameLogic`. Unit tests: `Spawn` adds + raises `EntitySpawned`; `Step` advances every entity by `dt`; a dead entity (`IsAlive=false`) is removed after `Step` and raises `EntityDespawned`; stepping is insertion-ordered; spawning during a step (via a `StubEntity` that spawns in `Step`) does not corrupt iteration. | @systems | M | S1-T1 |
| **S1-T3** | Migrate `Tank` to `IEntity`: assign `Guid Id`, expose `IsAlive` (always true for now). Move the fire/cooldown rule in — inject `IWorld` (+ `IArena`); `Step` spawns a `Projectile` into the world when `Fire` and cooldown elapsed. Tests: existing `TankTests` stay green; new tests assert firing spawns into a fake `IWorld` and the cooldown gates the rate. | @mechanics | M | S1-T2 |
| **S1-T4** | Migrate `Projectile` to `IEntity`: assign `Guid Id` (`IsAlive`/`Position`/`Step` already satisfy the contract; `IProjectile` body becomes empty). Tests: existing `ProjectileTests` stay green; add an `Id`-stability assertion. | @mechanics | XS | S1-T2 |
| **S1-T5** | Refactor `TankView`/`ProjectileView` to pure mirrors (stop calling `model.Step`; mirror only). Refactor `ArenaScene` to build the `World` + input, spawn the initial tank, subscribe once to `EntitySpawned`/`EntityDespawned`, map entity→view via type-switch + `Dictionary<Guid,Node2D>`, and call `world.Step` once per `_Process`. GoDotTest: `ArenaSceneTests` (TankView + Camera2D + walls) stays green; updated `TankViewTests`/`ProjectileViewTests` assert mirror-only behaviour; add a scene test that firing spawns a `ProjectileView` via the event path and despawn frees it. | @systems | L | S1-T3, S1-T4 |
| **S1-T6** | Promote this proposal to the next free numbered ADR; confirm full NetArchTest suite green (all five layer rules + the namespace-coverage guard); cross-link from `feature-roadmap.md` §7 (S1 row) and update `PROGRESS.md`. | @architect | XS | S1-T5 |

---

_This proposal is aspirational until S1 is scheduled into a numbered milestone in `development-plan.md` and tracked in `PROGRESS.md`. It refines `feature-roadmap.md` §3 (S1) and §7._
