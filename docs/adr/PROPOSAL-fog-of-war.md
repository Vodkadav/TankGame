# PROPOSAL — Fog of War / Limited Vision Mode

**Filename:** `docs/adr/PROPOSAL-fog-of-war.md`

**Date:** 2026-05-23
**Status:** Proposed (un-numbered; promotes to next free ADR number at M5b execution — ADR-0001..0009 are reserved by `docs/research/development-plan.md` for M0..M7)
**Deciders:** Solo developer + Claude Code (architect agent consulted)

## Context

TankGame is a Godot 4.6 .NET top-down tank-labyrinth game (Battle City lineage, destructible-walls maze, server-authoritative real-time multiplayer via Cloudflare Durable Objects at 20 Hz — see `docs/research/decisions.md` Decisions 1, 3, 6 and `docs/research/development-plan.md` M3 §lines 200–237). The Battle City baseline assumes **full-map awareness**: every tank, wall, and projectile is visible to every player at all times. That baseline is good for puzzle-style tactical play but flattens the labyrinth — corridors stop feeling like corridors when you can already see what is around the next corner.

The gameplay problem this proposal addresses:
- A destructible-walls labyrinth invites **ambush, sneak, and hunt** tactics that full-map awareness erases.
- Atmosphere and tension scale with what the player **cannot** see; a Diablo-style "explored but not currently visible" memory layer is a well-understood pattern for keeping a maze learnable while still hiding live threats.
- The current powerup design slot (M5, see `development-plan.md` §lines 262–280) leaves room for asymmetric vision-class pickups (e.g. "thermal scope", flare) that only make sense if a baseline vision constraint exists for them to break.

Acknowledged design pivot: this is a **deliberate departure** from the Battle City reference. We do not change the default mode — full visibility remains the lobby default — but we open a second supported mode in which vision is the central constraint. Treating it as opt-in keeps the existing player expectation intact and bounds the scope of the cross-cutting changes (rendering, networking, UI).

Forces at play:
- Multiplayer fairness is non-negotiable per `decisions.md` Decision 3 (authoritative server) and Decision 14 (server-side input clamps + server-only XP). Any vision system that only restricts the client renderer is by construction cheatable.
- Server CPU budget on a free-tier Cloudflare Durable Object is thin (30 ms CPU per request; see Decision 6). Per-player snapshot filtering increases per-tick work and must be justified against that budget.
- Android target (Galaxy A56, see `tech-stack.md`) — the rendering technique must be cheap on a mobile GPU. Godot 4.6's `Light2D` + `CanvasModulate` cover the visuals natively.

## Decision

We will add an **opt-in Limited Vision mode**, selected at lobby creation via a boolean lobby flag (`limited_vision: bool`, default `false`), in which each player sees:

- A **circular bright region of ~2.5-tile radius** centred on their tank, fully lit.
- A **~1-tile dim penumbra** beyond the bright region (gradient falloff).
- **Opaque darkness** beyond the penumbra.
- All walls inside the bright region are fully lit (no self-shadowing of geometry the player is standing next to).
- A **desaturated "memory" layer** that preserves the last-seen state of any tile the player has previously had inside their vision radius (Diablo-style fog of war). Damaged-wall states, traps placed, and powerup locations all persist in the memory layer at the resolution they were last observed. Live entities (enemy tanks, in-flight projectiles) do **not** appear in the memory layer — only static and semi-static terrain state.

The rendering technique is `Light2D` + `CanvasModulate` for the live vision cone, with a separate `RenderTexture`-backed memory layer for the desaturated last-seen state. [Decision deferred to M5b execution: exact shader/blend pipeline and memory-layer resolution.]

**The multiplayer-fairness model is the load-bearing part of this decision and is specified in the next section as a non-negotiable constraint.**

### Multiplayer fairness (mandatory, non-negotiable)

Per `decisions.md` Decision 3 (authoritative server with client prediction + reconciliation) and Decision 14 (server-side validation as the only trustworthy boundary), **visibility filtering is a server-side concern**. The `MatchSim` running inside the Durable Object (see `development-plan.md` M3-T5) **must emit per-player culled snapshots**:

- For each player on each tick, the server computes that player's current vision radius (and recent memory footprint) over the wall grid.
- Enemy tank state, in-flight projectile state, and any other "live" entity state is **included in that player's snapshot only if** the entity occupies a cell currently inside the player's vision radius (or, for last-known-position memory of live entities, [decision deferred to M5b execution: do we leak last-known-position of enemies into the memory layer at all, or only static terrain?]).
- Static terrain state (walls, damage levels, placed traps, powerup spawns) is included if the cell is in the player's vision radius **or** in that player's memory layer.
- The client receives only the culled snapshot. **Packet inspection of the WebSocket stream cannot reveal entity state that the server did not include**, because that state was never sent on the wire. A modified client cannot "un-fog" the map; there is nothing to un-fog.

This constraint is non-negotiable. Any implementation that does the visibility check only on the client side is rejected by this ADR. Any future optimisation that ships pre-filtered data to multiple players (e.g. broadcasting a single canonical snapshot and letting clients mask) must preserve the same property — that no client ever receives entity state for cells it cannot see — and must be re-reviewed by @architect before merge.

A consequence: the M3 snapshot-delta path (M3-T5) must be extended so that the per-player snapshot is a function of `(world state, player visibility)` rather than `(world state)` alone. Snapshot delta computation becomes per-player rather than broadcast-shared. This is the primary CPU cost we are accepting.

