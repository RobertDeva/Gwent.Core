# Architecture

Gwent.Core is a framework-independent domain engine and DSL toolchain. Unity remains an outer presentation adapter and is not referenced by Engine, Compiler, or Console.

- Engine owns card instances, zones, row-based boards, turn/round rules, scoring, and effect execution.
- Compiler translates DSL source into validated card and effect definitions.
- Console loads source and coordinates commands; it does not mutate game state or implement effect rules.

## Core invariants

- Card definitions produce independent physical instances.
- Each instance belongs to one game, one owner, and one zone.
- Game validates every zone transition and active-player operation.
- The field is divided into Melee, Ranged, and Siege rows; scores are derived from board contents.
- Passing ends a player's participation in the current round. Both players must pass to resolve it.
- A round winner earns one point; a tied round awards one point to each player. Field cards then move to graveyards.
- The match ends when a player reaches two round points or the third round resolves.
- Compiled effect data is converted into an Engine effect value and executed by Game with explicit player/card context.
- An effect can only target its assigned card while it is on the active player's field, and each effect ID can be applied once per instance.
- Compiler diagnostics are per compilation and invalid source publishes no partial catalogue.

## Dependencies

```text
Gwent.Console -> Gwent.Compiler -> Gwent.Engine
       |________________________> Gwent.Engine
```

.NET 10, nullable references, implicit usings, warnings as errors. No Unity dependency in core projects.
