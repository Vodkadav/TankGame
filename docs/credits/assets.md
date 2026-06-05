# Asset Credits

## Sprites

| Asset | Source | License | Notes |
|-------|--------|---------|-------|
| `client/src/Presentation/Tank/KenneyTankBody.png`, `KenneyTankTurret.png` | Kenney "Top-down Tanks Remastered" (sand body + barrel), processed by `scripts/prep_kenney_tank.py` | CC0 1.0 (public domain, no attribution required) | Source: <https://kenney.nl/assets/top-down-tanks-remastered>. Script scales to ~one tile, rotates north→east to match the rotation-0 convention, and centres the barrel mount for the pivot. Neutral sand, tinted per team via `TankView.ApplyTeamTint`. |
| `client/src/Presentation/Arena/IsoGroundDesert.png` | PixVoxel Wargame pack (`isometric/terrain/Desert`) | CC0 1.0 (public domain, no attribution required) | The isometric (Phase 2) ground tile: a native 128×90 iso diamond laid one-per-cell by `ArenaScene.BuildGround` through an isometric `TileMapLayer`, tinted per `ArenaTheme`. Licence snapshot: `docs/licenses/PixVoxel_Wargame-LICENSE.txt`. |
| `client/src/Presentation/Arena/IsoWater.png`, `IsoBridge.png`, `IsoMountain.png` | PixVoxel Wargame pack (`isometric/terrain/River`, `Road`, `Mountains`) | CC0 1.0 (public domain, no attribution required) | The Phase 2 natural-terrain tiles drawn by `IsoTerrainView` (water/bridge flat below entities, mountain depth-sorted). Same CC0 licence snapshot as the ground tile. |
| `client/src/Presentation/Arena/GroundSand.png` | Kenney "Top-down Tanks Remastered" (`tileSand1`), resized by `scripts/prep_kenney_ground.py` | CC0 1.0 (public domain, no attribution required) | Source: <https://kenney.nl/assets/top-down-tanks-remastered>. **Superseded** by the iso ground tile above (kept as the old flat-top-down ground). No attribution required either way. |
| `client/src/Presentation/Arena/PickupDisc.png` | Programmer-generated (`scripts/gen_pickup_disc.py`/PIL) | Public domain (trivial generated art) | Neutral white glowing disc with a dark coin edge; `PowerupView` tints it per `PowerupKind` via Modulate. Deterministic generator committed. (Base SDXL could not produce a clean neutral/tintable disc, so this is the "generated" pickup art.) |
| `client/src/Presentation/Projectile/KenneyBullet.png` | Kenney "Top-down Tanks Remastered" (`bulletSand2`), processed by `scripts/prep_kenney_bullet.py` | CC0 1.0 (public domain, no attribution required) | Source: <https://kenney.nl/assets/top-down-tanks-remastered>. Scaled to projectile size and rotated north→east; `ProjectileView` rotates it to the shot's travel direction. |
| `client/src/Presentation/Tank/TankBody.png`, `TankTurret.png` | Programmer-generated placeholder (`scripts`/PIL) | Public domain (trivial placeholder) | **Superseded** by the Kenney art above (kept as a fallback). No attribution required either way. |
| `client/src/Presentation/Arena/Walls.png` | Programmer-generated placeholder (`scripts/gen_wall_atlas.py`/PIL) | Public domain (trivial placeholder) | **Temporary.** 5-frame 32×32 atlas: brick intact/cracked/rubble (hp 3/2/1) + steel + crate (destructible, hp 2). To be replaced by Kenney crate/tile sprites (CC0). Deterministic generator committed for reproducibility. |

When a Kenney pack is integrated, snapshot its license into `docs/licenses/` and
update the affected row (Kenney license ≈ CC0, no attribution required).
