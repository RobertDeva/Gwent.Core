# Migration status

## Implemented

- .NET 10 solution with Engine, Compiler, Console, and unit/integration test projects.
- Reusable card definitions and independent physical instances.
- Deck, hand, Melee/Ranged/Siege field rows, and graveyard with validated transitions.
- Row and total scoring derived from board state.
- Active-player turns, passing, round resolution, round points, and best-of-three match completion.
- Staged compiler pipeline: lexer, token stream with source locations, parser, typed declaration AST, semantic analyzer, immutable compilation result, and effect executor.
- Lexer support for comments, identifiers, integers, quoted strings, escapes, line endings, and lexical diagnostics.
- Parser recovery at declaration boundaries and syntax diagnostics with source locations.
- Semantic validation for duplicate identifiers, numeric ranges, valid rows, and effect references, including forward references.
- Atomic compilation: invalid programs never publish partial card/effect catalogs.
- Engine-owned effect execution with explicit duration, operation, target scope, and source-instance identity.
- Instant power modifiers and field-bound row auras; persistent effects update existing and subsequently played cards, and are removed when their source leaves the field or the round ends.
- Weather-style auras can target both players' matching row; upgrade-style auras can target their owner's matching row.
- Power is derived from base power and ordered modifiers rather than destructively overwriting base power.
- Console sample compiles DSL, creates two decks, alternates turns, executes effects through the compiled catalog, and resolves a sample round.

## Remaining

- The current grammar supports card/effect declarations and simple boost metadata only. The historical programmable DSL still needs parameter declarations and binding, selectors and sources, predicates, expression/type analysis, lexical scopes, assignments, collection operations, bounded loops, nested actions, and PostAction.
- General effect execution is not yet implemented: current EffectExecutor binds the compiled boost effect to Game; it is not yet a general AST interpreter.
- Player-driven console interaction, initial hand/deck construction rules, and complete acceptance scenario.
- Card rank/type filtering (for example, weather affecting only the appropriate unit ranks), special-card zones, leader effects, round-persistence exceptions, and complete tie/round rules remain to be specified and implemented.
- The historical first project also contains temporary unit statuses (buff/debuff) reset at round cleanup; these must be modeled separately from source-bound row auras.

Historical Unity code is used as a source of domain and language ideas, not copied wholesale. A language capability is considered migrated only after its syntax, semantic checks, runtime behavior, and tests exist.
