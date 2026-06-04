# ADR-0013: Weapon / projectile-behaviour strategy + raycast normals (S2)

**Date:** 2026-06-04
**Status:** Accepted
**Deciders:** Solo developer + Claude Code (architect agent)

The S2 system foundation from `docs/research/feature-roadmap.md`. Numbered 0013 (0005–0009 are
reserved by name for M3–M7, 0010 entity spine, 0011 combat arc, 0012 stats). Built as
local-backlog item #13; the visible ammo variety (#14: bouncing / piercing / spread) lands on
top of this as new behaviours, not new code paths.

## Context

Every shot was a `Projectile` whose `Step` hard-coded one motion: travel straight, raycast, die
on hit. The roadmap's ammo catalogue — bouncing, zig-zag, piercing, homing, grenade, cluster — is
a dozen *different motions and impact responses* over the *same* shot data. Threading each as a
new branch in `Projectile.Step` (the exact anti-pattern the roadmap's rule-of-thumb names) does
not scale and is not server-runnable cleanly.

A ricochet also needs something the arena did not return: the **surface normal** of the face a
shot hits, to reflect the velocity. Both arenas (`GridArena` DDA, `RectArena` slab) compute which
face was crossed but discarded it.

## Decision

### 1. Split shot *data* from shot *behaviour* (Strategy)

```csharp
public sealed class ProjectileState { Vector2 Position, Direction; float Speed; int Damage, Team; bool IsAlive; }
public interface IProjectileBehaviour { void Step(ProjectileState s, IArena arena, float dt); }
```

`Projectile` becomes a thin `IEntity` holding a `ProjectileState` and an `IProjectileBehaviour`;
its `Step` just delegates. `StraightBehaviour` is the prior logic verbatim (stateless, one shared
`Instance`). A new ammo type is a new `IProjectileBehaviour` **only when the motion is genuinely
new** — bouncing reflects on the hit normal and decrements a bounce counter it owns; piercing
keeps going through walls up to a limit; spread is *not* a behaviour at all but N straight shots
fanned at the muzzle.

### 2. The behaviour instance carries per-shot counters

Bounce/pierce counts live on the behaviour instance (each shot gets its own
`new BouncingBehaviour(3)`), not on `ProjectileState`. This keeps the shared state minimal and
avoids a `WeaponDef` god-record before the catalogue actually needs one. `IWorld` is **not** in
the `Step` signature yet — no current behaviour spawns children; cluster/homing extend the
signature when they arrive, rather than carrying an unused parameter now.

### 3. Surface normals on `RaycastHit` (precursor, merged separately)

`RaycastHit` gained `Vector2 Normal` — the unit normal of the struck face, pointing back along
the incoming ray, so a reflection is `dir - 2·(dir·n)·n`. `GridArena` derives it from the DDA
axis it stepped across; `RectArena` from the exited wall. Shipped ahead of this ADR as PR #76 so
the contract change landed on its own.

### 4. Construction stays backward-compatible

`Projectile`'s new `behaviour` parameter is optional and defaults to `StraightBehaviour.Instance`,
so `Tank` and every existing call site are untouched — the whole refactor is behaviour-preserving,
proven by the unchanged `ProjectileTests`/`ProjectileContractTests`.

`IProjectileBehaviour`/`ProjectileState`/`StraightBehaviour` live in GameLogic alongside the other
pure systems; no Domain contract is added (nothing across a layer boundary depends on them — the
Presentation `ProjectileView` still mirrors only `IProjectile.Position`).

## Consequences

- **Positive:** ammo becomes data + a tiny behaviour class. Bouncing/piercing/spell-out-spread
  land as #14 without editing `Projectile`, `ArenaScene`, or the combat pass. The normal is
  available for reflection. The refactor changed no existing behaviour.
- **Negative / cost:** one extra indirection per shot (a virtual `Step` call) and a small state
  object per projectile — negligible at the shot counts in play. `ProjectileState` exposes public
  mutable fields (a deliberate state-bag) rather than encapsulated properties; acceptable for an
  internal GameLogic type driven only by behaviours.
- **Open / deferred:** how a *tank* acquires a non-straight shell (a weapon pickup à la S4, a
  weapon slot, or a match modifier) is **not** decided here — this ADR only makes alternate ammo
  *expressible*. `IWorld` joins the behaviour signature when the first child-spawning behaviour
  (cluster) is built. A data-driven `WeaponDef` table arrives if/when the catalogue outgrows
  hand-constructed behaviours (and ties into the §4 client/server parity ADR).
