# Migration status

## Implemented

- .NET 10 solution with Engine, Compiler, Console, and three test projects.
- Reusable card definitions and independent card instances.
- Deck, hand, row-based field, and graveyard zones.
- Validated transitions: add to deck, draw, play, destroy.
- Melee, Ranged, and Siege board rows with row and total scoring.
- Initial DSL compiler for card and boost-effect declarations.
- Console sample that compiles a DSL file, creates a deck, draws and plays cards, and displays row scores.

## Next milestones

- Turn order, passing, round resolution, and match victory points.
- Engine-owned effect execution with explicit context.
- Expanded DSL syntax, selectors, predicates, and effect instruction model.
- A player-driven console match and complete acceptance scenario.

## Historical migration

The historical Unity project is treated as a source of domain and language ideas, not copied wholesale. Its Unity coupling and static mutable state are not carried into this core.
