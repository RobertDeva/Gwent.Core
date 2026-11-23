# Migration status

## Implemented

- .NET 10 solution with Engine, Compiler, Console, and unit/integration test projects.
- Reusable card definitions and independent physical instances.
- Deck, hand, Melee/Ranged/Siege field rows, and graveyard with validated transitions.
- Row and total scoring derived from board state.
- Active-player turns, passing, round resolution, round points, and best-of-three match completion.
- Initial DSL compiler for card and power-boost effect declarations.
- Engine-owned effect execution using explicit player/card/effect context; each compiled effect can be applied once to its designated card instance.
- Console sample compiles DSL, creates two decks, alternates turns, executes effects through Game, and resolves a sample round.

## Remaining

- Player-driven console interaction, initial hand/deck construction rules, and complete acceptance scenario.
- Full DSL lexer/parser/AST/semantic analysis, selectors, predicates, scopes, loops, and general effect instruction runtime.
- Additional Gwent rules, status effects, special cards, and complete tie/round rules need explicit specifications before implementation.

Historical Unity code is used as a source of domain and language ideas, not copied wholesale.
