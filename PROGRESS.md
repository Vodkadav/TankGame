# TankGame — Progress

Real-time multiplayer tank-combat game: Godot 4 (C#) client, Cloudflare Worker +
Durable Objects backend, Supabase persistence, Android APK with shareable lobby
codes. Detailed plan: `docs/research/development-plan.md`.

This `## Status` block is the live checklist the Command Center `/projects`
dashboard reads — keep it current (mark `- [~]` when a milestone opens, `- [x]`
when done).

## Status
- [~] M0 — CI/CD live (done: scaffold, Worker /healthz skeleton, Godot client skeleton, NetArchTest layer rules, i18n EN/ES/DA bootstrap; pending: CI + deploy workflows, Sentry client+worker, pre-commit secret hook, ADR-0001/0002)
- [ ] M1 — One tank, empty arena, moves and shoots (local)
- [ ] M2 — Static labyrinth + destructible walls
- [ ] M3 — 2-player real-time via a single Durable Object
- [ ] M4 — Lobby code + invite-link flow
- [ ] M5 — Powerups + traps + enemies
- [ ] M6 — Auth (anonymous-then-claim) + persistent progression schema
- [ ] M7 — Unlockables + leaderboards
- [ ] M8 — Multi-region + ranked-mode hooks (gated off)

## M0 remaining work (resume here next session)

Direction chosen 2026-06-02: **finish M0 plumbing before M1**. Completed this
session: T4 (NetArchTest layer rules), T7 (i18n EN/ES/DA). Still open:

- **M0-T10** — pre-commit secret-scan hook (`hooks/` + `scripts/install-hooks.ps1`).
- **M0-T11** — Sentry in the Worker (`@sentry/cloudflare`, `/test-throw`, DSN via `wrangler secret`).
- **M0-T12** — Sentry in the Godot client (`Sentry` NuGet, init in `MainScene`, DSN via `OS.GetEnvironment`).
- **M0-T8 / T9** — `templates/adr-template.md` + ADR-0001 (layered arch) + ADR-0002 (monorepo/CI-CD).
- **M0-T5** — CI `ci.yml` (lint, arch-test, client-test, client-build APK, worker-test).
- **M0-T6** — deploy `deploy.yml` (Worker + Pages + GitHub Release APK; PR previews). Needs CF secrets verified in repo.

Verified local test commands:
- Arch tests: `dotnet test client/tests/Architecture/TankGame.Architecture.Tests.csproj`
- GoDotTest suite: `godot --headless --path client --run-tests --quit-on-finish`
