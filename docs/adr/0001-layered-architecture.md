# ADR-0001: Layered architecture for the Godot client

- **Status:** Accepted
- **Date:** 2026-06-02
- **Deciders:** @architect (recorded during M0)

## Context

The client is a Godot 4.6 C# game that will grow to include real-time
networking, client-side prediction, persistence, and a progression economy.
Game code that mixes engine calls, game rules, and I/O becomes untestable and
brittle. We want the bulk of the game's logic to be plain C# that can be
unit-tested without launching Godot, and we want a single, mechanically
enforced rule for what may depend on what — so the boundary does not erode as
parallel work lands.

## Decision

We will organise the client assembly into five namespaces, each a layer, with a
strict one-directional dependency rule:

- **Domain** (`TankGame.Domain`) — pure entities, value objects, and interfaces.
  Depends on nothing else in the project, and never on Godot.
- **GameLogic** (`TankGame.GameLogic`) — game rules implementing Domain
  interfaces. Depends only on Domain.
- **Data** (`TankGame.Data`) — local persistence. Depends only on Domain.
- **Infrastructure** (`TankGame.Infrastructure`) — engine/platform adapters
  (input, networking, storage impls). May depend on Domain, GameLogic, Data.
- **Presentation** (`TankGame.Presentation`) — Godot scenes and node scripts.
  Depends on GameLogic and Domain.

The rule is enforced by a NetArchTest suite (`client/tests/Architecture/`) that
fails the build on any violation, plus a coverage guard asserting every
`TankGame.*` type is classified into one of these layers.

## Consequences

- Game rules are testable as plain C# with no Godot runtime — fast unit tests.
- The dependency direction is a build gate, not a convention, so it cannot rot
  silently as agents work in parallel.
- New code must be placed in an explicit layer or the coverage guard fails;
  this is intentional friction that keeps the taxonomy honest.
- **Open tension — the composition root.** Presentation must wire Infrastructure
  implementations (e.g. an input source, a network transport) into scenes, which
  reads as a Presentation→Infrastructure dependency the rule forbids. For M0 the
  only such case (Sentry crash-reporting init) is kept inside Presentation and
  talks to the external SDK directly, so no `TankGame.Infrastructure` type is
  referenced. M1 introduces the first real Infrastructure impl that Presentation
  must compose; the interface-first convention and the composition-root approach
  will be settled in ADR-0003 (interface-first services). Revisit this ADR if
  that work shows the five-layer rule needs a dedicated composition-root layer.
