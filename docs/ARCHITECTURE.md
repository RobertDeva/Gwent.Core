# Architecture

Gwent.Core is a framework-independent domain engine and DSL toolchain. Unity remains an outer presentation adapter and is not referenced by Engine, Compiler, or Console.

- Engine owns game state, zones, board rows, scoring, turn/round rules, and effect execution.
- Compiler translates DSL source into validated card and effect definitions.
- Console loads source and coordinates player commands; it does not implement game rules.

## Invariants

- Card definitions produce independent instances.
- Each card instance belongs to one game, one owner, and one zone.
- Game validates every zone transition and active-player command.
- A player's field is divided into Melee, Ranged, and Siege rows.
- Scores are derived from current board contents.
- A player who passes cannot act again during that round.
- A round resolves only after both players pass; field cards then move to graveyard.
- The match ends when a player reaches two round points or the third round resolves.
- A tied round awards one point to each player.
- Effect execution belongs to Engine and receives explicit player/card context.
- Compiler diagnostics do not publish partial catalogues.

## Dependency direction

```text
Gwent.Console -> Gwent.Compiler -> Gwent.Engine
       |________________________> Gwent.Engine
```

.NET 10, nullable references, implicit usings, warnings as errors. No Unity dependency in core projects.
