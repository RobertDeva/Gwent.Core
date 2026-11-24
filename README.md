# Gwent.Core

Independent, Unity-free card game logic engine, programmable DSL compiler, and console application.

## Current capabilities

- .NET 10 engine with independent physical card instances, validated zones, row scoring, turns, rounds, and best-of-three match resolution.
- Validated faction decks with a leader and at least 25 cards. Repeated card definitions create distinct instances.
- Match initialization with shuffled decks, ten-card starting hands, and up to two mulligans per player.
- Runtime DSL with reusable typed effects, selectors, predicates, loops, expressions, nested PostActions, and static semantic analysis.
- Runtime card catalogs that build validated engine decks and execute card activations.
- Interactive console mode for inspecting hands/boards, playing cards, drawing, ending turns, and passing.

## Architecture

- Gwent.Engine owns game state, rules, zones, turns, rounds, scoring, and effect contracts.
- Gwent.Compiler implements Lexer → Parser → typed AST → SemanticAnalyzer → immutable program catalog → runtime executor.
- Gwent.Console hosts demo and interactive command-line workflows; it delegates rules and state changes to the engine.
- The core projects have no Unity dependency. A future Unity client can consume them through a separate adapter.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Runtime DSL](docs/DSL_RUNTIME.md)
- [DSL v1 compatibility grammar](docs/DSL_V1.md)
- [Migration status](docs/MIGRATION_STATUS.md)
- [Legacy migration inventory](docs/MIGRATION_INVENTORY.md)
- [Acceptance criteria](docs/ACCEPTANCE_CRITERIA.md)

## Build and test

```sh
dotnet build Gwent.Core.sln --configuration Release
dotnet test Gwent.Core.sln --configuration Release
dotnet run --project src/Gwent.Console
```

Run the interactive two-player console:

```sh
dotnet run --project src/Gwent.Console -- --interactive
```

Load a DSL source file instead of the built-in sample:

```sh
dotnet run --project src/Gwent.Console -- examples/starter.gwent
dotnet run --project src/Gwent.Console -- --interactive examples/starter.gwent
```

Interactive commands: `hand`, `board`, `status`, `draw`, `play <index> [Melee|Ranged|Siege]`, `end`, `pass`, `help`, and `quit`. During setup, each player may enter up to two hand indexes to mulligan.

The solution targets .NET 10 (net10.0).
