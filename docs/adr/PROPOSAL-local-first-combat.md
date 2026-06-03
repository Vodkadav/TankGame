# PROPOSAL — Local-first combat & controllers (defer networked M3)

**Filename:** `docs/adr/PROPOSAL-local-first-combat.md`

**Date:** 2026-06-03
**Status:** Proposed (un-numbered; promotes to the next free ADR at arc completion)
**Deciders:** Solo developer + Claude Code (architect agent)

## Context

M2 shipped a playable, destructible maze (drive, shoot, shove walls). The developer playtested
it and chose to grow the **game** before the **network**: first a single-player mode against
computer-controlled adversaries, then local same-device multiplayer (Player 1 on WASD + Space,
Player 2 on arrow keys + left mouse), and only later real-time networked play. The development
plan's M3 (2-player over a Cloudflare Durable Object) is therefore **deferred**, and a
**local-first combat arc** is inserted before it.

The hard constraint the developer set: *do not wire anything that makes networked multiplayer
hard to add later.*

## Decision

### 1. `IInputSource` is the universal "intent" seam

Every tank already advances from an injected `IInputSource` (move / aim / fire intent). We
keep that as the **single** way any agent drives a tank:

| Driver | Input source |
|---|---|
| Local player 1 | keyboard (WASD + Space) — exists today |
| Local player 2 | keyboard + mouse (arrows + left click) — new |
| Computer adversary | an AI controller that reads world state and emits intent — new |
| **Future** network player | a source fed by the wire / replaced by server authority |

This is the project's "client sends intent, server resolves outcome" rule made concrete: a
networked player is just another intent source (or a server-side resolver), so adding the
network later does not disturb tanks, combat, or rendering.

### 2. Combat is a deterministic GameLogic pass — server-runnable, no Godot, no network

Health, damage, teams, and projectile↔tank hit resolution live in **GameLogic**, operating on
the `World`'s entities. It is pure, deterministic, and engine-free, so the **same** code runs
as client prediction today and as server authority later. ADR-0010 anticipated this: "the
world's reap step is where S3's combat pass slots in." Combat runs inside the world's
step→resolve→reap cycle so dead tanks and spent shots are reaped uniformly.

### 3. More tanks are just more entities

The S1 entity spine (ADR-0010) already owns N entities with spawn/despawn events and a
type-switch view factory. A second player, or five AI enemies, are entities spawned into the
world — no `ArenaScene` surgery, one view arm reused. Tanks gain a **Team** so shots damage
opponents, not the shooter.

### 4. What stays out (so the seam stays clean)

No networking, no lobby, no reconciliation, no Durable Object. No Godot in combat/AI. AI is an
`IInputSource`, never a privileged path into the simulation. Health/teams are plain data on
the entity, not tied to any transport.

## Ticket breakdown (TDD, each a shippable slice)

| ID | Slice | Notes |
|---|---|---|
| **C1** | Tank health + death | `ITank`/`Tank` gain `MaxHp`/`Hp`; `IsAlive` becomes HP-driven (resolves the S1 stub); `TakeDamage`. Dead tank → reaped by the world. |
| **C2** | Teams + projectile ownership | `Team` on tank; `Projectile` carries owner team + damage; a shot never hurts its own team. |
| **C3** | Projectile↔tank combat pass | A deterministic combat resolver in the world's step cycle: overlapping shot vs enemy tank → damage + shot dies. Dead tanks despawn (view freed via existing event path). |
| **C4** | Computer adversaries | `AiInputSource` (GameLogic) emits intent toward the nearest enemy (seek / aim / fire); spawn enemy tanks on the enemy team. **Single-player vs AI playable.** |
| **C5** | Local 2-player | A second `IInputSource` (arrows + left mouse); spawn P2 on its own team. **Couch versus playable.** |
| **C6** | HUD + round flow + ADR | Health bars / win-lose text (EN/ES/DK), tidy spawn placement; promote this proposal to a numbered ADR. |

C1→C3 are the combat foundation both modes need; C4 (AI) is sequenced before C5 (local-2P)
per the developer's stated priority (single-player first).

## Consequences

**Easier:** a real game loop (enemies, winning, losing) on the existing spine; AI and players
share one intent path; combat is unit-testable with no engine and reusable server-side.

**Harder / costs:** combat introduces inter-entity interaction (the world gains a resolve
pass), the first logic that is not strictly per-entity. Tank↔tank body collision and richer AI
(pathfinding through the maze) are explicitly *later* — C4 ships line-of-sight-grade AI first.

**Defers / revisit:** networked M3 returns after this arc; when it does, the intent seam +
deterministic GameLogic sim are what make it tractable. The client/server parity ADR (before
M5 content) still governs how the server reuses this combat code.
