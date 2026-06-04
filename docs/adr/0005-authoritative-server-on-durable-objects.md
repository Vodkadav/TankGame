# ADR-0005: Authoritative match server on Cloudflare Durable Objects (M3)

**Date:** 2026-06-04
**Status:** Accepted (server model); implementation in progress (M3 scaffolding landed, live deploy developer-gated)
**Deciders:** Solo developer + Claude Code (architect agent)

Reserved by name for M3 in `docs/research/development-plan.md`; written as M3-T10 once the transport
seam (M3-T1), wire protocol (M3-T2), and the Durable Object skeleton (M3-T3) landed. Records the
server topology, the hibernation reliance, and the stay-free request-budget decision.

## Context

M3 is the first online milestone: two devices, one shared arena, real-time, **server-authoritative**
(a client cannot move faster or fire faster than the server allows). The whole backend must stay on
Cloudflare's **free tier** (project Decision 20 — rate-limit, never pay). The deterministic GameLogic
combat and the `IInputSource` intent seam from the local-first arc (ADR-0011) were built so the same
rules can run on an authoritative server without a rewrite.

The open questions: where does the authoritative simulation live, how do two clients find the same
match, and how do we avoid a surprise bill or a silent quota wall.

## Decision

### 1. One Durable Object per lobby = one authoritative match

A Cloudflare **Durable Object** (`MatchRoom`, `server/worker/src/MatchRoom.ts`) is the single
authoritative instance for one lobby. A DO is single-threaded with its own storage and a stable id,
so it is a natural "one room, one source of truth". The stateless Worker routes `/room/:code` to the
DO via `env.MATCH_ROOM.idFromName(code)`; a KV namespace will map shareable lobby codes → DO ids
(M3-T4). The DO runs the 20 Hz `MatchSim` (M3-T5): apply client inputs → tick the deterministic sim →
broadcast `SnapshotFrame`s; it clamps speed and fire-rate so a tampered client cannot cheat.

### 2. WebSocket transport with the hibernation API

Clients connect over WebSocket. The DO accepts sockets with `state.acceptWebSocket()` (the
**hibernation API**), so the runtime can evict an idle lobby from memory between messages and
rehydrate it on the next one. An idle or paused lobby therefore consumes no compute — essential for
staying inside the free tier when rooms sit open between rounds. The skeleton (M3-T3) relays each
peer's frame to the others; M3-T5 replaces the relay with the authoritative tick.

### 3. Hand-rolled binary wire protocol, mirrored in two languages

`InputFrame` / `SnapshotFrame` / `TankState` / `WallDelta` are encoded as fixed-layout little-endian
binary by `ProtocolCodec` — once in C# (`TankGame.Domain.Net`, the client) and once in TypeScript
(`server/worker/src/protocol/codec.ts`, the server). A **shared canonical byte-vector test** in each
language asserts identical bytes, so the two implementations cannot drift silently. Hand-rolled
(not a serializer library) keeps the format explicit, dependency-free, and trivially portable — the
pragmatic near-term answer to the client/server parity risk flagged in `feature-roadmap.md` §4 (a
full shared-schema + test-vector suite is its own future decision).

### 4. Stay-free request-budget alarm, not autoscaling spend

To honour Decision 20, a scheduled (cron) Worker will read Cloudflare Analytics and raise a Sentry
alert at **80% of the monthly Durable Object request budget** (M3-T11). The response to hitting the
ceiling is to **rate-limit / refuse new lobbies**, never to enable the paid plan. Federation (more
free accounts/regions) is the long-term scaling lever, not spend.

## Consequences

- **Positive:** the authoritative sim is a single, naturally-consistent object per match; hibernation
  keeps idle rooms free; the byte-vector parity test makes the two-language protocol safe to evolve;
  the cost ceiling is observable and enforced by policy, not by a credit card.
- **Negative / cost:** a DO is a single point of failure for its match (acceptable — a dropped match
  is a re-join, not data loss). The protocol is two hand-maintained implementations kept in lockstep
  by tests rather than generated from one schema — fine at four frame types, revisited if the
  catalogue of message types grows (the §4 parity ADR). Durable Objects on the free tier have request
  and duration caps that the M3-T11 alarm exists to watch.
- **Developer-gated:** going live needs a Cloudflare account, the DO/KV deploy, GitHub Actions
  secrets, and two-device testing — none automatable in CI. The secret-free scaffolding (transport
  seam, protocol both sides, DO skeleton with Miniflare tests) is done; `docs/setup/m3-go-live.md`
  is the runbook for the rest.
