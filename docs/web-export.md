# Web (WASM) export + Lundrea Arcade deploy + multiplayer plan

Status as of 2026-06-27. This branch (`feat/web-export`) holds the **experimental Godot .NET
WebAssembly build** of TankGame and everything needed to rebuild and redeploy it. It is **not merged
to `main`** (it carries web-only changes guarded by `#if GODOT_WEB`). Single-player is **live and
playable** at <https://lundrea-arcade.web.app/tank/index.html> (arcade home: <https://lundrea-arcade.web.app>).

---

## 1. Toolchain (what another machine needs)

- **Custom Godot editor with C# web export** — stock Godot can't export .NET to web. Use the
  ComplexRobot fork: <https://github.com/ComplexRobot/godot-dotnet-web-export>. Download the
  `*_mono_web_export_win64` release, extract it, run its `install.bat` (adds the web export
  templates to `%AppData%\Godot\export_templates\4.6.2.stable.mono\` and a local NuGet source). On
  the reference machine it lives at `C:\godot-web-export\Godot_v4.6.2-stable_mono_web_export_win64\`.
- **.NET `wasm-tools` workload** — `dotnet workload install wasm-tools`. **VERIFY with
  `dotnet workload list`** — a prior install silently failed to take; without it the export still
  produces a bundle but the managed C# is never compiled into WASM (tiny ~4 MB `.pck`, black screen).
  When present, `.pck` is ~48 MB.

## 2. Build & export

```sh
EDITOR="C:/godot-web-export/.../Godot_v4.6.2-stable_mono_web_export_win64.exe"
# 1. Reimport ONCE after a fresh pull — regenerates .import/.uid/.translation (all gitignored):
"$EDITOR" --headless --path client --import
# 2. Export (output goes to build/web/, gitignored):
"$EDITOR" --headless --path client --export-release "Web" "$(pwd)/build/web/index.html"
```

Serve locally with COOP/COEP to test (the build needs cross-origin isolation). A minimal server is
in the session scratchpad (`serve_coi.py`); any server sending
`Cross-Origin-Opener-Policy: same-origin` + `Cross-Origin-Embedder-Policy: require-corp` works.

## 3. Web-only code changes (all `#if GODOT_WEB`-guarded; desktop/Android untouched)

| File | Change | Why |
|---|---|---|
| `client/TankGame.csproj` | `OutputType=Exe` + `Program.cs`, `WasmEnableThreads`, `GODOT_WEB` define, Sentry+GoDotTest excluded, `TrimmerRootAssembly` for `System.Private.CoreLib`, `System.Runtime`, **`TankGame`, `GodotSharp`** | wasm needs an entry point; trimmer would otherwise prune the whole game (the engine calls `GodotPlugins.Game.Main` natively, so it's invisible to the trimmer) |
| `client/Program.cs`, `client/TankGame.sln` | new (`{}` entry + hand-written solution) | the static-mono runtime needs a top-level entry + a solution to publish |
| `client/src/GameLogic/EntityId.cs` | crypto-free counter-based entity ids on web | **`Guid.NewGuid()` returns the SAME value every call on this runtime** (no crypto RNG) → all tanks shared one id → `tank.Id == shot.Owner` / `== self.Id` always true → no damage + AI never fires. This was *the* combat bug. |
| `Tank/Projectile/Airstrike/Powerup/NetTank/NetProjectile.cs`, `GameMode.cs` | use `EntityId.Next()` | (same) |
| `Arena3DScene.cs` | fixed-timestep accumulator (`_world.Step(1/120)` N×/frame) | low web FPS → large `delta` → projectiles tunnel past tanks; fixed step prevents it |
| `Arena3DScene.cs` | `ExitGame()` → `JavaScriptBridge.Eval("window.top.location.href='/'")` on web | `GetTree().Quit()` is a no-op in a browser; Exit returns to the arcade |
| `SfxPool.cs` | `AttenuationModel = Disabled` on the 3D pool + `GD.Load` fallback in `LoadOgg` | camera is ~2500u back → default attenuation silenced all SFX; exports ship only the *imported* `.ogg`, so raw `FileAccess` returns null |
| `TranslationLoader.cs`, `TitleScene.cs` | `GD.Load` fallback when raw `FileAccess` fails | exports drop raw source files (csv/png); load the imported resource instead |
| `GameMode.cs` | `ShowEnemyNames` default **true**; `KillStreakTracker` no longer time-windowed | owner asks: see enemy names; streak = kills since last death |
| `TitleScene.cs` | map editor + Exit gated out on web | web is play-only |
| `client/audio/sfx/fire.ogg` | short cartoon "pew", ~25% volume | replaced a harsh continuous tone (asset, ffmpeg-synthesized; local Stable Audio model was down) |
| `export_presets.cfg` | `[preset.1]` Web preset | — |

### The 5 web-only bugs that had to be fixed (in order discovered)
1. `wasm-tools` not actually installed → managed C# never compiled → black screen.
2. Trimmer pruned the game (rooted `TankGame`/`GodotSharp`).
3. Exports drop raw `FileAccess` resources → untranslated menu, no SFX, no backdrop (GD.Load fallbacks).
4. `Guid.NewGuid()` collision → no combat damage, AI never fires (counter-based `EntityId`).
5. GitHub LFS bandwidth quota broke the CI deploy → store the bundle as **plain git blobs** (both files < 100 MB).

## 4. Deploy to Lundrea Arcade (ProjectX repo)

Deploy is **CI-on-push to ProjectX `main`** (GitHub Actions → `vite build` → Firebase Hosting). To
ship a new bundle:

```sh
cp -r build/web/* C:/programmering/ProjectX/public/tank/      # overwrite the vendored bundle
# in ProjectX: branch off main, commit, PR, merge → auto-deploys
```

ProjectX integration (already in place on `main`): `public/tank/` bundle as **plain git blobs**
(NOT LFS — quota); `firebase.json` scoped `/tank/**` COOP/COEP headers; vite PWA
`globIgnores:['tank/**']`; `src/arcade/games.ts` `tank` entry with `external:true`; contract/e2e
suites skip `external` games; tile `public/tiles/tank.webp`.

**Caveats:** Spark free tier ≈ 15–20 cold loads/day (≈58 MB bundle; repeat visits cached/free).
One harmless web-audio `sample_set_pause` console warning. Map editor is gated out of web.

---

## 5. NEXT MILESTONE — Multiplayer (the spec + plan)

**Owner spec (2026-06-27):**
- Title menu becomes: **Solo · Multiplayer · Settings · Back to Arcade** (Back-to-Arcade web-only).
- **Multiplayer** → choose **Team vs Team** or **FFA**.
- Either choice opens a **lobby** with **up to 8 slots** for players to join.
- Multiplayer also shows a **list of available lobbies to join** (a lobby browser).
- Any player can press **Start game** → **3 · 2 · 1 · Start** countdown → launches the arena.
- Both **FFA** and **Team vs Team** must work in the arena.

**Backend:** Firebase Spark can't host realtime (no Cloud Functions/WebSockets). There is already a
Cloudflare Worker **`tankgame-worker.vodkadav.workers.dev`** (ADR-0019 relay) — extend it with a
**Durable Object** for lobby state + the N-player relay. The Cloudflare MCP is available in-session
for deploys.

**Current netcode:** 2-player host/join (ADR-0019 steps 1–4: relay DO, Host/Join codes, HostSession
+ NetArena3DScene, protocol fidelity). Needs extending to **up to 8 players + FFA/Team + a lobby
browser + a synced countdown.**

**Proposed phases:**
1. **Client menu/lobby UX** — restructure the title to Solo/Multiplayer/Settings/Back-to-Arcade;
   Multiplayer → FFA/Team picker → lobby screen (8 slots, ready/start, countdown) + a lobby-browser
   list. Dev against a local stub first.
2. **Backend (tankgame-worker)** — a `Lobby` Durable Object: create / list / join / leave / start;
   broadcast slot changes + the start countdown; then relay game packets for N players.
3. **Netcode to N players** — extend HostSession/NetArena3DScene/snapshot codec from 2 → up to 8
   tanks; FFA (every tank its own team) vs Team (2 teams) win logic.
4. **Wire client ↔ worker** — lobby browser + join + countdown sync + launch into NetArena3DScene
   with the right roster/mode.
5. **Playtest** across 2+ devices/browsers.

**Known step-4 remnant (ADR-0019):** the guest's OWN shield/elevation isn't predicted
(`PredictedTank`/`Reconcile` doesn't track Shield/Layer).

---

## 6. Resume checklist (fresh machine)

1. Pull TankGame, `git checkout feat/web-export`; pull ProjectX `main`.
2. Install the ComplexRobot Godot web-export editor + `dotnet workload install wasm-tools` (verify with `dotnet workload list`).
3. `--import` once, then `--export-release "Web"` (section 2).
4. To deploy: copy `build/web/*` → `ProjectX/public/tank/`, then PR→main in ProjectX (section 4).
5. For multiplayer, start at section 5 phase 1.

**Also pending (desktop/main parity):** the SFX-attenuation fix is ProjectX-independent and lives in
TankGame PR #229 (`fix/sfx-attenuation`); the pew asset, `ShowEnemyNames` default, and the
no-window `KillStreakTracker` still need porting to TankGame `main` for the desktop build.
