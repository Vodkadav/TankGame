# Prior Art Survey — Top-Down Tank-Labyrinth Game

Survey of reference games whose mechanics, multiplayer model, and progression we can learn from. Each section is short on purpose — these are pointers, not deep dives. Conclusions distilled in the two lists at the bottom.

> Sources: this pass uses model knowledge only (WebSearch was unavailable in the slice). A follow-up pass should verify free-to-play status / monetization claims for the currently-live titles (BZFlag, Diep.io, ShellShock Live) before any design decision locks in based on them.

---

## Wii Play Tanks! (and Tanks! in Wii Play / Nintendo Land)

- **Core mechanic:** Single-screen top-down tank arena with destructible cover, ricochet shells, mines; enemy AI tanks with distinct archetypes (stationary turret, fast scout, mine-layer, sniper, super-tank).
- **Multiplayer model:** Local co-op (2 players, splitscreen-less shared arena). No online.
- **Progression / monetization:** Linear campaign of ~100 missions, no monetization, no unlocks beyond "next mission". Difficulty curve is the progression.
- **TO BORROW:** Enemy archetype variety — a small roster (5–8) of behaviorally distinct AI tanks creates huge replayability from few mechanics.
- **TO AVOID:** No replay value once the campaign is finished — needs procedural levels or PvP to survive past the campaign.

## Tank Trouble (browser, MadPet Games)

- **Core mechanic:** Single-screen procedurally generated maze, 1–3 tanks (keyboard-shared), ricochet shells, random powerups dropped in the maze (homing missile, double-shot, laser, mines, shotgun spread).
- **Multiplayer model:** Local same-keyboard (WASD / arrows / mouse). No online matchmaking.
- **Progression / monetization:** None — it's a free Flash-era browser game. Ads on the host page.
- **TO BORROW:** Tight, claustrophobic mazes that fit on one screen and resolve in 30–90 seconds — round length is short enough that losing doesn't sting. Random mid-round powerup drops as the source of variety.
- **TO AVOID:** Keyboard-sharing limits player count to 3 and feels cramped — for >2 players we need network multiplayer, not couch-only.

## BZFlag

- **Core mechanic:** First-person (not top-down, but mechanically relevant) tank shooter with capture-the-flag, multiple "super-flag" powerups (cloaking, guided missile, shield, jumping, etc.) picked up from the map.
- **Multiplayer model:** Dedicated servers, public server browser, free open-source client (cross-platform). Long-running community since 1992.
- **Progression / monetization:** None — pure skill ladder, leaderboards per server, no unlocks, no ads. Funded as open-source hobby project.
- **TO BORROW:** Powerup-as-flag-pickup-on-map design — powerups have a visible spawn location players fight over, not a private inventory. Also: server browser + community-hosted servers as a free-hosting strategy.
- **TO AVOID:** Steep learning curve and dated UX killed broad appeal — needs a much friendlier onboarding than "drop into a server browser".

## Diep.io

- **Core mechanic:** Top-down tank with twin-stick aim; kill polygons/players for XP, level up, choose stat upgrades (reload, damage, speed, etc.), evolve into specialized tank classes at level milestones.
- **Multiplayer model:** Browser .io, large open arena (~50 players), authoritative server, no lobby — instant-join.
- **Progression / monetization:** In-match progression (resets on death). Persistent unlocks added later are purely cosmetic (skins). Monetization via ads + cosmetic shop, but the core game is fully playable free.
- **TO BORROW:** In-match level/upgrade tree that resets per round — gives sense of growth without grinding outside the game. Instant-join (no lobby friction) for casual play.
- **TO AVOID:** Snowballing — early leaders dominate because XP advantage compounds. We need catch-up mechanics or shorter round lengths.

## Battle City / Tank 1990 (Namco, 1985)

