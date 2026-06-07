# ADR-0017: Render the game in a 3D world

**Date:** 2026-06-07
**Status:** Accepted
**Deciders:** Solo developer + Claude Code

Numbered 0017, the next free number after 0016.

## Context

The game has been presented in a 2D isometric view (ADR-era `IsoProjection`, iso `TileMapLayer`
ground, sprite/`SubViewport` tanks, `ZIndex` depth-sorting). Reaching a good tank look took several
attempts — baking a realistic 3D model to directional sprites (#140), a procedural cartoon sprite
(#142), then a live 3D model rendered through a per-tank `SubViewport` into the 2D world (#143). Each
fought the same friction: faking depth, calibrating a screen↔world projection, seating art on iso
cells, snapping continuous facings to frames, and a per-tank 3D viewport just to show a 3D model.

The owner asked to stop compromising and move to a genuine 3D world — keeping the gameplay, controls,
and ideas identical, changing only the visuals and the world they live in.

This is cheap to do *because of* the layered architecture (ADR-0001). The Domain and GameLogic layers
are rendering-agnostic — positions are `System.Numerics.Vector2`, facings are `float` radians, with
zero Godot references (enforced by `LayerRulesTests`). All gameplay (tanks, projectiles, world tick,
AI, combat, powerups, walls, game modes, **and the netcode**) is untouched by a render change. Only
the Presentation layer is iso-coupled: ~16 `IsoProjection` call sites across five `*View` classes and
the two arena scenes, plus the mouse-aim formula (a hardcoded inverse-iso projection) and the camera.

## Decision

Port the **Presentation layer** to a 3D world; leave Domain and GameLogic unchanged. The flat game
plane maps directly: game `(x, y)` → world `(x, 0, y)`.

- **Camera:** an orthographic Camera3D at a fixed ¾ angle, following the player (one-player) or
  framing the field (two-player) — preserving the readable, stable-aim feel of the iso view with real
  3D depth and lighting.
- **Aim:** replace the inverse-iso `ComputeAim` with a ray from the camera through the mouse onto the
  ground plane; the aim angle is the direction from the player's ground point to that hit. Exact, with
  no projection fudge.
- **Tanks:** the existing `Tank3D.glb` is placed directly in the world (no `SubViewport`); the hull
  and the independently-aiming turret node rotate in real time. Per-part materials give the team-colour
  body, yellow lights, black tracks; real lights/shadows provide the form shading.
- **Terrain:** the iso ground `TileMapLayer` and `IsoTerrainView` sprites become 3D meshes — a ground
  plane and a box (or themed mesh) per wall/terrain cell. Depth-sorting (`IsoProjection.DepthOf` →
  `ZIndex`) is deleted; the depth buffer handles it.
- **Projectiles, powerups, airstrike, bushes, sandbags, fog-of-war/vision:** rebuilt as 3D nodes;
  screen-space helpers (`PickupFloater`, `FireArrow`) use `Camera3D.UnprojectPosition`.
- **HUD/menus** (score, meters, brick counter, net status, title) are screen-space `CanvasLayer`/
  `Control` and are reused as-is.

Rollout is incremental to keep `main` playable: a parallel `Arena3DScene` is built up view-by-view
behind a title-screen entry, brought to parity with the iso arena, then made the default — after which
the iso presentation code (`IsoProjection`, `IsoTerrainView`, the iso ground, `ArenaScene`'s iso bits)
is deleted.

## Consequences

- **Positive:** removes the standing 2D-iso compromises (projection calibration, depth-sort bookkeeping,
  sprite-facing snapping, art seating, the per-tank `SubViewport`); real lighting/shadows give the 3D
  shading the owner wanted for free; aiming and depth become exact; the tank is a plain world node
  (cheaper than the viewport approach). Future visual work targets one real 3D world, not an iso
  emulation.
- **Negative / cost:** a sizeable Presentation rewrite (~6–8 PRs) and the Presentation scene tests are
  rewritten per view; during the incremental phase the iso and 3D scenes coexist (temporary duplication
  of the gameplay composition until the iso scene is deleted at cutover).
- **Unchanged:** Domain, GameLogic, the spawn/despawn→view binding loop, the network protocol, game
  modes, balance, and the screen-space HUD/menus. No gameplay behaviour changes.
- **Deferred:** camera polish (perspective option, shake, rotation), shadow/quality tuning, and
  mobile-performance passes on the Galaxy A56 land as their own follow-ups.
