# Multiplayer Architecture & Free Hosting

Status: research draft, Slice D
Owner: @architect
Date: 2026-05-21

Scope: pick a network model and a free-tier hosting stack that can run a ~2–10 player top-down tank game (web-first, native-portable) indefinitely at hobby scale (~50 CCU peak, normally <10), with lobby, invites, persistent progression, and leaderboards.

---

## 1. Network model

### Options considered

| Model | What it is | Pros | Cons |
|---|---|---|---|
| Authoritative server | Server owns world state, clients send inputs, server broadcasts snapshots | Cheat-resistant, single source of truth, easy late-join/spectate, recordable | Needs always-on server process (cost), latency hit (RTT for every action) |
| Authoritative server + client prediction & reconciliation | As above, but client locally simulates inputs and rewinds on server correction | Same correctness, feels responsive (sub-RTT input feedback) | More implementation work — prediction code on both sides, snapshot interpolation, lag compensation |
| Lockstep deterministic | All peers run the same simulation, only inputs exchanged, advance in lockstep | Tiny bandwidth, naturally fair, no server-side sim cost | Requires bit-exact determinism (hard in JS / floats / Godot physics), one slow peer stalls the match, brutal to debug, late-join is essentially impossible |
| WebRTC P2P (host migration) | One player is the host (authoritative), others connect to them via DataChannel | Zero realtime backend cost, low latency between geographically close peers | Host gets unfair LAN-style advantage and can cheat freely, host disconnect ends match, NAT traversal needs a STUN/TURN server, host's upstream bandwidth caps player count |

### Recommendation for MVP: **Authoritative server with client prediction & reconciliation**

Rationale:

