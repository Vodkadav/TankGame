# ADR-0002: Monorepo with GitHub Actions CI and Cloudflare deployment

**Date:** 2026-06-02
**Status:** Accepted
**Deciders:** Solo developer + Claude Code (architect / devops)

## Context

The product spans a Godot C# client, a Cloudflare Worker (+ Durable Objects)
backend, a shared wire protocol, and Supabase persistence. These pieces change
together (especially the client and the shared protocol), and the project is built
by a solo developer plus parallel specialist agents. We need one place to make
atomic cross-cutting changes and an automated pipeline so that "main is production"
holds from day one. The repository is public, so GitHub Actions minutes are free.

## Decision

We keep a single monorepo with top-level `client/`, `server/`, `shared/`, `docs/`,
`scripts/`, and `.github/`. CI/CD runs on GitHub Actions:

- A Docs CI (`.github/workflows/docs.yml`) validates markdown, internal links, and
  ADR section structure on every push and PR.
- A build/test CI (`ci.yml`, M0-T5) runs path-filtered jobs: lint, NetArchTest,
  Godot client tests (headless GoDotTest), the Android debug APK export, and Worker
  tests.
- A deploy workflow (`deploy.yml`, M0-T6) ships on merge to `main`: the Worker via
  Wrangler, a static landing page via Cloudflare Pages, and the Android APK to a
  GitHub Release. Pull requests get a Pages preview.

All secrets live in GitHub Actions secrets (and `wrangler secret` for Worker
runtime); none are committed. A pre-commit hook scans staged files for secrets.

## Consequences

- Cross-cutting changes (client + shared protocol) land in one reviewable PR.
- Every merge is deployable and observable, surfacing integration breakage
  immediately rather than at a release boundary.
- Path filters keep the median PR's CI short, at the cost of more complex workflow
  files than a single monolithic job.
- **Web → Android retarget.** Godot 4.6's .NET build cannot export to web/HTML5, so
  the client deploys an Android APK rather than an HTML5 bundle. The milestone
  sequence and this CI/CD shape are unaffected; the M1–M8 "live URL" language is
  pending an architect web→Android revision pass.
- A public repo means all code and history are visible, so secrets discipline is
  non-negotiable and enforced by the hook plus a CI secret grep.
