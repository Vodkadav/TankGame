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

1. **Iso projection foundation** — an `IsoProjection` helper (grid X,Y ↔ iso screen) used by every view;
   Y-sort (`YSortEnabled`/`ZIndex` by `x+y`) so nearer things draw over farther; switch the ground/wall
   render to an isometric `TileMapLayer`. Keep current art for one frame, then swap tiles.
2. **Iso ground + terrain** — PixVoxel terrain tiles for floor/water/bridge/mountain via the iso tilemap
   (replaces the flat ground polygon + the square `Walls.png` atlas).
3. **Iso tanks** — directional `Tank_Large_face{0..3}` sprites; map `Tank.Rotation` → nearest facing,
   team → `colorN`; turret handled by a separate facing sprite or an overlaid barrel. Replaces `TankView`.
4. **Buildings / crates** — solid building + crate iso sprites placed per cell.
5. **Bushes / nature** — Nature Kit bush sprites for concealment cells.
6. **UI re-skin + minimap** — fantasy-ui-borders panels for the title/HUD/game-over; cursor reticle; a
   code-rendered minimap.
7. **Explosions / projectiles** — PixVoxel explosion frames on death + airstrike; shot sprites.

The asset catalogue (`AssetCatalogue`) and the per-cell tile mapping (`WallGridView.FrameFor`) are the
seams these phases plug into; the move/shot blocking, generation, and combat are unchanged.
