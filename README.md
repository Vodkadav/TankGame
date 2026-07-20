# TankGame

TankGame is a real-time multiplayer top-down tank arena built with Godot 4.6 (C#, Android), a Cloudflare Worker (TypeScript) for authoritative server-side simulation via Durable Objects. Supabase-backed persistent player progression is a planned addition (`server/supabase/` is scaffolded but not yet built out — see `docs/research/progression.md`). Code is MIT-licensed; visual and audio assets are CC0.

## Repository map

```text
TankGame/
├── client/                  Godot 4.6 C# project
│   ├── src/
│   │   ├── Domain/          Pure C# interfaces and value objects (no Godot deps)
│   │   ├── GameLogic/       Game rules and simulation (depends on Domain only)
│   │   ├── Data/            Data-access implementations (depends on Domain only)
│   │   ├── Infrastructure/  Platform adapters — input, networking (depends on Domain+GameLogic+Data)
│   │   └── Presentation/    Godot scenes and scripts (depends on GameLogic+Domain)
│   └── tests/               GoDotTest + NetArchTest suites
├── server/
│   ├── worker/              Cloudflare Worker (TypeScript, Wrangler, Vitest)
│   └── supabase/            Reserved for Supabase schema migrations (planned, not yet built)
├── shared/
│   └── protocol/            Reserved, currently unused — the binary protocol is instead
│                             hand-mirrored in client/src/Domain/Net/ (C#) and
│                             server/worker/src/protocol/ (TypeScript)
├── docs/
│   ├── adr/                 Architecture Decision Records (Nygard format)
│   ├── credits/             Asset attribution files
│   ├── licenses/            Third-party license texts
│   ├── research/            Design research and development plan
│   └── setup/               One-time setup guides (Cloudflare, etc.)
├── scripts/                 Developer tooling (Pester tests, hook installers)
└── .github/workflows/       CI (ci.yml) and deploy (deploy.yml) pipelines
```

## Installation

Prerequisites: **Godot 4.6.2 .NET/Mono** (the C# editor build) and the **.NET 9 SDK**.
The Cloudflare Worker (multiplayer relay) additionally needs **Node 22 + pnpm** — it is
optional for local single-player and co-op.

```sh
# 1. Clone
git clone https://github.com/Vodkadav/TankGame.git
cd TankGame

# 2. Godot C# client
dotnet restore client/TankGame.csproj
# Regenerate .import/.uid/.translation caches (all gitignored) after a fresh pull:
godot --headless --path client --import

# 3. Cloudflare Worker relay (optional — only for online multiplayer)
cd server/worker
pnpm install --frozen-lockfile
cd ../..
```

## Usage

```sh
# --- Play ---
# Desktop: open the client/ folder in the Godot 4.6.2 .NET editor and press F5,
# or launch the main scene headlessly from the repo root:
godot --path client

# --- Build (compile check) ---
dotnet build client/TankGame.csproj -c Debug

# --- Tests ---
# Pure-C# xUnit suites (swap in Domain / Infrastructure / Architecture the same way):
dotnet test client/tests/GameLogic/TankGame.Tests.GameLogic.csproj -c Debug
# In-engine GoDotTest scene tests (headless Godot; exit code gates CI):
godot --headless --path client --run-tests --quit-on-finish
# Cloudflare Worker (Vitest):
cd server/worker && pnpm test && cd ../..

# --- Export the Android APK (arm64-v8a) ---
godot --headless --path client --export-release "Android" build/tankgame.apk

# --- Export the browser (WASM) build ---
# Needs the ComplexRobot C# web-export editor + `dotnet workload install wasm-tools`.
# Full walkthrough and gotchas: docs/web-export.md
godot --headless --path client --export-release "Web" build/web/index.html
```

## Branches

- **`main`** — primary development line. A fresh clone lands here.
- **`p8/web-export-refresh`** — long-lived branch that holds the browser/WASM
  export of the game (Godot web build, deployed to Firebase Hosting via the
  ProjectX arcade). Kept reconciled onto `main`'s gameplay batches.

To continue web-export work on another machine, clone and check out the branch:

```sh
git clone https://github.com/Vodkadav/TankGame.git
cd TankGame
git checkout p8/web-export-refresh
```

(Locally it is often kept as a separate git worktree alongside `main` — e.g.
`TankGame-web-export/` — but that is a per-machine convenience and does not need
to be recreated; the branch carries everything.)
