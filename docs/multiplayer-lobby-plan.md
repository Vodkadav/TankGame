# Multiplayer Lobby — State, Issues & Plan

> **Status (2026-07-17): EXECUTED.** This spec was implemented as the multiplayer redesign
> (#243/#244, Phases 0–8 via #245/#246, menu restructure #247/#248). Ported to main from the
> never-merged `docs/multiplayer-lobby-plan` branch during branch cleanup — PROGRESS.md cites
> this path. Kept as the design record; the "1b unmerged backbone" branch it describes is deleted.

**Goal (owner ask, 2026-07-05):** In the arcade, the title screen offers **Solo** and **Multiplayer**.
Multiplayer opens a **scrollable browser of open games** (each row shows **FFA** or **Team vs Team**
and a **Join** button) with a **Create Game** button below. Create lets you pick a **map** (or *random*,
the default) and a **mode** (FFA default / Team), and opens a **room with up to 4 slots**. Once created,
the game is listed in the browser. The room shows each **playable spot** with a **gray placeholder name**
until a real player joins, plus a **Start** button. Pressing Start replaces every un-joined slot with an
**AI tank carrying that slot's placeholder name**.

> This document is the hand-off spec. Implementation to be done by a separate pass (Fable 5). It records
> what already exists, the risks, the decisions, and a phased TDD plan. **No code has been written for it yet.**

---

## 1. What already exists

### 1a. LIVE today (on `main`, shipped to the arcade)
- **Title menu** (`client/src/Presentation/TitleScene.cs`): `Solo | Team vs Team | Select Map | Editor |
  Settings | Exit`. Every entry name-prompts first. Solo drops into a **random map** already
  (`SoloMapSelection.Pick` over built-in arenas + created maps).
- **"Team vs Team"** → `client/src/Presentation/LobbyScene.cs`: a **code-based Host/Join** screen (host
  mints a 6-char code; friend types it). **2-player only, no browser, no mode/map choice.** This is the
  screen the new design replaces.
- **Backend worker** (`server/worker/src/`): `lobby.ts` (`POST /lobby` create + join-by-code over KV),
  `MatchRoom.ts` relay Durable Object (`MAX_PLAYERS = 2`, host-authoritative per-slot routing),
  `protocol/codec.ts` snapshot codec.
- **Netcode** (`client/src/Presentation/Arena/NetArena3DScene.cs`, `HostSession.cs`, `NetworkSession.cs`):
  host/guest 3D match, host-authoritative. Netcode-fidelity fixes #218/#219/#220/#234 landed here.

### 1b. Unmerged backbone on branch `feat/multiplayer-lobby`
7 commits, ~1135 lines, **13 commits BEHIND main**, last touched 2026-06-30, never PR'd. It is the richer
browser+multi-slot design — mostly what we want, but **stale**:

| File (new unless noted) | What it gives us | Tests |
|---|---|---|
| `server/worker/src/lobbyState.ts` | Pure lobby reducer: **FFA/Team**, slots, teams, ready, **any-player start + 3s countdown**, host reassignment. `MAX_PLAYERS = 8`. | 18 |
| `server/worker/src/lobbyDirectory.ts` + `GET /lobbies` | **Lobby browser** backend — publish/withdraw a room summary to KV, list open lobbies in one call. | — |
| `server/worker/src/MatchRoom.ts` *(modified)* | 8-player room DO, JSON control channel (`0x10` state / `0x11` cmd, server stamps slot), countdown via DO alarm. | 57 total |
| `client/src/Domain/Net/LobbyProtocol.cs` | C# mirror: `LobbyView`/`LobbyPlayer`, parse state, encode commands. | 6 |
| `client/src/Domain/Net/IMatchTransport.cs` + `Infrastructure/Net/WebSocketTransport.cs` *(modified)* | Carry the lobby channel over the transport (`LobbyStateReceived` + `SendLobby`). | 8 |
| `client/src/GameLogic/LobbyController.cs` | Client lobby brain over the transport. | 6 |

### 1c. Netcode capacity — the good news
A dedicated investigation confirmed the **wire format and codec are already N-tank capable** — nothing
on the protocol layer is baked to 2:
- `codec.ts` / `ProtocolCodec.cs` write a **tank-count byte then loop** (up to 255). `TankState` already
  carries `byte Slot` **and** `byte Team` on the wire — **team modes need no wire change**.
- `MatchRoom.ts` routing already generalizes to N (slot = connection index; host↔guests fan-in/out). Only
  the `MAX_PLAYERS = 2` constant caps it.
- `HostSession.cs` already **loops N tanks** for snapshots/projectiles.
- Render/mirror path in both net scenes is already per-slot `Dictionary<byte,…>` — **no 2-cap**.
- `TeamPalette.cs` already defines **4 team colours** ("up to four teams").
- `AiInputSource.cs` is **team-aware and multiplayer-safe** (emits the same `TankInput` a human does,
  targets any tank on another team) — it can drop straight into a host slot as "AI fill".

**The only place "2" is truly hardcoded is the host spawn/team loop** (`NetArena3DScene.BecomeHost`) — one
host tank (team 0) + one guest (team 1) at a single hardcoded `GuestSpawn (25,7)`.

---

## 2. Issues, gaps & risks

1. **Two divergent lobby implementations.** `main` = code-based Host/Join (live); `feat/multiplayer-lobby`
   = browser+multi-slot (stale). **The feat branch's `MatchRoom.ts`/transport changes predate main's
   netcode fixes (#218/#220/#234)** — a straight merge will conflict on exactly those files. **This is the
   biggest sequencing risk.** → *Do not merge the stale branch.* Start fresh off `main`; pull the clean,
   non-overlapping new files across; hand-reconcile `MatchRoom.ts`/transport/`index.ts`/`codec.ts` on top.
2. **No map field in the lobby model.** `lobbyState.ts`, `OpenLobby` (directory), and `LobbyProtocol.cs`
   have **no `map` field**. Create-with-map needs an additive field end-to-end (reducer → directory
   summary → C# view → create request → host launch).
3. **No lobby UI at all.** Only backend endpoints exist. Need **two new Godot scenes**: the **browser**
   (list + Join + Create) and the **room** (4 slots with placeholder names + mode/map + Start). The old
   `LobbyScene` is replaced.
4. **Host loop hardcodes 2 + no AI fill.** `BecomeHost` must become a loop over a **roster** (slot, team,
   spawn, human-vs-AI). Un-joined slots get an `AiInputSource`-driven tank on Start. `HostSession`'s single
   `_guestInput`/single ack must become **per-slot relayed inputs keyed by sender slot**.
5. **Multi-spawn table missing.** Only one `GuestSpawn` cell. Need N spawns per map. The editor already
   supports **numbered spawns (up to 8)** — wire the map's spawn table into the net scene instead of the
   literal.
6. **FFA/Team win logic.** Current end = 2-player last-standing. Need: **FFA = last tank standing**;
   **Team = last team standing**. New win-condition in the net scene / a pure helper.
7. **Countdown → launch hand-off.** The lobby's `started` phase must pass the **roster (slots, names,
   teams, mode, map, AI-flags)** into `NetArena3DScene`, which today decides roles purely from the relay
   slot. Net scene must consume the roster + mode + map.
8. **Player-cap mismatch.** Backbone = 8, live relay = 2, owner wants **4**. Standardise on **4**
   (backbone note: "extendable later"): reducer `MAX_PLAYERS`, relay cap, spawn-table size.
9. **Placeholder names.** Reuse the existing **"derpy AI cast"** name set (commit `b0ee97a`) to fill empty
   slots gray in the room, and as the AI tank's name on Start.
10. **i18n.** All new strings need **EN/ES/DK** rows in `client/i18n/strings.csv` (existing `lobby.*` keys
    are for the old code flow — most get replaced).
11. **Reach players = P8.** Nothing is live until the **WASM re-export + ProjectX PR** (owner-gated merge;
    see `ref-arcade-wasm-pipeline`).

---

## 3. Decisions to confirm before coding
- **Max players = 4** (owner said "up to 4… extendable later"). ✅ assume yes.
- **Browser-primary, code under the hood.** The 6-char code stays as the DO room name / deep link, but the
  primary UX is the browser list. Drop the "type a code" screen. *(Recommended — matches the ask.)*
- **Team count = 2** for Team-vs-Team (teams 0/1); FFA = each slot its own team. ✅
- **Map pool for Create** = same pool as Solo random: built-in arenas (`DesertWar`, `CliffsAndValleys`) +
  user-created maps (`MapRepository.List()`), reusing `SoloMapSelection`. *Random is the default.*
- **AI difficulty for fill** = the existing `AiInputSource` defaults (no new tuning this pass).

---

## 4. Phased plan (TDD — every phase is red→green→refactor)

**Phase 0 — Reconcile the backbone onto `main`.**
New branch off current `main`. Bring across the **clean, non-overlapping** new files verbatim
(`lobbyState.ts` + tests, `lobbyDirectory.ts` + tests, `LobbyProtocol.cs` + tests, `LobbyController.cs` +
tests). Hand-reapply the `MatchRoom.ts`, `IMatchTransport`/`WebSocketTransport`, `index.ts`, `codec.ts`
deltas **on top of main's current versions** (which have the newer netcode). Get worker + C# suites green.
Set `MAX_PLAYERS = 4` in `lobbyState.ts` **and** `MatchRoom.ts`.

**Phase 1 — Map in the lobby model.**
Add `map` to `LobbyState`/`emptyLobby`/create path (`string` map id, `""` = random), to `OpenLobby`
summary, and to `LobbyView`/`LobbyProtocol` (C# mirror). Reducer tests for map carry-through. Resolve
`""` → a concrete map at host-launch via `SoloMapSelection`.

**Phase 2 — Title restructure.**
`TitleScene`: `Solo` (unchanged) + **`Multiplayer`** (replaces "Team vs Team") → the new **browser** scene.
New i18n key `title.multiplayer`. Update `TitleSceneTests`.

**Phase 3 — Lobby browser scene.**
New `LobbyBrowserScene` (+ `.tscn`): scrollable list from `GET /lobbies` via `ILobbyClient` (extend it with
`ListOpenLobbiesAsync`), each row = name/mode-badge (**FFA**/**Team**)/player-count + **Join**; **Create
Game** button below; **Back**. Click-path tests against the fake lobby client. Refresh on show.

**Phase 4 — Create-game flow.**
Create panel: **map dropdown** (Random default + pool) + **mode toggle** (FFA default / Team). Create →
`POST /lobby` (with mode+map) → connect → room scene. Reuse the map pool from `SoloMapSelection`.

**Phase 5 — Lobby room scene.**
New `LobbyRoomScene` (+ `.tscn`) driven by `LobbyView` (via `LobbyController`): **4 slot rows**, each real
player = name (+ ready), empty = **gray placeholder name** (from the derpy cast). Mode/map shown; host sees
**Start**; **Leave**. Wire ready/leave/start commands. Countdown display (`countdown` field). Click-path
tests over a fake transport pushing `LobbyView`s.

**Phase 6 — Netcode 2→4 + AI fill + modes.**
- `NetArena3DScene.BecomeHost`: replace the 2-tank block with a **loop over the roster** (slot, team, spawn
  from the map's spawn table, human-or-AI). Empty slots → `AiInputSource` tank named from the slot's
  placeholder. Multiple `RelayedInputSource`s keyed by sender slot.
- `HostSession`: per-slot relayed inputs + per-slot ack (tank/projectile loops already scale).
- Team assignment from mode (FFA = team==slot; Team = 0/1 split).
- **Win logic**: pure helper — FFA last-tank / Team last-team — consumed by the net scene's round end.
- Multi-spawn table from map data (numbered spawns, up to 4 used).

**Phase 7 — Hand-off wiring.**
`started` phase → launch `NetArena3DScene` with the roster (slots, names, teams, mode, map, AI-flags)
carried through `GameSetup`/`NetworkSession`. Guests mirror; host authoritative.

**Phase 8 — i18n + polish.**
All new keys in `strings.csv` (EN/ES/DK). Remove dead `lobby.*` code-flow keys. Touch controls already work
in the net scene — verify the new UI is touch-usable.

**Phase 9 — Export + deploy (P8).**
Re-export the .NET→WASM build, `cp build/web/* ProjectX/public/tank/`, PR ProjectX (owner-gated merge →
Firebase). See `ref-arcade-wasm-pipeline`.

---

## 5. Reuse ledger (don't rebuild)
- Lobby reducer, directory, C# protocol mirror, LobbyController, transport lobby channel — **from
  `feat/multiplayer-lobby`** (Phase 0).
- Snapshot codec, per-slot relay routing, `HostSession` N-tank loop, per-slot mirror/render, `TeamPalette`
  (4 colours) — **already on `main`, unchanged**.
- `AiInputSource` (team-aware, TankInput seam) — **AI fill, no new AI**.
- `SoloMapSelection` + `MapRepository` — **the Create map pool + random default**.
- Derpy-AI-cast names (`b0ee97a`) — **placeholder + AI slot names**.
- Touch controls (`TouchControls`/`TouchInput3DSource`) — already in the net scene.

## 6b. Live arcade bugs (Solo mode) — **Fable owns these alongside the lobby work**

Investigated 2026-07-05 after an iPad report: **Solo shots intermittently deal no damage** (works for
periods, then not) and **noticeable lag**. Solo is fully local (no netcode), so both are client-side. The
two are causally linked — the lag *causes* the flaky damage. **No fix was applied in the planning session:
none is a safe trivial one-liner** — the damage fix is substantive (and its fixed-timestep half is the same
determinism work Phases 6–7 need), and every lag lever touches the main loop, which the 60 fps rule gates on
an on-device iPad profiler result. Fable should take these **with** the multiplayer pass (do them first —
they're independently shippable and unblock nothing).

### Bug A — flaky shot damage = projectile-vs-tank **tunnelling** under low FPS

**Root cause (confirmed):**
- The whole simulation runs in `Arena3DScene._Process(delta)` → `_world.Step((float)delta)`
  (`Arena3DScene.cs:868-875`) — a **variable timestep with no accumulator, no substepping, no clamp**.
- A shot moves in **one discrete jump** per step: `position += direction * (Speed * delta)`
  (`StraightBehaviour.cs:28`), `ProjectileSpeed = 600` u/s (`Arena3DScene.cs:23`).
- The **projectile↔tank hit test is a single point-overlap sampled after the jump**:
  `Vector2.Distance(shot.Position, tank.Position) <= 28` (`CombatResolver.cs:76`, `CombatHitRadius = 28`).
  Tanks are **not swept** — only *walls* are (walls use a raycast over the step, `StraightBehaviour.cs:20`,
  so walls never tunnel; tanks do).

**Why it's flaky and lag-correlated.** The shot is only "seen" at discrete points spaced `600 × delta`
apart. The hit window is ~56 u wide (2 × 28). So:

| FPS | delta | step per frame | result |
|----:|------:|---------------:|--------|
| 60 | 16 ms | 10 u | always hits |
| 30 | 33 ms | 20 u | reliable |
| 20 | 50 ms | 30 u | glancing hits start missing |
| 15 | 66 ms | 40 u | flaky |
| ~11 | 91 ms | ~55 u | even centre hits start skipping |
| 8 | 125 ms | 75 u | shot reliably jumps clean over the tank → no damage |

On a laggy iPad WASM frame (dipping to 8–15 FPS, or a single GC hitch spiking one `delta`) the shot skips
past the tank between samples and deals no damage — exactly "works semi-correctly for periods, then not."

**Fix (recommend both; primary is the fixed timestep):**
1. **Primary — fixed-timestep accumulator.** Drain accumulated real time in a `while` loop stepping
   `_world.Step(1f/60f)` (cap ~5 substeps/frame to avoid a spiral of death). Makes hit-detection
   frame-rate-independent and deterministic; under extreme lag the game slows to slo-mo instead of losing
   hits. **This is also required for multiplayer determinism (composes with Phases 6–7).** Apply to all
   three play scenes (`Arena3DScene`, `ArenaScene`, `NetArena3DScene`).
2. **Defence-in-depth — swept projectile↔tank test.** Make `CombatResolver` test the shot's *segment*
   this step (`prevPos → pos`) against each tank circle (segment-vs-circle), mirroring the wall raycast, so
   a single large step can never skip a tank. This alone fixes the damage flakiness even before the loop
   change. Needs the projectile to expose its pre-step position.

### Bug B — lag on iPad WASM

**Confirmed contributor — per-frame GC garbage in the hot loop** (costly on single-threaded WASM .NET;
each hitch also feeds Bug A):
- `World.Step` allocates `_entities.ToArray()` **every frame** (`World.cs:37`).
- `CombatResolver.Resolve` runs `entities.OfType<Tank>().Where(...).ToList()` +
  `entities.OfType<Projectile>().Where(...)` **every frame** (`CombatResolver.cs:51,57`).
- *Low-risk fix:* remove the per-frame allocations — reuse a reusable buffer / index-loop instead of LINQ
  and `ToArray`. Pure-logic, unit-testable, no gameplay change.

**Rendering suspects — profile on the actual iPad first** (the 60 fps rule requires a before/after
profiler result on the target device before changing the main loop):
- `project.godot` sets **no** `rendering_method` override, so the web build likely runs the heavy
  **Forward+** path. Switching the **web export to the Compatibility renderer** is usually a large win on
  mobile Safari/WebGL2 — the single biggest lever to try first.
- Disable **MSAA / soft shadows** on web; consider dropping or lowering the per-player **fog spotlight**
  (`Arena3DScene.cs:954`) which is a real-time light + shadow source.
- Verify the export uses **thread support** where iOS Safari allows it.

**Suggested order:** (1) fixed timestep + swept collision → fixes damage regardless of FPS; (2) kill
per-frame allocations → fewer hitches; (3) profile on device → renderer/shadow/MSAA settings. Then re-export
(P8) to ship. Each of (1)/(2) is independently shippable and does **not** depend on the lobby work.

## 6c. Arcade menu/input bugs (all surfaces) — **Fable todo, with the lobby work**

Reported 2026-07-05 on the live arcade (iPad). Both need auditing/fixing **in the exported WASM build**, not
just the editor — static analysis alone can't confirm them, so Fable should drive the real arcade build.

**1. Menu buttons don't work (only Solo does).** All six Title targets (`MapSelect`, `Lobby`, `MapEditor`,
`SettingsOverlay`, `PlatformExit`) and their scene files **exist** and the obvious `:F2` float-culture crash
is already contained (`SettingsFormat`), so this is **not** a missing-scene problem. Audit hypotheses, in
likelihood order:
- A destination scene **throws in `_Ready()`/`Build()` on the ICU-less WASM runtime** (the exact class of
  bug #231 fixed for `SettingsOverlay`) → `ChangeSceneToFile` swaps in a scene that immediately errors →
  looks like "the button did nothing". Suspects still doing culture-sensitive `string.Format` on web:
  `MetersOverlay.cs:44`, `ScoreOverlay.cs:45`, `ArenaScene.cs:534` — and any `MapSelectScene`/`MapEditor`
  formatting. Grep every scene's `_Ready` path for culture-sensitive formatting / web-unavailable APIs.
- Or the nav works but the destination is **unusable on touch** (Editor is mouse-designed; the old code
  `Lobby` needs a friend + a keyboard for the code — this one is superseded by the new lobby anyway).
- **Deliverable:** a click-through audit of every menu button in the deployed arcade build, each verified
  to reach a working, touch-usable screen (or fixed). Add a headless/GoDotTest smoke test per scene's
  `_Ready` so a WASM-fatal scene fails CI instead of the arcade.

**2. No on-screen keyboard when renaming the tank on iPad.** The name prompt is a Godot `LineEdit`
(`TitleScene.BuildNamePrompt` → `_nameEntry.GrabFocus()`). Godot 4's HTML5/iOS Safari export **does not
raise the browser soft keyboard from a focused canvas `LineEdit`** — a known engine limitation. Fable fix
options: on web, call `DisplayServer.VirtualKeyboardShow(...)` on focus, or (robust) overlay a real HTML
`<input>` for text entry on the web export and copy its value back. Applies to **every** text field that
must work on the arcade — the name prompt now, and the new lobby's **create-game / player-name / join**
fields (Phases 3–5) — so build the touch-keyboard seam once and reuse it there.

## 6. Net-new to build
Two Godot scenes (**browser**, **room**) + their tests; `map` field across the stack; `ListOpenLobbiesAsync`
on `ILobbyClient`; roster-driven `BecomeHost` + per-slot `HostSession` inputs + AI fill; FFA/Team win helper;
multi-spawn wiring; roster hand-off; i18n rows.
</content>
</invoke>
