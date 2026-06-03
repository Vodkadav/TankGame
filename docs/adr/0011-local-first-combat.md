# ADR-0011: Local-first combat & controllers (single-player vs AI + local 2-player)

**Date:** 2026-06-03
**Status:** Accepted
**Deciders:** Solo developer + Claude Code (architect agent)

Promoted from `docs/adr/PROPOSAL-local-first-combat.md` after the arc landed (tickets
C1–C6 plus C5a/b/c). Numbered 0011 because ADR-0005…0009 are reserved by name for M3…M7 in
`docs/research/development-plan.md` and 0010 is the entity spine; 0011 is the next free number.

## Context

M2 shipped a playable, destructible maze. After playtesting it, the developer chose to grow
the **game** before the **network**: first single-player against computer adversaries, then
local same-device 2-player (P1 on WASD + Space, P2 on arrow keys + left-click), and only later
real-time networked play. The development plan's M3 (2-player over a Cloudflare Durable Object)
was therefore deferred. The hard constraint: *do not wire anything that makes networked
multiplayer hard to add later.*

## Decision

### 1. `IInputSource` is the universal "intent" seam

Every tank advances from an injected `IInputSource` (move / aim / fire intent). That stays the
single way any agent drives a tank:

| Driver | Source |
|---|---|
| Player 1 | `KeyboardMouseInputSource` — WASD + mouse-aim + space (and left-click in one-player) |
| Player 2 | `Player2InputSource` — arrow keys, turret aims where it drives, left-click/Enter to fire |
| Computer adversary | `AiInputSource` — reads world state, emits intent toward the nearest enemy |
| **Future** network player | a source fed by the wire, or replaced by server authority |

This is "client sends intent, server resolves outcome" made concrete: a networked player is
just another intent source, so adding the network later does not disturb tanks, combat, or
rendering. `KeyboardMouseInputSource` grew a `fireOnClick` flag so the left mouse button can
belong to Player 2 in two-player without firing Player 1.

### 2. Combat is a deterministic GameLogic pass — server-runnable, no Godot, no network

Health, teams, and projectile↔tank resolution live in GameLogic:

- **`IDamageable`** (`Hp`/`MaxHp`/`TakeDamage`); `ITank` extends it. `Tank.IsAlive` is now
  HP-driven, resolving the S1 always-true stub (ADR-0010).
- **Teams**: `Team` on `ITank` and `IProjectile`; a tank stamps its team on every shot.
- **`ICombatResolver`** / **`CombatResolver`**: each step the `World` runs the resolver between
  advancing entities and reaping the dead (the seam ADR-0010 anticipated). Every live shot
  overlapping an enemy tank damages it and is spent; same-team shots pass through. Killed tanks
  and spent shots are reaped uniformly.
- **`MatchTracker`**: a last-team-standing rule — decided once at most one team has a live tank.

All of it is pure and deterministic, so the same passes can run as client prediction now and
server authority later.

### 3. More tanks and game modes are just composition

The S1 entity spine (ADR-0010) already owns N entities with spawn/despawn events and a
type-switch view factory, so a second player or three AI enemies are entities spawned into the
world — no `ArenaScene` surgery, the same view arm reused. `GameMode`
(OnePlayer / TwoPlayerCoop / TwoPlayerVersus), carried from the title screen via the
`GameSetup` static, selects who spawns: P1 always; P2 on the player team (co-op) or enemy team
(versus); AI in every mode but versus. One-player follows the player with the camera;
two-player frames the whole maze so both tanks stay on screen. Adversaries are tinted red, and
every tank shows a health bar.

### 4. What stayed out (so the seam stays clean)

No networking, no lobby, no reconciliation, no Durable Object. No Godot in combat or AI. The AI
is an `IInputSource`, never a privileged path into the simulation. Health and teams are plain
data on the entity, tied to no transport. AI is line-of-sight-grade (seek / aim / fire); it
bulldozes brick walls in its path (reusing push-to-demolish) and is stopped by steel — maze
pathfinding is deliberately later.

## Consequences

**Easier:** a real game loop (enemies, winning, losing, restart) and couch co-op/versus on the
existing spine; AI and humans share one intent path; combat is unit-testable with no engine and
reusable server-side; new modes are composition, not new scenes.

**Harder / costs:** combat introduced the first inter-entity interaction (the world's resolve
pass), and two-player added a `GameSetup` static to carry the mode across a scene load. Tank↔tank
body collision and maze-aware AI pathfinding are not built.

**Forecloses / defers:** networked M3 returns after this arc; the intent seam and deterministic
GameLogic combat are what keep it tractable, and the client/server parity ADR (before M5
content) still governs how the server reuses this code. A dust/particle effect on demolition and
distinct enemy sprites are deferred to the art pass.

**Revisit triggers:** maze-aware AI (replace straight-line seek with pathfinding); a richer match
flow (rounds, scores, back-to-menu); and the networked-play ADR, which decides whether the server
mirrors `World`/`CombatResolver` or diverges.
