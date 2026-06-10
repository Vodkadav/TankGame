# ADR-0020: In-game map editor and a user-generated map format

**Date:** 2026-06-09
**Status:** Accepted (2026-06-09)
**Deciders:** Solo developer + Claude Code

Numbered 0020, the next free number after 0019.

## Context

The owner wants players to build their own battle arenas from inside the game: pick one of a few fixed
arena sizes (always pre-walled with a steel border), paint a ground/tile palette, place clutter and
obstacles (buildings, walls, fences, mountains, rivers, bridges), and place spawn points for tanks and
powerups — and eventually place ramps and raised roads for multilevel combat, with tanks able to drop
off a raised level to the one below.

Two facts about the current code shape the whole design:

1. **There is no serialization anywhere.** Maps exist only as C# — a hand-authored text literal
   (`Battlefield01.Text`, parsed by `LevelMap.Parse`) or procedural output (`ArenaGenerator` →
   `GeneratedArena`). The map data is split across two types: `LevelMap` holds materials / bushes /
   player spawn; `GeneratedArena` holds enemy spawns, pickup cells, and sandbags. An editor's first job
   is to invent a single serializable document and a load path the game has never had.

2. **The authoritative `World` is pure-C# and deterministic** (it is exactly what the host-authoritative
   netcode runs — ADR-0019). Therefore any "physics" for dropping off a raised level **must** be
   deterministic gameplay state integrated in `Tank.Step`, **not** Godot's RigidBody/physics engine.
   Godot only renders the height.

There is already a clean ingestion seam — `LevelMap.FromCells(materials, bushes, spawnX, spawnY)` →
`BuildGrid()` → `GridArena` — and the 3D renderer (`Terrain3DView`) is already a pure function of grid
data that reacts live to per-cell edits via the `CellChanged` event. So an editor needs no new renderer:
it paints into a grid and the existing terrain view shows real meshes immediately.

Genre precedent: Battle City / Tank 1990 (NES) shipped a near-identical construction mode (grid + tile
palette + base/spawn markers); Mario Maker / Trackmania contribute the validate-before-save gate, instant
test-play, and share codes; Halo Forge / Far Cry Arcade contribute WYSIWYG in-engine authoring; and the
RTS editors (WarCraft III, StarCraft, Age of Empires) contribute the height brush + auto-ramps model for
the elevation work.

## Decision

Build an **in-game, WYSIWYG map editor** that authors a new pure-C# **`MapDefinition`** value object,
serialized as **JSON**, persisted under Godot `user://maps/`, and loaded back through the existing match
pipeline. Ship it in two waves split along a real dependency line.

### The map document

A single serializable `MapDefinition` value object (Domain) carries everything a map needs — unifying
what is today scattered across `LevelMap` and `GeneratedArena`:

- dimensions (one of a few fixed sizes; steel border implicit via out-of-bounds)
- `CellMaterial[,]` materials grid
- `bool[,]` bushes (concealment) and `bool[,]` sandbags (speed) overlays
- player spawn, enemy spawns, powerup spawns (with `PowerupKind` per marker)
- **(Wave B)** `int[,]` per-cell layer + ramp cells

`MapCodec` (System.Text.Json) reads/writes it; `MapValidator` (pure) enforces the rules already implicit
in `LevelMap.Parse` / `ArenaGenerator`: rectangular, exactly one player spawn, every spawn on a Floor
cell, full reachability (flood-fill from the player spawn), water crossed by a bridge, and a minimum
open-floor fraction. `MapDefinition`, `MapCodec`, and `MapValidator` are pure C# → built TDD-first per
project rules. JSON is human-readable and diffable now and trivially turns into a **share string** later,
matching the 6-char lobby-code culture (share a custom arena before a networked match).

### The editor

- **WYSIWYG**: edit under a top-down orthographic camera over the real `Terrain3DView`, seeing actual
  meshes update live through `CellChanged`. One renderer, identical to the match look.
