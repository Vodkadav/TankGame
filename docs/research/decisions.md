# Big-Picture Decisions — Tank-Labyrinth Game

Status: **USER-SIGNED-OFF 2026-05-21** (Slice F1 complete)
Date: 2026-05-21
Owner: @architect
Inputs: research slices A (prior-art), B (tech-stack), C (assets), D (multiplayer-hosting), E (progression)

**User decisions on the 4 open-input items (2026-05-21):**
- **Decision 2 — Language:** **C#** (user override of the GDScript recommendation; reasoning: typed code is a stronger learning surface and matches the user's broader tooling).
- **Decision 12 — Asset style:** **Flat-vector (Kenney top-down)** (confirmed).
- **Decision 20 — Free-tier ceiling stance:** **Hard rate-limit to stay free indefinitely** — no paid uplift. Federation/community-hosting (BZFlag pattern) is the long-term sustainability lever.
- **Decision 25 — Tip jar:** **Yes from launch**, credits-screen only, zero in-game benefit. Optional one-time "supporter" emblem for any tip amount (no tiers).

Each decision below is structured: question, alternatives + pros/cons, recommendation, and what downstream choice it unlocks.

---

### Decision 1 — Engine / runtime

**Question:** What engine do we build the client in?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Godot 4.x | Excellent 2D toolset (TileMapLayer, Light2D, particles, audio buses) out of the box; built-in high-level multiplayer API; one-click native exports; friendly editor; strong community | Heaviest HTML5 bundle of the three serious candidates (25–40 MB gzipped); cold-load 10–15 s; SharedArrayBuffer/COOP+COEP headers needed for threads |
| Phaser 3 / 4 | Smallest bundle (1–3 MB); fastest cold-load; idiomatic TS web dev; mature Colyseus pairing for netcode; trivial static-host fit | BYO everything past the renderer (netcode, tilemap loader, particles); no editor; native ship via Tauri/Capacitor, not first-class |
| Bevy + WASM | Most modern stack; smallest "engine-tier" native bundle; ECS scales well | Rust + ECS + no editor = steep climb for a solo dev; iteration time hurt by compile; API churn between 0.x; mobile rough |
| Unity 6 WebGL | Mature 2D tools; trivial native exports | Heaviest WebGL bundle and slowest build; trust still dented post-2024 runtime-fee episode; not ideal for "send a link to friends" |

**Recommendation:** Godot 4.x. It is the only candidate combining excellent 2D tooling, built-in multiplayer, and one-click native exports — the constraint stack of "web-first, native later, ~10 networked tanks, destructible tilemaps" maps onto Godot more cleanly than any other option. Phaser 3 is the explicit fallback if cold-load testing on a real mobile browser proves unacceptable.

**Why this matters:** Sets language, asset pipeline, netcode integration story, and bundle/cold-load profile that drives the static-host decision.

---

### Decision 2 — Language (Godot only)

