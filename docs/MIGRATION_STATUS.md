# Migration status

## Implemented

- .NET 10 solution with Engine, Compiler, Console, and unit/integration test projects.
- Reusable card definitions and independent physical instances.
- Deck, hand, Melee/Ranged/Siege field rows, and graveyard with validated transitions.
- Row and total scoring derived from board state.
- Active-player turns, passing, round resolution, round points, and best-of-three match completion.
- Initial DSL compiler for card and power-boost effect declarations.
- Engine-owned effect execution with explicit duration, operation, target scope, and source-instance identity.
- Instant power modifiers and field-bound row auras; persistent effects update existing and subsequently played cards, and are removed when their source leaves the field or the round ends.
- Weather-style auras can target both players' matching row; upgrade-style auras can target their owner's matching row.
- Power is derived from base power and ordered modifiers rather than destructively overwriting base power.
- Console sample compiles DSL, creates two decks, alternates turns, executes effects through Game, and resolves a sample round.

## Remaining

- Player-driven console interaction, initial hand/deck construction rules, and complete acceptance scenario.
- Full DSL lexer/parser/AST/semantic analysis, selectors, predicates, scopes, loops, and general effect instruction runtime.
- Card rank/type filtering (for example, weather affecting only the appropriate unit ranks), special-card zones, leader effects, round-persistence exceptions, and complete tie/round rules remain to be specified and implemented.
- The historical first project also contains temporary unit statuses (buff/debuff) reset at round cleanup; these must be modeled separately from source-bound row auras.

Historical Unity code is used as a source of domain and language ideas, not copied wholesale.
