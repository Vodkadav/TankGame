# ADR-0004: Wall-grid data model and grid-arena collision

**Date:** 2026-06-03
**Status:** Accepted
**Deciders:** Solo developer + Claude Code (architect agent)

Records the M2 decisions (tickets M2-T1…T8): the destructible wall grid, how shots collide
with and damage it, and how the maze is authored. Number 0004 is the slot reserved for M2 in
`docs/research/development-plan.md`.

## Context

M2 replaces M1's empty `RectArena` box with a hand-authored labyrinth of brick walls (take
three hits, then break into floor) and steel walls (indestructible). This needs a data model
for the maze, a way for projectiles to collide with and damage it, a renderer, and a level to
load — without breaking the layer rules (ADR-0001), the interface-first convention
(ADR-0003), or the entity-spine tick model (ADR-0010), and while staying unit-testable with
no Godot in Domain/GameLogic.

Forces:

- **Collision must not be a mutating query.** A projectile asks "what do I hit?" every step.
  If that query also damaged walls, a future line-of-sight or AI-vision ray (roadmap S6)
  would chip walls just by looking. Querying and damaging must be separable.
- **The maze must be authorable and testable headlessly.** Godot's visual TileMap editor is
  not available in the CI/agent workflow, and a `.tscn` TileMap's packed cell data is not
  hand-editable or diff-friendly.
- **Coordinates are two different spaces.** The maze is naturally integer tiles; the
  simulation is continuous `System.Numerics.Vector2`. The model must not pick one and leak it
  into the other.

## Decision

### 1. `IWallGrid` (Domain) — a tile grid in pure tile space

`IWallGrid` exposes `Width`/`Height`, `GetCell(x,y)`, `IsBlocked(x,y)`, `DamageCell(x,y,amount)`,
and a `CellChanged` event. Cells are a `WallCell(CellMaterial Material, int Hp)` value object
over `CellMaterial { Floor, Brick, Steel }`. There are **no world coordinates** in the
contract — only tile indices. Out-of-bounds reads as blocking **steel**, so any grid is
implicitly enclosed and a ray can never escape it. `WallGrid` (GameLogic) backs this with a
2D array; `DamageCell` chips brick, breaks it to `Floor` at 0 hp (raising `CellChanged`), and
leaves steel and floor untouched. Brick starts at `WallGrid.DefaultBrickHp` (3).

### 2. `CellChanged` drives incremental rendering

`WallGrid` raises `CellChanged(x, y, newCell)` only when a cell's state actually changes.
`WallGridView` (Presentation, a `TileMapLayer`) subscribes once and re-tiles just that cell,
mapping `WallCell` → atlas frame (brick hp 3/2/1 → intact/cracked/rubble, steel → its frame,
floor → no tile). The view is a pure mirror, consistent with ADR-0010.

### 3. Collision lives in `GridArena`, which keeps `RaycastFirstHit` pure

`GridArena` (GameLogic) implements `IArena` over an `IWallGrid` plus a tile size and origin —
the **only** place the world↔tile mapping lives, keeping `IWallGrid` coordinate-free.
`RaycastFirstHit` walks the ray cell by cell (Amanatides–Woo DDA) to the first blocked cell
and returns the contact point/distance as a **pure query**. Impact damage is a separate
`IArena.DamageAt(point, direction, amount)`: the projectile carries a `damage` value and calls
it only when it dies on a hit; `GridArena` nudges half a tile along the ray to resolve the
struck cell and damages it. Open/boundary arenas (`RectArena`, test fakes) no-op `DamageAt`.

### 4. The maze is a text map, not a binary TileMap resource

`MazeDefinition.Parse` reads a text map (`#` steel, `x` brick, `.` floor, `@` spawn) into a
`WallGrid` + spawn cell, rejecting ragged rows, unknown characters, and anything other than
exactly one spawn. `Maze01` is the hand-authored 28×16 labyrinth embedded as a string. This is
hand-authored, diff-friendly, and unit-testable (size, enclosure, spawn validity, no orphan
wall tiles) — properties a packed `.tscn` TileMap could not be asserted on headlessly.

### 5. Placeholder art with baked damage frames

The wall art is a programmer-generated placeholder atlas (`Walls.png`, four 32×32 frames),
with brick damage **baked as atlas frames** rather than a runtime cracked-overlay shader —
simpler, and exactly what the view maps and the tests assert. To be replaced by Kenney CC0
later (see `docs/credits/assets.md`).

## Consequences

**Easier:**

- Destructible-wall content is cheap: a maze is text, a damage rule is `DamageCell`, and the
  view re-tiles itself off one event.
- Collision is deterministic and unit-tested with no Godot; the pure-query/explicit-damage
  split keeps future vision rays (S6) side-effect free.
- `IWallGrid` being coordinate-free is the seam S7 (dynamic terrain features — doors, smart
  walls) extends without touching world-space code.

**Harder / costs:**

- Authoring a maze in a text literal has no visual editor; large or organic layouts will get
  unwieldy and may later warrant a real import path.
- `IArena` gained a `DamageAt` method most implementations no-op — accepted as the price of
  keeping `RaycastFirstHit` pure.

**Forecloses / defers:**

- **Tank↔wall collision is not implemented.** M2's ticket scope is projectile↔wall only; the
  tank drives freely over walls. Blocking the tank is a small follow-up over
  `IWallGrid.IsBlocked` and is the most likely next enhancement.
- No procedural generation, no per-material variable damage, no multi-hit steel — all
  deliberately out of M2 scope.

**Revisit triggers:**

- A maze that outgrows hand-authored text → add a real level-import path (and revisit whether
  `MazeDefinition` consumes a resource instead of a string).
- S7 dynamic terrain → `IWallGrid` likely grows cell behaviours beyond material+hp.
- Tank↔wall collision → decide where movement resolution lives (the tank, the arena, or a
  physics pass) and whether it shares `GridArena`'s traversal.