1. **Destructible labyrinth + powerups + traps + enemies** is a lot of shared mutable state. Lockstep needs all of it to be bit-exact deterministic across browsers — Godot's HTML5 export, Bevy WASM, and Phaser all use floats and platform-dependent math. Determinism is achievable but it is a months-long tax we don't need to pay.
2. **WebRTC P2P** sounds free but the host advantage is severe in a twitch shooter, and any disconnect kills the lobby. For 2–10 player matches with friends-of-friends, that's a poor experience.
3. **Authoritative server** is the standard for this genre (Diep.io, agar.io, ShellShockLive's online mode). Client prediction makes it feel local; server reconciliation snaps you back on disagreement. Snapshot rate of 20–30 Hz with input rate of 30–60 Hz is plenty for a top-down tank pace.
4. Server-authoritative means the **protocol is transport-agnostic** — we can swap WebSocket for WebTransport or a native UDP layer when we port off the browser, without changing the game logic.

### Anti-cheat (matters because fun > fairness collapse)

Even without money, public lobbies attract trolls. Authoritative server gives us these for free:

- **Movement bounds**: server clamps speed/acceleration; reject input deltas larger than `max_speed * dt`.
- **Fire-rate cap**: server enforces cooldowns; client cannot trigger faster than weapon allows.
- **Line-of-sight / collision**: server resimulates shots against authoritative wall state.
- **No client-side scoring**: XP / kills awarded by server only, never trusted from client.
- **Rate-limit input messages**: reject obviously spammed inputs (>120 Hz from one client).

Things we explicitly **defer** for MVP: aimbot detection, statistical analysis, replay review. Hobby scale doesn't justify it. A "report player" button plus the ability for friends-lobbies to kick is enough.

---

## 2. Realtime transport: WebSocket vs WebRTC DataChannel

| Dimension | WebSocket | WebRTC DataChannel |
|---|---|---|
| Latency | TCP — head-of-line blocking under loss; typical 30–80 ms client↔server in same region | UDP-like; can be unreliable+unordered, ~10–30 ms better under loss |
| Browser support | Universal, trivial | Universal, but signaling + ICE setup is non-trivial |
| Free-tier server compat | Works on Fly.io, Render, Railway, Cloudflare Workers (with Durable Objects). Cloudflare also offers WebSockets to Workers for free | Needs a signaling channel + a TURN server (TURN is rarely free at any meaningful traffic) |
| NAT / firewall | Just HTTPS, always works | STUN free (e.g. Google's), TURN expensive; ~20% of users need TURN |
| Server-authoritative fit | Perfect — server is one endpoint, clients connect to it | Awkward — you'd run a "server peer" anyway, losing the P2P benefit |
| Browser connection limits | ~255 per origin, irrelevant at our scale | ~500 peer connections, irrelevant at our scale |

**Recommendation: WebSocket for MVP.** TCP head-of-line is acceptable at our tick rate and player count, free-tier hosts all support it natively, and signaling complexity disappears. Re-evaluate WebTransport (HTTP/3, UDP-like, free in modern browsers) once the protocol is stable — it's a drop-in upgrade later.

---

## 3. Hosting candidates — free-tier survey (2026)

> Limits below are as of writing. Free tiers shift; verify before committing. Volatile entries are flagged.

### Static frontend (HTML/JS/WASM bundle + assets)

| Host | Free-tier limits (2026) | Good for | Not good for |
|---|---|---|---|
| **Cloudflare Pages** | Unlimited bandwidth, 500 builds/month, 100 custom domains, 25 MiB per file | Big WASM bundles, global edge cache, unlimited traffic — **best fit** | Files >25 MiB (split or host on R2) |
| **GitHub Pages** | 100 GB bandwidth/month soft limit, 1 GB site, 10 builds/hr | Tiny static sites, trivially tied to repo | Large game bundles, traffic spikes (soft 100 GB cap can throttle), no headers control |
| **Netlify** | 100 GB bandwidth/month, 300 build min/month | Easy DX, form handling | Bandwidth ceiling is real — a 20 MB bundle * 5000 loads = cap hit |
| **Vercel** | 100 GB bandwidth/month (Hobby), commercial-use restriction on free tier | Next.js apps | Commercial-use clause + bandwidth cap make it riskier than Cloudflare Pages for a public game |

**Pick: Cloudflare Pages.** Unlimited bandwidth is the only sane choice for a game that may suddenly get shared on Reddit.

### Realtime backend (WebSocket server, game loop)

| Host | Free-tier limits (2026) | Good for | Not good for |
|---|---|---|---|
| **Fly.io** | No longer truly free since 2024 — "free allowances" of ~$5/month credit are now only for paid orgs; new hobby accounts need a card and a small monthly minimum (~$5). Three shared-cpu-1x machines, 3 GB volumes still common allowance. **Volatile.** | Region-anywhere VM-style hosts for stateful game servers (auto-stop when idle) | Pure-free; assume it now costs ~$0–5/mo even when idle |
| **Cloudflare Workers + Durable Objects** | 100k requests/day free, Durable Objects free tier: 1M requests/month, 400k GB-s, 1 GB storage, WebSockets supported with hibernation API (hibernated WS doesn't burn duration) | Lobby coordination, matchmaking, light tick servers using DO hibernation; global edge | Tight CPU loop — 30 ms CPU per request limit on free; long-running heavy sim is the wrong tool |
| **Render** | Free web service: 750 hr/month but **spins down after 15 min idle and cold-starts in ~30–60 s**, 512 MB RAM | Low-priority always-on-ish HTTP | Realtime game server — cold-start kills lobbies |
| **Railway** | $5 trial credit, then paid. **No real free tier since 2023.** | — | Free indefinite hosting |
| **Hathora** | Was game-server-specific; pricing model has shifted to paid-only / startup credits. **Volatile, assume not free.** | Production-grade if you can pay | Free hobby |
| **Colyseus Arena / Cloud** | Has had free dev tiers but commercial fair-use; needs verification | Colyseus-framework projects | Indefinite free at scale |
| **Glitch** | Project sleeps after 5 min idle, severe; pivoted away from hosting in 2024 | Toy demos | Anything serious |

**Pick: Cloudflare Workers + Durable Objects.** Each match = one Durable Object instance, players hold a WebSocket to it, DO hibernates between ticks if we batch. The hibernation-aware WebSocket API explicitly does **not** count idle WS time against duration billing — this is the unlock that makes the free tier viable. Backup: small Fly.io machine (~$2–5/mo) if we hit a CF Workers ceiling.

### Persistence / leaderboards / auth

| Service | Free-tier limits (2026) | Good for | Not good for |
|---|---|---|---|
| **Supabase** | 500 MB Postgres, 50k MAU auth (incl. magic link + OAuth), 5 GB egress, 2 free projects, **pauses after 7 days of inactivity** (resumeable) | Auth + relational data + leaderboards — one stop shop | Inactive periods (project pause is a real annoyance for a hobby game) |
| **Firebase** | Firestore 1 GiB stored, 50k reads/day, 20k writes/day, Firebase Auth free incl. OAuth + email | Auth + JSON data, generous reads | Heavy write traffic (20k writes/day = ~14 writes/min averaged) |
| **Cloudflare D1 + KV** | D1: 5 GB storage, 5M rows read/day, 100k rows written/day free. KV: 100k reads/day, 1k writes/day, 1 GB free | Lives next to Workers/DO — zero-egress reads from same edge | Auth (no built-in auth — you build it on Workers) |
| **Turso** | 9 GB storage, 1 billion row-reads/month, 25M row-writes/month, embedded SQLite replicas | Generous reads, embeddable | Auth (none built-in) |
| **PocketBase** | Free OSS; **must self-host** — so cost depends on host | Single-binary BaaS, auth + DB + realtime | Free hosting unless we have a free VM (we don't, durably) |

**Pick: Supabase for auth + leaderboards.** Postgres + magic-link auth + OAuth + RLS in one place; the 7-day pause is fine for a hobby project (a pinging keep-alive cron solves it if needed). Cloudflare KV as a hot cache for "current top 100" if leaderboard reads ever become a hot path.

### Game-specific managed platforms

| Platform | Free-tier reality (2026) | Verdict |
|---|---|---|
| **Hathora** | Pivoted away from a generous free hobby tier; mostly pay-as-you-go with credits | Skip for indefinite-free |
| **Nakama (Heroic Labs)** | OSS, **must self-host**. Excellent feature set (auth, matchmaking, leaderboards, social). Self-hosted on Fly.io = ~$3–7/mo realistic | Tempting but not free |
| **PlayFab** | Microsoft-owned. Free tier exists for indie use, but pricing tiers + lock-in are real risks | Skip for hobby — too much surface |
| **Colyseus** (framework, self-host) | OSS framework, self-host on Workers/Fly — like Nakama but Node/TS | Good fallback if our DIY DO server gets unwieldy |

---

## 4. Recommended stack

| Concern | Service | Why |
|---|---|---|
| Static frontend (HTML, JS, WASM, assets) | **Cloudflare Pages** | Unlimited bandwidth, fast edge, free custom domain |
| Realtime game server (one Durable Object per match) | **Cloudflare Workers + Durable Objects** | WebSocket hibernation makes free tier viable for stateful match servers; same provider as frontend |
| Matchmaking / lobby coordination | **Cloudflare Workers** (stateless API in front of DO) | Same project, free request quota |
| Auth (nickname + persistent identity) | **Supabase Auth** | Email magic link + OAuth (Google/GitHub/Discord) on the free tier |
| Persistent progression + leaderboards | **Supabase Postgres** with RLS | Relational fits XP / unlocks / friends; RLS gives us per-user safety with no server code |
| Hot leaderboard cache (optional, later) | **Cloudflare KV** | Sub-ms edge reads if top-N becomes hot |
| Static asset CDN for >25 MiB files (optional, later) | **Cloudflare R2** | Free egress to Cloudflare network; pair with Pages |

### Back-of-envelope free-tier capacity

Assume tick rate 20 Hz, snapshot ~400 bytes/player, 8 players/match, 5-minute matches.

- **WebSocket messages**: 20 ticks/s * 8 clients * 300 s = 48,000 outbound messages per match. With CF Workers free 100k req/day, **without DO hibernation** that would cap us at ~2 matches/day — fatal. With hibernation, idle WebSocket frames don't accrue duration; what we burn is CPU per actual tick. CF Workers DO free tier: 400k GB-s/month. At 128 MB per DO and ~10 ms CPU per tick, one match-second costs ~0.0128 GB-s, so 400k GB-s / 0.0128 = **~31 million match-seconds/month** of CPU headroom — far more than we'd ever hit at <50 CCU.
- **Real ceiling is requests**: 1M DO requests/month free. At 20 ticks/s = 1200 req/min per match, **~14 hours of total active match-time per month before hitting the request cap**. At 50 CCU peak in 8-player matches, that's ~6 concurrent matches, so ~14 / 6 = ~2 hours/day of peak play. For a hobby game with <10 normal users this is comfortable; for sustained 50 CCU it's borderline and a small paid uplift would be needed.
- **Supabase**: 50k MAU and 500 MB DB easily covers thousands of users; leaderboard table at ~200 bytes/row * 10k users = 2 MB. **No realistic ceiling for this scale.**
- **Cloudflare Pages**: unlimited bandwidth — irrelevant.

**Estimated sustainable free-tier capacity**: 6–10 concurrent 8-player matches, ~2 hours/day of peak activity, OR ~50 CCU spread thinner across more matches. Comfortable for friends-and-family hobby use; tight if the game goes viral for a week.

### What triggers paid spend

1. Crossing 1M DO requests/month — fix by batching (send 100 ms snapshots instead of 50 ms) or upgrading Workers Paid ($5/mo, 10M req included).
2. Supabase Postgres > 500 MB — unlikely without media uploads.
3. Static bundle > 25 MiB single file — move to R2 (still free egress within CF network).
4. Needing TURN for any reason — almost certainly means we went P2P, which we are not doing.

---

## 5. Identity & invites

### Account model

- **Anonymous-then-claim flow.** New player picks a nickname, plays immediately with a Supabase anonymous session (free, supported by Supabase Auth). On first unlock-worthy moment or when they want to friend someone, prompt: "Save your progress?" → email magic link or OAuth.
- **Magic link (primary)**: zero password storage, Supabase handles it, free tier includes the email send (rate-limited; fine for hobby).
- **OAuth (secondary)**: Google + Discord. Discord especially because it's the social hub for this kind of game and OAuth there gives us a verified social identity nearly free.
- **Nickname uniqueness**: nicknames are display-only; identity is the auth UUID. Two players can share a nickname; we suffix `#1234`-style discriminators when ambiguity matters (Discord pattern).

### Invites

- **Lobby code (primary)**: 6-character base32 code (e.g. `K7J3PQ`), generated by the Worker that creates the Durable Object, mapped in Workers KV (24-hour TTL). Friend types code on the homepage → joins.
- **Invite link (secondary)**: `https://tankgame.example/j/K7J3PQ` — same code embedded. Shareable in Discord/SMS/etc. The page reads the code and routes into the lobby.
- **Friend invites (later)**: once accounts are persistent, a friends list in Supabase + push (Supabase realtime channel) when an online friend opens a lobby. Skip for MVP.

---

## 6. Risks

The three realistic ways this architecture breaks:

1. **Free-tier policy change.** Cloudflare or Supabase tightens limits or removes a feature we rely on (most acute: DO WebSocket hibernation pricing, or Supabase free tier shrinking). Mitigation: keep the server logic engine-agnostic — our DO is just a state machine over WebSocket — so we can port to a $5/mo Fly.io machine in a day if needed. Avoid CF-specific APIs in the game-logic layer; only the transport adapter touches DO.

2. **Latency / region mismatch.** Cloudflare Workers run at-the-edge, but a Durable Object is pinned to one region (the region of the first request that created it). A match between players in Sydney and Stockholm will route both to the same DO — one of them gets a 250 ms RTT. For a top-down tank game with client prediction this is playable but noticeable. Mitigation: surface a region picker on lobby create ("EU/US/AP"), and pre-warm DOs by hitting a region-tagged endpoint.

3. **Scaling cliff at the request quota.** The 1M DO requests/month ceiling is the thinnest part of the free tier. A weekend of unexpected popularity blows past it and the game goes dark until month-end (Workers fails closed when the quota is hit). Mitigation: a request-budget alarm at 80%, plus a manual "switch to paid" toggle — $5/mo Workers Paid is 10x the headroom and is the only realistic step out of free-tier. We should be psychologically and financially ready for that to happen.

---

## Open user decisions

- **Acceptable to pay $5/mo if popularity demands it?** The "indefinitely free" constraint is comfortable for <10 normal users but has a ceiling. If the game catches on, are we willing to upgrade Workers Paid, or do we rate-limit new lobbies to stay under quota?
- **Region strategy**: single region (cheapest, simplest) or user-selected region (better latency, more DOs to manage)?
- **OAuth providers**: confirm Discord + Google is enough, or do we want GitHub / Apple / email-only?
