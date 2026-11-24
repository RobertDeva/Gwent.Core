# Gwent.Core programmable runtime DSL

The compiler exposes three entry points:

- DslCompiler.Compile(source): the small declaration grammar used by the initial engine vertical slice.
- DslCompiler.CompileRuntimeProgram(effectSource, cardSource): compiles effects and cards supplied separately.
- DslCompiler.CompileRuntimeProgram(source): compiles one complete source file containing effect declarations followed by card declarations.

## Effect declarations

~~~text
effect {
    Name: "rally",
    Params: {
        amount: Number,
        enabled: Bool,
    },
    Action: (targets, context) => {
        let counter = 0;
        while (counter < amount) {
            counter = counter + 1;
        };
        for unit in targets {
            unit.Power = unit.Power + amount;
        };
    }
}
~~~

Parameters support Number, String, and Bool. Action bodies support variable declarations, assignments, expression statements, nested while loops, and for variable in targets loops. Every runtime instruction and expression consumes an execution budget.

## Card declarations and activations

~~~text
card vanguard {
    Name: "Vanguard";
    Power: 4 + 2;
    Faction: Northern;
    Type: Unit;
    Rank: Silver;
    Range: ["Melee", "Range"];
    description: "A front line unit";
    OnActivation: [
        {
            Type: "rally",
            Params: { amount: 3, },
            Selector: {
                Source: "board";
                Single: false;
                Predicate: (unit) => unit.Power > 1;
            },
        },
    ];
}
~~~

A card activation references a reusable effect by its Type value. Arguments bind by parameter name and are checked against declared parameter types. A selector contains:

- Source: board, field, otherField, hand, otherHand, deck, otherDeck, graveyard, or parent inside a PostAction.
- Single: Boolean expression; when true, only the first matching card is selected.
- Predicate: optional lambda expression whose body must return Boolean.

PostAction declarations are recursively nestable to any depth supported by the source and runtime instruction budget. Each nested invocation can use Source: "parent" to operate on the immediately preceding invocation's selected targets; the selected target set is passed down one level at a time. PostAction arguments and selector expressions are evaluated in the lexical scope of their parent invocation, so they can reference parameters declared by that parent and its ancestors, as well as variables declared in the parent Action's top-level scope. Variables declared inside for/while child scopes do not escape into PostAction. Semantic analysis validates inherited types before the program is published.

## Runtime context and collections

Expressions support typed literals, identifiers, parentheses, arithmetic, comparisons, equality, Boolean operators, string concatenation, member access, collection indexers, lambda predicates, and variable assignment.

The runtime context exposes context.TriggerPlayer, context.OtherPlayer, context.SourceCard, context.Game, context.Hand, context.Deck, context.Field, context.Graveyard, context.Board, and the corresponding opponent collections when a game is available. The legacy helper functions HandOfPlayer(player), DeckOfPlayer(player), FieldOfPlayer(player), and GraveyardOfPlayer(player) are also supported and return the corresponding player-zone collection.

Collection expressions include Find, Add, Push, Remove, SendBottom, Pop, Shuffle, and Count. Count is available as both a property (`collection.Count`) and a no-argument method (`collection.Count()`). Collections obtained from player zones are live zone views: their mutations are routed through Game.MoveCard, including source-effect cleanup and ownership transfer. Pop temporarily detaches its card; every top-level effect execution verifies that all registered card instances finish in exactly one valid game zone. Collections without a game context are read-only snapshots. Filtered target lists and locally-created collections remain independent snapshots.

Card member access preserves the legacy Owner property as a Player, exposes OwnerId separately, and represents Range as a read-only collection of allowed row names (with Count and index access). Row identifies the instance's selected row; Name, Description, Power, Faction, Type, Rank, DefinitionId, and EffectId are also available. The context exposes OtherPlayer alongside the opponent's zone collections.

Card Power assignments are implemented as round-scoped Set modifiers and are removed during round cleanup, even if the affected card leaves the field or is destroyed before the round ends. Instant modifiers remain after their source leaves the field; source-bound auras are removed when their source leaves the field.

## Verification boundary

The runtime compiler validates declaration references, parameter names and types, selector source names, predicate types, variable scopes, expression types, and constant card power expressions. The card catalog preserves faction, type, rank, range, description, and activations as compiler metadata. Complete integration with engine card types, rank immunities, special zones, leaders, and all historical native effects remains part of the migration.
