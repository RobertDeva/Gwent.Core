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

- The compatibility grammar still supports card/effect declarations and simple boost metadata. The extended runtime DSL is currently compiled through DslCompiler.CompileRuntimeProgram(effectSource, cardSource); unifying both source files into a single source program remains pending.
- Collection expressions currently operate on immutable runtime snapshots; zone-changing Add/Remove/Push/Pop/SendBottom operations must be connected to explicit engine transitions before they can be considered behaviorally complete.
- A separate runtime DSL path now parses reusable effect blocks, typed parameters, action expressions, while/for statements, card activation selectors, predicates, and nested PostAction; it performs static type and reference checks before producing a runtime catalog.
- Runtime effect execution now uses per-invocation scopes, typed values, parameter binding, source resolution, selector filtering, and bounded interpretation. The original simple declaration compiler remains available as a compatibility path.
- Card metadata and activation declarations are parsed into a compiler-side card catalog. Mapping every historical card property and operation to engine-owned zone/rank/type semantics remains in progress.
- Player-driven console interaction, initial hand/deck construction rules, and complete acceptance scenario.
- Card rank/type filtering (for example, weather affecting only the appropriate unit ranks), special-card zones, leader effects, round-persistence exceptions, and complete tie/round rules remain to be specified and implemented.
- The historical first project also contains temporary unit statuses (buff/debuff) reset at round cleanup; these must be modeled separately from source-bound row auras.

Historical Unity code is used as a source of domain and language ideas, not copied wholesale. A language capability is considered migrated only after its syntax, semantic checks, runtime behavior, and tests exist.
