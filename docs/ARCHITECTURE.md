# Architecture

Gwent.Core is a framework-independent domain engine and DSL toolchain. Unity remains an outer presentation adapter and is not referenced by Engine, Compiler, or Console.

## Dependency direction

```text
Gwent.Console -> Gwent.Compiler -> Gwent.Engine
       |________________________> Gwent.Engine
```

- Engine owns game state, zones, board rows, scoring, turn/round rules, and effect execution.
- Compiler translates DSL source into validated card and effect definitions.
- Console loads source and coordinates player commands; it does not implement game rules.

## Domain invariants

- Every card definition can produce multiple independent card instances.
- A card instance belongs to one game, one owner, and exactly one zone.
- Legal zone transitions are controlled by Game; external callers receive read-only zone views.
- A player's field is divided into Melee, Ranged, and Siege rows.
- Row score and total score are derived from the current board; no duplicate score state is stored.
- Turns, passes, round resolution, and victory points are owned by Game.
- Effect execution is performed by Engine through explicit game/player/card context.
- Compiler errors do not publish partial catalogs and compilation state is per invocation.

## Current runtime flow

1. Compile DSL declarations into card/effect catalogues.
2. Create independent card instances and add them to player decks.
3. Draw and play cards through Game operations.
4. Resolve rounds through explicit turn/pass operations.
5. Apply compiled effects through Game's engine-facing effect API.
6. Derive row/total scores and match winner from authoritative state.

## Engineering constraints

- .NET 10, nullable references, implicit usings, warnings as errors.
- No Unity dependency in core projects.
- Prefer small explicit APIs, composition, immutable definitions, and observable domain operations.
- Tests cover invariants at unit and compiler-to-engine integration boundaries.
