# ADR-0002: Monorepo with GitHub Actions CI and Cloudflare deployment

- **Status:** Accepted
- **Date:** 2026-06-02
- **Deciders:** @architect / @devops (recorded during M0)

## Context

The product spans a Godot C# client, a Cloudflare Worker (+ Durable Objects)
backend, a shared wire protocol, and Supabase persistence. These pieces change
together (especially the client and the shared protocol), and the project is
built by a solo developer plus parallel specialist agents. We need one place to
make atomic cross-cutting changes, and an automated pipeline so that "main is
production" holds from day one. The repository is public, which makes GitHub
Actions minutes free and unlimited.

## Decision

We will keep a single monorepo with top-level `client/`, `server/`, `shared/`,
`docs/`, `scripts/`, and `.github/`. CI/CD runs on GitHub Actions:

- **CI** (`ci.yml`) runs path-filtered jobs (lint, architecture tests, client
  tests, client Android build, worker tests) so an unrelated change stays fast.
- **Deploy** (`deploy.yml`) ships on merge to `main`: the Worker via Wrangler, a
  static landing page via Cloudflare Pages, and the Godot **Android APK** to a
  GitHub Release. Pull requests get a Pages preview and an APK artifact.

All secrets live in GitHub Actions secrets (and `wrangler secret` for Worker
runtime); none are committed. A pre-commit hook scans staged files for secrets.

## Consequences

- Cross-cutting changes (client + shared protocol) land in one reviewable PR.
- Every merge is deployable and observable, which surfaces integration breakage
  immediately rather than at a release boundary.
- Path filters keep the median PR's CI short, but the workflow files grow more
  complex than a single monolithic job.
- **Web→Android retarget (M0, 2026-05-22).** Godot 4.6's .NET build cannot
  export to web/HTML5, so M0 deploys an Android APK instead of an HTML5 bundle.
  The milestone *sequence* and this CI/CD shape are unaffected; the M1–M8
  "live URL" language is pending an `@architect` web→Android revision pass.
- A public repo means all code and history are visible; secrets discipline is
  therefore non-negotiable and enforced by the hook plus a CI secret grep.
