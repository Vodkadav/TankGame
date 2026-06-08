# ADR-0018: Elevated zones (multi-layer arena)

**Date:** 2026-06-08
**Status:** Accepted (2026-06-08) — elevation ships as a *separate themed map*, not by changing the
current arena (see Decision → Map pool).
**Deciders:** Solo developer + Claude Code

Numbered 0018, the next free number after 0017.

## Context

The arena is a single flat plane: every cell, tank, and shot shares one ground height. The owner
wants **elevated zones** — parts of the field that sit higher, so a tank up on a plateau is a
separate fighting space from the tanks below: they cannot shoot each other, and a tank only moves
between heights by driving up a **broad ramp**. This adds verticality and positional play (hold the
high ground, flank up a ramp) without changing the top-down, drive-and-shoot core.

The game plane is already 3D in the world (`(x, y)` → `(x, 0, y)`, ADR-0017), but **gameplay is still
2D**: Domain positions are `Vector2`, combat and collision ignore height entirely. Elevation is the
first gameplay concept that needs a third spatial axis. Because combat (`CombatResolver`), collision
(`IArena.IsBlocked` / `RaycastFirstHit`), and the arena model (`LevelMap` / `GridArena` /
`ArenaGenerator`) are all pure Domain/GameLogic, this is a Domain-and-GameLogic change first, with
Presentation rendering the heights — the opposite of the render-only 3D port.

## Decision

Model elevation as **discrete integer layers**, not a continuous height field.

Every cell sits on a layer (0 = ground, 1 = first plateau, …). Every entity carries the layer it
currently occupies. Combat and collision are filtered to act **only within a single layer**; a tank
changes layer **only** by crossing a ramp cell that connects two adjacent layers.

### Map pool (owner decision)

Elevation is delivered as a **new, separately themed map** — not by adding heights to the current
arena. The owner is growing the game toward a **pool of ~8–10 named, themed battle arenas**; the
existing procedural arena is kept as one of them, **"Desert War"** (flat), untouched by this work.
The layer feature is developed behaviour-preserving (a flat arena is the single-layer case), and once
ready it is applied to a new map — a **"Cliffs & Valleys"** theme designed around the elevation. This
needs a **map seam** (a way to select which arena to build) alongside the layer mechanics; the seam +
the first elevated map ship together, the remaining themed arenas are future content. See the
`map-pool-direction` memory.

### Domain

- **`IEntity.Layer`** — a new `int Layer { get; }` on the base entity contract: every world object
  (tank, projectile) is on exactly one layer. A projectile inherits the firing tank's layer and keeps
  it for its whole flight (a shot does not climb or fall — it stays on the shooter's level).
- **`IArena` becomes layer-aware.** `IsBlocked`, `RaycastFirstHit`, and `DamageAt` take the querying
  entity's `layer`, so walls, the playable boundary, and destructible cells are resolved per layer.
  The edge of a plateau is a wall to a tank *on* that plateau and simply empty space to a tank below.
- A **ramp** is a cell tagged as connecting layer *n* and *n+1*. A tank whose centre is over a ramp
  cell is allowed to occupy either connected layer; crossing the ramp is the only way `Layer` changes.

### GameLogic

- **`Tank`** tracks its current `Layer`, updated as it drives onto/off a ramp (when its centre leaves
  a ramp onto a plain cell, it commits to that cell's layer). Movement collision uses the layer-aware
  `IArena`, so a tank cannot drive off the side of a plateau — the drop edge blocks it; it must take
  the ramp.
- **`CombatResolver`** adds `tank.Layer == shot.Layer` to its hit test: a shot only damages tanks on
  its own layer. Everything else (team rules, pierce, kill credit) is unchanged.
- **`ArenaGenerator` / `LevelMap`** gain elevation: cells carry a base layer, the generator places one
  or more raised zones with broad ramp approaches, and validation guarantees every layer's floor is
  reachable from a spawn via ramps (extends the existing flood-fill reachability check).
- The **airstrike** and other area effects act on their target's layer (out of scope to fully
  specify here; the airstrike's swathe stays a planar footprint, applied to tanks on the targeted
  layer).

### Presentation

- Each cell and entity renders at `y = Layer × LayerHeight`; plateaus are raised ground blocks, ramps
  are sloped meshes connecting the two heights. The orthographic ¾ camera already reads height
  naturally. This is purely how the existing per-cell terrain meshes and entity nodes are seated — no
  new depth bookkeeping (the depth buffer handles occlusion, ADR-0017).

### Net

Out of scope for this ADR's first cut. The 3D netcode (PR5) is deferred; when it lands, the wire
`TankState` gains a layer byte and the server sim filters combat by layer the same way. Noted so the
protocol change is not a surprise later.

## Alternatives considered

- **Continuous per-cell height field (smooth slopes, arcing projectiles).** Rejected: it turns combat
  into 3D ballistics (projectiles gain a Z velocity, hit-testing becomes 3D, the net snapshot grows a
  height float per entity), a far larger change for a feel the owner did not ask for. Discrete layers
  give "separate fighting spaces joined by ramps" directly.
- **Elevation as a Presentation-only effect (visual hills, no gameplay).** Rejected: the owner's
  requirement is explicitly that tanks on different heights cannot fight and must use ramps — that is
  gameplay, so it must live in Domain/GameLogic to stay net-safe and testable.
- **A separate `IElevated` interface instead of `IEntity.Layer`.** Rejected: every entity that exists
  in space has a layer (projectiles included), so it belongs on the base contract; a side interface
  would force every collision/combat site to type-test and default a missing layer.

## Consequences

- **Positive:** real verticality and high-ground play; the discrete model keeps projectiles, the net
  snapshot, and the tick loop essentially intact (one int per entity); combat/collision changes are
  small, local, and unit-testable (TDD, no Godot).
- **Negative / cost:** the first gameplay change to touch the `IEntity`/`IArena` contracts since the
  entity spine — it ripples to every `IArena` implementation (`GridArena`, `RectArena`, the test
  fakes) and every `IsBlocked`/`RaycastFirstHit`/`DamageAt` call site, plus `ArenaGenerator`,
  `LevelMap`, and the terrain views. Sizeable, several PRs. The net protocol change is deferred but
  inevitable.
- **Unchanged:** team rules, pierce/bounce/missile ammo, powerups, game modes, the spawn→view loop,
  the HUD. A flat arena is just the single-layer (all layer 0, no ramps) case, so existing maps and
  the net `Battlefield01` keep working with no elevation.
- **Rollout** (each step a squash-PR, every step behaviour-preserving until the elevated map exists):
  1. **Layer on the fighters + combat filter** — `Layer` on `Tank` and `Projectile` (a shot inherits
     its shooter's layer), `CombatResolver` adds `tank.Layer == shot.Layer`. All entities default to
     layer 0, so Desert War and every current map/test are unaffected. *(done — first PR)*
  2. **Layer-aware arena + ramps** — lift `Layer` onto the `IEntity`/`ITank`/`IProjectile` contracts
     (so views can read height), make `IArena` (`IsBlocked`/`RaycastFirstHit`/`DamageAt`) layer-aware,
     and add ramp cells + per-cell layers to the arena model. This is where the wider `IArena`
     call-site/fake ripple lands — justified because the elevated map needs it.
  3. **The "Cliffs & Valleys" map + raised meshes + the map seam** — a new themed map that uses
     multiple layers and ramps, rendered at `y = Layer × LayerHeight`, reached through a map-selection
     seam that also offers "Desert War". The other themed arenas are later content.
