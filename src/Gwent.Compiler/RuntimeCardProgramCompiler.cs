using System.Collections.ObjectModel;

namespace Gwent.Compiler;

public sealed record RuntimeDeckCardEntry(string CardId, Gwent.Engine.Row Row);

public sealed class RuntimeCardProgram
{
    public RuntimeCardProgram(
        RuntimeEffectCatalog effects,
        IReadOnlyDictionary<string, RuntimeCardDefinition> cards)
    {
        Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        ArgumentNullException.ThrowIfNull(cards);

        var catalog = new Dictionary<string, RuntimeCardDefinition>(StringComparer.Ordinal);
        foreach (var pair in cards)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                throw new ArgumentException("Card definition keys cannot be empty.", nameof(cards));
            if (pair.Value is null)
                throw new ArgumentException($"Card definition '{pair.Key}' cannot be null.", nameof(cards));
            if (!StringComparer.Ordinal.Equals(pair.Key, pair.Value.Id))
                throw new ArgumentException(
                    $"Card catalog key '{pair.Key}' does not match definition id '{pair.Value.Id}'.",
                    nameof(cards));
            catalog.Add(pair.Key, SnapshotDefinition(pair.Value));
        }
        Cards = new ReadOnlyDictionary<string, RuntimeCardDefinition>(catalog);
    }

    private static RuntimeCardDefinition SnapshotDefinition(RuntimeCardDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition.Ranges);
        ArgumentNullException.ThrowIfNull(definition.Activations);
        return definition with
        {
            Ranges = Array.AsReadOnly(definition.Ranges.ToArray()),
            Activations = Array.AsReadOnly(
                definition.Activations.Select(SnapshotInvocation).ToArray())
        };
    }

    private static RuntimeEffectInvocation SnapshotInvocation(RuntimeEffectInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(invocation.Arguments);
        ArgumentNullException.ThrowIfNull(invocation.Selector);
        return invocation with
        {
            Arguments = new ReadOnlyDictionary<string, RuntimeExpression>(
                new Dictionary<string, RuntimeExpression>(invocation.Arguments, StringComparer.Ordinal)),
            PostAction = invocation.PostAction is null
                ? null
                : SnapshotInvocation(invocation.PostAction)
        };
    }

    public RuntimeEffectCatalog Effects { get; }
    public IReadOnlyDictionary<string, RuntimeCardDefinition> Cards { get; }

    private static void ValidateCardMatchesDefinition(
        Gwent.Engine.Card card,
        RuntimeCardDefinition definition)
    {
        var expected = definition.ToEngineDefinition();
        if (!StringComparer.Ordinal.Equals(card.Name, expected.Name) ||
            card.BasePower != expected.BasePower ||
            !StringComparer.Ordinal.Equals(card.Faction, expected.Faction) ||
            !StringComparer.Ordinal.Equals(card.Type, expected.Type) ||
            !StringComparer.Ordinal.Equals(card.Rank, expected.Rank) ||
            !StringComparer.Ordinal.Equals(card.Description, expected.Description) ||
            !card.AllowedRows.SequenceEqual(expected.AllowedRows))
            throw new ArgumentException(
                $"Card instance does not match compiled definition '{definition.Id}'.",
                nameof(card));
    }

    public Gwent.Engine.Card CreateCardInstance(string definitionId, Gwent.Engine.Row row)
    {
        if (!Cards.TryGetValue(definitionId, out var definition))
            throw new KeyNotFoundException($"Card definition '{definitionId}' is not declared.");
        return definition.CreateInstance(row);
    }

    public Gwent.Engine.DeckDefinition CreateDeckDefinition(
        string deckId,
        string leaderCardId,
        IEnumerable<RuntimeDeckCardEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (!Cards.TryGetValue(leaderCardId, out var leader))
            throw new KeyNotFoundException($"Leader card definition '{leaderCardId}' is not declared.");

        var deckEntries = new List<Gwent.Engine.DeckCardEntry>();
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (!Cards.TryGetValue(entry.CardId, out var definition))
                throw new KeyNotFoundException($"Card definition '{entry.CardId}' is not declared.");
            deckEntries.Add(new Gwent.Engine.DeckCardEntry(
                definition.ToEngineDefinition(), entry.Row));
        }

        return new Gwent.Engine.DeckDefinition(
            deckId, leader.ToEngineDefinition(), deckEntries);
    }

    public IReadOnlyList<IReadOnlyList<Gwent.Engine.Card>> Play(
        Gwent.Engine.Game game,
        Gwent.Engine.Player player,
        Gwent.Engine.Card card,
        int instructionLimit = 10_000,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        return Play(game, player, card, card.Row, instructionLimit, random);
    }

    public IReadOnlyList<IReadOnlyList<Gwent.Engine.Card>> Play(
        Gwent.Engine.Game game,
        Gwent.Engine.Player player,
        Gwent.Engine.Card card,
        Gwent.Engine.Row row,
        int instructionLimit = 10_000,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(card);
        if (instructionLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(instructionLimit));
        if (!Cards.TryGetValue(card.DefinitionId, out var definition))
            throw new KeyNotFoundException($"No compiled card definition exists for '{card.DefinitionId}'.");
        ValidateCardMatchesDefinition(card, definition);
        if (!game.ContainsCard(card))
            throw new ArgumentException("Card instance does not belong to the supplied game.", nameof(card));

        game.Play(player, card, row);
        var context = new RuntimeExecutionContext(player, card, game, instructionLimit, random);
        var results = new List<IReadOnlyList<Gwent.Engine.Card>>(definition.Activations.Count);
        for (var index = 0; index < definition.Activations.Count; index++)
            results.Add(Activate(card, index, context));
        return results.AsReadOnly();
    }

    public IReadOnlyList<Gwent.Engine.Card> Activate(
        Gwent.Engine.Card card,
        int activationIndex,
        RuntimeExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(context);
        if (!ReferenceEquals(context.SourceCard, card))
            throw new ArgumentException("Execution context source card does not match the activation card.", nameof(context));
        if (context.Game is null)
            throw new InvalidOperationException("Compiled card activation requires a game context.");
        if (!context.Game.ContainsCard(card))
            throw new ArgumentException("Activation card does not belong to the current game.", nameof(card));
        if (card.Zone is not (Gwent.Engine.CardZone.Field or Gwent.Engine.CardZone.Weather or Gwent.Engine.CardZone.Upgrade))
            throw new InvalidOperationException("Only a card in an active effect zone can activate its effects.");
        if (!StringComparer.Ordinal.Equals(card.OwnerId, context.TriggerPlayer.Id))
            throw new ArgumentException("The trigger player must own the activation card.", nameof(context));
        if (!Cards.TryGetValue(card.DefinitionId, out var definition))
            throw new KeyNotFoundException($"Card definition '{card.DefinitionId}' is not declared.");
        ValidateCardMatchesDefinition(card, definition);
        if (activationIndex < 0 || activationIndex >= definition.Activations.Count)
            throw new ArgumentOutOfRangeException(nameof(activationIndex));
        return new RuntimeEffectExecutor(Effects).Execute(
            definition.Activations[activationIndex], context);
    }
}

