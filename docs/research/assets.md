# Free Asset & Audio Sources — Tank-Labyrinth MVP

Survey of free-license sources for a 2D top-down multiplayer tank game.

**Methodology note:** WebSearch and WebFetch were not available in this session, so the picks below are drawn from well-known canonical packs from the source sites the brief listed. Every URL is the canonical pack/index URL; before integrating any pack into the repo, re-open the link, re-read the current license text on the pack's own page, and snapshot the license file into `docs/licenses/` alongside the asset. Licenses on free-asset sites occasionally change, and OpenGameArt in particular is per-submission rather than site-wide.

**License shorthand used below:**
- **CC0** — public domain dedication, no attribution required, commercial OK.
- **CC-BY 3.0 / 4.0** — free + commercial, attribution required (credit name + source link).
- **CC-BY-SA** — same as CC-BY plus any derivative must also be CC-BY-SA (viral; avoid where possible for game assets).
- **Kenney license** — effectively CC0; donations welcome but not required.
- **SIL OFL 1.1** — Open Font License, free commercial use including embedding; can't sell the font itself standalone.

---

## 1. Tank sprites — top-down

Top picks:

1. **Kenney — "Top-down Tanks Redux"** — https://kenney.nl/assets/topdown-tanks-redux
   - License: Kenney/CC0, no attribution required.
   - Pack: ~110 PNG sprites, multiple chassis colours (red/blue/green/sand/dark), separate barrels/turrets, plus tracks, bullets, smoke, crates, sandbags, trees, oil drums, barricades. PNG at typical 64-ish px tank size, clean top-down, art aligned so turret pivots cleanly on chassis.
   - Fit: This is the "default win" for the MVP — separate turret means you can rotate aim independently of body, and the bundled crates/sandbags/barricades make a coherent in-arena prop set. Same art style across all of it.

2. **Kenney — "Tanks Pack"** — https://kenney.nl/assets/tanks
   - License: Kenney/CC0, no attribution required.
   - Pack: 50+ sprites, top-down vehicles + turrets + bullets + tile fragments. Smaller and slightly simpler than Redux but covers the same shape.
   - Fit: Good fallback if Redux feels too cartoony; can mix-and-match because the two packs share a colour palette.

3. **OpenGameArt — "Tanks" by surt / quale (search "top-down tank")** — https://opengameart.org/art-search-advanced?keys=tank&field_art_type_tid%5B%5D=9&sort_by=count
   - License: per-submission, mostly CC0 or CC-BY 3.0 — verify each before using.
   - Pack: dozens of individual submissions; varies from 16-bit pixel to clean vector. Useful for one-off uniques (boss tank, special skin).
   - Fit: Use for variety, not a base look; mixing styles will fight your Kenney foundation, so keep this for hidden unlock skins only.

Backup options:
- **Game-icons.net "tank" silhouettes** — https://game-icons.net/tags/tank.html — CC-BY 3.0, single-colour SVG silhouettes, great for emblems and minimap icons, not for in-world sprites.
- **itch.io free tank assets (search "top down tank free")** — https://itch.io/game-assets/free/tag-tank — licenses vary wildly (some CC0, some "free but credit me", some "free for non-commercial only"); each pack must be checked individually.

Caveats:
- Kenney sprites are vector-style flat shaded — clean but not pixel-art. If you want a pixel-art aesthetic instead, Kenney won't give it to you; pivot to OGA + bespoke.
- None of these include damaged/burning tank states — you'll need to either tint+overlay smoke or commission/draw destroyed states.

---

## 2. Labyrinth tilesets — destructible walls, floors, ambient props

Top picks:

1. **Kenney — "Top-down Shooter"** — https://kenney.nl/assets/top-down-shooter
   - License: Kenney/CC0.
   - Pack: ~500 sprites, top-down environment kit — floor tiles (concrete, dirt, grass, sand, metal grates), wall sections in multiple materials, crates, barrels, vegetation, doors. PNG + spritesheet.
   - Fit: Walls come in destroyable-looking variants (concrete blocks, sandbags, wood crates) — perfect for "shoot the wall, it crumbles" mechanic. Same palette family as Top-down Tanks Redux.

2. **Kenney — "Roguelike/RPG Pack"** — https://kenney.nl/assets/roguelike-rpg-pack
   - License: Kenney/CC0.
   - Pack: 16x16 pixel-art tileset, dungeon/labyrinth walls and floors, mobs, props, items.
   - Fit: If you go pixel-art instead of flat-vector, this is the tileset analog — and its props (chests, doors, fountains) double as powerup spawn points.

