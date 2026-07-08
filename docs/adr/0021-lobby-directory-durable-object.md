# ADR-0021: The open-lobby directory is a Durable Object, not KV

**Date:** 2026-07-08
**Status:** Accepted (2026-07-08)
**Deciders:** Solo developer + Claude Code

Numbered 0021, the next free number after 0020.

## Context

The multiplayer lobby browser (`GET /lobbies`) lists the open games a player can join. Each
`MatchRoom` Durable Object published a tiny summary of itself into **Workers KV** on every lobby
state change (`open:<code>` keys with the summary as list metadata), and the browser read them with a
single `KV.list({prefix:"open:"})`.

KV's `list()`/`get()` are **eventually consistent** — a write can take up to ~60 seconds to
propagate to the edge a reader hits. In practice this meant a second player did **not** see a game
another player had just created; the **Refresh button appeared dead** (it re-read the same
not-yet-propagated list); and the only thing that "worked" was fully leaving and rejoining the arcade,
which merely bought the ~30–60 s KV needed to propagate. Reported live 2026-07-08.

## Decision

Move the open-lobby directory into a **single global Durable Object** (`LobbyDirectory`,
`idFromName("global")`). A DO gives **read-your-writes strong consistency**, so a publish is visible
on the very next list. Each `MatchRoom` `POST /publish`es its summary (or `null` to withdraw) to the
directory on every state change; `GET /lobbies` reads `GET /list`. The pure `summarize()`
joinability rule is unchanged and still unit-tested; KV keeps only its original job (code→room
mapping for join validation).

## Alternatives considered

- **Keep KV, accept the lag** — rejected: the lag is the bug; a browser you must wait a minute for,
  or leave-and-rejoin to refresh, is not usable for 2 players in the same room.
- **Poll faster / cache-bust** — rejected: no client trick fixes server-side eventual consistency.
- **Reuse the `MATCH_ROOM` namespace with a reserved instance** — rejected: conflates a room's
  WebSocket/game responsibilities with directory listing in one class.

## Consequences

- **Positive:** the browser and Refresh now reflect a create/join/leave immediately; one strongly-
  consistent read per `/lobbies`; no second game implementation or new external service.
- **Trade-off:** a single hot DO instance serializes directory writes. At family-arcade scale (a
  handful of concurrent lobbies) this is a non-issue; if concurrent-lobby volume ever makes it a
  bottleneck, shard by region (`idFromName(region)`) and fan-out the list. Marked with a `ponytail:`
  comment at the class.
- Added migration `v2` (`new_sqlite_classes = ["LobbyDirectory"]`); additive, no data migration. Old
  `open:` KV keys are orphaned and harmless (ignored, they were never read after this change).
- Reaches players only after the **worker deploys** (deploy.yml on merge to `main`); verify live with
  a two-client create→refresh check.
