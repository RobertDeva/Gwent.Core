# Gwent.Core

Independent, Unity-free card game logic engine, domain-specific language compiler, and console application.

## Project status

The repository contains a working engine foundation and a compiler pipeline for the initial card/effect declaration grammar. The full historical programmable DSL is still being migrated; see [migration status](docs/MIGRATION_STATUS.md).

## Architecture

- Gwent.Engine owns game state, rules, zones, turns, rounds, scoring, and effect contracts.
- Gwent.Compiler implements Lexer → Parser → typed AST → SemanticAnalyzer → CompilationResult → EffectExecutor.
- Gwent.Console hosts the command-line sample and delegates rules to the engine.
- Automated tests verify engine behavior, compiler stages, and integration.

The engine and compiler must not depend on Unity. A future Unity client may consume them through an adapter.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [DSL v1](docs/DSL_V1.md)
- [Migration status](docs/MIGRATION_STATUS.md)
- [Legacy migration inventory](docs/MIGRATION_INVENTORY.md)
- [Acceptance criteria](docs/ACCEPTANCE_CRITERIA.md)

## Build and test

```sh
dotnet build Gwent.Core.sln --configuration Release
dotnet test Gwent.Core.sln --configuration Release
dotnet run --project src/Gwent.Console -- examples/starter.gwent
```

The solution targets .NET 10 (net10.0).