3. **OpenGameArt — "LPC" (Liberated Pixel Cup) tilesets** — https://opengameart.org/content/lpc-tile-atlas
   - License: CC-BY-SA 3.0 + GPL 3.0 (dual). Attribution required, and CC-BY-SA is viral.
   - Pack: Large coherent pixel tileset, walls/floors/grass/water/objects.
   - Fit: Backup only — the share-alike clause means anything you ship using LPC art must itself be CC-BY-SA, which complicates closed-source releases. Fine for hobby/open-source projects.

Backup options:
- **OpenGameArt "dungeon tileset 32x32" by Buch** — https://opengameart.org/content/dungeon-tileset — CC0.
- **itch.io "0x72 — Dungeon Tileset II"** — https://0x72.itch.io/dungeontileset-ii — CC0, gorgeous 16x16 pixel-art, animated.

Caveats:
- Destructible walls usually need 3–4 damage states (intact → cracked → rubble → empty). Kenney provides intact + rubble; the in-between cracked states often have to be drawn or generated programmatically (overlay a crack-decal texture).
- Tile size matters: pick one (16, 32, or 64 px) and stick to it across all imports, or you'll fight scaling artefacts.

---

## 3. Visual FX — explosions, muzzle flashes, smoke, particles

Top picks:

1. **Kenney — "Particle Pack"** — https://kenney.nl/assets/particle-pack
   - License: Kenney/CC0.
   - Pack: ~250 PNG particles — smoke puffs, sparks, fire, magic, light beams, dust, explosion rings. Pre-rendered, ready to drop into any particle system.
   - Fit: The most-cited free particle pack for indie shooters. Pair with engine particle emitter (Godot GPUParticles2D, Phaser ParticleEmitter, etc.). Smoke + explosion ring give you 90% of what a tank game needs.

2. **Kenney — "Smoke Particle Assets"** — https://kenney.nl/assets/smoke-particles
   - License: Kenney/CC0.
   - Pack: Smoke variations specifically (white, black, grey, coloured).
   - Fit: Plug into damaged-tank trail and explosion afterclouds.

3. **OpenGameArt — explosion sprite sheets by JROB774 / Sogomn / "explosion animation"** — https://opengameart.org/art-search-advanced?keys=explosion&sort_by=count
   - License: mostly CC0 or CC-BY 3.0, check per submission.
   - Pack: Individual 8–16 frame explosion sheets, both pixel and rendered.
   - Fit: Frame-by-frame animated explosions are nicer than emitter-only puff-puff if you want punch on tank kill.

Backup options:
- **OpenGameArt "Muzzle flash" by para** — https://opengameart.org/content/muzzle-flash-2d-explosion-effects — CC0.
- **itch.io "Pixel FX Designer" free output** — https://codemanu.itch.io/pixelfx-designer — if you don't mind the tool name in credits, very flexible and generates pixel-perfect FX.

Caveats:
- Pre-rendered particle PNGs vs. animated sprite sheets are different beasts. Decide early: a particle-emitter system needs single-puff PNGs (Kenney Particle Pack); animated-sheet rendering needs N-frame sheets (OGA).
- Muzzle flashes need to be tiny and short (~2 frames @ 30 ms) or they look ridiculous on a small tank — be prepared to crop and tune.

---

## 4. UI icons — buttons, weapon icons, emblems

Top picks:

1. **Kenney — "Game Icons" + "Game Icons Expansion"** — https://kenney.nl/assets/game-icons and https://kenney.nl/assets/game-icons-expansion
   - License: Kenney/CC0.
   - Pack: Hundreds of UI icons — hearts, ammo, shields, gears, arrows, X/check, weapons, medals. PNG (multiple resolutions).
   - Fit: One-stop shop for HUD ammo/health/shield/menu-cog icons.

2. **Kenney — "UI Pack" / "UI Pack RPG Expansion"** — https://kenney.nl/assets/ui-pack and https://kenney.nl/assets/ui-pack-rpg-expansion
   - License: Kenney/CC0.
   - Pack: Buttons, sliders, panels, checkboxes, progress bars in multiple colours and styles.
   - Fit: Whole menu/lobby UI without writing CSS or drawing widgets.

3. **game-icons.net** — https://game-icons.net
   - License: CC-BY 3.0 — attribution required (a single combined "icons by game-icons.net contributors" line is fine).
   - Pack: 4000+ single-colour SVG icons by category — perfect for weapon types, powerup types, emblem badges, faction logos.
   - Fit: Best in class for the *emblem/title* unlockables in the progression system; recolour freely.

Backup options:
- **Lucide / Heroicons / Tabler** — https://lucide.dev — MIT/ISC, generic UI iconography (menu, settings, close, sound), not game-specific.
- **OpenGameArt UI pack search** — https://opengameart.org/art-search-advanced?keys=ui — mixed licenses.

Caveats:
- game-icons.net requires attribution. Plan a "Credits" screen from day one; it's cheaper than retrofitting later.
- Kenney's UI pack has a baked-in colour scheme (red/blue/yellow buttons). Tinting at runtime is easy if assets are saved with neutral fills; otherwise you're recolouring in code.

