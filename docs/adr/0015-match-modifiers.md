# ADR-0015: Match modifiers — "everyone starts with effect X" (S9)

**Date:** 2026-06-05
**Status:** Accepted
**Deciders:** Solo developer + Claude Code (architect agent)

The first *gameplay* slice of the S9 "progression / meters / match-modifiers" system from
`docs/research/feature-roadmap.md` (the damage + K/D **meters** slice already shipped as a HUD).
Numbered 0015, the next free number after 0014. This slice covers the **match-modifier framework**
plus the simplest, art-free modifier the owner named — *everyone starts with effect X*. The
art-and-design-heavy modifiers (the mine / oil-slick traps from the reference image, shootable
NPC-animal XP) are deferred follow-ups, as is a title-screen selector.

## Context

The owner asked for match variety beyond the map: modifiers that change a whole round's rules, e.g.
"everyone starts with effect X". The stats system (ADR-0012) already models a tank buff as a
`StatusEffect` applied through `Tank.ApplyEffect`, and powerups/ammo crates already apply effects
mid-match. A starting-effect modifier is therefore not a new mechanic — it is the *same* effect
machinery applied once, to **every** tank, at spawn.

Two small things were missing:

1. **A whole-match duration.** A `StatusEffect` expires after its `Seconds`. A starting buff should
   last the round, not tick away. `Stats.Step` already never removes an effect whose remaining time
   stays positive, so a `Seconds` of `float.PositiveInfinity` yields a permanent effect with no
   change to the stats model.
2. **A place to carry the choice.** Like the arena seed, theme, and size, the active modifier is
   match-level configuration that the play scene reads when it spawns.

## Decision

### 1. A `MatchModifier` value object in GameLogic

`MatchModifier` carries an ordered list of `StartingEffects` (`StatusEffect`s) and applies them to a
tank via `ApplyTo(Tank)`. `MatchModifier.None` (empty) is the default and a no-op, so a default
match plays exactly as before. A `Permanent(stat, mult, addFlat)` helper builds an
infinite-duration effect, and one named preset — `MatchModifier.Blitz` (everyone permanently faster
and rapid-firing) — proves the framework end to end and is ready for a selector to offer. Pure C#,
deterministic, engine-free — it lives beside `Stats`/`StatusEffect` and runs server-side later
unchanged.

### 2. Applied to every tank at spawn, behind `GameSetup.Modifier`

`GameSetup.Modifier` (default `MatchModifier.None`) is the match-level seam, alongside `Mode`,
`Series`, `ArenaSeed`, `ArenaWidth/Height`, and `Theme`. `ArenaScene` applies it to the player,
Player 2, and every AI tank as they spawn — one composition-root call per tank — so the rule is
uniform across the field. Local only for now; `NetArenaScene` (authoritative server) is untouched,
since a modifier that changes movement/fire rates must be enforced server-side, which is its own
later concern.

## Consequences

- **Positive:** match variety for free — the modifier reuses the existing effect machinery, so no new
  combat code and nothing downstream changes. The seam matches the established `GameSetup` config
  pattern, and `Permanent` + `Blitz` make the framework real and tested without speculative breadth.
- **Negative / cost:** the preset's magnitudes are balance values that want a playtest; an
  infinite-duration effect is a slight reinterpretation of `StatusEffect` (fine, since `Stats.Step`
  already tolerates it). Applying movement/fire modifiers in net play would need server enforcement.
- **Deferred:** a title-screen modifier selector (the seam is settable but nothing exposes it yet,
  exactly like the theme/size controls), the trap modifiers (mine, oil slick) which need art + new
  hazard entities, shootable NPC-animal XP, and the cosmetic XP/unlock layer.
