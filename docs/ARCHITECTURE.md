# Architecture

Gwent.Core is a framework-independent domain engine and DSL toolchain. Unity remains an outer presentation adapter and is not referenced by Engine, Compiler, or Console.

- Engine owns card instances, zones, row-based boards, turn/round rules, scoring, and effect execution.
- Compiler is a staged pipeline: Lexer → Parser → typed AST → SemanticAnalyzer → immutable CompilationResult → EffectExecutor → Engine.
- Console loads source and coordinates commands; it does not mutate game state or implement effect rules.

## Core invariants

- Card definitions produce independent physical instances. DeckDefinition validates a leader, a minimum of 25 same-faction entries, and each entry's allowed row; repeated definitions intentionally create distinct card instances.
- Each registered instance belongs to one game and one owner, and must occupy exactly one matching player zone at validated operation boundaries. Runtime collection Pop may temporarily detach an instance; top-level effect completion rejects any card left detached, duplicated, missing, unregistered, or stored under zone/owner metadata that disagrees with its actual collection.
- Player IDs are unique within a game because ownership and effect-source resolution use those IDs.
- A validated deck is loaded only into an empty player; its leader remains deck metadata rather than entering the shuffled card pile.
- Match initialization shuffles both validated decks and deals ten cards to each player. Each player may exchange at most two distinct hand instances during the mulligan phase; the phase closes on the first normal turn action, and hands cannot exceed ten cards.
- Game validates every zone transition and active-player operation.
- The field is divided into Melee, Ranged, and Siege rows; scores are derived from board contents. Cards with multiple allowed rows choose their actual row at play time, and the engine rejects disallowed placements.
- Passing ends a player's participation in the current round. Both players must pass to resolve it.
- A round winner earns one point; a tied round awards one point to each player. Field, weather, and upgrade cards then move to their owners' graveyards.
- The match ends when a player reaches two round points or the third round resolves.
- Compiled effect data is converted into an Engine effect value and executed by Game with explicit player/card context.
- Effects declare duration, operation, and target scope. Instant modifiers remain after their source leaves; field-bound effects are recalculated from active sources and removed when the source leaves the field or the round ends. UntilRoundEnd modifiers remain on their card instance even if it leaves the field or is destroyed, and are cleared from every registered instance only during round cleanup.
- Persistent modifiers retain source-instance identity and are derived separately from base power, so removing one source does not erase other modifiers.
- Runtime DSL PostActions form a recursive chain. Each level selects from the preceding level's targets, while arguments and selectors are evaluated in the parent's lexical scope; semantic analysis carries ancestor parameter types through the same chain.
- Row auras are applied to cards already present and cards entering the row later. The current model supports the source card, its owner's row, or both players' matching row.
- Weather cards occupy their owner's weather zone but affect both players' matching row; a new weather card replaces the existing weather card in that row. Upgrade cards occupy their owner's upgrade zone and replace that player's existing upgrade in the same row.
- Gold units are immune to row-scoped weather and upgrade modifiers.
- An effect can only target its assigned card while it is in one of the active player's effect zones, and each effect ID can be applied once per instance.
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
