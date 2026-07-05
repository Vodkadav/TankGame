# TankGame

TankGame is a real-time multiplayer top-down tank arena built with Godot 4.6 (C#, Android), a Cloudflare Worker (TypeScript) for authoritative server-side simulation via Durable Objects, and Supabase for persistent player progression. Code is MIT-licensed; visual and audio assets are CC0.

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
│   └── supabase/            Supabase schema migrations
├── shared/
│   └── protocol/            Binary protocol types mirrored in C# and TypeScript
├── docs/
│   ├── adr/                 Architecture Decision Records (Nygard format)
│   ├── credits/             Asset attribution files
│   ├── licenses/            Third-party license texts
│   ├── research/            Design research and development plan
│   └── setup/               One-time setup guides (Cloudflare, etc.)
├── scripts/                 Developer tooling (Pester tests, hook installers)
└── .github/workflows/       CI (ci.yml) and deploy (deploy.yml) pipelines
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
