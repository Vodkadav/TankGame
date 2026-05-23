# Development Plan — Tank-Labyrinth Game

Status: **user-signed-off 2026-05-21** (Slice F2 complete; 7 open items resolved)
Date: 2026-05-21
Owner: @architect
Inputs: signed-off `decisions.md` + research slices A–E
Audience: solo dev + parallelised specialist agents
Revision: **2026-05-22** — M0 retargeted web→Android (Godot 4.6 .NET has no web/HTML5 export). M1–M8 web assumptions flagged **superseded** — see the banner before the M1 section; an `@architect` web→Android revision pass is due before M1 kickoff.

## User answers to the 7 F2 open items (2026-05-21)

1. **Repo location:** `github.com/Vodkadav/TankGame` (personal account).
2. **Visibility:** **public** (enables free unlimited GitHub Actions minutes per D17). License: MIT for code, CC0 for assets.
3. **Game name:** keep placeholder **"TankGame"** for now — search-and-replace later if a real name is picked.
4. **Cloudflare setup:** concrete walkthrough at `docs/setup/cloudflare.md`. User runs it once; reports back project slug + Worker name + workers.dev subdomain + confirmation that `CF_API_TOKEN` / `CF_ACCOUNT_ID` / `CF_PAGES_PROJECT` are in repo secrets.
5. **ADR template:** **@architect generates `templates/adr-template.md`** as part of M0-T8 (Nygard format: Context / Decision / Status / Consequences).
6. **Custom domain:** ship M0–M3 on `<project>.pages.dev`; buy custom domain at M4.
7. **Sentry:** **front-loaded to M0** — crash reporting from commit #1, not M4. See new M0-T11 + M0-T12 below; secrets list updated.

## Captured deployment targets (post-setup, 2026-05-21)

| Key | Value | Where it lives |
|---|---|---|
| GitHub repo | `Vodkadav/TankGame` (public, MIT planned) | https://github.com/Vodkadav/TankGame |
| `main` branch protection | active (ruleset id 16711116, squash-only, no force-push) | `gh api repos/Vodkadav/TankGame/rulesets` |
| Cloudflare Pages slug | `tankgame` | https://tankgame.pages.dev (placeholder live) |
| Cloudflare Worker name | `tankgame-worker` | url: `tankgame-worker.vodkadav.workers.dev` (404 placeholder — M0-T6 deploys real Worker) |
| Cloudflare workers.dev subdomain | `vodkadav.workers.dev` | claimed |
| Sentry org slug | `dm-6r` | hardcode in `wrangler.toml` (`org = "dm-6r"`) and Godot Sentry init |
| Sentry client project | `tankgame-client` | |
| Sentry worker project | `tankgame-worker` | |
| GitHub Actions secrets | `CF_ACCOUNT_ID`, `CF_API_TOKEN`, `CF_PAGES_PROJECT`, `SENTRY_DSN_CLIENT`, `SENTRY_DSN_WORKER`, `SENTRY_AUTH_TOKEN` — all 6 verified set | `gh secret list -R Vodkadav/TankGame` |

This plan translates the user-signed-off decisions matrix into a parallelisable, CI/CD-first roadmap. Every milestone after M0 produces something the user can play. Every ticket is PR-sized (XS/S/M/L), independently mergeable, and auto-deploys on merge.

The core operational rule: **main = prod. Every merge ships. Every PR gets a preview URL.** If a ticket cannot be shipped behind a feature flag in under a day, it is too big — split it.

---

## 1. ASAP-MVP definition

### The single smallest playable artifact

**One local player drives one tank around an empty arena, can press a key to fire one projectile, and the projectile despawns when it hits a placeholder wall.**

No netcode. No labyrinth. No enemies. No UI beyond "press WASD". No menu. The Godot project boots, a 64-px tank sprite is on screen, it moves, it fires, the projectile collides. That's the end of M1.

This is opinionated on purpose: the user has a hard "send a link to friends" goal, and the cheapest way to validate the engine + asset + build + deploy stack end-to-end is a single-tank single-player demo on the live URL. Multiplayer joins at M3, not M1 — premature netcode is the single biggest schedule risk in this kind of project.

### Next 5 minimum increments after MVP

Each is independently demoable on the live URL:

1. **Static labyrinth** — replace empty arena with a hand-authored TileMapLayer maze. Player navigates corridors. Projectile collides with tilemap.
2. **Destructible walls** — brick tiles take N hits then break and turn into floor. Steel tiles do not break. (Battle City legibility model.)
3. **Second local player on same keyboard (couch co-op)** — proves the Player abstraction is generic before netcode forces it. Throwaway-but-cheap.
4. **Networked 2-player same-arena via one Durable Object** — first online milestone. Lobby code hardcoded for now.
5. **Lobby code + invite link flow** — friend opens URL, types code, joins. The "send a URL" promise is now live.

After increment 5, all subsequent work (powerups, enemies, auth, progression, ranked) is additive — the core loop is proven, the deploy pipeline is live, and the architecture has been stress-tested by real netcode.

---

## 2. Milestone breakdown

Cadence target: M0 in days 1–2, M1 within a week of M0, M2–M3 within the first fortnight (the "ASAP MVP fortnight"), M4–M8 in the following 4–6 weeks at ~one milestone per week.

### M0 — CI/CD live

**Goal:** Green pipeline deploying a placeholder Godot **Android APK** (to GitHub Releases), a static **Cloudflare Pages** landing page, and a no-op **Cloudflare Worker**. Nothing playable.

> **Revised 2026-05-22:** Godot 4.6 stable's .NET build cannot export to web/HTML5 (the mono export templates ship no web template; Godot issue #70796 — .NET web export is prototype-only). M0 therefore deploys an Android APK instead of an HTML5 build. The C# layered architecture is unchanged.

**Definition of done:**
- CI builds a Godot **Android debug APK** of an empty scene that prints "TankGame M0" and the build SHA; installing it on the Galaxy A56 shows that text.
- Merging to `main` publishes the APK to a **GitHub Release** within 5 minutes.
- Pushing to a PR branch produces a downloadable APK **Actions artifact** plus a **Cloudflare Pages preview** within 5 minutes.
- A public Cloudflare **Pages** landing page shows the build SHA and links to the latest APK download.
- A public Cloudflare **Worker** URL responds `200 ok` to `/healthz`.
- Architecture test (NetArchTest) runs in CI and passes against the scaffold (empty layers, but the test exists).
- **Sentry captures errors from both client and Worker** — a deliberately-thrown test exception in each appears in the Sentry dashboard.

**What auto-deploys:** Every PR push → APK artifact + Pages preview. Every merge to `main` → APK on a GitHub Release + Pages landing page + Worker, all in prod. Each target deploys independently if its paths changed.

**Non-goals:** Game mechanics, multiplayer, auth, persistence, audio, UI beyond a placeholder label.

**Tickets:**

