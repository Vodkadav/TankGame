# Local Backlog — features buildable now (no new art, no networking)

A curated cut of `feature-roadmap.md` for the current **local-first** arc
(ADR-0011): everything here ships with programmer-drawn placeholder visuals
(`ColorRect`, `Light2D`, polygons, `modulate`) and runs entirely on one device.
Nothing here needs the M3 network or a new sprite import.

> **Key unblock:** fog of war and hide spots were filed under M5b/S6 assuming
> *networked* play, where per-player culling must be server-side so a sniffed
> packet cannot reveal the map. In **local** play there is no wire to sniff, so
> fog and concealment are pure client-side rendering plus AI-vision logic — they
> are doable now and only need the server treatment when M3 lands.

`PROGRESS.md` remains the single source of truth for what is *done*; this doc is
a forward plan, ordered by leverage, for what to build next.

## The battlefield + stealth cluster (the owner's headline asks)

| # | Feature | Needs | Notes |
|---|---|---|---|
| 1 | Open battlefield map (scattered cover, not a maze) | data only | re-author the level text map |
| 2 | Local vision system (radius + line of sight) | S6-lite | foundation for 3–5 |
| 3 | Fog of war (dark outside vision) | S6 + `Light2D` | 1P and co-op (team vision) |
| 4 | Bushes / hide spots | S6 occluder | hidden from AI, drawn faint |
| 5 | AI vision-gating (only chase what it can see) | S6 | makes fog and bushes matter |

Fog for two-player *versus* on a shared screen is deferred — it needs split-screen
or per-player viewports; co-op (shared team vision) and single-player are clean.

## Feel and AI quick wins

| # | Feature | Needs | Notes |
|---|---|---|---|
| 6 | Tank-vs-tank body collision | GameLogic | tanks stop pushing through each other |
| 7 | AI pathfinding (A* over the grid) | GameLogic | route around cover, stop bulldozing straight |

## Match flow (text-only HUD)

| # | Feature | Needs | Notes |
|---|---|---|---|
| 8 | Score / kill tracking | GameLogic + HUD | per-team kills on HUD and game-over |
| 9 | Lives and respawn (co-op) | GameLogic | respawn after a delay or N lives |
| 10 | Best-of-N rounds | GameLogic + HUD | multi-round matches |

## Map variety & meters — owner ask (2026-06-04)

Recorded in `feature-roadmap.md` as **S8** (arena generation & theming) and **S9**
(progression/meters/modifiers). The near-term, no-networking slices land here; the
deeper progression layer (XP, cosmetic unlocks, NPC-animal XP, trap/mine modifiers)
is post-systems content and stays in the roadmap, not this near-term cut.

| # | Feature | Needs | Notes |
|---|---|---|---|
| 15 | Procedural wall placement (brick **and** steel, not hardcoded) | S8 generator + existing `LevelMap` validator | seeded/deterministic; reuse the connectivity check |
| 16 | Adjustable map size | S8 + generalise `ArenaScene`'s hardcoded 28×16 framing | camera/zoom/spawns must read `LevelMap.Width/Height` |
| 17 | Swappable background + ground texture (themes) | S8 `ArenaTheme` (Presentation) | ground `TileMapLayer` / parallax under `WallGridView`; placeholder art first |
| 18 | Damage-dealt meter | combat-pass damage signal + HUD | `CombatResolver` gains a damage signal beside `TankKilled` |
| 19 | Kill / death meter | `TankKilled` + a death signal + HUD | per-player `MatchStats` block, sibling of `ScoreBoard` |
| 20 | "Everyone starts with effect X" match modifier | `Tank.ApplyEffect` on spawn | trivial once a modifier hook exists; proves S9 modifiers |

Deeper S9 (post-systems, in `feature-roadmap.md` only): extra-trap/mine modifiers
(S1+S7), shootable NPC `Critter` animals → XP drops (S1+S3), XP/levels, and
**cosmetic-only** unlocks (Data layer; **no power, no monetization** by mandate).

