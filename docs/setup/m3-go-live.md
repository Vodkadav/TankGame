# M3 Go-Live Runbook — networked 2-player

Status: handoff runbook (the developer-gated half of M3)
Date: 2026-06-04
Audience: project owner taking M3 from "scaffolded" to "playable across two devices"
Prerequisite: `docs/setup/cloudflare.md` (account, Account ID, API token, GitHub secrets). If M0
deploy already works, that prerequisite is done.

This runbook covers only what **cannot** be done in CI: enabling Durable Objects on your account,
creating the KV namespace, and the final two-device playtest. The model is recorded in
`docs/adr/0005-authoritative-server-on-durable-objects.md`.

## What is already built (no action needed)

Landed and tested headless — merged to `main`:

- **Transport seam** (`IMatchTransport`, `INetClock`) + **wire protocol** (`InputFrame` /
  `SnapshotFrame` / `TankState` / `WallDelta` + `ProtocolCodec`) in C# Domain, with a TypeScript
  mirror (`server/worker/src/protocol/codec.ts`) guarded by an identical byte-vector test.
- **`MatchRoom` Durable Object skeleton** — accepts the WebSocket upgrade (hibernation API) and
  relays frames between peers. `wrangler.toml` already declares the `MATCH_ROOM` binding and the
  `v1` migration. The Worker routes `/room/:code` to one DO per code.

The worker test suite (Vitest + Miniflare) runs in CI and exercises all of the above locally — no
Cloudflare account is touched by the tests.

## What still needs building (autonomous — ask me, no secrets)

These ride the scaffolding above and are testable headless; they do **not** need your account, so I
can build them on request before you do anything below:

| Ticket | What |
|---|---|
| M3-T4 | Lobby routes: `POST /lobby` → `{code, doId}`, `POST /lobby/:code/join` validates KV |
| M3-T5 | `MatchSim` in the DO — 20 Hz tick, applies inputs, broadcasts snapshots, clamps speed/fire-rate |
| M3-T6 | Client `WebSocketTransport` (Infrastructure) implementing `IMatchTransport` |
| M3-T7 | Client prediction + reconciliation (`PredictedTank`) |
| M3-T8 | Hardcoded `TEST01` join flow on the title screen |
| M3-T9 | EN/ES/DK strings for the new UI |
| M3-T11 | Request-budget cron alarm (80% of the free DO budget → Sentry) |

## What only you can do (the developer-gated steps)

### Step 1 — Confirm the Cloudflare prerequisites

If M0 deploy already works, `CF_API_TOKEN` and `CF_ACCOUNT_ID` are set in GitHub secrets and the
`tankgame-worker` Worker exists. Nothing to do. Otherwise complete `docs/setup/cloudflare.md`
Steps 1–7 first.

Durable Objects need **no dashboard toggle** — they are enabled per-script by the `wrangler.toml`
binding + migration already in the repo. The free tier covers them.

### Step 2 — Create the KV namespace for lobby codes (needed by M3-T4)

From `server/worker/`, with your token in the environment:

```powershell
$env:CLOUDFLARE_API_TOKEN = "paste-token"   # same value as the CF_API_TOKEN secret
pnpm wrangler kv namespace create LOBBY_KV
$env:CLOUDFLARE_API_TOKEN = $null
```

It prints an `id`. Add it to `wrangler.toml` (I will wire the binding when M3-T4 is built):

```toml
[[kv_namespaces]]
binding = "LOBBY_KV"
id = "the-id-it-printed"
```

Commit that `wrangler.toml` change (the `id` is not a secret — it is a namespace handle, safe to
commit). Tell me when it is in and I will finish M3-T4 against it.

### Step 3 — Deploy

The Worker (with the `MatchRoom` DO) deploys on merge to `main` via `deploy.yml` once the secrets
from Step 1 are present. The first deploy runs the `v1` migration that creates the `MatchRoom` DO
class — you will see it in the Cloudflare dashboard under Workers & Pages → your Worker → Durable
Objects. To deploy manually instead:

```powershell
cd server/worker
$env:CLOUDFLARE_API_TOKEN = "paste-token"
pnpm wrangler deploy
$env:CLOUDFLARE_API_TOKEN = $null
```

### Step 4 — Two-device playtest (the M3 definition of done)

Once M3-T4 to M3-T8 are merged and deployed:

1. Launch the desktop build → it connects to lobby `TEST01`.
2. Launch the APK on the phone → join `TEST01`.
3. Both tanks should be visible to each other, move and shoot in real time, and walls should break
   for both.

If a tank rubber-bands, that is prediction/reconciliation (M3-T7) tuning, not a wiring fault — note
what you see and I will adjust the reconciliation thresholds.

## What to send back

1. Confirmation that the Worker deployed (the dashboard shows a `MatchRoom` Durable Object).
2. The `LOBBY_KV` namespace `id` committed to `wrangler.toml` (or just say it is done).
3. Anything odd in the two-device test (rubber-banding, a tank not appearing, walls out of sync).

You never need to send me a secret value — only confirmations.

## Cost guardrail

Per Decision 20 we stay on the free tier. M3-T11 adds a cron alarm at 80% of the monthly Durable
Object request budget that posts to Sentry; the response to the ceiling is to refuse new lobbies,
never to enable the paid plan (`docs/adr/0005-authoritative-server-on-durable-objects.md` §4).
