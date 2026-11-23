# Architecture

## Goals

Gwent.Core is a framework-independent implementation of the game's domain, rules, and card/effect language. Unity, if reintroduced later, must remain an outer presentation adapter and must not be referenced by the core projects.

## Dependency direction

```text
Gwent.Console ───────┐
                     ├──> Gwent.Compiler ───> Gwent.Engine
Gwent.Tests ─────────┘                    └──> Gwent.Engine
```

The compiler translates source text into validated definitions and effect data. The engine owns game state and enforces all game rules. The console coordinates user input and renders results; it contains no game rules.

## Core invariants

- Every match owns its own players and zones; no mutable match state is static.
- A definition is a reusable template; each card instance has an independent identity and mutable state.
- Multiple instances of one definition may coexist in the same zone.
- A card instance is assigned to one game and occupies exactly one player zone at a time.
- Zone transitions are performed by the game, validate player ownership and source membership, and cannot be bypassed through public collection views.
- Drawing moves the top card from deck to hand; playing moves a card from hand to field; destroying moves a card from field to graveyard.
- An empty deck returns no card when drawn; it does not corrupt game state.
- Score is derived only from cards currently on the field through one authoritative calculation path.
- Compiler diagnostics are scoped to one compilation; invalid source does not publish partial catalogs.
- DSL effects operate through explicit engine-facing context and constrained capabilities, not global state or scene lookups.

## Projects

- `src/Gwent.Engine`: domain entities, zones, players, scoring, and game operations.
- `src/Gwent.Compiler`: DSL parsing, semantic validation, diagnostics, and definition catalog.
- `src/Gwent.Console`: command-line host and sample game workflow.
- `tests/Gwent.Engine.Tests`: isolated domain and rule tests.
- `tests/Gwent.Compiler.Tests`: compiler tests.
- `tests/Gwent.IntegrationTests`: compiler-to-engine scenarios.

## Engineering decisions

- Target framework: .NET 10 (`net10.0`), independent of Unity's runtime profile.
- Nullable reference types and implicit usings are enabled centrally.
- Warnings are treated as errors; analyzer/style level is set centrally.
- Prefer composition and explicit boundaries over inheritance-heavy hierarchies.
- Add abstractions only where they protect a domain boundary or support a real use case.

## Testing strategy

Unit tests cover domain invariants and compiler stages. Integration tests compile DSL, instantiate independent copies, move them through game zones, execute effects, and assert observable state and scores. CI builds and runs all tests on every push and pull request.