Suggested entry point when picked up: an **ADR for S8** (`ILevelSource`/`LevelParams`/
`ArenaTheme` + the `ArenaScene` size-generalisation) before #15–17, and the meters
(#18–19) as a quick HUD win that reuses the combat pass.

## Powerups — one system (S4) then cheap pickups

| # | Feature | Needs | Notes |
|---|---|---|---|
| 11 | Stats + timed status effects (S4) | S4 | the foundation |
| 12 | Speed-boost / shield / repair pickups | S4 + S3 | each a colored shape + one effect |

## Ammo variety — one system (S2) then cheap shells

| # | Feature | Needs | Notes |
|---|---|---|---|
| 13 | Weapon-behaviour strategy + raycast normals (S2) | S2 | the foundation |
| 14 | Bouncing / piercing / spread shells | S2 | each a small behaviour class, same bullet |

## Suggested order

1. **#1 battlefield + #6 tank collision** ✅ — quick, high-impact, make the open feel land.
2. **#2 to #5 vision cluster** ✅ — the headline fog + bushes, self-contained.
3. **#8 to #10 match flow** ✅ — turn skirmishes into matches.
4. **S4 powerups (#11 ✅, #12 ✅ stat pickups; shield/repair deferred)**, then
   **S2 ammo (#13 to #14)** — bigger system builds that each unlock a catalogue.
5. **Map variety & meters (#15 to #20, owner ask)** — the **S8 map system** (random
   walls incl. steel, adjustable size, themes) is its own slice with an ADR first; the
   **damage + K/D meters** (#18–19) are a quick HUD win on the existing combat pass.

(Done ✅ as of 2026-06-04; `PROGRESS.md` is authoritative for status.) Items are pulled
forward into scheduled tickets only as they are built; until then this is aspirational,
exactly like `feature-roadmap.md`.

## Owner request backlog (2026-06-05, post-art-pass)

Captured verbatim-in-spirit from two owner messages during the art/gameplay session.
`PROGRESS.md` is authoritative for what is built; these are the outstanding asks. Done items
struck through as they merge.

| # | Request | Size | Notes |
|---|---|---|---|
| 21 | ~~AI roams when idle (was standing still)~~ | S | ✅ #112 |
| 22 | Enemy tanks **visually hidden** when in grass (not just AI-blind) | S | view-side concealment using `BushField`; reveal up close like the AI |
| 23 | Pickups spawn at **random** spots each match/round (not the same place) | S | seed/placement variety |
| 24 | Pickup respawn → **drop where its carrier dies** (replace timed respawn). "thats best" | M | pickup tracks collector; on the collector's death it reappears at the death spot |
| 25 | **Weighted/clustered** cell generation: higher chance to place a cell next to a like cell, capped at 5 consecutive (then equal odds) | M | cellular-automata-ish run-length-capped clustering in `ArenaGenerator` |
| 26 | **Buildings, rivers + bridges** | L | new terrain: impassable water + passable bridge cells; multi-cell building obstacles. Exploratory ("see if we can") |
| 27 | A powerup that **speeds up** the tank | XS | NOTE: `PowerupKind.SpeedBoost` already exists (×1.6/6 s) — confirm with owner / maybe a stronger or permanent variant |
| 28 | **Missile** weapon: flies to the map edge, damaging everything in its wake | M | a long piercing/lance shot; new ammo or pickup |
| 29 | **Telephone** pickup → calls in an **airstrike** | M | a pickup that, on collect, rains damage over an area after a delay |
| 30 | **HUD top-left is unreadable** (meters/score overlap, stacked) — needs a clean layout | S | lay the top-left overlays out without overlap; high visibility |
| 31 | **Game modes** TODO: capture the flag, king of the hill, destroy enemy statue, … | L | roadmap-level; each its own design + ADR. Recorded per owner ("add to the todo list") |

**Suggested order:** 30 HUD (clear bug) → 22 grass-invisibility → 24 pickup-drop-on-death (+23 random) →
25 weighted generation → 28 missile → 29 telephone airstrike → 26 buildings/rivers/bridges (big) →
31 game modes (roadmap). 27 is likely already done.
