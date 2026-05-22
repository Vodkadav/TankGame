# Tech Stack & Engine — Research Note

Status: research / recommendation (pre-ADR)
Date: 2026-05-21
Owner: @architect
Scope: pick a single primary engine/stack for a top-down multiplayer (~10 players) tank-labyrinth game that is web-first, native-portable later, free to host indefinitely.

## Evaluation axes

- **Language** — what the game code is written in
- **Multiplayer story** — does it ship netcode, or BYO?
- **2D rendering** — sprite batching, tilemaps, lighting, particles fit for top-down
- **Asset pipeline** — sprite import, atlas, tilemap editor, audio
- **HTML5 build** — bundle size + cold-load time on a typical broadband link
- **Free-host fit** — can the client live on a free static host (GitHub Pages, Cloudflare Pages, Netlify, itch.io)? Any WASM/COOP/COEP gotchas?
- **Dev ergonomics** — edit-run loop, debugging, hot reload, tooling on Windows
- **Learning curve** — for a solo dev who wants to ship, not study
- **Community / longevity** — will this still be a sensible choice in 3 years?

## Comparison matrix

| Engine | Language | Multiplayer | 2D quality | Asset pipeline | HTML5 bundle (typical) | Free-host fit | Dev ergonomics | Learning curve | Community / longevity |
|---|---|---|---|---|---|---|---|---|---|
| **Godot 4.x** | GDScript / C# | High-level multiplayer API (MultiplayerSynchronizer, RPCs) + ENet/WebSocket/WebRTC peer; BYO authoritative server is straightforward | Excellent: TileMapLayer (4.3+), Light2D, GPUParticles2D, shader2D | Built-in editor, tilemap editor, AudioStreamPlayer, .import pipeline | ~25–40 MB gzipped for a small 2D game; requires SharedArrayBuffer (COOP/COEP headers) for threads, otherwise single-thread fallback works | Good on Cloudflare Pages / Netlify (custom headers supported); GitHub Pages cannot set COOP/COEP so threads disabled but game still runs | Excellent: editor + scene tree, hot reload of scripts, F5 web export one-click | Low–moderate; GDScript is approachable, docs are good | Strong and growing; large indie adoption post-Unity-fee; LTS pattern with 4.x |
| **Bevy 0.15+** | Rust | BYO (renet, lightyear, bevy_replicon — all maturing, none "official"); WebTransport/WebSocket transports available | Very good: bevy_sprite, bevy_ecs_tilemap (community), wgpu-backed; rendering is modern but 2D ergonomics still trail Godot | Code-driven; asset loader is good but no editor — Aseprite/LDtk + community crates | ~10–20 MB gzipped for small games; wasm-bindgen + wasm-opt required; WebGL2 backend works, WebGPU optional | Excellent on any static host; pure WASM + JS shim, no special headers needed for WebGL2 path | Iteration time hurt by Rust compile times even with hot-reload crates; debugging needs effort | High: Rust + ECS + no editor is a steep combined climb | Vibrant, fast-moving; API churn between 0.x releases is real; longevity bet rather than proven |
| **Phaser 3 (+ Phaser 4 beta)** | TypeScript / JS | BYO entirely — typically Colyseus, Geckos.io (UDP via WebRTC), or socket.io | Good 2D: Arcade/Matter physics, tilemap (Tiled JSON), particles, shaders | Tiled for maps, Texture Packer for atlases, native Web Audio | ~1–3 MB gzipped engine + game code; fastest cold-load of the bunch | Perfect: plain JS bundle, drops onto any static host with zero headers | Outstanding: Vite/esbuild HMR, browser devtools, source maps, instant reload | Low: well-documented, huge tutorial corpus, idiomatic web dev | Mature and active; Phaser Studio is commercially backed; longevity solid |
| **PixiJS + custom loop** | TypeScript | BYO entirely (Colyseus, Geckos.io, raw WS) | Best-in-class WebGL2/WebGPU 2D renderer (Pixi v8); you assemble everything else | Roll your own: Tiled loader, Howler for audio, Matter/Rapier.js for physics | ~0.5–2 MB gzipped; smallest of the bunch | Perfect static-host fit | Excellent if you enjoy plumbing; lots of decisions up front | Moderate as a renderer; high as a "build a game framework" project | Strong; Pixi is the dominant 2D web renderer and isn't going anywhere |
| **Unity 6 / WebGL** | C# | NGO (Netcode for GameObjects) or third-party (Mirror, Fish-Net); WebGL transport limited to WebSocket | Excellent 2D toolset (Tilemap, 2D lights, Sprite Shape) | Mature editor, Asset Store, Addressables | ~15–30 MB gzipped minimum; cold-load 5–15s; Brotli + server config matter | Workable on Cloudflare Pages / Netlify with custom headers; itch.io supports Unity WebGL builds; free tier OK | Editor is heavyweight; iteration on WebGL specifically is slow (long builds) | Low-moderate; well-trodden path | Huge; licensing reversal of 2024 calmed waters but trust dented; still default for many studios |
| **Defold (honourable mention)** | Lua | Built-in WebSocket; partner integrations (Nakama free self-host, Colyseus) common; no built-in netcode framework | Very good 2D, designed for it; tile sources, atlas, particles | Compact editor, opinionated and fast | **~3–7 MB gzipped — best-in-class for an "engine" tier**; load time competitive with Phaser | Excellent static-host fit, no special headers | Fast iteration, project hot-reload, good profiler | Low–moderate; Lua + scene model is small | Backed by The Defold Foundation (King); steady cadence; smaller community than Godot but very stable |

