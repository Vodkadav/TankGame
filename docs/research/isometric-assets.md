# Isometric art pivot — asset needs + best-fit mapping

**Decided 2026-06-05:** move the game from flat top-down to **2D isometric**. The GameLogic layer is a
flat grid simulation and does **not** change — isometric is a Presentation projection
(`screenX = (x - y)·w/2`, `screenY = (x + y)·h/2`) plus depth (Y-)sorting and an isometric art set.

Source assets live outside the repo at `C:\programmering\Assets\visual` (`2d/`, `isometric/`, `ui/`).
Chosen files are copied into the repo and credited; all picks below are **CC0 / free-commercial**.

## Primary source: `isometric/PixVoxel_Wargame` (CC0)

One cohesive isometric wargame pack that covers most needs:

- **Units** — `isometric/color0`…`color7` (8 team colours) with directional `Tank_Large_face{0..3}_{0..3}.png`
  (4 hull facings × sub-frames), plus `attack`/`move`/`explode` animation frames in `animation_frames/`.
  Also Artillery, Boat, Copter, Plane, Infantry, Supply — future unit types.
- **Terrain tiles** — `terrain/`: Plains, Desert, Hills, Jungle, Forest, Mountains, River, Road, Ruins
  (+ `_bold` variants).
- **Buildings** — `Factory_Large`, `City_Large`, `Castle_Large`, `Estate_Large`, `Airport_Large`,
  `Laboratory_Large`, `Civilian_Large` in every colour.
- **Explosions** — `*_fiery_explode_*` frames (tank death, airstrike blast).

## Needs → best fit

| Need | Best fit | Source |
|------|----------|--------|
| Ground / floor | `Plains` (vary with `Desert`/`Hills`) iso tile | PixVoxel `terrain/` |
| Water (river) | `River` iso tile | PixVoxel `terrain/` |
| Bridge | `Road` tile over water (reads as a crossing) | PixVoxel `terrain/` |
| Mountain | `Mountains` iso tile | PixVoxel `terrain/` |
| Building (solid) | `Factory`/`City`/`Estate` building sprite | PixVoxel units / `isometric/Isometric Buildings` (Kenney CC0) |
| Brick / steel wall | low wall / `Ruins`, or Kenney iso blocks | PixVoxel `Ruins`; `isometric/mapPack` (Kenney CC0) |
| Crate (destructible) | crate sprite | `isometric/Crates Asset Package` |
| Bush / grass cover | discrete tree/bush sprite, or `Forest`/`Jungle` tile | `isometric/Nature Kit` (Kenney CC0) |
| Tank (hull + turret) | `Tank_Large_face{0..3}` in `colorN` — snap rotation to nearest facing; team = colour | PixVoxel units |
| Bullet / projectile | small shot sprite (or keep generated) | PixVoxel `frames` / generated |
| Explosion (death, airstrike) | `_fiery_explode_` frame sequence | PixVoxel `animation_frames/` |
| Pickup discs | keep the generated glowing disc (tinted per kind) — reads well on any ground | repo `gen_pickup_disc.py` |
| Fire-direction arrow | keep code-built arrow | repo `FireArrow` |
| UI panels / buttons / game-over | bordered panels | `ui/kenney_fantasy-ui-borders` (CC0) |
| Cursor / aim reticle | pixel cursor | `ui/kenney_cursor-pixel-pack` (CC0) |
| Input prompts (controls help) | keyboard/mouse glyphs | `ui/kenney_input-prompts_1.4` (CC0) |
| Medals / achievements | medal sprites | `2d/kenneyMedals` (CC0) |
| Minimap | rendered in code from the grid (no asset) — optional UI frame from fantasy-ui-borders | — |

## Implementation phases (each its own PR, GameLogic untouched)

1. **Iso projection foundation** — ✅ **DONE** (#TBD). `IsoProjection` helper (`WorldToScreen` /
   `ScreenToWorld` / `DepthOf` / `ScreenTransform`); every entity view projects its position and sets
   `ZIndex` from `x+y`; the static layers (ground, wall tilemap, bush/sandbag overlays) shear via the
   affine `ScreenTransform`; mouse aim inverts the projection. GameLogic untouched. Proper iso
   `TileMapLayer` + tiles come in Phase 2.
