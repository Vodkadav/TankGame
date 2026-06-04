# ADR-0012: Stats & timed status-effect model (S4)

**Date:** 2026-06-04
**Status:** Accepted
**Deciders:** Solo developer + Claude Code (architect agent)

The S4 system foundation from `docs/research/feature-roadmap.md`. Numbered 0012 because
0005–0009 are reserved by name for M3–M7 in `docs/research/development-plan.md`, 0010 is the
entity spine, and 0011 is the local-first combat arc; 0012 is the next free number. Built as
local-backlog item #11 (the `Stats` + `StatusEffect` foundation), ahead of #12 (the actual
pickups), so powerups, traps, and tank abilities all land later as data, not new mechanics.

## Context

Until now a tank's tunables were `readonly` fields — `_speed`, `_fireInterval`. The roadmap's
whole upgrade/powerup/trap catalogue (speed boost, over-shield, EMP slow, incendiary
damage-over-time, "bigger tank" trade-offs, repair-over-time) is, underneath, the same
operation: *temporarily modify a numeric stat, then let the modifier expire*. Hard-coding each
one as bespoke tank state would not scale and would not be server-runnable.

The constraint is the same as the rest of the GameLogic layer: pure C#, deterministic, no Godot
and no networking, so the identical computation can run on an authoritative server when M3
lands. It must also leave the existing `Tank` construction API untouched so the combat, AI, and
view code already in the tree keep working unchanged.

## Decision

### 1. A `Stats` block replaces the tank's raw stat fields

`Stats` holds a base value per `StatKind` plus a list of live `StatusEffect` modifiers. The
public surface is small:

```csharp
public enum StatKind { Speed, FireInterval }

public sealed record StatusEffect(StatKind Stat, float Mult, float AddFlat, float Seconds);

public sealed class Stats
{
    public float Base(StatKind kind);
    public float Current(StatKind kind);   // base ∘ live effects
    public void  Apply(StatusEffect effect);
    public void  Step(float deltaSeconds); // age effects, drop the expired
}
```

### 2. Composition is *flat-then-scale*, and effects stack

`Current(kind) = (Base(kind) + Σ AddFlat) × Π Mult` over the live effects on that stat. A pure
additive effect uses `Mult: 1`; a pure multiplicative one uses `AddFlat: 0`. Multiple effects
on one stat stack (their flats sum, their multipliers multiply); an effect on one stat never
touches another. This makes "speed boost ×1.5" and "EMP speed ×0 for 3 s" the same primitive.

### 3. Effects age in real time, owned by the entity's tick

`Tank.Step` calls `_stats.Step(dt)` once per tick — before the down/respawn check — so a timed
effect counts down in wall-clock seconds whether or not the tank is currently moving. Movement
reads `Current(StatKind.Speed)` and the fire cooldown resets to `Current(StatKind.FireInterval)`,
so both existing knobs now flow through the stat block. The `Tank` constructor is unchanged: it
seeds `Stats` from the same `speed`/`fireInterval` arguments it always took.

### 4. Powerups and traps become "apply a `StatusEffect`"

`Tank.ApplyEffect(StatusEffect)` is the single entry point. Local-backlog #12 (speed-boost /
shield / repair pickups) and any future trap will be a world entity that, on contact, calls
`ApplyEffect` — no new tank mechanics. `Stats`/`StatKind`/`StatusEffect` live in GameLogic
alongside the other pure systems (`CombatResolver`, `ScoreBoard`, `SeriesTracker`); no Domain
contract is introduced because nothing across a layer boundary depends on the type yet.

## Consequences

- **Positive:** the upgrade/powerup/trap catalogue collapses to data. New effects are a
  `StatusEffect` literal, not code. Both of the tank's live stats are now modifiable, and the
  same deterministic computation is server-ready for M3. No existing call site changed.
- **Negative / cost:** a tiny per-tick allocation-free fold over the effect list (negligible at
  the entity counts in play). The `StatusEffect` record requires callers to spell out both
  `Mult` and `AddFlat`; forgetting `Mult: 1` on an additive effect silently zeroes the stat —
  mitigated for now by tests, and a couple of named factory helpers can be added with #12 if the
  call sites grow.
- **Deferred:** over-shield and incendiary damage-over-time touch `IDamageable`/`MaxHp`, not the
  movement stats, so they need a `MaxHp`/health-modifier path that is **not** built here — this
  ADR covers the additive/multiplicative stat model only. `StatKind` gains members (e.g. a
  radius or damage stat) as the content that needs them lands.