---

## 5. SFX — fire, explode, hit, pickup, UI clicks, ambient

Top picks:

1. **Kenney — "Sci-fi Sounds" + "Impact Sounds" + "Interface Sounds"** — https://kenney.nl/assets?q=audio
   - License: Kenney/CC0.
   - Pack: ~80 sci-fi shoots/lasers, ~50 impact thumps, ~50 UI clicks/blips. WAV/OGG.
   - Fit: Covers cannon fire, hit, pickup, UI click in one consistent foley library. Drag-and-drop ready.

2. **Freesound.org curated tags** — https://freesound.org/browse/tags/explosion/ and https://freesound.org/browse/tags/cannon/
   - License: per-sample — most are CC0 or CC-BY 3.0 / 4.0. Always read the per-sample license.
   - Pack: Open community library, millions of samples. Best for "I need a *specific* sound" — distant artillery rumble, tank engine idle, ricochet.
   - Fit: Use to fill the gaps Kenney doesn't cover (engine loops, ambient wind, distant battle rumble).

3. **sfxr / jsfxr / Chiptone (procedural)** — https://sfxr.me and https://sfbgames.itch.io/chiptone
   - License: tool is free; output you generate is yours (effectively CC0).
   - Pack: Generate retro-game SFX in-browser — perfect for pickup blips, UI confirms, powerup activations. Save & version the generation seed for reproducibility.
   - Fit: The dependable "we need a coin-pickup ding *right now*" tool. Daughters can use it too.

Backup options:
- **Sonniss "GameAudioGDC" annual bundles** — https://sonniss.com/gameaudiogdc — royalty-free, free download, multi-GB pro foley libraries. Massive but unstructured; great for ambient and explosions if you're willing to dig.
- **Pixabay sound effects** — https://pixabay.com/sound-effects/ — Pixabay license (free commercial, no attribution required), searchable. Quality is uneven.

Caveats:
- Freesound credits each sample individually — keep a `docs/credits/sfx.md` updated as you import.
- Loudness varies wildly between sources. Plan to normalize all SFX to ~ -14 LUFS or similar on import.

---

## 6. Music — short loops for menu and in-match

Top picks:

1. **Kevin MacLeod / incompetech.com** — https://incompetech.com/music/royalty-free/music.html
   - License: CC-BY 4.0 (attribution required: "Music: 'Track Name' by Kevin MacLeod (incompetech.com), Licensed under Creative Commons By Attribution 4.0").
   - Pack: Vast catalogue, browseable by mood/genre. "Action", "Electronica", "Drama" categories have plenty of tense-loopable tracks.
   - Fit: Reliable, professionally produced, well-tagged. Many tracks already designed to loop. Attribution is one line in credits.

2. **Pixabay Music** — https://pixabay.com/music/
   - License: Pixabay license — free commercial use, no attribution required (though encouraged).
   - Pack: Growing catalogue of royalty-free tracks across genres. Some excellent action/cyberpunk loops.
   - Fit: The attribution-free option if you don't want a credits screen at all (though you should still have one for icons/SFX).

3. **OpenGameArt music section** — https://opengameart.org/art-search-advanced?field_art_type_tid%5B%5D=12 (filter to CC0 / CC-BY 3.0)
   - License: per-track.
   - Pack: Loops specifically designed for games — composer tagged tracks as "loopable" frequently. Search "action loop" or "battle music".
   - Fit: Use for in-match background; quality varies but the loopable tag is reliable.

Backup options:
- **FreePD.com** — https://freepd.com — CC0 music, no attribution required, curated.
- **Bensound** — https://bensound.com — CC-BY required, large catalogue, but read license carefully (free tier has restrictions).

Caveats:
- Menu music and in-match music have different needs: menu can be longer and lyrical; in-match needs to *loop* seamlessly without drawing attention. Many "free" tracks aren't seamless loops — test in-engine.
- MP3 vs OGG: web builds prefer OGG; have both formats ready.

---

## 7. Fonts — readable in HUD, free for commercial use

Top picks:

