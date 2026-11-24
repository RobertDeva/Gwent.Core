using Gwent.Engine;

namespace Gwent.Compiler;

/// <summary>Resolves the legacy DSL's named sources against one explicit game invocation.</summary>
public static class RuntimeSourceResolver
{
    public static IReadOnlyList<Card> Resolve(
        RuntimeExecutionContext context,
        string source,
        IReadOnlyList<Card>? parentTargets = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Selector source is required.", nameof(source));

        var game = context.Game;
        var player = context.TriggerPlayer;
        var opponent = game is null
            ? null
            : ReferenceEquals(player, game.First) ? game.Second
            : ReferenceEquals(player, game.Second) ? game.First
            : throw new InvalidOperationException("Trigger player does not belong to the game.");

        return source switch
        {
            "board" when game is not null =>
                game.First.Field.Concat(game.Second.Field).ToArray(),
            "field" => player.Field.ToArray(),
            "weather" => player.Weather.ToArray(),
            "upgrades" => player.Upgrades.ToArray(),
            "otherField" when opponent is not null => opponent.Field.ToArray(),
            "otherWeather" when opponent is not null => opponent.Weather.ToArray(),
            "otherUpgrades" when opponent is not null => opponent.Upgrades.ToArray(),
            "hand" => player.Hand.ToArray(),
            "otherHand" when opponent is not null => opponent.Hand.ToArray(),
            "deck" => player.Deck.ToArray(),
            "otherDeck" when opponent is not null => opponent.Deck.ToArray(),
            "graveyard" => player.Graveyard.ToArray(),
            "parent" when parentTargets is not null => parentTargets.ToArray(),
            "board" or "otherField" or "otherWeather" or "otherUpgrades" or "otherHand" or "otherDeck" =>
                throw new InvalidOperationException($"Source '{source}' requires a game execution context."),
            "parent" => throw new InvalidOperationException("Source 'parent' is only valid inside a PostAction."),
            _ => throw new ArgumentException($"Unknown selector source '{source}'.", nameof(source))
        };
    }
}

public sealed record RuntimeSelector(
    string Source,
    RuntimeExpression Single,
    RuntimeExpression? Predicate = null)
{
    public IReadOnlyList<Card> Select(
        RuntimeExecutionContext context,
        IReadOnlyList<Card>? parentTargets = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(Single);

        var candidates = RuntimeSourceResolver.Resolve(context, Source, parentTargets);
        var matches = new List<Card>(candidates.Count);
        var lambda = Predicate is LambdaExpression
            ? Predicate.Evaluate(context).AsLambda()
            : null;

        foreach (var card in candidates)
        {
            context.ConsumeInstruction();
            if (Predicate is null)
            {
                matches.Add(card);
                continue;
            }

            if (lambda is not null)
            {
                if (lambda.Invoke(context, RuntimeValue.From(card)).AsBoolean())
                    matches.Add(card);
                continue;
            }

            context.EnterScope();
            try
            {
                context.Scope.Define("it", RuntimeValue.From(card));
                if (Predicate.Evaluate(context).AsBoolean()) matches.Add(card);
            }
            finally { context.ExitScope(); }
        }

        return Single.Evaluate(context).AsBoolean()
            ? matches.Take(1).ToArray()
            : matches.ToArray();
    }
}