- **Core mechanic:** Top-down grid, destructible brick walls, indestructible steel walls, water/ice tiles, base to defend ("the eagle"), waves of enemy tanks across 35 levels. Powerups appear on the map (star = upgrade, shovel = wall around base, grenade = clear enemies).
- **Multiplayer model:** Local 2-player co-op alternating or simultaneous.
- **Progression / monetization:** None — arcade-era, lives + score.
- **TO BORROW:** Tile-grid destructibility (brick = breaks, steel = doesn't, bush = cover, water = blocks-but-shoots-through) is a simple and proven legibility model. Base-defense objective gives PvE meaning beyond "kill everything".
- **TO AVOID:** Lives-based fail state — modern players expect respawn, not "game over" after 3 deaths.

## ShellShock Live (kChamp Games)

- **Core mechanic:** Turn-based artillery (Worms-style trajectory aim) with tanks, deformable terrain, hundreds of weapons. Up to 8 players.
- **Multiplayer model:** Online lobbies, matchmaking, persistent account, dedicated servers. Cross-platform.
- **Progression / monetization:** XP per match → levels → unlock weapons. Buyable cosmetics + weapon-XP boosters. Core is unlock-by-play; boosters accelerate but don't gate.
- **TO BORROW:** "Earn weapons by playing, cosmetics are optional decoration" — the exact spirit of our no-money-gating constraint. Weapon mastery (per-weapon XP) gives players a reason to use anything other than the best gun.
- **TO AVOID:** XP-grind weapon unlocks can leave new players underpowered against veterans with a full arsenal. Unlocks must be sidegrades or cosmetic-leaning, not power-creep.

## Bombermine / Bomberman (Hudson Soft)

- **Core mechanic:** Top-down grid, destructible "soft" blocks vs indestructible "hard" blocks, drop bombs with timed explosions in a cross pattern. Powerups (more bombs, longer flame, kick-bomb, speed) appear when soft blocks are destroyed.
- **Multiplayer model:** Bomberman is local; Bombermine (browser fan game) was massively-multiplayer (~1000 players, persistent server). Now defunct.
- **Progression / monetization:** Bombermine: in-match powerups only, no persistent progression, donation-supported. Bomberman proper: arcade/console, no progression.
- **TO BORROW:** Powerups hidden inside destructible walls — destruction and reward are the same action, which makes wall-breaking inherently rewarding (not just movement). Cross-pattern AoE attack is a strong alternative to direct-fire shells.
- **TO AVOID:** Bombermine shut down because one person ran the server out of pocket — single-point-of-failure free hosting is fragile. Plan for community-hosting or zero-cost-at-rest infra.

## Atomic Tanks (atanks, open source)

- **Core mechanic:** 2D side-view turn-based artillery, hundreds of weapons (nukes, dirt-movers, MIRVs, lasers, theme weapons), per-match shop between rounds, deformable terrain.
- **Multiplayer model:** Hot-seat local only. No network play.
- **Progression / monetization:** None — free, GPL'd, no ads.
- **TO BORROW:** Weapon variety as the primary fun-generator — even mundane weapons differ in trajectory, AoE, and side-effect, which keeps "just shooting" interesting for hundreds of rounds.
- **TO AVOID:** Turn-based pacing won't fit our real-time vision; also, hundreds of weapons is overwhelming — curate, don't pile on.

---

## Adjacent / Bonus Mentions (briefer)

### Tanki Online / Tanki X
- 3D MMO tank action with heavy F2P monetization (premium currency, paint-to-win turrets). **Avoid this model entirely** — it's the cautionary tale for our "no money gating, ever" rule.

### Wii Tanks clones (e.g. "Tank Wars" itch.io, "Tanky" web games)
- Numerous open-source / hobby clones exist; they confirm the core loop (top-down + ricochet + powerups) is well-trodden and prototype-able in days. Search "Wii Tanks clone" on GitHub for reference code in Godot, Unity, and Unreal.

### Brigador / Cannon Brawl
- Not tank-shaped exactly, but Brigador's destructible voxel city blocks are a reference for destructibility-as-tactics (cover-creation by demolition).

### slither.io / agar.io
- Same ".io" lineage as Diep.io; relevant for the **instant-join, no-account, no-lobby** UX pattern. A user clicks the URL, types a name, plays in 5 seconds.

### Worms Armageddon
- Turn-based, but the **weapon-variety-as-personality** lesson generalizes. Each weapon has flavor text, sound, and animation that make it feel like a character. Cheap to replicate.

---

## Patterns We Want (distilled, tailored to our vision)

1. **Tile-grid destructibility with legible material rules** — brick breaks, steel doesn't, bush hides — Battle City taught this and players intuit it immediately.
2. **Powerups dropped on the map, contested as terrain** — BZFlag flags / Tank Trouble drops / Bombermine wall-rewards. Pickups create flashpoints; they shouldn't sit in a private inventory.
3. **Short rounds (30–120 seconds), instant respawn or instant new round** — Tank Trouble proves it; losing must not sting.
4. **Small, distinct enemy/tank archetypes (5–8 of them)** — Wii Tanks proves a tiny roster carries huge variety when each behaves differently.
5. **In-match progression that resets per round** — Diep.io's level-and-upgrade tree gives growth-feel without grind, and equalizes new players each match.
6. **Persistent progression is cosmetic + sidegrade only** — ShellShock spirit minus the gated weapons. Skins, emblems, titles, nickname accents = yes. Stat boosts = no.
7. **Mastery tracks per weapon / per tank** — small XP bars per item give reasons to switch loadouts, not just "use the strongest".
8. **Instant-join URL play (.io-style)** — type a name, drop into a match, no account required for casual play; account only when you want persistent unlocks.
9. **Community-hostable servers as the long-term plan** — BZFlag's 30-year lifespan came from letting fans run their own. Designing the protocol so a hobbyist can run a server keeps us alive past any free-tier we choose.
10. **Base / objective modes alongside deathmatch** — Battle City's eagle-defense and BZFlag's CTF show that an objective shifts the fight pattern from "shoot the other guy" to "control space", which deepens replayability cheaply.

## Patterns We Reject

1. **Pay-to-win or pay-for-power** — Tanki Online style. Our constraint is absolute; even "convenience" boosters that sell time should be off the table.
2. **Lives-based game-over** — Battle City era; modern players quit on first hard-fail. Use respawn or short-round replacement.
3. **Long, account-gated onboarding** — no forced signup, email verification, or 5-minute tutorial before the first match.
4. **Snowballing power gaps inside a match** — Diep.io's "first to level 30 wins" failure mode. Catch-up bands, bounty-on-leader, or shorter rounds.
5. **Single-server single-point-of-failure** — Bombermine's lesson. Either community-hostable or cheap-enough-to-run-forever; never both depending on one paid VM.
6. **Hundreds of weapons / endless upgrade trees** — Atomic Tanks fun-but-paralysis. Curate ~15–25 weapons with clear identities.
7. **Steep, jargon-heavy UX** — BZFlag's onboarding. Server browsers, console commands, and acronyms keep players out.
8. **Couch-only multiplayer as the headline feature** — Tank Trouble's ceiling. Local hot-seat is a nice extra for siblings / playtests, not the product.
