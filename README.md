# Gwent.Core

Independent, Unity-free card game logic engine, domain-specific language compiler, and console application.

## Project status

The repository foundation is in place. The next milestone is a working .NET solution with a playable console vertical slice and automated tests.

## Architecture

- `Gwent.Engine` owns game state, rules, zones, turns, rounds, scoring, and effect contracts.
- `Gwent.Compiler` compiles card and effect definitions from the Gwent DSL.
- `Gwent.Console` hosts a playable command-line workflow and delegates all rules to the engine.
- Automated tests verify the engine, compiler, and their integration.

The engine and compiler must not depend on Unity. A future Unity client may consume them through an adapter.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Legacy migration inventory](docs/MIGRATION_INVENTORY.md)
- [Acceptance criteria](docs/ACCEPTANCE_CRITERIA.md)

## Build

The solution bootstrap targets .NET 10 (`net10.0`). Build and test commands will be added with the first runnable solution commit.
