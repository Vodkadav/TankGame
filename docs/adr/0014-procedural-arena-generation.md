# ADR-0014: Procedural arena generation (S8)

**Date:** 2026-06-04
**Status:** Accepted
**Deciders:** Solo developer + Claude Code (architect agent)

The first slice of the S8 "arena generation & theming" system from
`docs/research/feature-roadmap.md`. Numbered 0014 because 0005–0009 are reserved by name for
M3–M7 in `docs/research/development-plan.md`, 0010–0013 are taken (entity spine, local-first
combat, stats, weapon behaviours), and 0014 is the next free number. This slice covers
**generation** only; theming (swappable background/ground, biome palettes) and adjustable size as
a player-facing option are deferred follow-ups.

## Context

The playfield was a single hand-authored 28×16 text map (`Battlefield01`), parsed by `LevelMap`.
The owner asked for map variety. The roadmap's S8 is exactly that: produce arenas procedurally
rather than authoring each one, so every match can play on a fresh battlefield.

`LevelMap` already is the producer seam the rest of the game consumes — the arena (`GridArena`),
the wall view, the bush field, and the tank/pickup spawns all read a `LevelMap`'s `Materials`,
`Bushes`, and spawn. So a generator only needs to **produce a `LevelMap`**; nothing downstream
changes. The constraints are the usual GameLogic ones — pure C#, deterministic, no Godot — plus
two gameplay invariants that a hand-authored map satisfied by eye and that a generator must
guarantee mechanically:

1. **No stranding.** Every floor cell must be reachable from the spawn, or a tank could spawn (or
   a pickup could sit) in a walled-off pocket.
2. **Anchors stay valid.** The player, Player 2, the AI, and the pickups need open, reachable floor
   to sit on. The generator chooses those cells itself (spread out) and guarantees them — the scene
   no longer hard-codes any cell, so generation works at any size.

## Decision

### 1. A seeded `ArenaGenerator` produces a validated `LevelMap`

`LevelMap` gains a `FromCells(materials, bushes, spawnX, spawnY)` factory — the second producer
beside `Parse` — and `ArenaGenerator.Generate(ArenaGenParams)` returns one. Generation is a pure
function of the `ArenaGenParams.Seed`: the same parameters always yield the same arena, so a match
is reproducible (and, later, a server and client can agree on an arena from a shared seed without
shipping the map).

### 2. Valid by construction, not by hope

`Generate` scatters brick (≈12%), a little steel (≈4%), and bush hide-spots over an open interior
inside a steel border, then **repairs** the result rather than trusting it:

- The generator first chooses the anchor cells it needs — player and Player 2 spawns in opposite
  corners, the enemies and pickups spread across the field with a separation preference — and locks
  them (plus a clearing around the player spawn) to floor before scattering, so a wall never lands
  on one.
- A flood-fill from the spawn finds every reachable floor cell; any **unreachable** floor pocket is
  filled with steel (so "every floor cell is reachable" holds trivially), unless it contains a
  locked cell — in which case the layout is rejected and re-rolled.
- The attempt is accepted only if the interior is still ≥80% open floor.

Up to 40 seed-derived layouts are tried; the vanishingly rare total failure falls back to a
bare border-only arena, which is trivially valid. Keeping density low makes the first attempt
succeed almost always.

### 3. Wired into local play behind a per-match seed; networked play unchanged

`GameSetup.ArenaSeed` is fixed by default (so a direct launch — tests, dev — is reproducible) and
rolled fresh by `StartNewMatch`, then held across the per-round scene reloads so one best-of-N
series plays a single arena. The map size lives in `GameSetup` too (`ArenaWidth`/`ArenaHeight`,
default 28×16), so it is adjustable per match; `ArenaScene` reads the generated spawn/pickup
placements from the result and fits the two-player camera to whatever size was generated. The
networked `NetArenaScene` keeps loading the shared `Battlefield01` — server and client must agree on
the same map, which is its own (later) concern, so procedural generation is local-only for now.

## Consequences

- **Positive:** map variety with no new authoring; `LevelMap` stays the single seam, so the arena,
  views, and pickups are untouched. The generator owning placement means the scene hard-codes no
  cell and works at any size. Determinism keeps it testable and sets up seed-based networked map
  sync later.
- **Negative / cost:** a generated arena loses the deliberate composition of a hand-tuned map; the
  density/validity knobs are balance values that will want a playtest. Filling unreachable pockets
  with steel can leave small solid blobs — acceptable, but a cosmetic follow-up could smooth them.
- **Deferred:** theming (swappable ground/background, biome palettes), a player-facing size control
  on the title screen (the size is adjustable in `GameSetup`, but nothing exposes it yet),
  symmetry/fairness guarantees for versus, and weighting placement by quadrant for better spread.
  Networked procedural arenas wait on a shared-seed handshake. `Battlefield01` is retained —
  `NetArenaScene` and the `LevelMap` tests still use it as the reference hand-authored map.