(Notes on the HTML5 sizes: ranges reflect "small finished 2D game" not "hello world" — your final bundle depends on assets far more than engine. The figures are engine + minimal game logic, gzipped over the wire, as observed in published 2D projects on each engine through 2025. These figures are from model knowledge — verify against current published 2D projects before committing.)

## Free-host considerations specific to this project

1. **SharedArrayBuffer / COOP+COEP headers.** Godot 4 web export uses threads by default; without `Cross-Origin-Opener-Policy: same-origin` + `Cross-Origin-Embedder-Policy: require-corp`, it falls back to a slower single-threaded build. **GitHub Pages does not let you set response headers** — Cloudflare Pages, Netlify, and Vercel do (via `_headers` / `netlify.toml` / `vercel.json`). itch.io has a per-project "SharedArrayBuffer support" toggle. This eliminates GitHub Pages as the optimal host for Godot but not the others.
2. **Bundle ceiling.** itch.io free hosting accepts builds up to ~1 GB but practical cold-load matters; Cloudflare Pages free tier is generous (unlimited bandwidth, 500 builds/month). None of the candidates blow the limits.
3. **Realtime backend is a separate concern** (Slice D), but the engine choice constrains the protocol. WebGL builds can only do WebSocket / WebRTC / WebTransport from the browser — no raw UDP. All candidates handle this; Geckos.io (WebRTC data channels) is the lowest-latency option and is engine-agnostic.

## Multiplayer story — sharper look

The game wants ~10 simultaneous tanks per match with destructible terrain. That's small enough that an **authoritative server with snapshot/delta replication** is the right model — not lockstep (desync risk with destruction), not pure P2P (cheating + NAT). Per-engine reality:

- **Godot 4**: `MultiplayerSpawner` + `MultiplayerSynchronizer` give you replication out of the box; you write the server in Godot too (headless export) or in any language behind a WebSocket. Lowest "first playable online prototype" cost.
- **Bevy**: `lightyear` and `bevy_replicon` are credible but require you to learn ECS replication patterns. Highest-ceiling, highest-effort.
- **Phaser / Pixi**: Colyseus (TS server, room-based, free self-host on Fly.io/Render free tiers) is the de facto pairing. Excellent docs, schema-based state sync, ~10-player rooms are well within free-tier capacity.
- **Unity**: NGO works but WebGL is a constrained transport; many devs end up on Mirror + a WebSocket transport. Free-tier server hosting is identical to the others (you bring your own).
- **Defold**: typically pairs with Nakama (free self-host) for matchmaking + realtime; works but you're stitching it together.