| ID | Description | Owner | Size | Depends | Parallel-with |
|---|---|---|---|---|---|
| M0-T1 | Scaffold monorepo directory layout (`client/`, `server/`, `shared/`, `docs/`, `.github/`, `scripts/`); add root `README.md`, `LICENSE`, `.editorconfig`, `.gitignore`. Test-first: bats/pwsh script asserts every required path exists. | @housekeeper | XS | — | M0-T2, M0-T3 |
| M0-T2 | Add Cloudflare Worker skeleton (`server/worker/`) with `wrangler.toml` and a `/healthz` route. Test-first: Vitest test hits the route via Miniflare; passes. | @devops | S | — | M0-T1, M0-T3 |
| M0-T3 | Add Godot 4.x C# client skeleton (`client/`) — `project.godot`, `TankGame.csproj`, empty `Main.tscn` with a Label node bound to a script that prints the version, and `export_presets.cfg` with an Android preset. Test-first: GoDotTest scene-load test asserts Main loads and the label exists. | @platform | S | — | M0-T1, M0-T2 |
| M0-T4 | NetArchTest project (`client/tests/Architecture/`) asserting layer rules: Domain → no deps; GameLogic → Domain only; Data → Domain only; Infrastructure → Domain+GameLogic+Data; Presentation → GameLogic+Domain. Test-first: write five failing tests then create the empty namespaces. | @architect | S | M0-T3 | M0-T5, M0-T6 |
| M0-T5 | GitHub Actions workflow `ci.yml` — triggered on PR and push to main; jobs: `worker-test` (path filter `server/worker/**`), `client-build` (path filter `client/**`, exports a Godot Android debug APK), `arch-test` (path filter `client/**`). Test-first: workflow lint via `actionlint`. | @devops | M | M0-T2, M0-T3, M0-T4 | M0-T6, M0-T7 |
| M0-T6 | GitHub Actions workflow `deploy.yml` — on push to main and PR sync: deploys the Worker via `wrangler deploy`; deploys a static Cloudflare Pages landing page; publishes the Android APK to a GitHub Release on main (Actions artifact on PRs). Secrets via `secrets.CF_API_TOKEN` / `secrets.CF_ACCOUNT_ID` (GitHub Actions secrets — per `sec-no-hardcoded-secrets`); the APK release uses the built-in `GITHUB_TOKEN`. | @devops | M | M0-T5 | M0-T7 |
| M0-T7 | i18n bootstrap — `client/i18n/strings.csv` with three columns (`en`, `es`, `dk`), one row (`m0.boot_label = "TankGame M0" / "TankGame M0" / "TankGame M0"`), Godot translation import config, language autoselect from browser locale. Test-first: GoDotTest asserts `tr("m0.boot_label")` returns the right string when locale is forced. | @i18n | S | M0-T3 | M0-T4, M0-T5, M0-T6 |
| M0-T8 | ADR-0001 `docs/adr/0001-layered-architecture.md` recording the layered-client decision. Use `templates/adr-template.md`. | @architect | XS | M0-T4 | all others |
| M0-T9 | ADR-0002 `docs/adr/0002-monorepo-and-cicd.md` recording the monorepo + GitHub Actions + Cloudflare deploy decision. | @architect | XS | M0-T6 | all others |
| M0-T10 | Pre-commit hook `hooks/no-secrets-scan.sh` wired via `scripts/install-hooks.ps1`. Test-first: bats test confirms a staged file containing `password = "abc"` is rejected. | @security | S | M0-T1 | M0-T2..T9 |
| M0-T11 | Sentry SDK in the Worker (`@sentry/cloudflare`). Wrap the fetch handler; `SENTRY_DSN_WORKER` from `wrangler secret put` in deploy. Test-first: Vitest+Miniflare test injects a `/test-throw` route and asserts a Sentry transport mock received the event. | @security | S | M0-T2 | M0-T12, M0-T4..T10 |
| M0-T12 | Sentry SDK in the Godot C# client (`Sentry.NET` package). Initialise in `MainScene.cs` with `SENTRY_DSN_CLIENT` injected via `Godot.OS.GetEnvironment` (set at build time in CI). Test-first: GoDotTest verifies `SentrySdk.IsEnabled` is true after init. | @security | S | M0-T3 | M0-T11, M0-T4..T10 |

**Parallelization map (M0):**
- Wave 1 (3 agents in parallel): M0-T1 (@housekeeper), M0-T2 (@devops), M0-T3 (@platform).
- Wave 2 (after T1+T3 merge; 5 agents in parallel): M0-T4 (@architect), M0-T7 (@i18n), M0-T10 (@security), M0-T11 (@security), M0-T12 (@security).
  - @security has 3 tickets but they're sequential within that agent; @security can dispatch to general-purpose for T10 to truly parallelise.
- Wave 3 (after T2+T3+T4 merge): M0-T5 (@devops), then M0-T6 (@devops) — sequential because deploy depends on CI.
- Wave 4 (after T4 and T6 land): M0-T8 + M0-T9 (@architect, both XS, in parallel).

**Demo:** the user installs the APK on the Galaxy A56 and sees "TankGame M0 — build abc1234"; the Cloudflare Pages URL shows the same SHA and links to that APK. Cynical but vital — the deploy pipe is alive.

---