public sealed record RuntimeCardProgramCompilationResult(
    RuntimeCardProgram? Program,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null && Diagnostics.Count == 0;
}

public sealed class RuntimeCardProgramCompiler
{
    public RuntimeCardProgramCompilationResult Compile(string effectSource, string cardSource)
    {
        ArgumentNullException.ThrowIfNull(effectSource);
        ArgumentNullException.ThrowIfNull(cardSource);

        var effectResult = new RuntimeEffectCompiler().CompileForProgram(effectSource);
        var cardResult = new RuntimeCardParser(cardSource).Parse();
        var diagnostics = effectResult.Diagnostics.Concat(cardResult.Diagnostics).ToList();
        if (effectResult.Catalog is null)
            return new RuntimeCardProgramCompilationResult(null, diagnostics.AsReadOnly());

        var cards = new Dictionary<string, RuntimeCardDefinition>(StringComparer.Ordinal);
        var typeAnalyzer = new RuntimeTypeAnalyzer();
        foreach (var card in cardResult.Cards)
        {
            if (!cards.TryAdd(card.Id, card))
            {
                diagnostics.Add(new Diagnostic(1, $"Duplicate card id '{card.Id}'."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(card.Name))
                diagnostics.Add(new Diagnostic(1, $"Card '{card.Id}' must have a non-empty Name."));
            if (string.IsNullOrWhiteSpace(card.Faction))
                diagnostics.Add(new Diagnostic(1, $"Card '{card.Id}' must have a non-empty Faction."));
            if (string.IsNullOrWhiteSpace(card.Type))
                diagnostics.Add(new Diagnostic(1, $"Card '{card.Id}' must have a non-empty Type."));
            if (string.IsNullOrWhiteSpace(card.Rank))
                diagnostics.Add(new Diagnostic(1, $"Card '{card.Id}' must have a non-empty Rank."));

            diagnostics.AddRange(typeAnalyzer.AnalyzeExpression(card.Power, RuntimeType.Number));
            try
            {
                var evaluationContext = new RuntimeExecutionContext(
                    new Gwent.Engine.Player("__compiler"),
                    new Gwent.Engine.Card("__compiler", "__compiler", 0, Gwent.Engine.Row.Melee),
                    instructionLimit: 1_000);
                var power = card.Power.Evaluate(evaluationContext).AsNumber();
                if (power < 0)
                    diagnostics.Add(new Diagnostic(1, $"Card '{card.Id}' has negative power."));
            }
            catch (Exception exception) when (exception is InvalidOperationException or
                ArgumentOutOfRangeException or OverflowException or DivideByZeroException)
            {
                diagnostics.Add(new Diagnostic(1, $"Card '{card.Id}' has an invalid constant power expression: {exception.Message}"));
            }
            diagnostics.AddRange(new RuntimeTypeAnalyzer()
                .AnalyzeActivations(effectResult.Catalog, card.Activations));
            var seenRanges = new HashSet<string>(StringComparer.Ordinal);
            foreach (var range in card.Ranges)
            {
                if (range is not ("Melee" or "Range" or "Ranged" or "Siege"))
                {
                    diagnostics.Add(new Diagnostic(1, $"Card '{card.Id}' has unknown range '{range}'."));
                    continue;
                }

                var normalizedRange = range == "Ranged" ? "Range" : range;
                if (!seenRanges.Add(normalizedRange))
                    diagnostics.Add(new Diagnostic(1, $"Card '{card.Id}' declares range '{range}' more than once."));
            }
        }

        if (diagnostics.Count > 0)
            return new RuntimeCardProgramCompilationResult(null, diagnostics.AsReadOnly());

        return new RuntimeCardProgramCompilationResult(
            new RuntimeCardProgram(effectResult.Catalog, cards), diagnostics.AsReadOnly());
    }
}
