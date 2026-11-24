# Architecture

Gwent.Core is a framework-independent domain engine and DSL toolchain. Unity remains an outer presentation adapter and is not referenced by Engine, Compiler, or Console.

- Engine owns card instances, zones, row-based boards, turn/round rules, scoring, and effect execution.
- Compiler is a staged pipeline: Lexer → Parser → typed AST → SemanticAnalyzer → immutable CompilationResult → EffectExecutor → Engine.
- Console loads source and coordinates commands; it does not mutate game state or implement effect rules.

## Core invariants

- Card definitions produce independent physical instances.
- Each registered instance belongs to one game and one owner, and must occupy exactly one matching player zone at validated operation boundaries. Runtime collection Pop may temporarily detach an instance; top-level effect completion rejects any card left detached, duplicated, missing, unregistered, or stored under zone/owner metadata that disagrees with its actual collection.
- Player IDs are unique within a game because ownership and effect-source resolution use those IDs.
- Game validates every zone transition and active-player operation.
- The field is divided into Melee, Ranged, and Siege rows; scores are derived from board contents.
- Passing ends a player's participation in the current round. Both players must pass to resolve it.
- A round winner earns one point; a tied round awards one point to each player. Field cards then move to graveyards.
- The match ends when a player reaches two round points or the third round resolves.
- Compiled effect data is converted into an Engine effect value and executed by Game with explicit player/card context.
- Effects declare duration, operation, and target scope. Instant modifiers remain after their source leaves; field-bound effects are recalculated from active sources and removed when the source leaves the field or the round ends. UntilRoundEnd modifiers remain on their card instance even if it leaves the field or is destroyed, and are cleared from every registered instance only during round cleanup.
- Persistent modifiers retain source-instance identity and are derived separately from base power, so removing one source does not erase other modifiers.
- Runtime DSL PostActions form a recursive chain. Each level selects from the preceding level's targets, while arguments and selectors are evaluated in the parent's lexical scope; semantic analysis carries ancestor parameter types through the same chain.
- Row auras are applied to cards already present and cards entering the row later. The current model supports the source card, its owner's row, or both players' matching row.
- An effect can only target its assigned card while it is on the active player's field, and each effect ID can be applied once per instance.
- Compiler diagnostics are per compilation and invalid source publishes no partial catalogue.

## Compiler stages

- Lexer owns character scanning, comments, escapes, token locations, and lexical diagnostics.
- Parser owns grammar and syntax recovery; it produces declaration nodes and never mutates the game.
- The AST is a typed representation of declarations, independent of runtime state.
- SemanticAnalyzer validates identifiers, numeric ranges, rows, uniqueness, and cross-references before publishing a catalog.
- CompilationResult is the immutable compiled catalog. Invalid source never exposes partial definitions.
- EffectExecutor binds a compiled card/effect to explicit game/player/card context and delegates all game-state changes to Engine.

## Dependencies

```text
Gwent.Console -> Gwent.Compiler -> Gwent.Engine
       |________________________> Gwent.Engine
```

.NET 10, nullable references, implicit usings, warnings as errors. No Unity dependency in core projects.