> **⚠ Superseded — web→Android (2026-05-22).** Godot 4.6 stable's .NET build cannot
> export to web/HTML5 (the mono export templates ship no web template; see Godot issue
> #70796 — .NET web export is prototype-only). M0 above was revised to deploy an Android
> APK. **M1–M8 below still describe a browser/HTML5 product** — "open the prod URL",
> "Chromium browser", "60 fps on a laptop browser", the cross-device tests in M3, the
> invite-link flow in M4, and risk R1 (HTML5 cold-load) are all **superseded** pending an
> `@architect` web→Android revision pass due before M1 kickoff. Until that pass lands,
> treat M1–M8 DoDs (and §1's "live URL" language) as Android/desktop targets, not web.
> The C# layered architecture and the milestone *sequence* are unaffected.

### M1 — One tank, empty arena, moves and shoots (local)

**Goal:** the ASAP-MVP artifact. A single locally-controlled tank in an empty room with placeholder walls. Press WASD to move, mouse to aim, space/click to fire one projectile that collides and despawns.

**Definition of done:**
- Open prod URL → see arena → drive tank → fire projectile → projectile hits wall and despawns.
- 60 fps on a mid-tier laptop browser (Chromium-based).
- All inputs go through an `IInputSource` interface (Domain) so we can swap a network input source later without touching tank code.
- All UI strings (instructions overlay) exist in EN+ES+DK.

**What auto-deploys:** Every merge auto-ships. The live URL becomes the demo from this milestone onward.

**Non-goals:** Labyrinth tiling, destructible walls, multiplayer, audio, menus, scoreboards, multiple tanks.

**Tickets:**

| ID | Description | Owner | Size | Depends | Parallel-with |
|---|---|---|---|---|---|
| M1-T1 | Define `ITank`, `IProjectile`, `IInputSource`, `IArena` interfaces in `client/src/Domain/`. Pure C#, no Godot references. Test-first: unit tests instantiating mock impls verify the contract surface. | @architect | S | M0 done | M1-T2 |
| M1-T2 | Import Kenney Top-down Tanks Redux pack; commit one chassis + turret sprite at 64px; add `docs/credits/assets.md` (one line, Kenney CC0). Test-first: GoDotTest asserts the resource loads at the expected `.tres` path. | @housekeeper | S | M0 done | M1-T1 |
| M1-T3 | `Tank` implementation in GameLogic — pure C# class implementing `ITank`, takes `IInputSource`, updates position/rotation. Test-first: unit test feeds a scripted `IInputSource` and asserts position after N ticks. | @domain | M | M1-T1 | M1-T4 |
| M1-T4 | `Projectile` implementation in GameLogic — pure C#, takes spawn pos/dir, advances per tick, exposes "did I hit anything" via `IArena.RaycastFirstHit`. Test-first: unit test with a stub arena. | @domain | M | M1-T1 | M1-T3 |
| M1-T5 | Presentation: `TankView.tscn` + `TankView.cs` Node2D wrapping `ITank` and rendering the sprite. Test-first: GoDotTest scene-load test. | @platform | M | M1-T3, M1-T2 | M1-T6 |
| M1-T6 | Presentation: `ProjectileView` + arena scene `Arena.tscn` (empty rect with 4 wall colliders). Test-first: GoDotTest asserts an `Arena` scene loads with exactly 4 static body children. | @platform | M | M1-T4 | M1-T5 |
| M1-T7 | `KeyboardMouseInputSource` in Infrastructure implementing `IInputSource`. Test-first: integration test feeds Godot input events into the source and asserts the emitted struct. | @platform | S | M1-T1 | M1-T5, M1-T6 |
| M1-T8 | Instructions overlay (Label) in EN+ES+DK reading "WASD to move, mouse to aim, click to fire" via `tr()`. Test-first: GoDotTest forces each locale and asserts the rendered string. | @i18n | S | M0-T7 | M1-T5, M1-T6 |
| M1-T9 | Wire it all together in `Main.tscn`: arena + tank + input source + overlay. Test-first: integration GoDotTest plays one tick and asserts the tank moved. | @platform | S | M1-T5, M1-T6, M1-T7, M1-T8 | — |
| M1-T10 | ADR-0003 `docs/adr/0003-interface-first-services.md` recording the interface-first rule and the `IServiceName → Domain` location convention. | @architect | XS | M1-T1 | all others |

**Parallelization map (M1):**
- Wave 1 (after M0): M1-T1 (@architect, blocks much), M1-T2 (@housekeeper) — parallel.
- Wave 2 (after T1): M1-T3 (@domain), M1-T4 (@domain or general-purpose), M1-T7 (@platform), M1-T8 (@i18n), M1-T10 (@architect) — 5 in parallel.
- Wave 3 (after T2+T3): M1-T5 (@platform). After T4: M1-T6 (@platform). Can run as 2 in parallel by different platform-capable agents.
- Wave 4 (after T5+T6+T7+T8): M1-T9 (@platform, the wiring ticket).

**Demo:** "I can drive a tank around an empty box and shoot a wall." Five seconds of gameplay. The user sees the actual game for the first time.

---

### M2 — Static labyrinth + destructible walls

**Goal:** replace the empty box with a hand-authored maze. Brick walls take 3 hits and break; steel walls don't break.

**Definition of done:**
- Open prod URL → enter a corridor maze → shoot a brick wall, hear nothing (audio is M5), see the wall progress through damage states and turn into floor → shoot steel, it doesn't break.
- 60 fps with the full tilemap rendered.
- Maze is loaded from a single hand-authored TileMap resource — no procedural generation yet.

**What auto-deploys:** as before — every merge ships.

**Non-goals:** procedural maze generation, audio, particles, networking, multiple players.

**Tickets:**

| ID | Description | Owner | Size | Depends | Parallel-with |
|---|---|---|---|---|---|
| M2-T1 | Define `IWallGrid` interface in Domain — exposes `GetCell(x,y)`, `DamageCell(x,y,amount)`, `IsBlocked(x,y)`. Test-first: contract tests on a mock impl. | @architect | S | M1 done | M2-T2 |
| M2-T2 | Import Kenney Top-down Shooter wall sprites; create damage-state atlas (intact / cracked / rubble) — placeholder cracked overlay via runtime shader for now. | @housekeeper | S | M1 done | M2-T1 |
| M2-T3 | `WallGrid` impl in GameLogic — backing array of `WallCell` value objects (material + hp). Test-first: unit tests covering brick→break-at-0-hp and steel→never-breaks. | @domain | M | M2-T1 | M2-T4, M2-T5 |
| M2-T4 | `TileMapLayer`-backed `WallGridView` in Presentation that subscribes to `IWallGrid` change events. Test-first: GoDotTest applies a damage event and asserts the displayed atlas frame. | @platform | M | M2-T3, M2-T2 | M2-T5 |
| M2-T5 | Projectile↔WallGrid collision — `Projectile.Step` calls `IArena.RaycastFirstHit` which now consults `IWallGrid`. Test-first: regression test from M1 stays green; new test asserts brick hp decrement on impact. | @domain | M | M2-T3 | M2-T4 |
| M2-T6 | Hand-author one labyrinth scene `Mazes/Maze01.tscn` (~30×30 tiles). Test-first: scene-load test plus a "no orphaned wall tiles" sanity check. | @platform | S | M2-T4 | — |
| M2-T7 | EN+ES+DK strings for any new UI ("Bricks: 12 destroyed" debug overlay during dev). Test-first: locale assertions. | @i18n | XS | M2-T6 | M2-T6 |
| M2-T8 | ADR-0004 `docs/adr/0004-wallgrid-data-model.md` recording the `IWallGrid` interface and tile-state model. | @architect | XS | M2-T1 | all others |

**Parallelization map (M2):**
- Wave 1: M2-T1 (@architect) + M2-T2 (@housekeeper) + M2-T8 (@architect).
- Wave 2: M2-T3 (@domain).
- Wave 3: M2-T4 (@platform) + M2-T5 (@domain) + M2-T7 (@i18n).
- Wave 4: M2-T6 (@platform, the scene authoring).

**Demo:** "I can drive into a maze and chip away brick walls until I can shoot through them."

---

### M3 — 2-player real-time via a single Durable Object

**Goal:** two browsers, same maze, both tanks moving in real time over WebSocket with server-authoritative simulation, client prediction, and reconciliation.

**Definition of done:**
- User opens prod URL on laptop → clicks "Host" → gets hardcoded lobby `TEST01` → opens prod URL on phone → types `TEST01` → both tanks visible to each other → both can move and shoot → walls break for both.
- Server tick 20 Hz. Snapshot delta + client prediction. Reconciliation snaps on disagreement.
- One Durable Object per lobby code; KV maps code→DO id.
- Cheat surface: server clamps speed and fire-rate; XP is unused this milestone but the structure for "server-only" awards is in.

**What auto-deploys:** Worker + DO on every merge; client on every merge. The KV bindings are created via Wrangler in CI.

**Non-goals:** matchmaking UI beyond the hardcoded `TEST01`, auth, persistent progression, enemies, powerups, audio.

**Tickets:**

| ID | Description | Owner | Size | Depends | Parallel-with |
|---|---|---|---|---|---|
| M3-T1 | Define `IMatchTransport` (Domain) — `SendInput(InputFrame)`, `OnSnapshot(SnapshotFrame)`. Define `INetClock`. Test-first: contract tests on mock impls. | @architect | S | M2 done | M3-T2 |
| M3-T2 | Shared protocol package `shared/protocol/` — `InputFrame`, `SnapshotFrame`, `WallDelta`, `TankState` as schema (use [MemoryPack] or hand-rolled binary). Test-first: roundtrip serialise/deserialise tests in both .NET and TS. | @architect | M | M2 done | M3-T1 |
| M3-T3 | Durable Object skeleton `server/worker/src/MatchRoom.ts` — accepts WebSocket upgrade, attaches with `state.acceptWebSocket()` (hibernation API), broadcasts to peers. Test-first: Vitest+Miniflare test asserts 2 connected sockets receive each other's input echoes. | @devops | M | M0 done | M3-T2 |
| M3-T4 | Lobby Worker route `POST /lobby` returns `{code, doId}`; `POST /lobby/:code/join` validates KV mapping and returns the WS upgrade URL. Test-first: Vitest covers code generation collision retry. | @devops | M | M3-T3 | M3-T5 |
| M3-T5 | Server-side `MatchSim` in TS — runs 20 Hz tick inside the DO, applies inputs, broadcasts snapshots, enforces speed/fire-rate clamps. Test-first: pure-function tick test with scripted inputs. | @domain | L | M3-T2, M3-T3 | M3-T6 |
| M3-T6 | Client `WebSocketTransport` in Infrastructure implementing `IMatchTransport`. Test-first: integration test against a Miniflare-hosted MatchRoom. | @platform | M | M3-T1, M3-T2 | M3-T5 |
| M3-T7 | Client prediction + reconciliation — `PredictedTank` wraps `ITank`, re-runs unacknowledged inputs on snapshot arrival. Test-first: pure-unit deterministic test using stubbed transport. | @domain | L | M3-T5, M3-T6 | M3-T8 |
| M3-T8 | Hardcoded `TEST01` lobby join flow on the home screen — one button "Join TEST01". Test-first: GoDotTest exercises the click path against a mock transport. | @platform | S | M3-T6 | M3-T7 |
| M3-T9 | EN+ES+DK strings for new UI ("Connecting...", "Connected", "Player 2 joined"). | @i18n | S | M3-T8 | M3-T8 |
| M3-T10 | ADR-0005 `docs/adr/0005-authoritative-server-on-durable-objects.md` — records the DO-per-match model, hibernation reliance, and the request-budget alarm decision. | @architect | XS | M3-T3 | all others |
| M3-T11 | CF request-budget alarm — a scheduled Worker (cron trigger) that reads CF Analytics and posts to a Sentry alert at 80% of monthly DO requests. Test-first: Vitest with a stubbed analytics client. | @security | S | M3-T3 | M3-T10 |

**Parallelization map (M3):**
- Wave 1 (3 in parallel): M3-T1 (@architect), M3-T2 (@architect), M3-T10 (@architect).
- Wave 2 (3 in parallel): M3-T3 (@devops), M3-T6 (@platform — can start once T1+T2 land), M3-T11 (@security).
- Wave 3: M3-T4 (@devops), M3-T5 (@domain) — sequential within domain but parallel with each other.
- Wave 4: M3-T7 (@domain), M3-T8 (@platform), M3-T9 (@i18n).

**Demo:** "I can play against my partner from the next room — different browsers, same maze, real-time, walls breaking for both."

---

### M4 — Lobby code + invite link flow

**Goal:** real lobby creation UI, generated 6-char codes, shareable invite link, region picker.

**Definition of done:**
- Home screen has "Create lobby" + "Join with code" + region selector (EU/US/AP).
- Creating a lobby shows a 6-char code and an invite link `tankgame.example/j/ABCDEF`.
- Opening the invite link in a fresh browser prefills the code.
- Codes expire after 24 h (KV TTL).

**What auto-deploys:** as before.

**Non-goals:** auth (still anonymous), friends list, matchmaker, public lobby browser.

**Tickets:** (8 tickets — abbreviated; same structure as M3) — covers lobby UI scene, region-routing logic in Worker, KV TTL handling, share-link copy button, deep-link parser, EN+ES+DK strings, ADR-0006 on the lobby-code scheme, Sentry SDK integration on the client.

**Parallelization map (M4):** @platform owns UI scenes (3 tickets in parallel by different agents on different scenes); @devops owns Worker routing; @i18n owns strings; @architect owns the ADR.

**Demo:** "Send my partner the URL on Discord, she clicks, she joins."

---

### M5 — Powerups + traps + enemies

**Goal:** make the arena feel alive — pickups spawn, traps trigger, simple AI tanks roam.

**Definition of done:**
- 3 powerups (speed boost, double-shot, shield) spawn from destructible walls (Bomberman pattern).
- 2 traps (mine, oil slick) placed in maze.
- 1 enemy archetype (turret) that aims and fires at the nearest player.
- All effects are server-authoritative; client visualises.

**Non-goals:** all 8 powerups and all enemy archetypes — that's M5b. We ship 3+2+1 to prove the systems, then add content additively.

**Tickets:** ~12 tickets across @domain (powerup state machine, trap state machine, AI behaviour tree), @platform (visualisation, particle FX from Kenney Particle Pack), @i18n, @architect (ADR-0007 on pickup-as-terrain pattern).

**Parallelization map (M5):** powerups, traps, and enemies are three independent feature tracks. Three @domain-capable agents can develop them in parallel against the same `IMatchSim` interface; @platform agents do the visualisation in parallel.

**Demo:** "I picked up a double-shot, blew through a wall, found a mine and died."

---

### M5b — Limited Vision mode (opt-in, deferred)

**Status:** Deferred — Proposed in `docs/adr/PROPOSAL-fog-of-war.md`; depends on M3 (authoritative sim) and M5 (powerup baseline).

**Goal:** an opt-in lobby mode that turns the labyrinth into a tense hide-and-seek arena. Each player sees a roughly 2.5-tile bright radius around their tank with a one-tile penumbra of dimmed fog; everything beyond is opaque dark. Walls inside the radius are fully lit, and last-seen tile state (damaged bricks, broken cover) persists as a desaturated "memory" layer so the maze stays learnable. Enemy tanks, projectiles, and powerups only appear when they fall inside a player's current vision or memory — flanks, ambushes, and corridor stand-offs become the dominant tactic.

**Definition of done:**
- Lobby host can toggle "Limited Vision" on; the flag round-trips through the Worker and is reflected on every joined client before the match starts.
- Match runs at the same 20 Hz tick; server emits a per-player culled `SnapshotFrame` so each client receives only entities its visibility (current radius ∪ memory) covers.
- Fairness integration test: a scripted match with Players A and B in disjoint corridors asserts that Player A's raw inbound packets contain no `TankState` for Player B until line-of-sight overlaps — packet inspection cannot un-fog.
- Client `FogOfWarOverlay` renders at 60 fps on the Galaxy A56 reference device with four tanks active; profiler trace attached to the PR.
- Memory layer persists for the lifetime of the match: a brick damaged earlier still shows its damaged state (desaturated) when the player re-enters that tile's history, even if no enemy is currently visible there.
- HUD chip "Limited Vision" visible while the mode is active, in EN+ES+DK.
- Per-snapshot CPU benchmark gate (see R6) green on a synthetic 4-player worst-case tick.

**What auto-deploys:** as before — every merge ships behind the `limited_vision` lobby flag; default off until M5b-T6 promotes the ADR.

**Non-goals:** thermal scope, spotter drone, or other vision-altering powerups (separate M5c proposal); spectator-mode fog handling (spectators see full map for now); replay fog re-simulation; AI enemies adapting their behaviour to fog (M5 turret AI remains vision-agnostic this milestone).

**Tickets:**

| ID | Description | Owner | Size | Depends | Parallel-with |
|---|---|---|---|---|---|
| M5b-T1 | Define `IVisibilityFilter` in Domain — pure C# contract: `IReadOnlySet<CellCoord> ComputeVisible(TankState viewer, IWallGrid maze)` plus a `MergeWithMemory(prev, current)` helper signature. No Godot, no networking types. Test-first: contract tests on a mock impl assert determinism (same input → same set) and that walls block transitively across two corridors. | @architect | S | M3 done + M5 done | M5b-T3, M5b-T4a |
| M5b-T2 | Server-side `VisibilityFilter` impl in TS inside `MatchSim`; snapshot path forks per-connected-player and emits a culled `SnapshotFrame`. Test-first: deterministic-tick Vitest test with Players A and B in disjoint corridors asserts A's outbound snapshot omits B's `TankState`; second test asserts that once B enters A's radius, B appears in the very next snapshot. | @domain | M | M5b-T1 | M5b-T3 |
| M5b-T3 | Client `FogOfWarOverlay` Presentation node — `CanvasModulate` dark background + per-tank `Light2D` for the bright/penumbra falloff + a `RenderTexture`-backed memory layer that accumulates last-seen tiles per match. Test-first: GoDotTest scene-load asserts the overlay loads, the light follows the local tank's `ITank.PositionChanged` event, and the memory texture is non-empty after a scripted traversal of three cells. | @platform | M | M5b-T1 | M5b-T2, M5b-T4b |
| M5b-T4a | Worker lobby route accepts `limited_vision: bool` on `POST /lobby` and persists it on the DO; `POST /lobby/:code/join` returns the flag to joining clients. Test-first: Vitest+Miniflare round-trip — create with `limited_vision: true`, join, assert payload contains the flag. | @devops | S | M3-T4 | M5b-T4b, M5b-T1 |
| M5b-T4b | Client lobby UI toggle bound to the new flag; "Limited Vision" checkbox on the create-lobby scene plus a read-only indicator on the join-lobby scene. Test-first: GoDotTest exercises the toggle, asserts the outbound `POST /lobby` payload contains the flag and the indicator reflects the joined-lobby flag. | @platform | S | M5b-T4a | M5b-T3, M5b-T5 |
| M5b-T5 | EN+ES+DK strings for the lobby toggle label, its tooltip, and the in-match "Limited Vision active" HUD chip. Test-first: GoDotTest forces each locale and asserts `tr()` returns the expected strings for all three keys. | @i18n | XS | M5b-T4b | — |
| M5b-T6 | Promote `docs/adr/PROPOSAL-fog-of-war.md` to a numbered ADR (next free number after the M0–M7 reservations) with **Status: Accepted**; update cross-references in `development-plan.md` (this block) and in any ADRs that referenced the proposal path. Test-first: a docs-link sanity script (`scripts/check-adr-links.ps1`) asserts no surviving references to `PROPOSAL-fog-of-war.md` and that the numbered ADR exists with `Status: Accepted`. | @architect | XS | all M5b-T1..T5 merged + R6 benchmark gate green | — |

**Parallelization map (M5b):**
- Wave 1 (1 agent): M5b-T1 (@architect) — single-threaded by the interface-first rule; blocks T2, T3, T4a.
- Wave 2 (3 agents in parallel after T1 merges): M5b-T2 (@domain, server filter), M5b-T3 (@platform, overlay), M5b-T4a (@devops, Worker lobby flag).
- Wave 3 (2 agents in parallel after T4a merges): M5b-T4b (@platform, lobby UI), M5b-T5 (@i18n, strings) — T5 starts as soon as T4b lands the string keys.
- Wave 4 (1 agent, gated): M5b-T6 (@architect, promote ADR) — held until all of T1..T5 merge **and** the R6 per-snapshot benchmark gate is green on the 4-player worst-case scenario.

**Demo:** "I toggle Limited Vision on, drive into a corridor, my partner ambushes me from a side tunnel I couldn't see — we high-five."

---

### M6 — Auth (anonymous-then-claim) + persistent progression schema

**Goal:** Supabase wired in. Anonymous play continues to work. Player can claim their account via Discord/Google/magic-link. XP and unlock tables exist and are read/written server-side from the DO via a Worker→Supabase service-role binding.

**Definition of done:**
- New player nicknames in, plays, sees XP bar fill, finishes match, sees end-of-match level-up screen.
- "Save your progress?" prompt offers Discord / Google / email-link.
- After claiming, next session loads their XP.
- All schema migrations live in `server/supabase/migrations/` and run in CI.

**Non-goals:** actual cosmetic content beyond a `default_skin`; leaderboards; ranked.

**Tickets:** ~10 tickets — Supabase project bootstrap (@devops), schema migrations (@domain), `IPlayerRepository` interface + Postgres impl (@architect+@domain), Worker→Supabase server-side service-role calls (@platform), anonymous-claim merge logic (@domain), OAuth provider app registrations (@security — secrets in GH Actions secrets), EN+ES+DK auth strings (@i18n), ADR-0008 on the anonymous-claim merge, Sentry on the auth flow.

**Parallelization map (M6):** schema, repository, Worker integration, and UI can run in parallel by 4 agents once the `IPlayerRepository` interface is agreed in week 1.

**Demo:** "I played as Guest, finished a match, signed in with Discord, came back tomorrow, my XP was still there."

---

### M7 — Unlockables + leaderboards

**Goal:** the cosmetic unlock loop and a global leaderboard.

**Definition of done:**
- 5 skins, 3 emblems, 3 titles, 2 nickname accents implemented.
- Unlocks awarded server-side on the right triggers.
- Equip UI in the menu (per-category tabs).
- Global top-100 leaderboard backed by Postgres; KV hot-cache invalidated on write.

**Non-goals:** all-content unlock catalogue (additive over later milestones), friends-filter leaderboard, mastery tracks beyond a single weapon.

**Tickets:** ~12 tickets across @domain (unlock rule engine), @platform (equip UI scenes — three in parallel), @architect (ADR-0009 on opaque-ID stability for unlocks), @devops (KV cache warming + invalidation Worker), @i18n.

**Demo:** "I won 10 matches, the *Bronze Tread* emblem unlocked, I equipped it, the leaderboard shows me at rank 47."

---

### M8 — Multi-region + ranked-mode hooks (still gated off)

**Goal:** multiple DO regions (EU/US/AP) actually wired up; ranked-mode infrastructure (MMR table, fixed weapon pool flag) exists but the queue is hidden behind a feature flag.

**Definition of done:**
- Lobby create routes the DO to the chosen region.
- Cross-region play is honest (one side sees higher RTT, predictable).
- MMR table, ranked queue schema, and "verified account" flag all migrated to Postgres but no UI to enter the queue.

**Non-goals:** opening ranked to players. Tip-jar — that goes in M9. Friends list — M10. Mastery tracks beyond weapon 1 — M11.

**Demo:** "I selected AP region, played with a friend in Sydney, latency was sane."

---

### Milestones beyond M8 (named, not detailed)

- **M9 — Tip jar (credits screen only) + supporter emblem.**
- **M10 — Friends list + online-presence push.**
- **M11 — Full mastery tracks per weapon + per map.**
- **M12 — Ranked queue opens (verified accounts, solo queue, fixed weapon pool).**
- **M13 — Community challenges + prestige.**
- **M14 — Federation hooks (community-hosted DO replacement) — long-term sustainability per `decisions.md` D20.**

---

## 3. First-fortnight concrete tickets

These are the 15 tickets we'd dispatch in the first 7–14 days. Every ticket has a "test first" sub-step.

### Day 1–2: M0 wave 1 (3 parallel agents)

**M0-T1 — Scaffold monorepo layout** — owner @housekeeper, size XS.
- Create dirs: `client/`, `client/src/Domain/`, `client/src/GameLogic/`, `client/src/Data/`, `client/src/Infrastructure/`, `client/src/Presentation/`, `client/tests/`, `server/worker/`, `server/supabase/`, `shared/protocol/`, `docs/adr/`, `docs/credits/`, `docs/licenses/`, `scripts/`, `.github/workflows/`.
- Add `README.md` (one paragraph + repo map), root `LICENSE` (placeholder — see open user decisions below), `.gitignore` (Godot+dotnet+node+CF), `.editorconfig`.
- **Test first:** `scripts/test-scaffold.ps1` (Pester) — asserts every required path exists.

**M0-T2 — Worker skeleton + healthz** — owner @devops, size S.
- `server/worker/package.json` (workspaces if we like), `wrangler.toml` with `name = "tankgame-worker"`, `compatibility_date`, no secrets committed.
- `src/index.ts` exporting a `fetch` handler routing `/healthz` → `200 ok`.
- **Test first:** Vitest + `vitest-environment-miniflare` test — `expect(await env.fetch('/healthz')).toHaveStatus(200)`.

**M0-T3 — Godot 4.x C# client skeleton** — owner @platform, size S.
- `client/project.godot` (Godot 4.6, C# enabled), `TankGame.csproj` with `Godot.NET.Sdk` (TFM per the installed SDK — the dev box has .NET 9, no .NET 8).
- `Main.tscn` with a CanvasLayer + Label.
- `src/Presentation/MainScene.cs` setting the label text to "TankGame M0 — build " + assembly version.
- `export_presets.cfg` with an Android preset (package id, target ABIs); the debug keystore is generated in CI, never committed.
- **Test first:** `tests/Presentation/MainSceneTests.cs` using Chickensoft.GoDotTest — `LoadScene("res://Main.tscn")` succeeds and label text starts with "TankGame".

### Day 2–3: M0 wave 2 (3 parallel agents)

**M0-T4 — NetArchTest layer rules** — owner @architect, size S.
- `tests/Architecture/LayerRulesTests.cs` with five `[Fact]`s, one per rule. Use NetArchTest.Rules. Test against assembly namespaces `TankGame.Domain`, `TankGame.GameLogic`, `TankGame.Data`, `TankGame.Infrastructure`, `TankGame.Presentation`.
- **Test first:** literally is the test — write all 5 failing first, then create the empty namespaces to make them pass.

**M0-T7 — i18n bootstrap** — owner @i18n, size S.
- `client/i18n/strings.csv` with header `keys,en,es,dk` and one data row `m0.boot_label,"TankGame M0","TankGame M0","TankGame M0"` (placeholder strings identical across locales is fine for boot label).
- Add to `project.godot`: `[internationalization] locale/translations=PackedStringArray("res://i18n/strings.en.translation", ...)`. Use Godot's CSV import.
- Switch `MainScene.cs` to `Label.Text = Tr("m0.boot_label")`.
- **Test first:** GoDotTest asserts `Tr` returns the expected string when `TranslationServer.SetLocale("es")` and `("dk")`.

**M0-T10 — Pre-commit secret-scan hook** — owner @security, size S.
- Copy `hooks/no-secrets-scan.sh` pattern from the Vodkadav global config; mirror to `scripts/hooks/no-secrets-scan.ps1` for Windows dev.
- `scripts/install-hooks.ps1` installs hooks into `.git/hooks/`.
- **Test first:** Pester test creates a tmp file containing `password = "abc12345"` and asserts the hook exits non-zero.

### Day 3–5: M0 wave 3+4 (sequential CI/CD then ADRs)

**M0-T5 — CI workflow** — owner @devops, size M. Path-filtered jobs:
- `worker-test` (paths `server/worker/**`): node 20, `pnpm install`, `pnpm test`.
- `client-build` (paths `client/**`): install the .NET SDK, install Godot 4.6 mono + Android export templates via cached download, install JDK + Android SDK, generate a debug keystore, `dotnet build`, `godot --headless --export-debug "Android" build/tankgame.apk`, upload the APK artifact.
- `arch-test` (paths `client/**`): `dotnet test client/tests/Architecture/`.
- **Test first:** lint the workflow with `actionlint` in a separate `.github/workflows/lint.yml`; assertion is the lint passing.

**M0-T6 — Deploy workflow** — owner @devops, size M.
- On push to `main`: deploy the Worker via `cloudflare/wrangler-action`; deploy the static Pages landing page via `cloudflare/pages-action`; publish the Android APK to a GitHub Release (`softprops/action-gh-release` or `gh release`).
- On PR sync: deploy a Pages preview to a `pr-<num>.tankgame.pages.dev` URL; upload the APK as an Actions artifact.
- Secrets: `CF_API_TOKEN`, `CF_ACCOUNT_ID`, `CF_PAGES_PROJECT` — GitHub Actions secrets, never in repo. The APK release uses the built-in `GITHUB_TOKEN`.
- **Test first:** dry-run job in a scratch PR — assert the Pages preview URL responds 200 and the APK artifact is present.

**M0-T8 + M0-T9 — ADR-0001 and ADR-0002** — owner @architect, size XS each.
- Use `templates/adr-template.md` (create it now if absent — see open decisions). Fill in context (decisions.md D26 / D5+D6+D16+D17), decision (the chosen path), consequences (what this forecloses; what it enables; what triggers a revisit).

### Day 6–10: M1 — first playable

**M1-T1 — Domain interfaces** — owner @architect, size S.
- `client/src/Domain/ITank.cs`, `IProjectile.cs`, `IInputSource.cs`, `IArena.cs` — pure C# interfaces, no Godot namespaces. Value types `Vector2D`, `InputFrame` in `Domain/Primitives/` (custom — do not depend on Godot's `Vector2`).
- **Test first:** `tests/Domain/InterfaceContractTests.cs` — instantiate mocks via Moq, assert each method is callable on the contract.

**M1-T3 — Tank GameLogic impl** — owner @domain, size M.
- `client/src/GameLogic/Tank.cs` implementing `ITank`. Takes `IInputSource`, advances per `Step(float dt)`.
- **Test first:** unit test instantiates a `Tank` with a scripted input source replaying `{forward=1}` for 60 ticks at dt=1/60, asserts position advanced by `MaxSpeed * 1.0` ± epsilon.

**M1-T5 — TankView presentation** — owner @platform, size M.
- `client/src/Presentation/TankView.cs` extending `Node2D`. Wraps an `ITank`. Subscribes to its `PositionChanged` event. Updates sprite + rotation.
- **Test first:** GoDotTest scene-load on `TankView.tscn`; inject a mock `ITank`; raise event; assert the sprite node's `GlobalPosition`.

**M1-T9 — Wire and ship** — owner @platform, size S.
- Replace `Main.tscn` with the arena + tank + projectile + overlay composition. Assemble in code, not via editor magic.
- **Test first:** integration test plays 30 ticks with scripted input, asserts the tank moved and a projectile collided with a wall.

### What's open at the end of fortnight 2

After M1-T9 merges, the user has the smallest playable artifact live. M2 begins immediately — the next 5 tickets (M2-T1..T5) follow the same pattern. By end of week 3 we should be at M2 demo; M3 (the first real multiplayer milestone) starts in week 4.

---

## 4. Parallel-agent dispatch patterns

### The default M_x kickoff pattern

When a new milestone starts, the orchestrator (main session via `/delegate`) dispatches in this canonical order:

1. **@architect** — drafts the new Domain interfaces (always first, single-threaded by design).
2. **@architect + @housekeeper + @i18n** — in parallel: ADR, asset/license import, string-table additions.
3. **@domain × N** — in parallel on independent GameLogic implementations behind the interfaces from step 1. Each ticket has a separate file; merge conflicts are minimal because there is no shared state.
4. **@platform × N** — in parallel on Presentation/Infrastructure wrapping each GameLogic class.
5. **@devops + @security** — in parallel on any pipeline/secret/quota changes.
6. **@qa** — sweeps for missing integration tests and adds them in a final PR.

### Concrete dispatches by milestone

**M0:** 3 parallel agents in wave 1 (@housekeeper, @devops, @platform). Then 3 more in wave 2 (@architect, @i18n, @security). Then @devops alone for CI/CD. Then @architect for ADRs.

**M1:** 1 @architect on interfaces. Then up to 5 parallel agents (2× @domain, @platform, @i18n, @architect). Then @platform on view glue. Then @platform on the final wire-up.

**M3 (peak parallelisation):** 11 tickets, of which 5 can run in parallel after waves 1+2. Sample dispatch:
- @architect on interfaces + protocol + ADR (sequential, 1 agent).
- @devops on DO skeleton + lobby Worker + alarm (3 tickets, 1 agent).
- @domain on MatchSim + prediction (2 tickets, 1 agent).
- @platform on transport + lobby UI (2 tickets, 1 agent).
- @i18n on strings (1 ticket, 1 agent).
- @security on the budget alarm (1 ticket, 1 agent).
- Total: 6 agents in flight simultaneously after wave 1 unblocks.

### The "definition of merge" gate

A PR may merge when, and only when:

1. CI green (all jobs pass — lint, tests, build, arch-test).
2. @reviewer has approved (architecture + style + TDD compliance).
3. Auto-deploy to staging (PR preview URL) succeeded — verified by a smoke-test job that hits the preview URL and asserts the relevant route.
4. No secrets in the diff (pre-commit hook + a CI grep on the diff as belt-and-braces).
5. Architecture test still green.

Merging is squash-merge; the PR title becomes the commit subject; the ticket ID (e.g. `M3-T7`) is the prefix.

### When agents disagree

Per the architect role: layer-boundary disputes between @domain and @platform (or anyone else) are decided by @architect with explicit reasoning, recorded as an ADR amendment if the precedent is new.

---

## 5. CI/CD pipeline specification

### Workflows

Two GitHub Actions workflows live in `.github/workflows/`:

#### `ci.yml`

**Triggers:** `pull_request` (any branch), `push` to `main`.

**Jobs (all run in parallel where path filters permit):**

- `lint`
  - Triggers always.
  - Steps: `actionlint`, `dotnet format --verify-no-changes`, `pnpm lint` (eslint+prettier on `server/worker` and `shared/protocol`).
- `arch-test`
  - Path filter: `client/**`.
  - Steps: install dotnet 8, restore, `dotnet test client/tests/Architecture/`.
- `client-test`
  - Path filter: `client/**`.
  - Steps: install dotnet 8, install Godot 4.x mono headless (cached), `dotnet test client/tests/Unit/`, `dotnet test client/tests/Integration/` (GoDotTest runs Godot headless).
- `client-build`
  - Path filter: `client/**`.
  - Steps: same setup + JDK + Android SDK + a generated debug keystore + `godot --headless --export-debug "Android" build/tankgame.apk`. Upload artifact `client-apk`.
- `worker-test`
  - Path filter: `server/worker/**` or `shared/protocol/**`.
  - Steps: install node 20, `pnpm install --filter ./server/worker...`, `pnpm test` (Vitest + Miniflare).
- `protocol-roundtrip`
  - Path filter: `shared/protocol/**`.
  - Steps: roundtrip-serialise tests in both .NET and TS to detect protocol drift.
- `supabase-migrations`
  - Path filter: `server/supabase/**`.
  - Steps: spin up `supabase` CLI locally, apply migrations to ephemeral Postgres, assert no errors.

#### `deploy.yml`

**Triggers:** `push` to `main`, `pull_request` (sync — for preview deploys), `workflow_run` (after `ci.yml` success on the same SHA).

**Jobs:**

- `deploy-worker`
  - Depends on `worker-test` success.
  - Path filter: `server/worker/**` or `shared/protocol/**`.
  - Uses `cloudflare/wrangler-action@v3`. Env: `CF_API_TOKEN`, `CF_ACCOUNT_ID` from GitHub secrets. On PRs deploys to a preview Worker (`<branch>-tankgame-worker`); on main deploys to prod.
- `deploy-pages`
  - Depends on `client-build` success.
  - Uses `cloudflare/pages-action@v1` to publish a static landing page (`TankGame M0 — build <SHA>`, links to the latest APK) — not a Godot export. On PR deploys to a preview branch URL; on main to prod.
- `release-apk`
  - Depends on `client-build` success.
  - On `main`: publishes the `client-apk` artifact to a GitHub Release (uses the built-in `GITHUB_TOKEN`). On PR: the APK stays an Actions artifact (already uploaded by `client-build`).
- `deploy-supabase`
  - Depends on `supabase-migrations` success.
  - On main only (no preview migrations — branch DBs are a M6 problem). Uses `supabase db push` with the service role key from secrets.
- `smoke-test`
  - Depends on the deploy jobs.
  - Hits `https://<preview-or-prod>/healthz` (worker) and the Pages landing page; fails the workflow if either is non-200.

### Secrets (all in GitHub Actions secrets — per `sec-no-hardcoded-secrets`)

- `CF_API_TOKEN` — scoped to Pages+Workers+KV+DO deploy permissions only.
- `CF_ACCOUNT_ID`.
- `CF_PAGES_PROJECT` (just the project slug — could be public, kept in secret for tidiness).
- `SUPABASE_PROJECT_ID` (M6+).
- `SUPABASE_DB_PASSWORD` (M6+, for migrations).
- `SUPABASE_SERVICE_ROLE_KEY` (M6+, used by Worker via secret binding, never in client).
- `SENTRY_DSN_CLIENT`, `SENTRY_DSN_WORKER` (**M0 — front-loaded per user decision 2026-05-21**).
- `SENTRY_AUTH_TOKEN` (**M0** for sourcemap upload; previously deferred to M4).

Worker-side runtime secrets (Supabase service-role key, Sentry DSN) are set via `wrangler secret put` in the deploy job, not in `wrangler.toml`.

### Path filters and why

Path filtering keeps the median PR's CI under 2 minutes:
- A Worker-only change skips Godot install (~3 min cold) and arch-test.
- A client-only change skips Worker + Supabase jobs.
- A docs-only change runs only `lint`.

Cache strategy: dotnet NuGet cache keyed on `*.csproj`; Godot binary cached at `~/.cache/godot/` keyed on Godot version; pnpm store cached keyed on `pnpm-lock.yaml`. Cold builds ~5 min; warm builds ~1.5 min.

---

## 6. Definition of done per ticket

Every PR must satisfy this checklist before merge. The checklist lives in `.github/pull_request_template.md`.

- [ ] **Test first.** A failing test was committed before the implementation (visible in the commit history as a red→green sequence). For trivial wiring tickets, document the exception in the PR description.
- [ ] **Three locales.** Any new UI-facing string has entries in `en`, `es`, `dk` columns of `i18n/strings.csv`. (Universal rule.)
- [ ] **Architecture test green.** `dotnet test client/tests/Architecture/` passes locally and in CI.
- [ ] **No secrets in diff.** Pre-commit hook passed; CI secret-grep passed.
- [ ] **@reviewer approved.** A `/review` dispatch has signed off.
- [ ] **CI green.** All required jobs (lint, tests, build, deploy-staging) pass.
- [ ] **Preview URL smoke-tested.** The PR description includes the preview URL and a one-line check ("opened, saw X, clicked Y, it worked").
- [ ] **ADR.** If the ticket made a cross-layer or cross-service decision, a new or amended ADR is part of the PR.
- [ ] **Auto-deploy to staging succeeded.** Visible green check from the deploy workflow.

A merge to main triggers an additional gate: a post-merge smoke test on prod. If it fails, an auto-revert PR is opened and tagged `@bugfix`.

---

## 7. Risk register & open user decisions

### Plan-level risks

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R1 | **[SUPERSEDED 2026-05-22 — web→Android pivot; see the banner before M1]** Godot HTML5 cold-load is unacceptable on mobile (raised in `tech-stack.md`'s "one realistic risk"). If a 25-40 MB gzipped bundle takes 15 s on a 4G phone, the "send a link to friends" flow dies. | Medium | High | Streamed-bundle cold-load no longer applies to an installed APK. The `@architect` revision pass replaces R1 with the equivalent APK-download-size / on-device-fps risk; M1-T9 keeps the on-device measurement step on the Galaxy A56. |
| R2 | **DO request quota blown by inefficient tick batching** (multiplayer-hosting.md §4). At 1M DO requests/month free and 1200 req/min/match, a few enthusiastic days of testing can chew through the budget. | Medium | High | M3-T11 lands the budget alarm before M3-T8 hits prod. Default tick is 20 Hz on M3 staging; if alarm fires, drop to 10 Hz on prod and raise an ADR. |
| R3 | **Parallel agents write conflicting Domain interfaces** because @architect is a bottleneck. | Medium | Medium | Enforce the rule: only @architect creates files under `client/src/Domain/`. Other agents propose interface changes via an ADR amendment PR that @architect must approve first. |
| R4 | **Architecture test rots into a no-op** because new namespaces aren't covered. | Medium | Medium | The arch test asserts a complete-coverage rule: every type in the assembly must be in one of the 5 known namespaces; an "untagged" type fails the build. Forces every new module to be classified explicitly. |
| R5 | **i18n debt sneaks in via "I'll add the locale later"** despite the universal rule. | Low | Medium | CI job `i18n-completeness` greps the codebase for `tr(` calls and asserts every key has all three locale columns populated. |
| R6 | **Fog of war snapshot CPU may push DO over its CPU budget under 4-player matches** (visibility computation runs per player per tick). See `docs/adr/PROPOSAL-fog-of-war.md` and M5b. | Medium | Medium | Per-snapshot benchmark gate is a release-blocker for M5b-T6 (promote-to-ADR). If the gate fails, drop tick rate for limited-vision lobbies to 15 Hz or precompute visibility on damage events only. |

### Open user decisions

**All 7 resolved 2026-05-21** — see the user-decisions block at the top of this document. No items block M0 kickoff.

### Pre-M0 user task (one-time)

The user must complete `docs/setup/cloudflare.md` (Steps 1–7) before M0-T6 (deploy workflow) can be dispatched. M0-T1..T5, T10, T11, T12 can start in parallel today without it.

---

## Appendix A — Ticket size legend

- **XS** = < 1 hour. One file change, trivial test.
- **S** = 1–4 hours. A small feature behind an existing interface.
- **M** = half-day (4–6 hours). A new module + tests + wire-up.
- **L** = 1 day (6–9 hours). A complex feature involving multiple files; we try to avoid these and split where possible.

Anything bigger than L is a split candidate. If a ticket is estimated > L during scoping, return it to @architect to break down.

## Appendix B — Filename / namespace conventions

- C# namespaces: `TankGame.Domain.*`, `TankGame.GameLogic.*`, `TankGame.Data.*`, `TankGame.Infrastructure.*`, `TankGame.Presentation.*`.
- Godot scenes live alongside their owning Presentation class: `client/src/Presentation/Tank/TankView.tscn` next to `TankView.cs`.
- Tests mirror the production layout under `client/tests/{Domain,GameLogic,Data,Infrastructure,Presentation,Architecture,Integration}/`.
- Worker TS files: `server/worker/src/{routes,sim,storage}/*.ts`. Tests next to source as `*.test.ts`.
- Shared protocol: `shared/protocol/{InputFrame,SnapshotFrame,...}.cs` + `.ts` (hand-mirrored; the `protocol-roundtrip` CI job catches drift).

End of plan.
