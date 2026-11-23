# Gwent.Core

Independent, Unity-free card game engine, domain-specific language compiler, and console application.

## Status

Initial project bootstrap. The engine, compiler, and console are being developed as separate .NET components with automated verification.

## Design goals

- Keep game rules independent of presentation and runtime frameworks.
- Compile and execute custom card/effect definitions through a documented DSL.
- Exercise the same engine through a command-line application and automated tests.
- Prefer explicit state, deterministic behavior, and clear error reporting.

## Planned solution

- `src/Gwent.Engine` — game domain and rules.
- `src/Gwent.Compiler` — lexer, parser, semantic validation, and DSL execution.
- `src/Gwent.Console` — interactive command-line host.
- `tests` — unit and integration tests.
