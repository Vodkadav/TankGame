# Asset Browser Survey — local 3D library

Groundwork for the map-editor asset browser (search field + expandable categories).
Library root: `C:\programmering\Assets\visual\3d` — 54 top-level packs.

## Pack inventory

*Self-contained* = single-file `.glb` usable as-is. Lesson learned in this repo: Kenney `.glb`
around 2–5 KB are geometry-only (render white, need `ModelFit.Tint`); larger `.glb` embed
textures. KayKit `.gltf` always need their sibling `.bin` + texture `.png` files.

| Pack | Source / License | Model files | Self-contained? | Category |
|---|---|---|---|---|
| 3D Nature pack | Kenney, CC0 | 77 .obj(+.mtl) | No — obj, needs conversion | Nature |
| attackchopper | Cheese Animal Productions, custom EULA (commercial OK, credit required, "All Rights Reserved") | 1 .fbx + 1 .obj | No | Military — *exclude by default* |
| Crates Asset Package | Unknown — **no license file** | 11 .fbx | No | Props — *exclude* |
| KayKit Adventurers 2.0 | KayKit, CC0 | 31 .gltf, 8 .glb, 70 .fbx | .glb yes (334–809 KB); .gltf needs .bin+tex | Characters |
| KayKit Character Animations 1.1 | KayKit, CC0 | 16 .glb, 16 .fbx | Yes (410 KB–1.4 MB) | Characters |
| KayKit City Builder Bits 1.0 | KayKit, CC0 | 41 .gltf, 82 .fbx, 61 .obj | No — .gltf+.bin+tex | Buildings |
| KayKit Dungeon Remastered 1.1 | KayKit, CC0 | 211 .gltf, 422 .fbx, 216 .obj | No — .gltf+.bin+tex | Dungeon |
| KayKit Forest Nature 1.0 | KayKit, CC0 | 105 .gltf, 210 .fbx, 105 .obj | No — .gltf+.bin+tex | Nature |
| KayKit Medieval Hexagon 1.0 | KayKit, CC0 | 221 .gltf, 442 .fbx, 263 .obj | No — .gltf+.bin+tex | Terrain |
| KayKit Resource Bits 1.0 | KayKit, CC0 | 76 .gltf, 152 .fbx, 76 .obj | No — .gltf+.bin+tex | Props |
| KayKit Restaurant Bits 1.0 | KayKit, CC0 | 144 .gltf, 288 .fbx, 150 .obj | No — .gltf+.bin+tex | Props |
| KayKit Skeletons 1.1 | KayKit, CC0 | 13 .gltf, 6 .glb, 32 .fbx | .glb yes (259–809 KB) | Characters |
| KayKit Space Base Bits 1.0 | KayKit, CC0 | 57 .gltf, 114 .fbx, 73 .obj | No — .gltf+.bin+tex | Sci-fi |
| kenney_blaster-kit 2.1 | Kenney, CC0 | 40 .glb (+fbx/obj) | Mixed (3–79 KB; small = tint) | Weapons |
| kenney_blocky-characters 2.0 | Kenney, CC0 | 18 .glb | Yes (~111 KB) | Characters |
| kenney_brick-kit | Kenney, CC0 | 296 .glb | Mostly bare (avg 10 KB) | Terrain |
| kenney_building-kit | Kenney, CC0 | 79 .glb | Mostly bare (avg 11 KB) | Buildings |
| kenney_car-kit 3.1 | Kenney, CC0 | 50 .glb | Mostly yes (avg 110 KB) | Vehicles |
| kenney_castle-kit | Kenney, CC0 | 76 .glb | Mixed (2–141 KB) | Buildings |
| kenney_city-kit-commercial 2.1 | Kenney, CC0 | 41 .glb | Mostly yes (avg 89 KB) — building-a already in use | Buildings |
| kenney_city-kit-suburban 2.0 | Kenney, CC0 | 40 .glb | Mostly yes (avg 64 KB) | Buildings |
| kenney_coaster-kit | Kenney, CC0 | 183 .glb | Mixed (avg 35 KB) | Props |
| kenney_cube-pets 1.0 | Kenney, CC0 | 15 .glb | Yes (38–96 KB) | Characters |
| kenney_factory-kit 3.0 | Kenney, CC0 | 143 .glb | Mixed (avg 21 KB) | Industrial |
| kenney_fantasy-town-kit 2.0 | Kenney, CC0 | 167 .glb | Mostly bare (avg 15 KB) | Buildings |
| kenney_food-kit | Kenney, CC0 | 200 .glb | Mixed (avg 16 KB) | Props |
| kenney_furniturePack | Kenney, CC0 | 120 .obj/.fbx — **no .glb** | No — needs conversion | Props |
| kenney_graveyard-kit 5.0 | Kenney, CC0 | 91 .glb | Mixed (avg 36 KB) | Dungeon |
| kenney_hexagon-kit | Kenney, CC0 | 72 .glb | Mixed (avg 23 KB) | Terrain |
| kenney_holiday-kit | Kenney, CC0 | 99 .glb | Mixed (avg 27 KB) | Props |
| kenney_mini-arcade | Kenney, CC0 | 20 .glb | Mostly yes (avg 50 KB) | Props |
| kenney_mini-characters | Kenney, CC0 | 26 .glb | Mostly yes (avg 128 KB) | Characters |
| kenney_mini-dungeon | Kenney, CC0 | 21 .glb | Mixed (avg 27 KB) | Dungeon |
| kenney_mini-market | Kenney, CC0 | 20 .glb | Mixed (avg 38 KB) | Props |
| kenney_modular-buildings | Kenney, CC0 | 108 .glb | Mostly bare (avg 7 KB) | Buildings |
| kenney_modular-dungeon-kit 2.0 | Kenney, CC0 | 39 .glb | Mixed (2–802 KB) | Dungeon |
| kenney_modular-space-kit 1.0 | Kenney, CC0 | 40 .glb | Mixed (2–921 KB) | Sci-fi |
| kenney_pirate-kit 2.1 | Kenney, CC0 | 72 .glb | Mixed (avg 41 KB) | Props |
| kenney_platformer-kit 4.1 | Kenney, CC0 | 153 .glb | Mixed (avg 21 KB) | Terrain |
| kenney_prototype-kit | Kenney, CC0 | 145 .glb | Mostly bare (avg 13 KB) | Terrain |
| kenney_space-station-kit | Kenney, CC0 | 97 .glb | Mostly bare (avg 10 KB) | Sci-fi |
| kenney_survival-kit | Kenney, CC0 | 80 .glb | Mixed (avg 16 KB) | Props |
| kenney_tower-defense-kit | Kenney, CC0 | 160 .glb | Mixed (avg 35 KB) | Military |
| kenney_toy-car-kit | Kenney, CC0 | 106 .glb | Mixed (avg 25 KB) | Vehicles |
| kenney_train-kit | Kenney, CC0 | 103 .glb | Mostly yes (avg 66 KB) | Vehicles |
| kenney_watercraft_kit | Kenney, CC0 | 46 .glb | Mixed (avg 41 KB) | Vehicles |
| low_poly_tanks | Unknown itch.io — **no license file** | 1 model (.obj/.fbx/.blend) | No | Military — *exclude* |
| military_vehicles_lp | Zsky, **CC BY 4.0 — attribution required** | 9 .obj/.fbx | No — needs conversion | Military |
| mini-arena-1.0 | Kenney, CC0 | 22 .glb | Mixed (avg 22 KB) | Terrain |
| Nature Kit | Kenney, CC0 | 329 .glb (+dae/fbx/obj/stl) | Mostly bare (avg 9 KB) | Nature |
| naturePack_extended | Kenney, CC0 | 188 .obj — **no .glb** | No — needs conversion | Nature |
| Racing Kit (1.2) | Kenney, CC0 | 71 .gltf(+.bin) | No — .gltf+.bin+tex | Vehicles |
| space-kit-1.0 | Kenney, CC0 | 76 .obj — **no .glb** | No — needs conversion | Sci-fi |
| spacekit_2.0 | Kenney, CC0 | 153 .glb | Mostly bare (avg 13 KB) | Sci-fi |
| tank | MrEliptik "FREE Lowpoly Tank", credit requested | 2 .glb, 2 .fbx | Yes (~908 KB) — already in use | Military |

