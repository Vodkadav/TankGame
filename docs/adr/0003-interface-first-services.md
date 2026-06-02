# ADR-0003: Interface-first services, with interfaces in the Domain layer

**Date:** 2026-06-02
**Status:** Accepted
**Deciders:** Solo developer + Claude Code (architect)

## Context

The five-layer architecture (ADR-0001) fixes which layer may depend on which, but
not *how* a layer consumes behaviour that lives in another. As M1 begins adding game
behaviour — a tank driven by input, a projectile querying the arena, and soon a
network transport and persistence — there is a recurring choice: should `GameLogic`
call a concrete `Infrastructure`/`Presentation` type directly, or an abstraction?

Calling concretes directly would reverse the dependency arrow (GameLogic →
Infrastructure), break the "rules are plain C#, unit-tested without Godot" promise,
and make a class like `Tank` impossible to test without the real keyboard or the real
arena. We need a single, mechanical convention for where a service contract lives and
who depends on it, so the boundary holds as parallel work lands.

## Decision

We will define a **C# interface for every service before its implementation**, and
those interfaces **live in `TankGame.Domain`**.

- A service contract is named `IServiceName` (e.g. `IInputSource`, `IArena`) and is
  declared in `client/src/Domain/`, alongside the value types it exchanges
  (`TankInput`, `RaycastHit`). Domain interfaces use only `System.Numerics` and other
  Domain types — never Godot.
- `GameLogic` types depend on these interfaces, never on a concrete implementation.
  Implementations are constructed elsewhere and injected via the constructor
  (e.g. `Tank(IInputSource, …)`, `Projectile(IArena, …)`).
- Implementations live in the layer that owns the concern: engine/platform adapters
  (`KeyboardMouseInputSource`, the network transport) in `Infrastructure`; the arena
  and persistence implementations in their respective layers. `Presentation` (the
  composition root, per ADR-0001) wires the concrete implementations into the
  constructors.
- Tests substitute hand-written stubs/fakes for the interfaces — no mocking
  framework needed for these small contracts.

## Consequences

- Game rules are unit-tested as plain C# with scripted fakes (`ScriptedInput`,
  a stub `IArena`) — fast, no Godot runtime. M1-T3/T4 already do this.
- The dependency arrow always points *into* Domain, reinforcing ADR-0001's rule; the
  NetArchTest coverage guard keeps every type in a known layer.
- "Interface in Domain, implementation in the owning layer" is a mechanical rule: when
  a new cross-layer need appears, the contract's location is not a debate.
- Cost: a little ceremony (an interface + a stub per service) even for one-implementation
  contracts, and an extra indirection to read through. Accepted for the testability and
  boundary guarantees.
- This forecloses GameLogic reaching for Godot or concrete adapters directly. If a
  dependency-injection container is later introduced, revisit how implementations reach
  their constructors (see the composition-root note in ADR-0001); the interface-in-Domain
  rule itself is expected to stand.
