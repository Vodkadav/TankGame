# Asset Credits

## Sprites

| Asset | Source | License | Notes |
|-------|--------|---------|-------|
| `client/src/Presentation/Tank/KenneyTankBody.png`, `KenneyTankTurret.png` | Kenney "Top-down Tanks Remastered" (sand body + barrel), processed by `scripts/prep_kenney_tank.py` | CC0 1.0 (public domain, no attribution required) | Source: <https://kenney.nl/assets/top-down-tanks-remastered>. Script scales to ~one tile, rotates north→east to match the rotation-0 convention, and centres the barrel mount for the pivot. Neutral sand, tinted per team via `TankView.ApplyTeamTint`. |
| `client/src/Presentation/Tank/TankBody.png`, `TankTurret.png` | Programmer-generated placeholder (`scripts`/PIL) | Public domain (trivial placeholder) | **Superseded** by the Kenney art above (kept as a fallback). No attribution required either way. |
| `client/src/Presentation/Arena/Walls.png` | Programmer-generated placeholder (`scripts/gen_wall_atlas.py`/PIL) | Public domain (trivial placeholder) | **Temporary.** 4-frame 32×32 atlas: brick intact/cracked/rubble (hp 3/2/1) + steel. To be replaced by Kenney "Top-down Shooter" (CC0). Deterministic generator committed for reproducibility. |

When a Kenney pack is integrated, snapshot its license into `docs/licenses/` and
update the affected row (Kenney license ≈ CC0, no attribution required).
