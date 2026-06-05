# ADR-0016: Composable ammo (stacking pickups)

**Date:** 2026-06-05
**Status:** Accepted
**Deciders:** Solo developer + Claude Code (architect agent)

Supersedes the firing-strategy half of ADR-0013. Numbered 0016, the next free number after 0015.

## Context

ADR-0013 modelled each ammo crate as a whole `IWeapon` strategy — `BehaviourWeapon` (one
straight/bouncing shot), `SpreadWeapon` (a fan of straight pellets), `PiercingWeapon` (one piercing
shot) — and `Tank.LoadAmmo` swapped the active weapon wholesale. The owner asked that pickups
**stack**: holding bouncing ammo and then collecting spread should fire a *fan of bouncing pellets*,
not replace the bounce with a plain spread.

Whole-weapon strategies cannot express that — each is a fixed point in the combination space, and
picking one discards the other. The combinations the owner wants (spread × bouncing, spread ×
piercing) are the **product of two independent axes**:

- a **spread pattern** — how many pellets, fanned how wide (1 = a single shot);
- a **per-pellet behaviour** — straight / bouncing / piercing, with a pierce budget.

## Decision

### A single mutable `AmmoLoadout` with two independent axes

`AmmoLoadout` (GameLogic) holds the spread axis (`SpreadCount`, `SpreadRadians`) and the behaviour
axis (`BehaviourFactory`, `Pierce`), and `Fire` spawns `SpreadCount` pellets fanned about the aim,
each built from a fresh `BehaviourFactory()` and the `Pierce` budget. Its default state — one
straight pellet — *is* the ordinary tank shot, so there is one firing path, not a default-vs-special
branch.

### Pickups are `AmmoModifier`s that set ONE axis

`AmmoModifier.ApplyTo(AmmoLoadout)` mutates only its own axis: `SpreadAmmo` sets the spread and
leaves the behaviour; `BouncingAmmo` / `PiercingAmmo` set the behaviour and leave the spread. So a
crate stacks onto whatever is already loaded. Spread and behaviour are orthogonal; bouncing and
piercing share the behaviour axis, so the last of those wins (a pellet bounces *or* pierces, not
both). `Tank.LoadAmmo(modifier, shots)` applies the modifier and refreshes the special-shot count;
when the count runs out the loadout `Reset()`s to the single straight shot.

The per-pellet motion/impact classes from ADR-0013 (`IProjectileBehaviour`:
`StraightBehaviour` / `BouncingBehaviour` / `PiercingBehaviour`) are unchanged — only the
weapon/firing layer above them is replaced. `IWeapon`, `BehaviourWeapon`, `SpreadWeapon`, and
`PiercingWeapon` are removed.

## Consequences

- **Positive:** pickups compose as the owner asked, with no combinatorial explosion of weapon
  classes — N behaviours × M spreads is two small axes, not N×M strategies. One firing path; the
  default shot is just the loadout's reset state. Each axis and the stacking are directly unit-testable.
- **Negative / cost:** a small breaking refactor of the weapon layer (`Tank`, `AmmoPickup`,
  `ArenaScene`, the weapon tests). `AmmoLoadout` is mutable (a deliberate per-tank value), unlike the
  immutable weapon strategies it replaces.
- **Deferred:** additional axes (e.g. projectile speed/size, explosive on impact) would each be a new
  loadout field + modifier; combining bouncing *and* piercing on one pellet is intentionally not
  supported.
