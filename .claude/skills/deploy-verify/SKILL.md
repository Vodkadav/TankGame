---
name: deploy-verify
description: Prove a TankGame change is actually live and working in the Lundrea Arcade before calling it fixed — re-export the .NET-to-WASM web build, ship it through ProjectX, then verify the deployed artifact itself with a byte-size cache check and a scripted click-through at the live URL. Use when the user says "is the fix live", "verify the deploy", "re-export the web build", "ship the tank game to the arcade", or before declaring ANY TankGame web fix done — a fix verified only in the working tree is not fixed.
user-invocable: true
allowed-tools: Read, Grep, Glob, Bash, mcp__Claude_Browser__navigate, mcp__Claude_Browser__computer, mcp__Claude_Browser__read_page, mcp__Claude_Browser__javascript_tool, mcp__Claude_Browser__read_console_messages, mcp__Claude_Browser__read_network_requests, mcp__Claude_Browser__tabs_create
---

Verify a TankGame change end-to-end on the **deployed** web build. This skill exists because a
bug was once declared fixed across 6+ sessions while still broken at the live URL — the working
tree, tests, and even a local run are not the artifact players get.

## Facts

| What | Where |
|---|---|
| Game repo / web branch | `C:\programmering\games\TankGame`, branch `p8/web-export-refresh` (check README "Branches" — the web branch name has changed before) |
| Deploy repo | `C:\programmering\ProjectX` → `public/tank/` (plain git blobs, **never LFS** — quota killed a deploy once) |
| Deploy trigger | CI on push to ProjectX `main` (PR → merge → GitHub Actions → `vite build` → Firebase Hosting) |
| Live URL | `https://lundrea-arcade.web.app/tank/index.html` |
| Procedure doc (authoritative) | `docs/web-export.md` **on the web branch** — toolchain, export commands, the 5 known web-only bug classes |
| Healthy export | `index.pck` ≈ 48–49 MB. **~4 MB means the managed C# never compiled in** (missing `wasm-tools` workload) — black screen, do not ship |

## Steps

1. **Preconditions.** `git status` BOTH repos first — never clobber another session's tree.
   Confirm the change under verification is actually on the web branch (it is reconciled from
   `main`, not automatically). Read `docs/web-export.md` on that branch before exporting.
2. **Export.** Per `docs/web-export.md`: verify `dotnet workload list` shows `wasm-tools`; use the
   ComplexRobot web-export editor (`C:\godot-web-export\...`), `--import` once after a fresh pull,
   then `--export-release "Web"` into `build/web/`. Gate on the healthy `.pck` size above.
3. **Deploy.** Copy `build/web/*` over `ProjectX/public/tank/`, branch → commit → PR → merge to
   `main`, then watch the Actions deploy run to green (`gh run watch`). No green run = not deployed.
4. **Cache-bust check.** `curl -sI https://lundrea-arcade.web.app/tank/index.pck` — its
   `content-length` must equal the local `build/web/index.pck` byte size (spot-check `index.wasm`
   too). Also read the response's `cache-control`, don't assume it — ProjectX's `firebase.json`
   declares `max-age=31536000, immutable` for `/tank/**` binaries, but the live header observed
   2026-07-16 was `max-age=0, must-revalidate`. If the live header is ever long-lived/immutable,
   the unfingerprinted names mean a *returning* browser keeps the old build long after deploy —
   flag the fingerprinting decision to the user instead of shipping silently.
5. **Live smoke test** (the desktop app's built-in browser in a new tab — a fresh context with an empty cache is the point).
   The game is one WASM `<canvas>` with no DOM UI: drive it by screenshot + coordinate clicks and
   verify every step visually, adapting to the current live menu labels.
   - Boot gate: page loads, canvas renders the title, no fatal console errors, no failed
     `/tank/` network requests.
   - Required trio: **create a game** (host), **join it from a second tab**, **press Start** —
     the match must start on both tabs.
   - If the change under verification has its own visible symptom, reproduce the original bug
     scenario at the live URL and screenshot the fixed behaviour.
6. **Verdict.** PASS only if every gate above passed. **Nothing is "fixed" until step 5 passes
   against the live URL.** If any step is blocked (owner gate, another session's dirty tree,
   Firebase quota), report BLOCKED at that step — never downgrade to "probably fine".

## Report format

```text
Deploy-verify — {date}
Export:  PASS — index.pck {n} MB
Deploy:  PASS — {actions run url}
Cache:   PASS — live index.pck == local ({bytes} B)
Smoke:   PASS — boot, create/join/start ({screenshots})
Verdict: LIVE-VERIFIED   (or: FAILED/BLOCKED at {step} — {why})
```

## Rules

- Never report a TankGame web fix as done from working-tree or local-run evidence alone; cite
  this skill's report instead.
- Firebase Spark free tier serves roughly 15–20 cold loads of the ~58 MB bundle per day — batch
  verification into one browser session, don't re-run the smoke repeatedly.
