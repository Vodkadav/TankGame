# ADR-0019: Host-authoritative netcode over a relay (supersedes ADR-0005's server sim)

**Date:** 2026-06-08
**Status:** Accepted (2026-06-08)
**Deciders:** Solo developer + Claude Code

Numbered 0019, the next free number after 0018.

## Context

Multiplayer is networked (players join a remote match), not local same-machine. The M3 milestone already
built and deployed most of the plumbing for this: a Cloudflare **Durable Object per match**
(`MatchRoom`), a **lobby-code directory** (`POST /lobby` mints a 6-char code, `POST /lobby/:code/join`
validates it, KV maps code→room), a binary **wire protocol** mirrored in C# and TypeScript, a
**WebSocket transport** (`IMatchTransport`), and **client-side prediction/reconciliation**
(`PredictedTank`). ADR-0005 chose a **server-authoritative** model: the DO runs the match sim
(`server/worker/src/sim/matchSim.ts`).

The cost of server-authority has become the dominant concern. The server sim is a **second, full
implementation of the game in TypeScript** that must mirror every C# gameplay rule byte-for-byte. Today
it is a bare MVP (basic move/fire on the old `Battlefield01`); it knows nothing of the features built
since — persist-until-death pickups, the carpet-bomb airstrike, real terrain, 8-HP wounded tanks,
elevated zones, and everything to come. Keeping a TS twin in lockstep with the C# game forever, for a
solo developer making a friends-and-family game, is a permanent tax that dwarfs the netcode itself.

## Decision

Adopt a **host-authoritative** model: one player's client is the authority and runs the **real C#
`World` / GameLogic** (the exact same code as single-player); the Cloudflare room is demoted to a pure
**relay + lobby directory**. There is no second implementation of the game.

### Roles

- **Host (slot 0):** runs the authoritative `World`. It feeds its own local input and each guest's
  relayed `InputFrame` into that guest's `IInputSource`, steps the world, and broadcasts `SnapshotFrame`s.
  Because the authority is the shipping game code, every current and future feature works in multiplayer
  with no porting.
- **Guest (slot 1+):** sends `InputFrame`s up and applies `SnapshotFrame`s with the existing prediction
  and reconciliation (`PredictedTank`). Unchanged from the current client design.
- **Relay (the Durable Object):** assigns slots, sends each client a `WelcomeFrame` with its slot, and
  forwards bytes — guest inputs to the host, host snapshots to the guests. It no longer simulates. It
  keeps the hibernation API (idle rooms cost nothing) and the lobby-code routes.

### Join UX

- **Lobby codes now.** Host presses Host → `POST /lobby` returns a 6-char code shown on screen to share;
  the host connects to `/room/:code` as slot 0. A friend presses Join, types the code →
  `POST /lobby/:code/join` validates → connects as slot 1. Cross-platform (desktop + Android), no
  account or sign-in.
- **Shareable link later, additively.** If a web build (Godot HTML5 export) is ever shipped, a link
  carrying the code (`…/?lobby=ABC123`) can auto-join in the browser. On native Android/desktop a link
  needs per-OS deep-link registration, so it is a follow-on on top of codes, not the first step.

### What is kept from ADR-0005

The lobby-code directory, the KV mapping, the binary protocol and its two-language byte-vector parity
test (the host and guest are both C#, but the TS codec stays the relay's contract), the hibernation
reliance, and the stay-free request-budget alarm. **What changes:** the DO stops running `matchSim.ts`
(retired) and becomes the relay it originally was; authority moves to the host client.

## Alternatives considered

- **Keep server-authoritative (ADR-0005 as built).** Best anti-cheat — no player can tamper. Rejected
  for this project: it requires porting and then forever-maintaining a TypeScript mirror of all gameplay,
  written twice and kept identical. Unjustifiable maintenance for a friends-and-family game.
- **Lockstep / deterministic P2P** (all clients run the same sim on shared inputs, no authority). No
  server sim and the GameLogic is already deterministic, but it demands strict cross-platform determinism
  (floating-point parity) and an input-delay model, and degrades badly with one slow/dropped peer.
  Heavier and more fragile than a relay with one authority.

## Consequences

- **Positive:** reuses nearly all of the deployed M3 plumbing (lobby, DO, transport, protocol,
  prediction); **no second game to maintain**, so multiplayer fidelity tracks single-player for free;
  the relay is simpler and cheaper than a sim (less compute → easier to stay free-tier). Players join a
  remote match by sharing a short code.
- **Negative / trade-off:** the host is **trusted** — a tampered host could cheat, and the host carries a
  slight latency/availability edge (if the host leaves, the match ends and players re-host). Acceptable
  for the target audience; revisit only if the game ever needs fair ranked play among strangers.
- **Anti-cheat:** weaker than server-authority by design. The relay still rate-limits lobby creation
  (the budget alarm), so the free-tier protection is unchanged.

## Rollout (each step a squash-PR; multiplayer renders in the 3D arena)

1. **Relay mode on the DO** — revert `MatchRoom` from the MVP sim to slot-assignment + frame relay;
   retire `matchSim.ts`. Worker (Vitest/Miniflare) tests for two-socket relay + slot/welcome.
2. **Host/Join lobby UX** — client `Host` (creates a lobby, shows the code) and `Join` (enters a code)
   flows over the existing lobby routes; replaces the old hardcoded "TEST01" seam. Wire the title's
   currently-disabled **Team vs Team** to this.
3. **Host authority loop** — a C# `HostSession` that runs the real `World` from local + relayed inputs
   and broadcasts snapshots; guest applies them via `PredictedTank`. Rendered in a 3D net scene
   (`NetArena3DScene`, the 3D port of `NetArenaScene`).
4. **Protocol fidelity** — extend `TankState`/snapshots as needed to carry current gameplay (turret aim,
   HP up to max, shield, projectile style, layer) so the guest's view matches the host's.
5. **Two-device playtest** — owner-gated (the developer half of going live).
