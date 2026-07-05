# Fable task — Multiplayer menu restructure

**Owner ask (2026-07-05):** the title menu should read **Solo / Multiplayer / Settings / Back to
Arcade**. Opening **Multiplayer** goes straight to the lobby browser, and map picking/creating lives
**inside** that lobby screen (a "Maps" button), not as top-level title items.

This spec is scoped so Fable can implement it end-to-end. Trivial pre-work is already done (see
"Already done"). Ops steps (branch reconcile, WASM re-export, arcade redeploy) are **handled by
Claude Code after Fable's code lands** — out of scope here.

## Confirmed design decisions

1. **Map creation on web = pick-map only.** The Map Editor stays desktop-only (it needs the local
   dev asset library that isn't bundled into the WASM build). Web players can *pick* a map, not
   author one.
2. **Restructure both branches** — `main` (desktop/Android) and `p8/web-export-refresh` (web). Use
   one platform-aware implementation so both branches share the same code (Claude reconciles the web
   branch onto `main` afterwards; the `OS.HasFeature("web")` checks make the web/desktop difference
   automatic).
3. **Multiplayer → straight to lobby browser, maps inside it.** Multiplayer opens
   `LobbyBrowserScene` directly; a **"Maps"** button inside that scene opens the map picker (and, on
   desktop only, the editor).

## Already done (trivial, by Claude Code)

- `client/i18n/strings.csv`: added `title.back_to_arcade,"Back to Arcade","Volver al Arcade","Tilbage til Arcade"`.
  (The i18n agent should sanity-check the ES/DA wording.)
- `title.multiplayer` already exists on `main` (EN "Multiplayer" / ES "Multijugador" / DA "Multiplayer").

## Current state (on `main`)

- `client/src/Presentation/TitleScene.cs` builds, in order: `Solo`, `Multiplayer` (→
  `LobbyBrowserScenePath`), `SelectMap` (→ `MapSelectScenePath`), `Editor` (→
  `MapEditorScene.MapEditorScenePath`), `Settings`, `Exit` (→ `PlatformExit.Run`).
- `client/src/Presentation/LobbyBrowserScene.cs` is the lobby browser: list of open games + a Create
  Game panel + Refresh + Back. **No Maps button.**
- `client/src/Presentation/MapSelectScene.cs` = the pick-map browser (built-in arenas + saved maps →
  Play). Back returns to `Title.tscn`.
- `client/src/Presentation/MapEditorScene.cs` = the authoring editor (desktop).
- `client/src/Infrastructure/PlatformExit.cs` already returns to the arcade on web and quits on
  desktop — the "Exit" button's **behaviour** is already correct; only its **label** should change.

## Required changes

### 1. `client/src/Presentation/TitleScene.cs`

- **Remove** the top-level `SelectMap` and `Editor` buttons (they move into the lobby — see change 2).
- Keep `Solo`, `Multiplayer`, `Settings`.
- **Rename the last button's label to be platform-aware:** on web
  (`OS.HasFeature("web")`) use text key `title.back_to_arcade`; otherwise `title.exit`. Keep its node
  **Name = "Exit"** (tests and PlatformExit wiring key off the node name; only the visible label
  changes) — or, if you rename the node, update every test reference too. Behaviour stays
  `PlatformExit.Run(GetTree())`.
- Update the class doc comment to describe the slimmed menu.

### 2. `client/src/Presentation/LobbyBrowserScene.cs`

- Add a **"Maps"** button (node Name `Maps`, text key `browser.maps` — add that i18n key, see change 4).
  Pressing it navigates to `MapSelectScene` (`res://src/Presentation/MapSelect.tscn`) via the existing
  guarded `Go(...)` helper.
- `MapSelectScene.Back` currently returns to `Title.tscn`. Decide the back target so a player who
  entered maps *from the lobby* returns to the lobby, not the title. Simplest: leave MapSelect's Back
  → Title (both are one hop from each other) **or** thread a "return to lobby" flag. Pick the simpler
  one and note it.
- **Editor access (desktop only):** on `!OS.HasFeature("web")`, also add an `Editor` button here (text
  key `browser.editor` or reuse `title.editor`) → `MapEditorScene.MapEditorScenePath`. On web, omit it
  (mirrors how the title currently gates the editor out on web).

### 3. Tests (this project is strict TDD — write/adjust tests first, Red→Green)

- `client/tests/Presentation/TitleSceneTests.cs`
  - `Title_OffersTheSlimmedMenu` currently requires `{ "Solo", "Multiplayer", "SelectMap", "Editor",
    "Exit" }`. Change the required set to `{ "Solo", "Multiplayer", "Settings", "Exit" }` and add
    `"SelectMap"`, `"Editor"` to `Title_DropsTheOldModeAndNetButtons`.
  - `Title_RendersTheMenuLabelsInDanish` asserts `SelectMap`/`Editor` Danish labels — remove those
    (they leave the title) and assert the last button now renders the Danish `title.exit`/`back_to_arcade`
    label appropriate to the test's platform (desktop test host → `Afslut`).
- `client/tests/Presentation/LobbyBrowserSceneTests.cs` — add: a `Maps` button exists and is enabled;
  pressing it wires toward `MapSelect` (follow the existing guarded-`Go` test pattern used elsewhere
  so the runner's scene isn't swapped). On desktop, an `Editor` button exists; assert it's absent on web
  if the test harness can simulate `OS.HasFeature("web")` (if not, note it as manually verified).
- `client/tests/Presentation/MapSelectSceneTests.cs` — if you change MapSelect's Back target, update
  the corresponding assertion.

### 4. `client/i18n/strings.csv`

- Add any new keys you introduce: `browser.maps` (and `browser.editor` if used) with EN/ES/DA.
  Follow the existing `browser.*` row style. `title.back_to_arcade` is already present.

## Definition of done

- `Solo / Multiplayer / Settings / Back-to-Arcade` on the title (last label = "Back to Arcade" on web,
  "Exit" on desktop).
- Multiplayer → lobby browser; a **Maps** button there opens the pick-map browser; an **Editor**
  button there on desktop only.
- No top-level SelectMap/Editor on the title.
- Full headless GoDotTest suite green (the qa/devops agent has the local headless command; CI must be
  green before merge — see repo CLAUDE.md branch-protection rules).
- **No behaviour change to `PlatformExit`** — label only.

## Handled by Claude Code after this lands (NOT Fable)

1. Reconcile `p8/web-export-refresh` onto `main` so the web build gets this + the modern lobby browser.
2. Re-export the .NET→WASM bundle (`../TankGame-web-export`).
3. Open the ProjectX PR to redeploy the arcade, then re-run the deployed click-through audit.
