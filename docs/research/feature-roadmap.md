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

The wishlist looks like ~40 features. It is really **~7 systems**. Once each system
exists, the individual features become *data + a small behaviour class*, not new
engine work. "Add a bouncing shell" should be a 20-line `BouncingBehaviour` + one
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

None of these are large. They are *foundational* — the difference between a
content pipeline and a pile of special cases.

---

## 3. The seven foundational systems

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
content work starts in earnest. _Flagged, not decided._

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
S1–S7 exist — that's the whole point of front-loading the systems.

---

## 7. ADRs to write (when each system is scheduled)

- **ADR — entity/world model & spawn-event spine** (S1). *Largest leverage; write first.*
- **ADR — weapon definition & projectile-behaviour strategy** (S2), incl. adding
  surface normals to `RaycastHit`.
- **ADR — health/damage resolution & the combat step** (S3).
- **ADR — stats & status-effect model** (S4).
- **ADR — client/server gameplay parity: data-driven defs + shared test vectors**
  (§4). *Write before M5 content.*
- **ADR — dynamic terrain features over `IWallGrid`** (S7).
- Vision is already covered by the fog-of-war proposal (`docs/adr/PROPOSAL-fog-of-war.md`);
  extend it (or a sibling ADR) to cover vision **sources** and **occluders** (S6).

---

_This roadmap is aspirational. Nothing here is committed until it appears as
scheduled tickets in `development-plan.md` and as status in `PROGRESS.md`._
