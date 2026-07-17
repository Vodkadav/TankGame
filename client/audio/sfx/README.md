# Sound effects — audition guide

Every clip the game plays lives in this folder as a plain OGG Vorbis file. They are loaded
raw at runtime (`SfxPool.LoadOgg` → `AudioStreamOggVorbis.LoadFromBuffer`), so **replacing a
file by name is all it takes** — no Godot import step, the next launch picks it up. Any media
player can preview them.

Rules for replacements: OGG Vorbis only (a WAV renamed `.ogg` fails silently), short one-shots
(~0.3–1.5 s; airstrike up to ~2 s), loudness consistent with the rest of the set. Per-kind
volume offsets (fire, UI click/hover) are code-side in `client/src/Presentation/Arena/SfxPool.cs`.
Regeneration: assetfactory MCP `generate_sound` (see `docs/credits/assets.md` for the prompts
used per clip).

| File | Plays when | Design brief |
|---|---|---|
| `fire.ogg` | every cannon shot (positional, extra −20 dB) | cartoon pew |
| `explosion.ogg` | a tank is destroyed | punchy cartoon death boom |
| `wall_break.ogg` | brick/crate crumbles to floor | masonry crumble |
| `pickup.ogg` | powerup collected (generic fallback) | bright collect blip |
| `powerup_speed.ogg` | Speed Boost collected | engine rev |
| `powerup_rapid.ogg` | Rapid Fire collected | machine-gun rattle |
| `powerup_bounce.ogg` | Bouncing Ammo collected | ricochet ping |
| `powerup_spread.ogg` | Spread Shot collected | shotgun pump |
| `powerup_pierce.ogg` | Piercing Ammo collected | armour-piercing zing |
| `powerup_repair.ogg` | Repair collected | restorative chime |
| `powerup_shield.ogg` | Shield collected | energy hum |
| `powerup_missile.ogg` | Missile collected | rocket whoosh |
| `powerup_airstrike.ogg` | Telephone/Airstrike collected | incoming jet + siren |
| `victory.ogg` | match end sting (non-positional, full volume) | festive fanfare |
| `ui_click.ogg` | menu button press (−6 dB) | gentle tick |
| `ui_hover.ogg` | mouse enters a menu button (−16 dB) | softest tick |
| `kill_enemy.ogg` | you destroy an enemy (voice channel) | "Enemy destroyed" |
| `streak_double.ogg` | 2nd kill in the streak window | "Double kill" |
| `streak_triple.ogg` | 3rd kill in the streak window | "Triple kill" |
| `streak_multi.ogg` | 4+ kills in the streak window | "Multi kill" |

The file→kind mapping is `SfxFiles` in `SfxPool.cs`; the per-kind design brief lives as doc
comments on `SfxKind`. Keep all three (folder, mapping, briefs) in sync when adding a sound.