- Pick one of **3–4 fixed sizes** (e.g. Small 20×12, Medium 28×16 = today's default, Large 40×24, XL).
- A **paint brush** over the cell palette (Floor, Brick, Crate, Steel, Water, Bridge, Mountain, Building)
  plus **overlay toggles** (Bush, Sandbag) and **marker tools** (player spawn, enemy spawns, powerup
  spawns by kind). Buttons: Validate, Test-Play, Save.
- `MapSelectScene` gains a **"My Maps"** section listing `user://maps/*.json` plus a **"Create New Map"**
  entry that opens the editor. Loading deserializes `MapDefinition` → `LevelMap.FromCells` + spawn lists →
  the existing `Arena3DScene`. Procedural "Desert War" is unaffected; both feed the same `GridArena`.

### Multilevel + drop-off-ledge physics (Wave B)

Rides on ADR-0018 elevated zones (discrete integer layers). Each layer renders at world height
`Z = Layer × LayerHeight`. `Tank` gains a deterministic vertical sub-state integrated in `Step(dt)`:

- **Grounded** — `Z` snapped to the tank's current layer.
- Driving off a **raised edge** (into a cell whose layer is lower and is not a ramp) enters **Falling**:
  downward velocity under fixed-step gravity until the tank lands on the lower layer's `Z`, then it
  re-grounds at the lower layer.
- **Ramps are the only way up** (and a smooth way down); cliffs only drop. A move toward a higher layer
  with no ramp is blocked (treated as a wall).
- A falling tank keeps its source layer until it lands, so the existing `CombatResolver` layer filter
  (`tank.Layer == shot.Layer`) needs no special airborne case. Optional flavour: no-fire while airborne,
  a landing dust puff, minor fall damage.

This is a `Tank`/Domain/Presentation change and amends ADR-0018 (which today models layers as flat ints
with no continuous Z); it is captured here as Wave B intent and will get its own ADR amendment when built.

## Rollout (each step a squash-PR)

**Wave A — flat editor (no blockers; ships against existing seams):**

1. **Map format** — `MapDefinition` value object + `MapCodec` (JSON) + `MapValidator`, all pure C#,
   TDD-first. No UI. *(done — #167)*
2. **Load path** — deserialize a `MapDefinition` into `LevelMap.FromCells` + spawn lists and run it in
   `Arena3DScene`; a `user://maps/` repository (save/list/load). `MapSelectScene` "My Maps" + a bundled
   sample custom map prove the round trip. *(done — #168)*
3. **Editor scene** — WYSIWYG top-down-over-3D authoring: size picker, paint brush over the cell palette,
   bush/sandbag overlays, spawn/powerup markers, Validate, Test-Play, Save. "Create New Map" entry wired.
   *(done — #169)*

**Wave B — multilevel (after ADR-0018 step 2 makes `IArena` layer-aware + ramps exist):**

1. **(step 4) Drop-off-ledge physics** — `Tank` vertical sub-state + gravity + edge-fall + ramp-only-up;
   render at `Y = Z`; landing FX. *(done — see the ADR-0018 amendment)*
2. **(step 5) Elevation tools** — height brush + ramp tool in the editor; `MapDefinition` gains layers +
   ramps. *(done — format/validator/load path #185, editor tools follow-up PR; layer-aware reachability
   validates ramp-up/drop-down movement)*

## Alternatives considered

- **A separate flat 2D grid editor view** (edit coloured tiles, preview in 3D separately). Simpler, but
  not WYSIWYG and needs a second renderer to maintain. Rejected — the existing `Terrain3DView` already
  renders a grid live, so editing over it is both less code and a truer preview.
- **Godot `.tres` resources as the map format.** Engine-native, but couples user content to Godot's
  serializer, is not human-diffable, and resists a future plain-text share string. Rejected in favour of
  JSON owned by our pure layers.
- **Godot physics (RigidBody/CharacterBody) for the fall.** Rejected outright: the authoritative `World`
  is deterministic pure-C# for netcode parity; gameplay vertical motion must live in `Tank.Step`.

## Consequences

- **Positive:** gives the game its first serialization format (reusable for share codes and networked
  custom maps); reuses the existing renderer and `LevelMap.FromCells`/`GridArena` ingestion seam with no
  second game representation; Wave A ships immediately with zero dependency on the pending elevation work.
- **Trade-off:** Wave B's multilevel editing and drop-off physics depend on ADR-0018 step 2 (layer-aware
  `IArena` + ramps), the wide-ripple step (~13 inline test fakes) — so the headline multilevel feature is
  gated on that work landing first.
- **Scope note:** user-generated content is unmoderated and local-only at first; sharing/publishing is a
  later additive step, not part of this ADR.
