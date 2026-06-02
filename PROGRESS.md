# TankGame — Progress

Real-time multiplayer tank-combat game: Godot 4 (C#) client, Cloudflare Worker +
Durable Objects backend, Supabase persistence, Android APK with shareable lobby
codes. Detailed plan: `docs/research/development-plan.md`.

This `## Status` block is the live checklist the Command Center `/projects`
dashboard reads — keep it current (mark `- [~]` when a milestone opens, `- [x]`
when done).

## Status
- [~] M0 — CI/CD live (all tickets implemented + locally verified; awaiting first-PR validation that ci.yml is green and deploy.yml ships — see "M0 verification gate" below)
- [ ] M1 — One tank, empty arena, moves and shoots (local)
- [ ] M2 — Static labyrinth + destructible walls
- [ ] M3 — 2-player real-time via a single Durable Object
- [ ] M4 — Lobby code + invite-link flow
- [ ] M5 — Powerups + traps + enemies
- [ ] M6 — Auth (anonymous-then-claim) + persistent progression schema
- [ ] M7 — Unlockables + leaderboards
- [ ] M8 — Multi-region + ranked-mode hooks (gated off)

## M0 verification gate (resume here next session)

Direction chosen 2026-06-02: **finish M0 plumbing before M1**. All M0 tickets
are now implemented and committed on branch `m0-cicd`:

- T4 NetArchTest layer rules · T7 i18n EN/ES/DA · T10 pre-commit secret hook ·
  T11 Sentry Worker · T12 Sentry client · T8/T9 ADR template + ADR-0001/0002 ·
  T5 `ci.yml` · T6 `deploy.yml`.

**Only one thing stands between here and M0 done:** the CI/CD workflows have not
yet run on real GitHub Actions (cannot be executed locally; deploy.yml performs
live Cloudflare deploys + GitHub Releases). To close M0:

1. Open a PR from `m0-cicd` → `main`; watch `ci.yml`. Iterate on any failure.
   Highest-risk job: `client-build` (Android export — verify `setup-godot`
   inputs, export-template install, and the `GODOT_ANDROID_KEYSTORE_DEBUG_*`
   env names).
2. Confirm the Pages preview URL loads and the worker preview path responds.
3. Merge; confirm the APK Release publishes and `smoke-test` hits `/healthz`.
4. Confirm a thrown test exception (`/test-throw`) reaches the Sentry dashboard.
   Note: client-side DSN is not yet baked into the Android build (env vars do
   not reach the running app) — that delivery is a deferred refinement.

Then flip the M0 line to `- [x]`.

Verified local test commands:
- Arch tests: `dotnet test client/tests/Architecture/TankGame.Architecture.Tests.csproj`
- GoDotTest suite: `godot --headless --path client --run-tests --quit-on-finish`
- Worker tests: `pnpm -C server/worker test`
- Secret-scan hook: `Invoke-Pester scripts/test-secret-scan.ps1`
