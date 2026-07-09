# Code review — feat/cartoon-menu-ui vs main (2026-07-08)

Recorded incrementally; findings appended as found.

**TL;DR: one blocker.** #12 — no client ever sends the `loaded` handshake, so every networked match deadlocks in the new `loading` phase after countdown. #7 (leave-during-loading deadlock server-side) compounds it. Fix both before merging/exporting. Everything else is low/cosmetic.

### Repo hygiene

- **#21 [Low] Duplicated head-include** — `web_export_head_include.html` and the inline `html/head_include` string in `export_presets.cfg [preset.1.options]` are two copies of the same CSS; the preset only reads the inline string. *Fix:* keep one (delete the .html, or note it as the editable source and regenerate).
- **#22 [Low] Untracked files** — new `.uid` files (ArenaBuilders, MenuStyle, SeatIcon, tests) should be committed with their .cs files; `live.pck`/`live.wasm` at repo root look like local export artifacts — gitignore them.

## Findings

### Client wiring gaps (CRITICAL)

- **#12 [CRITICAL] No client ever sends the `loaded` handshake — multiplayer start deadlocks** — server flow is now countdown → `loading` → started, and `markLoaded` requires every seated player to send `{type:"loaded"}`. On the client, `LobbyController.SendLoaded()` and `IsLoading` exist but have **zero callers** (grep confirms only their definitions). After the countdown every room sits in `loading` forever and the match never starts. *Fix:* in `LobbyRoomScene.Render()`, when `_controller.IsLoading` flips true, transition/build the arena and call `SendLoaded()` (or, minimal: send `SendLoaded()` immediately on entering Loading and keep the existing HasStarted handoff). Add a scene test that drives Waiting→Countdown→Loading→Started.
- **#13 [Med] Match seed is plumbed but never consumed** — worker stamps `seed` at start; `LobbyController.Seed` exists; nothing reads it (grep: no consumer). `AiInputSource` docs claim "the host passes matchSeed ^ slot" but no call site does. Bots still seed from tank-id hash; fine single-player, but net clients won't agree if AI ever runs guest-side. *Fix:* wire `NetworkSession.StartedLobby.Seed` into the net arena's AI construction, or delete the plumbing until the slice that needs it.

### GameLogic (AiInputSource, SpawnTable, Tank, Airstrike, ArenaBuilders)

- **#1 [Low] SpawnTable.NearestOpen fallthrough can return a duplicate spawn** — `SpawnTable.cs:~75`: when every ring cell is blocked/taken it returns `from` unchanged, ignoring the `taken` set, so two of the eight spawns can coincide on a nearly-full map. *Fix:* on fallthrough, return `from` only if not taken; else nudge deterministically (e.g. scan row-major for first free cell) — or accept and document, real maps never fill.
- **#2 [Low] AiInputSource field initializer wastes a personality roll** — `AiInputSource.cs:116`: `_personality = AiPersonality.Roll(new Random())` runs per construction and is always overwritten in `Bind`. *Fix:* initialize to `default`; `Read()` already guards on `_self is null` before use. Cosmetic.
- **#3 [Info] AiInputSource stale target reference** — `PickEnemy` returns the cached `_target` object matched by Id. Safe only because ITank instances are persistent per match (they are, today). If tanks ever become per-tick snapshots this silently uses stale positions. No action now; worth a comment.
- **#4 [Low] WeightedPick evaluates weight delegate twice per item** (total pass + roll pass). Lists are tiny (≤8); fine. No action.
- **#5 [OK] Airstrike telegraph-first timing** (`ExplodeTime = armWindow + delay + ArmTime(i)`) matches the commit intent; single-zone case (`ArmTime=0`) correct.
### Server (lobbyState.ts, MatchRoom.ts) + Net protocol

- **#7 [HIGH] Loading-phase deadlock when a player leaves mid-load** — `lobbyState.ts` `leave()`: `allLoaded` is only re-evaluated inside `markLoaded()`. If the one player who hasn't loaded disconnects during `loading` (and players remain ≥ MIN_PLAYERS_TO_START), the remaining players are all loaded but no further `loaded` command ever arrives — room stuck in `loading` forever. *Fix:* in `leave()`, when phase is `loading` and the remaining players are all loaded, transition to `started` (or re-run the allLoaded check after any roster change).
- **#8 [Med] No reconnect path once launching** — `MatchRoom.ts:61`: joins rejected whenever phase ≠ waiting, so a browser tab hiccup during the 3s countdown + loading permanently locks that player out (and per #7 may hang the room). Acceptable for now, but worth a deliberate decision; document if intended.
- **#9 [Low] GodotHttpLobbyClient has no request timeout** — Godot `HttpRequest.Timeout` defaults to 0 (never); a stalled request awaits forever and leaks the child node. *Fix:* `request.Timeout = 10;`.
- **#10 [Info] Client `loaded` handshake fields wired symmetrically** (Loaded flag, seed, "loading" phase parse) — matches worker JSON; missing-field fallbacks keep old servers compatible. OK.
- **#11 [OK] Reflection-free Utf8JsonWriter command encoding** — correct WASM fix; reader side already JsonDocument.

### Presentation / UI

- **#14 [Low] MenuStyle.Lift stacks tweens** — `MenuStyle.cs` `Lift()` creates a new tween per hover/focus event without killing the previous one; rapid enter/exit runs competing scale tweens. *Fix:* keep the tween in metadata (`control.SetMeta`) and `Kill()` the old one, or use `control.CreateTween()` → `tween.SetParallel(false)` with `Tween` stored per control. Cosmetic jitter only.
- **#15 [Low] LobbyBrowserScene refresh rebuilds rows next to QueueFree'd ones** — children are `QueueFree`d (removed end-of-frame) then new rows added in the same frame; visually fine since freed nodes hide at frame end, but `_list.GetChildren()` briefly contains both. Guarded by `_refreshing`; acceptable. Consider `child.Free()` or `RemoveChild`+`QueueFree` if flicker appears.
- **#16 [Low] Stale MatchRoom.ts header comment** — top-of-file comment still says "Up to 4 players" (`lobbyState.ts:3`) and "countdown … → phase 'started'" (`MatchRoom.ts:5-6`) — both outdated by this branch (8 players, loading phase). Doc-only.
- **#17 [Info] Frozen arena uses Sand theme, Canyon uses Mars** — stubs, flagged in code as content-slice placeholders. OK per plan.
- **#18 [OK] NetArena3DScene slot carry** (`NetworkSession.LocalSlot` + idempotent `OnWelcome`) — sound fix for the one-shot welcome race; duplicate-welcome guard present.
- **#19 [OK] i18n** — 8 new string rows match the 8 new keys (5 maps, editor.lava, room.ready, room.host_left).
- **#20 [OK] Web lobby client selection** via `OS.HasFeature("web")`; browser scene parents the Node-based client so requests pump.

- **#6 [OK] Tank lava kill** uses `TakeDamage(Hp + Shield)` so shield order doesn't matter; airborne exemption present.