## Alternatives considered

| Alternative | Why rejected (or kept) |
|---|---|
| (a) Client-side mask only — apply the fog purely as a render-time overlay; server still broadcasts the full world state to every client. | **REJECTED.** Cheatable by definition: any packet-inspecting client, mod, or custom Godot build can read the un-fogged entity state directly off the wire. Violates `decisions.md` Decision 3 and Decision 14. Fails the "trolls in public lobbies" test even at hobby scale. |
| (b) Hard fog with no memory layer — beyond the live vision radius, everything is pitch black; nothing is remembered. | **REJECTED.** Makes the labyrinth unreadable. In a destructible-walls game, players need to remember "I damaged this wall on the last circuit" — without a memory layer, the maze becomes a guessing game rather than a tactical space. Atmosphere gain is not worth the loss of the labyrinth-learning loop that the destructible-walls mechanic was designed around (M2). |
| (c) `Light2D` + `CanvasModulate` only — use Godot's built-in 2D lighting to produce the vision effect and treat that as the whole solution. | **KEPT as the rendering technique**, explicitly noted as **not** addressing multiplayer fairness. The rendering layer produces the visuals; the server-side culling produces the fairness. Both are required; neither replaces the other. This row exists in the table to make the separation explicit, not to propose `Light2D` as a fairness mechanism. |
| Status quo (no change) — ship Battle City baseline full-map visibility only. | Acceptable for MVP; the proposal is for an **additional opt-in mode**, not a replacement. Not rejected — this is the default behaviour preserved by the `limited_vision: false` default. |

## Consequences

**Positive:**
- Atmosphere and tension in the labyrinth: corridors feel like corridors; ambush is possible.
- Enables a class of vision-asymmetric powerups (thermal scope, flare, periscope) that have no design space under full visibility.
- Strengthens the destructible-walls loop: damaging a wall to peek through it becomes a tactical choice rather than a cosmetic one.
- The memory layer keeps the maze learnable across a round, preserving the "I remember where the powerups spawned" loop.
- Lobby flag means the mode is additive: existing match expectations are unchanged for players who do not opt in.

**Negative / trade-offs:**
- **Server snapshot cost increases per player.** Snapshot delta is no longer a single broadcast computation — it is N per-player computations per tick, each gated on a visibility check against the wall grid. CPU cost on the Durable Object scales with `players × visible_entities × tick_rate`.
- **Spectator UX is harder.** A spectator either gets one player's view (loses the global picture), all players' views unioned (information leak that defeats the fairness goal if any spectated player is still alive in the match), or an omniscient view (only acceptable for post-match playback). [Decision deferred to M5b execution: spectator policy.]
- Some players will dislike reduced map awareness as a matter of taste. The opt-in lobby flag mitigates: no player is forced into the mode.
- Increased client rendering work for the live light cone + memory-layer composition. Must be validated against the 60 fps Android baseline.
- The per-player snapshot path complicates client prediction + reconciliation (M3-T7): a tank that the local prediction "saw" briefly may be culled out of the next snapshot, producing a reconciliation pop ("ghost tank" disappearing).

**Risks and mitigations:**
- **Risk:** Snapshot CPU on the DO exceeds the 30 ms-per-request budget under 4-player matches with the visibility filter in the hot path → mitigation: per-snapshot benchmark gate added to CI before M5b ships; if the gate fails, simplify the visibility check (e.g. grid-coarsened radius test, cached per-tick visibility bitmask per player) before raising the request budget.
- **Risk:** Latency-sensitive "ghost tank" reconciliation — a predicted enemy position is shown locally for a frame, then the next snapshot culls that entity and the tank pops out of existence on the client → mitigation: brief client-side fade-out animation on cull, sourced from the prediction layer rather than the snapshot layer; [decision deferred to M5b execution: exact fade duration and whether last-known-position lingers in the memory layer for live entities].
- **Risk:** Information leakage via timing side channels (e.g. snapshot size correlating with how many enemies are near a player) → mitigation: snapshot size padding policy [decision deferred to M5b execution].
- **Risk:** Mode splits the playerbase across two queues (full-visibility vs limited-vision lobbies). At hobby scale this could leave one queue empty → mitigation: ship as a lobby flag (not a separate matchmaking queue) so the mode is a per-match decision, not a per-player matchmaking preference.

## References

- Related ADRs: forward-link to `docs/adr/0005-authoritative-server-on-durable-objects.md` (M3-T10 — establishes the authoritative-server model this proposal extends), forward-link to the eventual M5b ADR that supersedes this proposal when promoted to a numbered ADR.
- Related plans: `docs/research/development-plan.md` — M3 (§lines 200–237, authoritative server + snapshot path that the per-player culling extends), M5 (§lines 262–280, powerup baseline that vision-asymmetric powerups build on), M5b block (drafted in parallel as Slice S2 of `.claude/plans/20260523-fog-of-war-design.md`).
- Related decisions: `docs/research/decisions.md` Decision 3 (authoritative server), Decision 6 (Durable Object + 30 ms CPU/req constraint), Decision 14 (server-side validation as the cheat boundary), Decision 26 (layered architecture — `IVisibilityFilter` will live in Domain per the interface-first principle).
- Engine references: Godot 4.6 `Light2D`, `CanvasModulate`, `SubViewport` / `RenderTexture` (see `docs/research/tech-stack.md` for the engine + Android constraints).
- Brief: `.claude/plans/20260523-fog-of-war-design.md` Slice S1.
