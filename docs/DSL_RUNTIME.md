# Gwent.Core programmable runtime DSL

The compiler exposes two compatible entry points:

- DslCompiler.Compile(source): the small declaration grammar used by the initial engine vertical slice.
- DslCompiler.CompileRuntimeProgram(effectSource, cardSource): the extended programmable DSL path migrated from the earlier compiler architecture.

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
            let resultingPower = unit.Power + amount;
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

- Source: board, field, otherField, hand, otherHand, deck, otherDeck, or parent inside a PostAction.
- Single: Boolean expression; when true, only the first matching card is selected.
- Predicate: optional lambda expression whose body must return Boolean.

A nested PostAction can use Source: "parent" to operate on the preceding activation's selected targets.

## Runtime context and expressions

Expressions support typed literals, identifiers, parentheses, arithmetic, comparisons, equality, Boolean operators, string concatenation, member access, collection indexers, lambda predicates, and variable assignment.

The runtime context exposes context.TriggerPlayer, context.SourceCard, context.Game, context.Hand, context.Deck, context.Field, context.Graveyard, context.Board, and the corresponding opponent collections when a game is available.

Collection expressions include Find, Add, Push, Remove, SendBottom, Pop, and Count. They currently transform runtime collection snapshots; they do not yet move cards between engine zones. Zone mutations must go through validated engine operations and are not silently simulated by collection copies.

## Verification boundary

The runtime compiler validates declaration references, parameter names and types, selector source names, predicate types, variable scopes, expression types, and constant card power expressions. The card catalog currently preserves faction, type, rank, range, description, and activations as compiler metadata. Complete integration with engine card types, rank immunities, special zones, leaders, and every legacy collection operation remains part of the migration.
