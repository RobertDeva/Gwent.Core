# Migration status

## Implemented

- .NET 10 solution with Engine, Compiler, Console, and three test projects.
- Independent card definitions and physical instances.
- Deck, hand, Melee/Ranged/Siege field rows, and graveyard.
- Validated card transitions and row/total scoring.
- Turn ownership, end-turn, pass, round resolution, round points, and best-of-three match completion.
- Initial DSL compiler for card and boost-effect declarations.
- Console compiles a DSL source and runs a minimal scripted flow.

## Next milestones

- Engine-owned effect execution through an explicit context.
- Console turn/pass interaction and a full two-player match flow.
- Expanded DSL syntax, selectors, predicates, and effect instruction model.

Historical Unity code is used as a source of domain and language ideas, not copied wholesale.