## Native portability later

- **Godot 4**: one-click export to Windows / macOS / Linux / Android / iOS. Best-in-class.
- **Bevy**: native is the default; web is the harder target. Mobile (iOS especially) is rough as of 2025.
- **Phaser / Pixi**: web-only natively; ship native via Capacitor, Tauri, or Electron. Tauri gives you a sub-10 MB native binary, which is genuinely viable.
- **Unity**: trivially exports to every platform.
- **Defold**: one-click to all major platforms; very strong here.

## Daughter-friendliness (learning curve)

User flagged that learning curve matters because daughters might engage. GDScript and Phaser/TS are the two stacks where a beginner can read the code and follow what's happening. Rust + ECS is the opposite of that. C# in Unity is fine but the editor surface area is large.

## Recommendation

**Godot 4.x with the C# bindings (or GDScript — see open question).**

Rationale, paragraph 1: Godot is the only candidate that combines (a) excellent 2D tooling out of the box — TileMapLayer, Light2D, particles, audio buses — so you don't burn weeks rebuilding what Phaser/Pixi force you to assemble, (b) **built-in high-level multiplayer** that gets you to "two tanks moving on two browsers" in an afternoon rather than a week, and (c) genuinely good HTML5 export plus one-click native exports for the "ship to desktop/mobile later" constraint. No other candidate hits all three. Phaser wins on bundle size and ergonomics but you'll write the netcode and the tilemap loader and the particle system yourself. Bevy is technically the most exciting but punishes a solo dev's iteration time and has no editor. Unity is the safe legacy choice but the WebGL build pipeline is slow, the bundle is heavy, and trust has not fully recovered from the 2023–24 runtime-fee episode.

Rationale, paragraph 2: For free hosting, Godot's web export sits comfortably on Cloudflare Pages or Netlify with the COOP/COEP headers set (a single `_headers` file). The realtime backend is decoupled from the client choice and can be a headless Godot server, a tiny Go/TS WebSocket service, or Nakama/Colyseus on a free Fly.io machine — all of which Godot's networking layer can speak to. The destructible tilemap requirement maps cleanly onto `TileMapLayer` + server-authoritative tile state changes. And for a solo dev who values ergonomics, the edit-play loop in Godot 4 is fast: change a script, press F5, see the change.

## The one realistic risk

**Godot's HTML5 export is the weakest of its export targets.** Specifically: cold-load is heavier than Phaser/Pixi (25–40 MB gzipped vs 1–3 MB), audio on iOS Safari has historically been finicky, and the threads-vs-no-threads split means you have to test both header configurations. If the share-with-friends path is "send a URL, they click, they play in 3 seconds," that's a Phaser experience, not a Godot one. If it's "send a URL, they wait 10–15 seconds on first load, then play smoothly," Godot is fine. **You should validate the cold-load experience on a real mobile browser before committing past the prototype phase** — this is the single decision-reversing risk.

If the cold-load proves unacceptable on the target audience's devices, the fallback is **Phaser 3 + Colyseus**, which trades engine-provided 2D conveniences for a fast-loading, infinitely-static-hostable web build with a mature TS netcode story.

## Open questions for the user

1. **GDScript or C#?** GDScript is faster to iterate and friendlier for kids reading code; C# gives you a stronger type system and shared idioms with the broader .NET world. The architect's lean is **GDScript for the prototype** to maximise iteration speed, with a clear seam to swap individual hot-path systems to C# later if needed — but this is a user call because it touches your daughters' potential involvement.
2. **Is a 10–15s cold-load on first visit acceptable** for the "send a link to friends and daughters" path, or is sub-3-second a hard requirement? Answer determines whether Godot stays as the recommendation or Phaser takes over.

## Downstream implications (for Slice D)

- Realtime transport: WebSocket from the browser, with WebRTC/Geckos.io as a possible upgrade path for latency.
- Authoritative server language is now a separate decision — headless Godot is one option, but a small TS/Go service is equally valid and may be cheaper to host. Slice D should evaluate both with the engine choice already pinned.