1. **Google Fonts** — https://fonts.google.com
   - License: SIL OFL 1.1 (commercial OK, embedding OK, can't resell the font alone).
   - Picks:
     - **Press Start 2P** (https://fonts.google.com/specimen/Press+Start+2P) — retro pixel font, instantly says "video game", excellent for headers and arcade feel.
     - **Russo One** (https://fonts.google.com/specimen/Russo+One) — heavy slab/sans, militaristic, great for tank HUD.
     - **Orbitron** (https://fonts.google.com/specimen/Orbitron) — futuristic display font, good for sci-fi HUD numerals.
     - **Roboto Mono** or **JetBrains Mono** for any debug/stat display.
   - Fit: Pair one display font (Press Start 2P or Russo One) with one body font (Roboto / Inter) for menus. Always self-host the woff2 in repo rather than hot-linking, so offline/native builds work.

2. **Dafont "free for commercial use" filter** — https://www.dafont.com/theme.php?cat=303 (Sci-fi) — filter to "100% Free" + read each font's license file
   - License: per-font, mixed. Always download and read the bundled `.txt` license.
   - Picks: Search "Pixel", "Stencil", "Military" categories. Many free-for-commercial pixel fonts that look great over a tank HUD.
   - Fit: When Google Fonts can't deliver a sufficiently grungy/military feel.

3. **Fontstruct & FontLibrary** — https://fontlibrary.org
   - License: per-font, mostly OFL/CC.
   - Pack: Indie/community fonts not on Google.
   - Fit: Backup if you want something less seen.

Backup options:
- **MonoGram** by datagoblin (itch.io) — https://datagoblin.itch.io/monogram — CC0, minimal pixel font, free even for commercial.

Caveats:
- "Free" on dafont sometimes means "free for personal use only" — *always* read the bundled license file. Don't trust the front-page filter alone.
- Bundle exactly the weights/subsets you need (woff2-subset for web) — full font files can be 200+ KB each and inflate web builds.

---

## 8. Powerup / pickup art

Top picks:

1. **Kenney — "Generic Items" / "Platformer Art Pickups"** — https://kenney.nl/assets/generic-items and https://kenney.nl/assets/platformer-art-pickups
   - License: Kenney/CC0.
   - Pack: Hearts, stars, coins, gems, keys, ammo crates, shields — already in Top-down Tanks Redux palette family.
   - Fit: Direct fit for powerup pickups (health, shield, ammo, speed boost). Drop straight in.

2. **OpenGameArt "powerup" search** — https://opengameart.org/art-search-advanced?keys=powerup
   - License: per-submission, mostly CC0.
   - Pack: Glowing orbs, rotating crystals, animated capsules.
   - Fit: Use the *animated* sheets here — pickup motion (rotate + bob) is half the visual signal that "this is grab-able".

3. **game-icons.net "powers"** — https://game-icons.net/tags/power.html
   - License: CC-BY 3.0.
   - Pack: Icon-style power symbols (lightning, shield, speedlines) for the HUD readout of *active* powerups.
   - Fit: Pair the in-world pickup sprite (Kenney) with the matching HUD icon (game-icons.net) — same visual language across world + HUD.

Backup options:
- Re-tint existing Kenney items at runtime to multiply pickup variety without new art.
- Bespoke: a "trap" debuff pickup (mine, oil slick, EMP) probably needs custom art — these aren't standard in any free pack.

Caveats:
- "Pickup feel" depends as much on the *spawn animation* (drop-from-sky, fade-in, light pulse) and *grab SFX* as the sprite itself. Budget engine time for that, not just sprite picking.

---

## Recommended MVP asset stack

**Primary sources (use these three and you have a coherent MVP look + sound):**

1. **Kenney.nl** — Top-down Tanks Redux + Top-down Shooter + Particle Pack + Game Icons + UI Pack + audio (Sci-fi/Impact/Interface). Single-source CC0, no attribution, consistent flat-vector style across sprites, environment, FX, UI, and SFX. This is 70-80% of the MVP's art and sound on its own.
2. **game-icons.net** — Emblems, faction badges, weapon-type icons, and active-powerup HUD icons. CC-BY 3.0 — one credits line.
3. **incompetech.com (Kevin MacLeod)** — Menu loop + in-match loop. CC-BY 4.0 — two credits lines.

**Plus Google Fonts** (Russo One + Roboto/Inter) for typography — OFL, embed and ship.

**Bespoke work you'll still need:**
- **Destructible-wall intermediate damage states** (cracked but not yet rubble) — overlay decals or commission ~6 frames.
- **Trap / debuff pickup sprites** (mines, EMPs, slows) — no good free pack covers these.
- **Destroyed/burning tank states** — overlay smoke + tint, or draw ~3 frames per chassis.
- **Game logo / wordmark** — bespoke vector, ~1 evening's work or a fiverr commission.
- **Voice / announcer SFX** — if you want "Round start!" / "Powerup!" / "Victory!" calls, you'll need text-to-speech (ElevenLabs free tier) or a friend with a microphone.

**Decision needed from user:** pixel-art aesthetic (Kenney Roguelike + 0x72 + bespoke pixel work) **or** flat-vector aesthetic (Kenney Top-down Tanks Redux + Top-down Shooter)? Pick before importing anything — mixing the two looks amateurish and you can't reverse the choice cheaply. Default recommendation: **flat-vector**, because the Kenney top-down line gives you the broadest pre-built coverage with one consistent style and zero attribution overhead.
