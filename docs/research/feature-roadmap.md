# TankGame — Feature Roadmap & Extensibility Plan

Forward-looking design doc. **Not** a status tracker — `PROGRESS.md` owns "what is
done." This captures where the game can go and, more importantly, the handful of
**system abstractions** that make 90% of the wishlist cheap to add later.

Source of the wishlist: owner brainstorm (2026-06-02) — new ammo types, tank
upgrades, smart walls/doors, wormholes, droppables (mines/oil/turrets/sensors),
points of interest (towers/buttons/drone-wave triggers), and LoL-style hide spots.
This doc captures all of those plus adjacent ideas, and maps each to the
architecture work that unlocks it.

---

## 1. The core insight

The wishlist looks like ~40 features. It is really **~9 systems** (the original seven
plus arena generation/theming and progression). Once each system exists, the individual
features become *data + a small behaviour class*, not new engine work. "Add a bouncing shell" should be a 20-line `BouncingBehaviour` + one
row in a weapon table — never a new code path threaded through the scene.

So the roadmap is ordered by **system leverage**, not by feature flashiness. Build
the spawn/entity spine and the weapon-behaviour strategy first; then mines, drones,
turrets, grenades, and bouncing shells are all the same shape.

> **Rule of thumb for every new feature below:** if adding it touches
> `ArenaScene.cs` (the composition root) or needs a new `if` in `Projectile.Step`,
> the abstraction is missing — build the abstraction, not the special case.

---

## 2. Where M1 stands today (the honest baseline)

