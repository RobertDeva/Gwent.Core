# Architecture

## Goals

Gwent.Core is a framework-independent implementation of the game's domain, rules, and card/effect language. Unity, if reintroduced later, must remain an outer presentation adapter and must not be referenced by the core projects.

## Dependency direction

```text
Gwent.Console ───────┐
                     ├──> Gwent.Compiler ───> Gwent.Engine
Gwent.Tests ─────────┘                    └──> Gwent.Engine
```

The compiler translates source text into validated definitions and executable effect behavior. The engine owns game state and enforces all game rules. The console coordinates user input and renders results; it contains no game rules.

## Projects

- `src/Gwent.Engine`: domain entities, board/zones, players, decks, turns, rounds, scoring, and effect contracts.
- `src/Gwent.Compiler`: lexer, parser, syntax tree, semantic analysis, diagnostics, and DSL-to-engine binding.
- `src/Gwent.Console`: command-line application and sample game workflow.
- `tests/Gwent.Engine.Tests`: isolated domain and rule tests.
- `tests/Gwent.Compiler.Tests`: lexer, parser, semantic and interpreter tests.
- `tests/Gwent.IntegrationTests`: end-to-end compiler-to-game scenarios.

## Core invariants

- Every match owns its own board and zones; no mutable match state is static.
- A card belongs to exactly one zone at a time.
- Zone moves validate source membership, destination capacity, ownership, and nullability before mutation.
- Score is derived from current board state through one authoritative calculation path.
- Compiler diagnostics are scoped to one compilation; no mutable static error or execution state.
- Invalid source must fail with diagnostics, not partially register definitions.
- DSL effects operate through explicit engine-facing context and constrained capabilities, not global state or reflection-based scene lookup.

## Runtime flow

1. Console loads a DSL source file.
2. Compiler tokenizes and parses it into a syntax representation.
3. Semantic analysis validates declarations, references, types, and effect operations.
4. Successful compilation returns an immutable definition catalog plus diagnostics.
5. Console constructs players, decks, and a match using that catalog.
6. Player commands invoke engine operations; compiled effects execute through an explicit effect context.
7. Console displays the resulting state and score.

## Engineering decisions

- Target framework: .NET 10 (`net10.0`), independent of Unity's runtime profile.
- Nullable reference types and implicit usings are enabled centrally.
- Warnings are treated as errors; analyzer/style level is set centrally.
- Prefer composition and explicit interfaces over inheritance-heavy card hierarchies.
- Add abstractions only where they protect a domain boundary or support a real use case.
- Keep public APIs small and validate invariants at domain boundaries.

## Testing strategy

Unit tests cover domain invariants and compiler stages. Integration tests compile a representative DSL file, instantiate a match, execute effects, and assert observable state and scores. Console smoke tests verify a complete playable scenario. CI must build and run all tests on every pull request.
