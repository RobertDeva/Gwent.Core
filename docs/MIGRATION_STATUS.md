# Migration status

## Implemented

- .NET 10 solution with Engine, Compiler, Console, and unit/integration test projects.
- Reusable card definitions and independent physical instances.
- Immutable DeckDefinition validates a leader, at least 25 cards, faction consistency, and per-entry row legality. Repeated card definitions are permitted and create separate physical instances; Game.LoadDeck loads the validated list and keeps the leader outside the card pile.
- Deck, hand, Melee/Ranged/Siege field rows, and graveyard with validated transitions. An active player can destroy a card on either player's field; the card enters its owner's graveyard and source-bound effects are deactivated.
- Row and total scoring derived from board state.
- Active-player turns, passing, round resolution, round points, and best-of-three match completion.
- Staged compiler pipeline: lexer, token stream with source locations, parser, typed declaration AST, semantic analyzer, immutable compilation result, and effect executor.
- Lexer support for comments, identifiers, integers, quoted strings, escapes, line endings, and lexical diagnostics.
- Parser recovery at declaration boundaries and syntax diagnostics with source locations.
- Semantic validation for duplicate identifiers, numeric ranges, valid rows, and effect references, including forward references.
- Atomic compilation: invalid programs never publish partial card/effect catalogs.
- Engine-owned effect execution with explicit duration, operation, target scope, and source-instance identity.
- Instant, round-bound, and source-bound power modifiers; persistent auras update existing and subsequently played cards, and are removed when their source leaves the field, while round-bound modifiers survive card movement/destruction and are removed from every registered instance during round cleanup.
- Weather-style auras can target both players' matching row; upgrade-style auras can target their owner's matching row.
- Power is derived from base power and ordered modifiers rather than destructively overwriting base power.
- Console sample compiles DSL, creates two decks, alternates turns, executes effects through the compiled catalog, and resolves a sample round.
- The runtime DSL parses reusable effect blocks, typed parameters, action expressions, while/for statements, card activation selectors, predicates, and nested PostAction; it performs static type and reference checks before producing a runtime catalog. The graveyard selector source is accepted consistently by semantic analysis and runtime resolution.
- Runtime effect execution uses per-invocation scopes, typed values, parameter binding, source resolution, selector filtering, and bounded interpretation. Recursively nested PostActions inherit ancestor parameters and parent Action top-level locals during semantic analysis and runtime execution; loop-local variables remain isolated. A four-effect chain and top-level-local visibility are covered by integration tests. The original simple declaration compiler remains available as a compatibility path.
- Runtime card metadata and activation declarations compile into a card catalog. RuntimeCardProgram creates distinct engine card instances, plays them through the engine, and executes every OnActivation entry in order; indexed activation remains available for explicit triggers.
- Player-zone collections are live views when a game context exists. Add, Push, Remove, SendBottom, Pop, and Shuffle route mutations through validated engine transitions; local and filtered collections remain snapshots. Top-level effect execution validates card-zone integrity, including exact zone membership, instance registration, and owner/zone metadata consistency.
- End-to-end DSL execution is covered by integration tests; current CI passes the complete build, test suite, and console smoke test.
- Runtime DSL effect bodies are semantically analyzed during compilation, including identifier resolution, assignment compatibility, expression types, loop scopes, and boolean Find predicates. Program compilation aggregates effect-body and activation diagnostics before publishing an executable program.
- Legacy card member semantics are represented explicitly: Owner resolves to a Player, OwnerId remains available as a string, Range exposes the allowed row names as a read-only indexed collection, and Row identifies the chosen instance row.
- Runtime card catalogs and activation argument maps are immutable snapshots; duplicate card fields, duplicate activation/selector fields, duplicate allowed rows, and blank required card metadata are rejected during compilation.
- The original player-zone helper calls HandOfPlayer, DeckOfPlayer, FieldOfPlayer, and GraveyardOfPlayer are supported as typed runtime functions in addition to context member access.

## Remaining

- The compatibility grammar still supports card/effect declarations and simple boost metadata. The extended runtime DSL supports both a single combined source file and separate effect/card source inputs through DslCompiler.CompileRuntimeProgram.
- Collection semantics are integrated and tested for deck/hand ordering, removal, Pop/Push/SendBottom, and mutation-safe iteration; broader parity testing remains for other zones and combinations of operations.
- Player-driven console interaction, initial 10-card hands, mulligan of up to two cards, and the complete acceptance scenario.
- Card rank/type filtering (for example, weather affecting only the appropriate unit ranks), special-card zones, leader effects, round-persistence exceptions, and complete tie/round rules remain to be specified and implemented.
- The historical first project also contains temporary unit statuses (buff/debuff) reset at round cleanup; these must be modeled separately from source-bound row auras.

Historical Unity code is used as a source of domain and language ideas, not copied wholesale. A language capability is considered migrated only after its syntax, semantic checks, runtime behavior, and tests exist.