What exists (pure C# Domain + GameLogic, rendered by Presentation):

- `ITank`, `IProjectile`, `IInputSource`, `IArena` — four interfaces.
- `Tank` (constant-speed mover), `Projectile` (straight line, dies on raycast hit),
  `RectArena` (slab raycast against 4 walls).
- `ArenaScene` is both the play scene **and** the gameplay composition root: it
  hand-spawns the tank view and, on fire input, hand-instantiates each
  `ProjectileView`.

What does **not** exist yet, and blocks the wishlist:

| Missing | Blocks |
|---|---|
| A **world/entity registry** that owns and `Step`s a *collection* of things | every droppable, drone, turret, grenade, mine — anything that isn't the one tank |
| **Spawn/despawn events** the Presentation layer can react to generically | adding any new entity without editing `ArenaScene` by hand |
| **Weapon/projectile behaviour** abstraction (movement + hit response as strategy) | bouncing, zigzag, penetrating, homing, grenade, cluster |
| **Health + damage resolution** (`IDamageable`, a combat step) | *all* combat — tanks currently have no HP and nothing takes damage |
| **Stats with timed modifiers** (`Stats` + `StatusEffect`) | speed boost, shield, bigger/tankier tank, EMP slow, any powerup |
| **Surface normals on raycast hits** | bouncing/ricochet (reflection needs the normal `IArena` doesn't return) |
| A small **game-event bus** | sensors revealing, buttons triggering, wormhole teleport, drone-wave trigger |
| **Vision sources with TTL** (generalising the planned `IVisibilityFilter`) | sensors, lookout towers, hide spots, smoke, drone fog-bypass |
| **Arena generation + theming** (a level *source* + a theme, not a hardcoded text map) | varied/random maps, adjustable size, swappable background & ground, random wall (incl. steel) placement |
| **Progression + match modifiers** (XP/levels, cosmetic unlocks, pre-match rule mods) | cosmetics rewards, "everyone starts with X" effects, extra-traps/mines modifiers, shootable NPC animals that grant level-ups |

None of these are large. They are *foundational* — the difference between a
content pipeline and a pile of special cases.

---

## 3. The foundational systems (S1–S9)

Each is an interface-first addition (per ADR-0003: interfaces land in `Domain`,
impls in `GameLogic`, server mirror in TS `MatchSim`). Sketches are illustrative,
not final signatures — the owning ADR pins each contract.

### S1 — Entity spine: a world that owns and steps many things
The single highest-leverage refactor. Generalise "the scene spawns a tank and some
projectiles" into "the world holds entities; each entity steps itself; spawns and
deaths are events."

```csharp
public interface IEntity { Guid Id { get; } Vector2 Position { get; } bool IsAlive { get; } void Step(float dt); }
public interface IWorld {
    IReadOnlyCollection<IEntity> Entities { get; }
    void Spawn(IEntity e);                 // raises EntitySpawned
    event Action<IEntity> EntitySpawned;   // Presentation maps this → a View
    event Action<IEntity> EntityDespawned; // …and frees the View
    void Step(float dt);                   // steps all, reaps the dead, resolves combat
}
```

Presentation subscribes once: `EntitySpawned → look up a view scene by entity type →
instance + bind`. After this, **no new feature edits `ArenaScene`** to appear on
screen. `Tank` and `Projectile` become `IEntity`s; `ArenaScene` shrinks to "build the
world, build the input, render whatever spawns."

### S2 — Weapon/projectile behaviour (Strategy + data)
Split *what a shot is* (data) from *how it moves and what it does on contact*
(behaviour). A weapon is a data row; a behaviour is a tiny class.

```csharp
public sealed record WeaponDef(float Speed, float Damage, float Lifetime,
    int Pierce, int Bounces, float BlastRadius, ProjectileBehaviourKind Behaviour, ...);
public interface IProjectileBehaviour {           // one per movement/impact style
    void Step(ProjectileState s, IArena arena, IWorld world, float dt);
}
```

`StraightBehaviour` (today's logic), `BouncingBehaviour` (reflect on the hit
**normal**, decrement `Bounces`), `ZigZagBehaviour` (sine offset across travel
axis), `PiercingBehaviour` (pass through up to `Pierce` walls, damaging each),
`HomingBehaviour` (steer toward nearest enemy), `GrenadeBehaviour` (travel
`range` then start a fuse → AoE on expiry), `ClusterBehaviour` (on death, spawn N
child projectiles). New ammo = new row (+ new behaviour only if the motion is
genuinely new).

### S3 — Health & damage resolution
Introduce HP and a single authoritative place where "X damages Y" resolves.

```csharp
public interface IDamageable { float Health { get; } void ApplyDamage(in DamageEvent d); }
public readonly record struct DamageEvent(float Amount, Vector2 Source, DamageKind Kind, Guid? Attacker);
```

`World.Step` runs a combat pass: projectile/blast/mine/spike → overlap query →
`ApplyDamage` → death raises `EntityDespawned`. Everything lethal (ammo, traps,
drones, turrets, crushers) funnels through this one pass. Foundational for *any*
combat — nothing has HP today.

### S4 — Stats + timed status effects
Replace the tank's `readonly float _speed` with a computed stat block and a list of
expiring modifiers.

```csharp
public sealed class Stats { float Base(StatKind k); float Current(StatKind k); }   // base ∘ modifiers
public sealed record StatusEffect(StatKind Stat, float Mult, float AddFlat, float Seconds);
```

Speed boost, over-shield (a temporary `MaxHealth`/damage-absorb modifier), "bigger
tank" (radius + HP + slower as a tradeoff), EMP (speed→0 for N s), incendiary
(damage-over-time effect on a `IDamageable`). Powerups become "apply a
`StatusEffect`," traps become "apply a debuff or a `DamageEvent`."

### S5 — Game-event bus
A tiny publish/subscribe surface so producers (buttons, sensors, deaths, pickups)
don't reach into reactors. Keep it boring and synchronous.

```csharp
public interface IGameEvents { void Publish<T>(in T e); IDisposable Subscribe<T>(Action<T> h); }
```

Drives: button pressed → door opens / drone wave spawns; tank entered wormhole →
teleport; sensor expired → revoke vision; pickup taken → apply effect + telemetry.
Also the natural carrier for the `PositionChanged`-style events the fog-of-war work
(M5b) already assumes.

### S6 — Vision sources with lifetime (generalise `IVisibilityFilter`)
M5b already defines `IVisibilityFilter` (what a viewer can see through a maze). Make
*who/what grants vision* pluggable and time-boxed, so sensors, towers, drones, and
hide spots are all the same concept.

```csharp
public interface IVisionSource { Vector2 Position { get; } float Radius { get; } float SecondsLeft { get; } }
// Tank radius, deployed Sensor (≈10 s), Lookout Tower (large, while occupied),
// Scout Drone (mobile). Per player, visible = ⋃ sources ∪ memory, minus occluders.
```

Hide spots and smoke are **occluders** that *suppress* an entity's visibility to
others (you vanish from radar/drones while inside, LoL-bush style) unless an enemy
vision source overlaps you. Thermal-scope / spotter powerups are the counterplay
(ignore occluders for a few seconds). This keeps the whole stealth meta in one
system instead of scattered booleans.

### S7 — Dynamic terrain features (over `IWallGrid`)
M2 lands `IWallGrid` (static destructible tiles). Layer *active* terrain on top: a
feature that updates per tick and can intercept movement, raycasts, or teleports.

```csharp
public interface ITerrainFeature { void Step(float dt); }   // door, moving wall, wormhole, conveyor, crusher
```

Doors (open on adjacent occupancy or a button event), moving/crusher walls (mutate
`IWallGrid` blocked-cells over time), wormholes (a linked-pair feature that
teleports an entity crossing cell A to cell B), conveyor/ice/mud (modify the mover's
effective velocity in `Tank.Step` via a terrain query). One-way gates and pressure
plates are buttons (S5) wired to features.

### S8 — Arena generation & theming (a level *source* + a theme)
*(Owner ask, 2026-06-04: "different types of maps", random walls **including steel**,
adjustable size, swappable background / ground texture.)* Today `LevelMap.Parse`
turns one hardcoded text literal (`Battlefield01`) into `Materials[x,y]` + `Bushes[x,y]`
+ a spawn. That array form is already the seam: **a generator is just another producer
of the same data.** Make the producer pluggable and add a presentation-side theme.

```csharp
public interface ILevelSource { LevelMap Build(LevelParams p); }   // hand-authored OR procedural
public sealed record LevelParams(int Width, int Height, int Seed, float CoverDensity,
    float SteelRatio, int SpawnsPerTeam, ThemeId Theme);
public sealed record ArenaTheme(/* background colour/image, ground tile texture, wall atlas, fog tint */);
```

- **Random wall placement (incl. steel).** A `ProceduralLevelSource(seed)` scatters
  brick/steel cover by density/ratio, carves connectivity, and reuses the existing
  `LevelMap` validator ("≥X% open floor + every floor cell reachable from every spawn")
  so a generated map is never walled-off. Deterministic from `Seed` — same seed, same
  map — which is also what keeps it server-runnable for M3.
- **Adjustable size.** `LevelParams.Width/Height` flow through. **Constraint to fix
  first:** `ArenaScene` hardcodes `28×16` in the camera-framing math (`ArenaCentre`,
  `TwoPlayerZoom`) and several spawn cells; generalise those to read `LevelMap.Width/
  Height` before sizes can vary.
- **Theming.** An `ArenaTheme` (background colour/image, ground tile texture, wall
  atlas, fog tint) chosen per match/level, applied by the Presentation layer (a ground
  `TileMapLayer` / parallax background under the existing `WallGridView`). Pure visual —
  no gameplay coupling. Placeholder themes are code-built/PIL-generated per the art
  convention until real CC0 art lands.

The `WallGrid`/`IWallGrid` model and the entity spine are unaffected — this is a new
*producer* + a *theme*, not a rules change. **Near-term buildable** (no networking): the
generator, adjustable size, and theming are all local-arc work; only *fair* server-side
generation needs M3.

### S9 — Progression, cosmetics & match modifiers
*(Owner ask, 2026-06-04: damage / kill-death meters, cosmetic rewards, and "cool
effects from everyone" at match start — extra traps/mines, shootable NPC animals that
grant level-ups.)* Three layers, smallest first:

- **Meters (near-term HUD).** Per-tank damage-dealt and kill/death counters ride on the
  existing combat pass: `CombatResolver` already raises `TankKilled`; add a
  `DamageDealt(attackerTeam, amount)` signal and a death signal, tally per player in a
  `MatchStats` block (sibling of `ScoreBoard`), render in the HUD + end-of-round panel.
  No new systems needed — buildable now.
- **Match modifiers (pre-match rules).** A `MatchModifier` set chosen before a match that
  mutates setup: spawn extra mines/traps (S1 deployables + S7), seed NPC `Critter`
  entities (S1 movers) that any tank can shoot for an XP drop, "everyone starts with
  status effect X" (apply a `StatusEffect` to every tank on spawn — already possible via
  `Tank.ApplyEffect`). Each modifier is a small function over `ArenaScene` setup + the
  world; needs S1 (+ S3 for trap damage, S5 events for triggers).
- **Progression & cosmetics (persistent).** An XP/level curve fed by kills, damage, NPC
  drops, and objectives; levels unlock **cosmetics only** (no power — tank skins, turret
  trails, colours) and/or the match modifiers above as togglable "loadout" flair. Needs
  local persistence (the Data layer / SQLite already in the architecture) and, for shared
  unlocks, the M3 server. **Cosmetics-only by mandate — no pay-to-win, no monetization
  psychology.** This is the largest layer and lands post-systems.

NPC animals are ordinary `IEntity`s on the spine (a wandering `Critter` with an
`IInputSource`-style mover and HP via S3); shooting one routes through the same combat
pass and emits an XP event on death — no special case.

---

## 4. The one cross-cutting decision to settle early

**Client (C#) vs server (TS) parity.** From M3 on, the authoritative sim is a
TypeScript `MatchSim` inside a Durable Object; the client predicts in C#. Every
*deterministic* gameplay rule risks being written twice, in two languages, and
drifting. With a rich content catalogue (dozens of ammo/effect rows) this is the
biggest long-term threat to "add features easily."

Recommendation: **data-driven definitions in a shared, language-neutral file**
(weapon/effect/terrain tables as JSON or a `shared/` schema), consumed by both the
C# client and the TS server, so *adding content is adding a data row*, and only
genuinely new *behaviour* needs code in both places — backed by a **shared
test-vector suite** (same inputs → same outputs, run in both CI jobs) to catch
drift. The alternative (dual hand-coded impls) is faster for the first three
features and quietly lethal by the thirtieth. This deserves its own ADR before M5
content work starts in earnest. *Flagged, not decided.*

---

## 5. Feature catalogue

Every wishlist item plus adjacent ideas, tagged with the system that unlocks it and
a rough milestone band. "Band" is indicative; the milestone plan in
`development-plan.md` is authoritative once these are scheduled.

### Ammunition (S2; AoE items also S3)
| Feature | Needs | Band |
|---|---|---|
| Bouncing / ricochet shell | S2 + raycast **normals** | M5-family |
| Zig-zag shell | S2 | M5-family |
| Penetrating / piercing shell | S2 + S3 | M5-family |
| Grenade (lob ahead, fuse, blast) | S2 + S3 (AoE) | M5-family |
| Cluster (splits into shards on death) | S2 (child spawn) | M5-family |
| Homing / guided missile | S2 + target query | post-M5 |
| Sticky bomb (attaches, then detonates) | S2 + S3 | post-M5 |
| EMP shell (disables movement/turret briefly) | S2 + S4 | post-M5 |
| Incendiary (lingering fire tile) | S2 + S3 DoT + S7 | post-M5 |
| Smoke shell (vision-blocking cloud) | S2 + S6 occluder | post-M5 |
| Railgun (charge-up hitscan, pierces) | S2 + charge on weapon | later |

### Tank upgrades & abilities (S4; combat via S3)
| Feature | Needs | Band |
|---|---|---|
| Speed boost | S4 | M5-family |
| Shield / over-shield (absorb) | S4 + S3 | M5-family |
| More HP / heavier hull | S4 + S3 | M5-family |
| Bigger tank (size↑, HP↑, speed↓ tradeoff) | S4 + entity radius | M5-family |
| Active camo (timed invisibility) | S4 + S6 | post-M5 |
| Dash / boost ability (+ram damage) | S4 + S3 | post-M5 |
| Repair kit (heal over time) | S4 + S3 | post-M5 |
| Radar ping (briefly reveal enemies) | S6 | post-M5 |

### Droppables / deployables (S1 spawn + relevant system)
| Feature | Needs | Band |
|---|---|---|
| Mine (proximity) | S1 + S3 | M5-family |
| Oil slick (slow/slip zone) | S1 + S7 terrain effect | M5-family |
| Remote-detonated mine | S1 + S5 | post-M5 |
| Deployable turret (limited ammo/life) | S1 + S2 + S3 | post-M5 |
| Sensor (≈10 s area reveal, fog bypass) | S1 + S6 | post-M5 |
| Decoy (fake radar blip) | S1 + S6 | later |
| Jammer (suppresses enemy sensors/radar in radius) | S1 + S6 | later |
| Deployable barricade / cover | S1 + S7 | later |

### Smart terrain & hazards (S7; triggers via S5)
| Feature | Needs | Band |
|---|---|---|
| Solid (steel) vs destructible (brick) walls | M2 `IWallGrid` | M2 (planned) |
| Motion-controlled doors (open on approach) | S7 + S5 | post-M5 |
| Button-triggered doors / one-way gates | S5 + S7 | post-M5 |
| Moving walls | S7 | post-M5 |
| Wormholes (linked teleport pair) | S7 + S1 | post-M5 |
| Conveyor belts / ice / mud (velocity mods) | S7 | later |
| Crusher walls / spike traps (lethal) | S7 + S3 | later |
| Explosive barrels (chain reactions) | S1 + S3 AoE | later |
| Collapsing floor / breakable bridge | S7 + S3 | later |

### Points of interest & objectives (S5 + S6)
| Feature | Needs | Band |
|---|---|---|
| Trap-activation buttons | S5 | post-M5 |
| Lookout tower (large vision while occupied) | S6 + S5 | post-M5 |
| Drone-wave trigger | S5 + drones | post-M5 |
| Capture point / king-of-the-hill | S5 + mode logic | later |
| Capture-the-flag | S5 + carry state | later |
| Destructible base/core (MOBA-ish) | S3 + objective | later |
| In-match scrap economy (salvage → deploy) | S1 + S3 + S5 | later |

### Drones (S1 mover + S2/S3/S6)
| Feature | Needs | Band |
|---|---|---|
| Scout drone (mobile sensor) | S1 + S6 | post-M5 |
| Attack-drone wave | S1 + S2 + S3 + simple AI | post-M5 |
| Supply drone (airdrops a powerup) | S1 + S5 | later |

### Vision & stealth meta (S6) — already anchored by M5b
| Feature | Needs | Band |
|---|---|---|
| Limited Vision / fog of war | `IVisibilityFilter` | M5b (proposed) |
| Hide spots / bushes (LoL-style) | S6 occluder | post-M5 |
| Smoke clouds | S6 occluder + S2 | post-M5 |
| Thermal scope / spotter (see through cover) | S6 counter-occluder | later |
| Sound-based detection (firing pings you) | S5 + S6 | later |

### Maps, generation & theming (S8)
| Feature | Needs | Band |
|---|---|---|
| Random wall placement (brick **and** steel), validated connectivity | S8 generator + existing `LevelMap` validator | local-arc |
| Adjustable map size | S8 + generalise `ArenaScene`'s 28×16 framing | local-arc |
| Swappable background / ground texture (themes) | S8 theme (Presentation) | local-arc |
| Multiple authored map layouts | S8 (more `ILevelSource`s) | local-arc |
| Biome/theme variety (desert, snow, arena, ruins) | S8 theme + art | post-M5 |
| Seeded/shareable maps (same seed → same map) | S8 deterministic gen | post-M5 |

### Progression, meters & match modifiers (S9)
| Feature | Needs | Band |
|---|---|---|
| Damage-dealt meter | combat-pass damage signal + HUD | local-arc |
| Kill / death meter | `TankKilled` + death signal + HUD | local-arc |
| "Everyone starts with effect X" modifier | `Tank.ApplyEffect` on spawn | local-arc |
| Extra traps / mines match modifier | S1 deployables + S7 (+ S3 damage) | post-M5 |
| Shootable NPC animals → XP drops | S1 mover entity + S3 + XP event | post-M5 |
| XP / levels | S9 progression curve + Data persistence | post-M5 |
| Cosmetic unlocks (skins, trails, colours — **no power**) | S9 + Data (+ M3 for shared) | post-M5 / later |

### Game modes (sit on the systems above)
Deathmatch · co-op survival vs drone waves · CTF · control point · payload escort ·
PvE "clear the maze." Each is a thin rules layer once S1/S3/S5/S6 exist.

---

## 6. Suggested sequencing

The content milestones already exist (M5 powerups/traps/enemies; M5b limited
vision). What's new here is **inserting the system foundations** so the content is
cheap. Recommended shape:

1. **Fold S1 (entity spine) + S3 (health/damage) into the run-up to M5.** They are
   prerequisites for *any* powerup, trap, or enemy — M5's own definition of done
   (shield, mine, oil, turret enemy) silently assumes both. Make them explicit
   tickets, ideally an "M4.5 / systems foundation" slice or the first wave of M5.
2. **S2 (weapon behaviour) + S4 (stats) land with the first M5 content** — the
   3 powerups and the grenade/bouncing ammo are the proof those abstractions work.
3. **S5 (events) + S7 (terrain features)** unlock the doors/buttons/wormholes/drone
   triggers wave — a natural **M5c** ("active arena") after M5/M5b.
4. **S6 (vision sources)** extends the M5b `IVisibilityFilter` into sensors, towers,
   hide spots, drones — an **M5d** ("vision & stealth") riding on M5b.
5. **The parity decision (§4)** gets its ADR **before** the catalogue grows past a
   handful of rows — cheapest to fix while the content table is short.

Everything tagged "later" is genuinely post-M8 content that becomes near-free once
S1–S9 exist — that's the whole point of front-loading the systems.

**S8 (arena gen/theming) and S9 (progression/meters/modifiers)** were added 2026-06-04
from an owner ask. Their *near-term* slices fit the current local-arc (the procedural/
sized/themed map and the damage + K/D meters need no networking); the deeper progression
layer (XP, cosmetic unlocks, NPC-animal XP, trap/mine modifiers) is post-systems content
that rides S1/S3/S5/S7 and the Data layer. Sequencing: tackle the **map system (S8)** as
its own slice when the owner wants map variety, and the **meters** as a quick HUD win;
defer the full progression/cosmetics build until after the combat systems (S2/S3) land.

---

## 7. ADRs to write (when each system is scheduled)

- **ADR — entity/world model & spawn-event spine** (S1). *Largest leverage; write first.*
  ✅ Accepted: [`0010-entity-spine.md`](../adr/0010-entity-spine.md) (interface contracts,
  the ArenaScene migration, and the as-built S1-T1..T6 record). Slice complete.
- **ADR — weapon definition & projectile-behaviour strategy** (S2), incl. adding
  surface normals to `RaycastHit`.
- **ADR — health/damage resolution & the combat step** (S3).
- **ADR — stats & status-effect model** (S4).
- **ADR — client/server gameplay parity: data-driven defs + shared test vectors**
  (§4). *Write before M5 content.*
- **ADR — dynamic terrain features over `IWallGrid`** (S7).
- **ADR — arena generation (`ILevelSource`, procedural + validator) & theming** (S8).
  *Write before the first procedural/themed map ticket; settle the `LevelParams`/`ArenaTheme`
  contracts and the `ArenaScene` size-generalisation.*
- **ADR — progression, XP/levels & cosmetic-only unlocks + match modifiers** (S9).
  *Cosmetics-only is a hard constraint; pin where XP is earned and that nothing unlocks power.*
- Vision is already covered by the fog-of-war proposal (`docs/adr/PROPOSAL-fog-of-war.md`);
  extend it (or a sibling ADR) to cover vision **sources** and **occluders** (S6).

---

*This roadmap is aspirational. Nothing here is committed until it appears as
scheduled tickets in `development-plan.md` and as status in `PROGRESS.md`.*