### License flags

- **Exclude from browser**: `Crates Asset Package`, `low_poly_tanks` (no license file — no
  provenance possible per `docs/credits/assets.md` rule) and `attackchopper` (custom EULA,
  "All Rights Reserved", redistribution rights unclear).
- **Attribution required if used**: `military_vehicles_lp` (CC BY 4.0) and `tank`/MrEliptik
  (credit requested, already recorded). Everything else is Kenney/KayKit CC0.

## Design sketch — import pipeline

### Map format fit

`MapDefinition` already mixes per-cell grids with sparse lists (`TeleportPads`,
`PowerupSpawns`, `Orientations`). Decorations slot in as one more optional sparse list,
lean-encoded by `MapCodec` exactly like the existing back-compat fields:

```json
"decorations": [ { "assetId": "kenney_castle-kit/towerSquare", "x": 4, "y": 7, "yawDeg": 90, "scale": 1.0 } ]
```

### Option A — copy-on-place (recommended)

On first placement of an asset, the editor copies its source file(s) into
`res://client/src/Presentation/Arena/models/imported/<pack>/<file>` and the map stores the
stable `assetId` (`<pack>/<basename>`). Imported files are committed via Git LFS like the
existing `models/` content and recorded in `docs/credits/assets.md`.

- Works after `git clone` and inside the Android APK (`res://` ships with the export).
- Maps stay shareable: any machine that has the repo can render the map.
- Only assets actually used are committed — the 4+ GB library never enters the repo.
- For non-self-contained packs (all KayKit `.gltf`, Racing Kit) the copy step must also carry
  the sibling `.bin` and referenced texture files. Prefer the `.glb` variant when a pack
  ships both (KayKit Adventurers/Skeletons do).
- `.obj`/`.fbx`-only packs (furniturePack, naturePack_extended, space-kit-1.0, 3D Nature
  pack, military_vehicles_lp) are a later wave: they need a one-time Blender/gltf conversion
  before they can be browsed, or the browser simply hides packs with no .glb/.gltf.

### Option B — absolute-path reference (rejected)

Storing `C:\programmering\Assets\...` paths is zero-copy but breaks on every other machine,
in CI scene tests, and fatally on Android export (no such filesystem). A shared map would
render holes. Unacceptable for anything beyond a single-machine prototype.

### Catalogue & browser notes

- Build the catalogue at editor runtime by scanning **both** `res://.../models/imported/`
  (always available — what shipped maps need) and, when present, the external library dir
  (dev machines only, gated behind an editor setting). Assets found only externally show a
  "will be imported on place" badge.
- Category = the suggested column above, keyed per pack; search field filters by file
  basename + pack name.
- Small bare-geometry `.glb` (most Nature Kit, brick-kit, prototype-kit) render white —
  reuse the existing `ModelFit.Tint` path and surface a tint swatch in the browser.
- Cap visual sanity with `ModelFit` auto-fit (AABB → footprint scale → seat on ground),
  same as terrain/emblem placement today.