**Question:** GDScript or C# for game code?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| GDScript | Fastest iteration loop; friendliest to a beginner / daughter reading the code; smaller web export; idiomatic in Godot docs | Weaker type system; refactoring tools thinner; perf ceiling lower (rarely hit at this scale) |
| C# | Strong static types; .NET ecosystem (NuGet, LINQ); shared idioms across non-Godot work | Slower iteration; larger web export (extra runtime); some platforms (web especially) lag GDScript in maturity |
| Mixed (GDScript for game, C# for hot paths) | Best of both | Two language surfaces to maintain in one project |

**Recommendation:** ~~GDScript for the prototype~~ → **USER DECISION 2026-05-21: C#.** Typed code is a stronger learning surface (daughters see proper types from day one) and matches the user's broader tooling preferences. Test framework: GoDotTest / Chickensoft.GoDotTest. CI pipeline must install dotnet SDK in addition to Godot CLI.

**Why this matters:** Locks the test framework choice (Chickensoft.GoDotTest), the CI build pipeline (needs dotnet SDK + Godot CLI), and the project structure (.csproj at root, src/ for game code).

---

### Decision 3 — Network model

**Question:** How is shared world state authoritative across players?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Authoritative server + client prediction & reconciliation | Cheat-resistant; standard for the genre; protocol transport-agnostic; supports late-join and spectate | Most implementation work upfront; needs server prediction + rewind code |
| Lockstep deterministic | Tiny bandwidth; naturally fair; no server-side sim cost | Requires bit-exact determinism across browsers (very hard in float-heavy JS/WASM); one slow peer stalls match; late-join essentially impossible |
| WebRTC P2P (host migration) | Zero realtime backend cost; low peer-to-peer latency | Host gets unfair LAN advantage; host disconnect ends match; NAT/TURN cost; bandwidth scales off host's upstream |

**Recommendation:** Authoritative server with client prediction & reconciliation. Destructible labyrinth + powerups + AI enemies = too much shared mutable state for lockstep determinism, and P2P fails the "trolls in public lobbies" test even at hobby scale. This is the standard for the genre.

**Why this matters:** Locks anti-cheat strategy, enables server-side XP awards, and decouples transport from game logic (so we can swap WebSocket → WebTransport later without touching game code).

---

### Decision 4 — Realtime transport

**Question:** What protocol carries match traffic from browser to server?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| WebSocket (TCP) | Universal browser support; no signaling/TURN; works on every free-tier host (Cloudflare Workers/DO included); trivial to debug | TCP head-of-line blocking under packet loss; 30–80 ms baseline RTT in-region |
| WebRTC DataChannel (UDP-like) | ~10–30 ms latency advantage under loss; unordered/unreliable modes | Signaling + ICE setup complex; ~20% of users need TURN (which is rarely free); awkward fit for server-authoritative (you run a "server peer") |
| WebTransport (HTTP/3, UDP-like) | Modern, UDP semantics, no signaling, simpler than WebRTC | Browser support still maturing in 2026; server-side library support uneven on free-tier hosts |

**Recommendation:** WebSocket for MVP. Free-tier hosts all natively support it; TCP head-of-line is acceptable at our 20 Hz tick / sub-10-player counts. Re-evaluate WebTransport as a drop-in upgrade once it stabilises broadly.

**Why this matters:** Allows Cloudflare Workers + Durable Objects (with hibernation API) as the realtime backend, which is the keystone of the free-tier plan.

---

### Decision 5 — Static frontend host

**Question:** Where does the HTML/JS/WASM bundle live?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Cloudflare Pages | Unlimited bandwidth (free); global edge cache; custom headers (COOP/COEP for Godot threads); 500 builds/month | 25 MiB per-file ceiling (move large bundles to R2 if hit) |
| GitHub Pages | Trivially tied to repo; free | No custom headers (kills Godot threaded mode); 100 GB/mo soft bandwidth cap; 1 GB site limit |
| Netlify | Easy DX; form handling | 100 GB/mo bandwidth cap — a 20 MB bundle × 5000 loads hits it |
| Vercel | Strong DX | 100 GB/mo cap; commercial-use restriction on free Hobby tier (risky for a public game) |

**Recommendation:** Cloudflare Pages. Unlimited bandwidth is the only safe choice if the game gets shared on Reddit/Discord, and it co-locates naturally with Workers + DO + R2.

**Why this matters:** Enables a single-provider stack (Pages + Workers + DO + R2 + KV) — fewer accounts, fewer free-tier ceilings to track.

---

### Decision 6 — Realtime backend host

**Question:** Where does the WebSocket game server run?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Cloudflare Workers + Durable Objects | One DO per match; WebSocket hibernation API exempts idle WS time from duration billing; same provider as Pages; global edge | 1M DO requests/month free cap is the thinnest part of the stack; DO pinned to one region per match; 30 ms CPU/req limit |
| Fly.io | Region-anywhere VM-style hosts; auto-stop when idle | No longer truly free since 2024 — assume ~$0–5/mo even idle |
| Colyseus self-host (on Fly.io / Render) | Mature TS netcode framework; rooms map naturally to matches | Needs a paid host underneath |
| Nakama self-host | Production-grade feature set | Free only if you have a free VM — we don't durably |
| Render free web service | 750 hr/mo | Sleeps after 15 min idle, 30–60 s cold-start kills lobbies |

**Recommendation:** Cloudflare Workers + Durable Objects. The WebSocket hibernation API is the single unlock that makes a stateful match server viable on a free tier. Keep game logic transport-adapter-isolated so we can port to a small Fly.io machine (~$3–5/mo) in a day if we hit the request ceiling.

**Why this matters:** Defines deployment model (Wrangler), match-server architecture (one DO instance per lobby code), and the request-budget alarm we need to instrument.

---

### Decision 7 — Auth & persistence

**Question:** Where does account / progression / leaderboard data live?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Supabase (Auth + Postgres + RLS) | One-stop: magic-link, OAuth, relational DB, row-level security; 500 MB DB + 50k MAU free | Project pauses after 7 days idle (resumeable; cron keep-alive solves it) |
| Firebase | Free auth + Firestore; generous reads | 20k writes/day cap (~14/min averaged) — tight under bursts; NoSQL fits progression awkwardly |
| Cloudflare D1 + KV (no built-in auth) | Lives next to Workers; zero-egress reads at edge | No built-in auth — we build it on Workers (sessions, OAuth flows, etc.); more code |
| PocketBase self-host | Single-binary BaaS; clean DX | Self-host means a paid VM — kills free tier |

**Recommendation:** Supabase for auth + Postgres leaderboards. RLS gives us per-user safety with no server code; the 7-day pause is trivially solved with a cron ping. Cloudflare KV is available as a hot edge cache for top-N leaderboards if it ever becomes a hot path.

**Why this matters:** Locks the identity model, the OAuth provider integration surface, and the schema-migration strategy for unlockables.

---

### Decision 8 — Identity model

**Question:** How do players become "an account"?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Anonymous-then-claim | Zero-friction first match; account is offered only when the player wants persistence | Slightly more state to handle (anonymous → claimed merge) |
| Forced sign-up | Simpler data model; every player is a real account | Friction kills casual / "send-a-link" sharing |
| Guest-only (no accounts) | Simplest | No persistent progression — kills the whole unlock system |

**Recommendation:** Anonymous-then-claim. Supabase Auth supports anonymous sessions natively; new players nickname-in and play within seconds, and only commit to email/OAuth at the first unlock-worthy moment. Matches the .io-genre conventions and supports our progression design.

**Why this matters:** This is what makes the "send a URL to friends, they play in 5 seconds" flow possible while still supporting persistent unlocks.

---

### Decision 9 — OAuth providers

**Question:** Which OAuth providers do we integrate on first launch?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Discord + Google | Discord is the social hub for this genre; Google covers nearly everyone with one click; both free via Supabase | Two integrations to maintain |
| Google only | Simplest; covers ~80% of users | Misses the Discord social graph |
| Discord + Google + GitHub | GitHub adds developer-friendly identity | One more app registration to maintain for marginal coverage |
| Email magic link only | Zero OAuth registrations | More friction; some users distrust mail-link flows |

**Recommendation:** Discord + Google + email magic link as fallback. Discord because the target audience lives there; Google for universal coverage; magic link so nobody is excluded if they don't want OAuth. GitHub/Apple can be added later if requested.

**Why this matters:** Determines how many provider app registrations + redirect URIs we configure on day one.

---

### Decision 10 — Invite mechanism

**Question:** How does a player invite friends into a match?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| 6-char lobby codes + invite links (both backed by same code) | Works on any device; shareable in Discord/SMS; no account required to use | Codes need a short TTL to avoid collisions |
| Friends-list-only invites | Reinforces account adoption | Requires accounts on both sides; can't share with strangers |
| Public lobby browser | Lowest-friction discovery | Empty browsers feel dead at hobby scale; moderation surface |

**Recommendation:** Lobby code + invite link in MVP (one code, two delivery channels). Friends list with online-presence push as a later-milestone addition. Public lobby browser deferred until CCU justifies it.

**Why this matters:** Drives the lobby-creation API on Workers and the KV-backed code-to-DO mapping.

---

### Decision 11 — Match pacing

**Question:** Is the core match real-time (Tank Trouble / Diep.io style) or turn-based (ShellShock / Atomic Tanks style)?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Real-time | Matches the user's "top-down tank labyrinth" vision; short rounds (30–120 s); higher engagement per minute; suits all referenced inspirations (Wii Tanks, Tank Trouble, Diep.io) | Needs prediction + reconciliation; tight server tick budget |
| Turn-based | Trivially netcode-light; suits ShellShock fans; lower server CPU | Doesn't fit the labyrinth/destructible/powerup vision; pacing is wrong for the references the user picked |
| Hybrid (real-time movement, turn-based fire) | Novel | Untested for this audience; design overhead |

**Recommendation:** Real-time. Every reference game the user named that fits the "top-down + labyrinth + powerups + destructible" mould is real-time. Turn-based is its own genre with different reference patterns.

**Why this matters:** Confirms the prediction/reconciliation netcode investment, tick rate (20 Hz server / 30–60 Hz client input), and 30–120 s round length target.

---

### Decision 12 — Asset style direction

**Question:** Flat-vector (Kenney top-down) or pixel-art?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Flat-vector (Kenney top-down) | One coherent CC0 source covers tanks, environment, FX, UI, and SFX (~70-80% of MVP art); zero attribution overhead; scales cleanly | Less retro charm; can read as "asset-flip" if uncustomised |
| Pixel-art (Kenney roguelike + 0x72 + bespoke) | Strong genre identity; small file sizes | Less single-source coverage; tile-size discipline required; more bespoke work needed |
| Mixed | More variety | Mixing flat-vector and pixel-art looks amateurish; choice is hard to reverse |

**Recommendation:** Flat-vector via the Kenney top-down line, per the assets slice. **USER CONFIRMED 2026-05-21.** Maximises coverage from one consistent CC0 source — we keep budget for bespoke work on destructible-wall damage states, trap pickups, destroyed/burning tanks, and the game logo.

**Why this matters:** Locks the tile-size standard (likely 64 px), the audio foley library, and the UI kit. Reversible only at significant cost.

---

### Decision 13 — Weapon progression stance

**Question:** How does weapon unlocking interact with competitive fairness?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Sidegrade-only + ranked fixed pool (progression slice) | New players competitive at match 1; all weapons unlockable in ~3 hours; ranked queue is fair by construction | Requires disciplined balancing (every weapon stays viable) |
| Power-tier unlock chain (ShellShock-style) | Familiar pattern; long-tail unlock chase | Veterans dominate new players; fundamentally pay-to-win-feeling even with zero money |
| Cosmetic-only weapons (skins on one base gun) | Simplest balance | Removes the weapon-variety fun-generator (Atomic Tanks lesson) |

**Recommendation:** Sidegrade-only with ranked fixed pool, exactly as the progression slice argued. Mastery per-weapon grants skins/accents, never power. This is the only model that honours "no money gating, ever" in spirit and not just letter.

**Why this matters:** Sets the design contract for every future weapon: sidegrade, never upgrade. Power-creep is a balance bug, not a feature.

---

### Decision 14 — Anti-cheat depth for MVP

**Question:** How much anti-cheat do we ship in MVP?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Server-side input clamps + rate limits + server-only XP (MVP minimum) | Free with authoritative-server architecture; defeats trivial cheats and bot-grinding via daily soft caps | Doesn't catch aimbots or sophisticated bots |
| Add replay review + manual report tools | Catches more; community-policed | Real engineering effort; storage cost for replays |
| Statistical / ML cheat detection | Catches subtle cheats | Massive cost; way above hobby scope |

**Recommendation:** Server-side input clamps + rate limits + server-only XP + per-day XP soft cap + a "report player" button that lands in a Supabase table. Defer replay review and ML to post-MVP — hobby scale doesn't justify the build.

**Why this matters:** Sets minimum server-side validation footprint; protects the XP economy without consuming the build budget.

---

### Decision 15 — Ranked mode

**Question:** Does MVP ship with a ranked queue, or casual-only?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Casual queue only in MVP, ranked in M3+ | Smaller MVP scope; fewer rules to ship; weapon balance can stabilise before MMR is layered on | Defers the strict-rules surface that defuses smurf/win-trade concerns |
| Ranked in MVP | Solves competitive fairness from day one | Needs MMR, fixed pool plumbing, verified-account tier, solo-queue restriction — significant extra work |
| Both queues from MVP, simpler ranked | Compromise | Splits a small playerbase across two queues |

**Recommendation:** Casual queue only in MVP; ranked queued for M3+. Match the progression slice's recommendation. Design weapons as if ranked exists so we can drop it in without rebalancing.

**Why this matters:** Removes verified-account gating, MMR, and solo-queue logic from MVP scope while preserving the design hook.

---

### Decision 16 — Repo / build layout

**Question:** One repository or several?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Monorepo (game client + Worker server + landing site + shared types) | Single PR can touch client+server+schema atomically; one CI pipeline; shared TS types/protobuf live in one place; trivial cross-cutting refactors | Larger checkout; needs CI path filtering to avoid running everything on every PR |
| Split repos (one per service) | Smaller surface per repo | Cross-cutting changes need coordinated PRs; schema drift risk; harder to keep client/server protocol in sync |
| Polyrepo with shared package | Decoupled releases | Most coordination cost |

**Recommendation:** Monorepo. At hobby scale the cross-cutting velocity (protocol changes touch both ends) is the dominant factor; CI path filters trivially keep build times bounded. One repo is also one place for ADRs, docs, and credits.

**Why this matters:** Affects CI design (path filters), deploy targets (multiple deploy jobs from one workflow), and ADR location.

---

### Decision 17 — CI/CD provider

**Question:** Where do CI builds + deploys run?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| GitHub Actions | Free for public repos with generous minutes; ubiquitous; integrates with every host; first-party Wrangler + Cloudflare Pages actions | If the repo is private, free minutes are limited (2000/mo) |
| Cloudflare Pages built-in CI | Zero-config for the static client deploy; integrates natively with Pages | Limited to the Pages site; can't run Godot exports or Worker tests there |
| Self-hosted runner | Unlimited | Needs a paid VM — defeats the free-tier story |

**Recommendation:** GitHub Actions for everything (Godot export, Worker tests, Wrangler deploys, Pages deploy via the Cloudflare Pages action). Keep the repo public for unlimited minutes; secrets live in GitHub Actions secrets (per `sec-no-hardcoded-secrets`).

**Why this matters:** Defines the M0 milestone ("green pipeline deploying a hello-world build") and the secret-management story.

---

### Decision 18 — Branching / deploy strategy

**Question:** How do changes reach production?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Main = prod, PR previews on Cloudflare Pages, no long-lived branches | Tight feedback loop; previews are free on Pages; trunk-based development | Requires green CI to be reliable (no merges with red tests) |
| GitFlow (develop + release branches) | Familiar | Overhead for a solo/small team; doesn't fit Cloudflare's preview model |
| Trunk + feature flags | Decouples deploy from release | Feature-flag plumbing has its own cost; overkill for MVP |

**Recommendation:** Main = prod with PR previews and trunk-based development. Feature flags introduced only when a feature needs gradual rollout (post-MVP). Tests must be green to merge (matches the universal rule).

**Why this matters:** Sets the "definition of done" for tickets in F2's development plan — a ticket is done when it's merged to main and auto-deployed.

---

### Decision 19 — Localisation

**Question:** Do we design for i18n from day one or ship English-only MVP?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| i18n-from-day-one (EN + ES + DK strings alongside) | Matches the universal rule ("Spanish + Danish locale strings created alongside every English string"); cheap when done from start; near-impossible to retrofit fully later | Small upfront tooling cost (string table + locale switcher + Godot's translation pipeline) |
| English-only MVP, localise later | Smaller initial scope | Retrofit is expensive and tends to miss strings baked into images/animations |
| EN + ES only (DK later) | Cheaper than three locales | Violates the universal rule which calls out Danish specifically |

**Recommendation:** i18n from day one with EN + ES + DK string tables alongside. The universal rule mandates Spanish + Danish strings alongside every English string — this is non-negotiable per the global config. Use Godot's CSV-driven translation pipeline; every UI string lives in a key.

**Why this matters:** Forces every UI ticket to author three locale entries; cheap if it's the workflow from day one, painful if retrofitted. Sets the string-table tooling for F2.

---

### Decision 20 — Free-tier ceiling stance

**Question:** What do we do if popularity pushes us past the free-tier ceiling?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Accept paid uplift up to ~$5/mo (Workers Paid) if quota is hit | Removes the "game goes dark mid-month" risk; 10× headroom for one Workers Paid plan | Crosses the "free indefinitely" preference, even if cheap |
| Hard rate-limit new lobbies to stay free | Keeps the "free forever" promise intact | When the cap is hit, new players bounce — risk of negative press |
| Federate / open-source server so community can host instances | Long-term sustainability (BZFlag pattern) | Real engineering work; not an MVP option |

**Recommendation:** Hard rate-limit to free-tier ceiling. **USER CONFIRMED 2026-05-21: no paid uplift, ever.** Federation/community-hosting (BZFlag-style) is the only long-term sustainability lever beyond rate limits.

**Why this matters:** Defines the monitoring/alarm we need (request-budget alarm at 80%), the rate-limit endpoint in the matchmaking Worker, and the eventual post-MVP federation work. No "switch to paid" toggle is built.

---

### Decision 21 — Region strategy

**Question:** Single region or user-selected regions?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Single region (e.g. EU) | Simplest; one DO region; minimal config | Australian / Asian / US players get 150–250 ms RTT |
| User-selected region on lobby create (EU/US/AP) | Better latency for players in those regions; DOs spread naturally | More DOs in flight; need to test cross-region behaviour |
| Auto-routed by GeoIP | Best UX | More logic; harder to debug; surprises when friends in different regions try to play together |

**Recommendation:** User-selected region on lobby create (EU/US/AP) with EU default. Three buttons in the create-lobby UI. Friends choose explicitly so cross-region play is honest, not surprising.

**Why this matters:** Touches the lobby-code generation flow (region tag in the code) and the DO instantiation strategy.

---

### Decision 22 — Versioning / save-format stability

**Question:** How do we evolve the unlock schema without invalidating saves?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Versioned schema in Postgres + forward-only migrations + tolerant client deserialisation | Safe long-term; Supabase migration tooling supports it; old clients keep working through additive changes | Discipline tax — every schema change is a migration ticket |
| Snapshot-and-reset on breaking changes | Simplest engineering | Players lose progress — kills trust in a no-money game built on earned unlocks |
| JSON blob with no schema | Trivially flexible | Bugs hide; data integrity bad; long-term unmaintainable |

**Recommendation:** Versioned Postgres schema with forward-only additive migrations; every unlock has a stable opaque ID (never reorder, never repurpose). New cosmetics are inserts, never updates. Client deserialisation is tolerant (ignores unknown fields, defaults missing fields). Migration files live in the monorepo and run in CI.

**Why this matters:** Protects the no-money-gating promise — players' earned unlocks must survive every update.

---

### Decision 23 — Telemetry / crash reporting

**Question:** Do we ship telemetry/crash reporting in MVP?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| None for MVP | Smallest scope; no privacy story to write | Bugs go undetected until users report; no perf baseline |
| Sentry free tier (5k errors/mo) | Mature crash reporting; Godot + JS SDKs | One more provider; needs a privacy notice |
| PostHog free tier (1M events/mo) | Product analytics + session replay | Heavier; analytics is mostly meaningless at hobby scale |
| DIY (errors to Supabase via a Worker) | Cheap; fully owned | We build the dashboard ourselves |

**Recommendation:** Sentry free tier for client + server crash reporting. **USER DECISION 2026-05-21: front-loaded to M0** (was M4 in the original plan) — crash reporting from commit #1. Skip product analytics (PostHog) until there's a question we actually want answered. A short privacy notice ("we collect crash reports; no personal data") goes in the credits screen. Tokens live in GitHub Actions secrets / CF Workers secret bindings — never in repo (per `sec-no-hardcoded-secrets`).

**Why this matters:** Without crash reporting we discover bugs by player report, which at hobby scale means we don't discover them.

---

### Decision 24 — Audio approach

**Question:** What audio stack do we use, and what's the MVP audio scope?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Godot's built-in AudioStreamPlayer + bus system | Zero extra dependencies; bus mixing, ducking, 2D positional all native; works on web export | iOS Safari has historically been finicky with audio context unlock — needs a "click to play" gate |
| Howler.js (if we go Phaser fallback) | Excellent web audio compatibility shim | Not relevant unless we drop Godot |
| Web Audio raw | Most control | Most code; AudioStreamPlayer covers what we need |

**Recommendation:** Godot's built-in audio system with a deliberate iOS audio-context unlock on first user interaction. MVP scope: SFX for fire / hit / explode / pickup / UI click / wall break (Kenney foley); one menu music loop + one in-match music loop (Pixabay/Incompetech). Defer ambient layers, dynamic music, and announcer voice.

**Why this matters:** Defines a small, achievable audio implementation scope and the foley/music sources for credits.

---

### Decision 25 — Tip jar policy (added)

**Question:** Do we ship a "support the project" link from day one, or only when costs demand it?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Tip jar from launch (Ko-fi / GitHub Sponsors), zero in-game benefit | Channel is there if a viral moment comes; explicitly signals "no payments give advantage" | Could be perceived as soliciting if shown prominently |
| Add only if costs become a problem | Cleaner launch story | If we need it suddenly we have nothing wired up |
| No tip jar ever | Purest "no money" stance | Removes the only honourable lever before paid-tier or shutdown |

**Recommendation:** Tip jar from day one as a credits-screen link only — never on the main menu, never tied to any cosmetic or gameplay benefit. The progression slice's "bright line" rule stands: tippers get zero advantage. **USER CONFIRMED 2026-05-21: include "supporter" emblem awarded for any tip amount (no tiers).**

**Why this matters:** Pre-wires the only sustainability lever short of federation, without compromising the "no money gating" rule.

---

### Decision 26 — Layered architecture for the client (added)

**Question:** Do we structure the Godot client into enforced layers (Domain / GameLogic / Data / Infrastructure / Presentation) from day one?

**Alternatives:**
| Option | Pros | Cons |
|---|---|---|
| Layered from day one with interface-first design and architecture tests | Keeps the codebase testable as it grows; surfaces violations in CI; matches the architect agent's mandate | Slightly more ceremony per feature (interface, test, implementation) |
| Flat scenes / nodes, refactor when it hurts | Fastest early velocity | "When it hurts" usually means "too late" — the labyrinth/destructible/netcode/progression cross-cutting will hurt by week 4 |
| Layered but no enforcement | Compromise | Drift inevitable without tests |

**Recommendation:** Layered from day one with interface-first design (define `IServiceName` in Domain before implementing), with a smoke architecture test in CI that asserts layer boundaries. The constraints (multiple cross-cutting concerns + multi-platform target + long-tail evolution) make this cheaper to enforce early.

**Why this matters:** Sets the project structure F2's milestone M0 needs to scaffold; defines the test categories (architecture, unit, integration) and the ADR cadence.

---

## Decisions still requiring user input

**All resolved 2026-05-21.** See the top-of-doc user-decisions block.

## Decisions that will be re-evaluated after MVP

- **Decision 4 — Transport:** Re-evaluate WebTransport as a drop-in upgrade once browser + server support stabilises.
- **Decision 6 — Realtime backend:** Re-evaluate if CF DO request quota becomes a bottleneck (port to Fly.io machine) or if we move to community-hosted federation (BZFlag pattern).
- **Decision 10 — Invites:** Add friends-list invites + online-presence push after accounts are persistent.
- **Decision 14 — Anti-cheat:** Add replay review + manual report tools post-MVP if cheating becomes a measurable problem.
- **Decision 15 — Ranked mode:** Ship in M3+ with verified-account gating, MMR, solo queue, and fixed weapon pool.
- **Decision 21 — Region strategy:** Consider auto-route by GeoIP if the explicit-region UX proves friction-heavy.
- **Decision 23 — Telemetry:** Add product analytics (PostHog or similar) if/when there are specific questions to answer.
- **Decision 24 — Audio:** Add dynamic music layers, ambient passes, and announcer SFX once core gameplay is locked.
