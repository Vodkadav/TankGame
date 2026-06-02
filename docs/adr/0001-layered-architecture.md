# ADR-0001: Layered architecture for the Godot client

**Date:** 2026-06-02
**Status:** Accepted
**Deciders:** Solo developer + Claude Code (architect)

## Context

The client is a Godot 4.6 C# game that will grow to include real-time networking,
client-side prediction, persistence, and a progression economy. Code that mixes
engine calls, game rules, and I/O becomes untestable and brittle. We want the bulk
of the game's logic to be plain C# that unit-tests without launching Godot, and we
want a single, mechanically enforced rule for what may depend on what — so the
boundary does not erode as parallel work lands.

## Decision

We organise the client assembly into five namespaces, each a layer, with a
one-directional dependency rule:

- `TankGame.Domain` — entities, value objects, interfaces. Depends on nothing else
  in the project and never on Godot.
- `TankGame.GameLogic` — game rules implementing Domain interfaces. Domain only.
- `TankGame.Data` — local persistence. Domain only.
- `TankGame.Infrastructure` — engine/platform adapters (translation loading, input,
  networking, storage impls). May depend on Domain, GameLogic, Data.
- `TankGame.Presentation` — Godot scenes and node scripts.

The rule is enforced by NetArchTest in `client/tests/Architecture/LayerRulesTests.cs`,
with a coverage guard asserting every `TankGame.*` type lives in one of the five
layers.

**Composition root.** Presentation (the Godot scene scripts) is the composition
root: it MAY reference Infrastructure to wire concrete implementations — e.g.
`TranslationLoader`, and later the input source and network transport — into the
scene tree. It must NOT depend on the raw Data layer; persistence is consumed via
GameLogic. This refines the initial "Presentation → GameLogic + Domain" sketch in
`docs/research/development-plan.md` (M0-T4): a separate composition-root layer is
unnecessary while the scene tree fills that role.

## Consequences

- Game rules are testable as plain C# with no Godot runtime — fast unit tests.
- The dependency direction is a build gate, not a convention, so it cannot rot
  silently as agents work in parallel.
- New code must land in an explicit layer or the coverage guard fails — intentional
  friction that keeps the taxonomy honest.
- Allowing Presentation → Infrastructure trades a little purity for not having to
  hand-roll a DI container at M0. If a DI container or a dedicated composition-root
  layer is introduced later (see the interface-first work planned for ADR-0003),
  revisit whether the Presentation → Infrastructure edge should be removed again.