2. **Iso ground + terrain** — 🚧 **IN PROGRESS.** Ground slice ✅ **DONE (#127):** the flat ground
   polygon is replaced by a real isometric `TileMapLayer` (`TileShape=Isometric`, `DiamondDown`) laying
   one native PixVoxel `Desert` diamond per cell, themed; the projection was scaled up 2× (AxisX=1,
   AxisY=0.5) so the 128×64 tiles render crisp, and mouse-wheel camera zoom was added as a playtest aid.
   **Remaining:** the terrain materials — water/bridge/mountain (and the brick/steel/crate/building
   walls) still render via the Phase-1 sheared square `WallGridView`; swap those to PixVoxel terrain
   tiles on an iso tilemap (or per-cell iso sprites for the raised ones that must depth-sort vs tanks).
3. **Iso tanks** — directional `Tank_Large_face{0..3}` sprites; map `Tank.Rotation` → nearest facing,
   team → `colorN`; turret handled by a separate facing sprite or an overlaid barrel. Replaces `TankView`.
4. **Buildings / crates** — solid building + crate iso sprites placed per cell.
5. **Bushes / nature** — Nature Kit bush sprites for concealment cells.
6. **UI re-skin + minimap** — fantasy-ui-borders panels for the title/HUD/game-over; cursor reticle; a
   code-rendered minimap.
7. **Explosions / projectiles** — PixVoxel explosion frames on death + airstrike; shot sprites.

The asset catalogue (`AssetCatalogue`) and the per-cell tile mapping (`WallGridView.FrameFor`) are the
seams these phases plug into; the move/shot blocking, generation, and combat are unchanged.

## Phase 1 starting checklist (fresh session) — ✅ COMPLETE

Goal: render the *existing* world isometrically with **no GameLogic change** and no new art yet — prove
the projection + depth sort, then later phases swap in iso tiles/sprites. All items below are done; the
square wall tilemap and code-built overlays are sheared into iso via `IsoProjection.ScreenTransform`
(item 4's "keep the square `WallGridView` but project its tile positions" path).

1. **`IsoProjection` helper** (Presentation, pure): `WorldToScreen(Vector2 world)` →
   `new Vector2((world.X - world.Y) * 0.5f, (world.X + world.Y) * 0.25f)` (2:1 dimetric; tune the Y
   factor). Add `ScreenToWorld` (inverse) for mouse-aim. Unit-test the round-trip.
2. **Apply it in every view that mirrors a world position** — `TankView`, `ProjectileView`,
   `PowerupView`, `BushOverlay`, `SandbagOverlay`, `AirstrikeView`, the `Ground` polygon, the fog
   `PointLight2D`s, and the camera target in `ArenaScene` (the camera centres on
   `WorldToScreen(player.Position)`). The `FireArrow`/HUD are screen-space — leave them.
3. **Depth sort** — set each entity view's `ZIndex` (or use a `Node2D` with `YSortEnabled`) from
   `world.X + world.Y` so nearer (greater x+y) draws over farther. Tanks/projectiles update it each frame.
4. **Walls** — for Phase 1 keep the square `WallGridView` but project its tile positions; the proper iso
   `TileMapLayer` (Godot supports `TileSet.TileShape = Isometric`) comes in Phase 2 with the PixVoxel
   terrain tiles. Simplest Phase-1: draw each non-floor cell as a projected coloured diamond so the
   layout reads correctly in iso.
5. **Input** — `KeyboardMouseInputSource` aim must invert the projection (`ScreenToWorld`) so the mouse
   still aims at the world point under the cursor.
6. **Tests** — `IsoProjection` round-trip; a `TankView`/scene test that a known world position maps to the
   expected screen position. Existing GameLogic/Domain tests are untouched (logic unchanged).

Files most affected: `client/src/Presentation/**` (views, `ArenaScene`, new `IsoProjection.cs`) and
`client/src/Infrastructure/KeyboardMouseInputSource.cs` (aim inversion). `NetArenaScene` mirrors the
same projection. Keep each phase a squash-PR (branch → PR → CI green → squash-merge).
