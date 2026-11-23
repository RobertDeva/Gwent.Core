using System.Collections.ObjectModel;

namespace Gwent.Compiler;

public sealed class RuntimeCardProgram
{
    public RuntimeCardProgram(
        RuntimeEffectCatalog effects,
        IReadOnlyDictionary<string, RuntimeCardDefinition> cards)
    {
        Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        Cards = new ReadOnlyDictionary<string, RuntimeCardDefinition>(
            cards.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    public RuntimeEffectCatalog Effects { get; }
    public IReadOnlyDictionary<string, RuntimeCardDefinition> Cards { get; }

    public Gwent.Engine.Card CreateCardInstance(string definitionId, Gwent.Engine.Row row)
    {
        if (!Cards.TryGetValue(definitionId, out var definition))
            throw new KeyNotFoundException($"Card definition '{definitionId}' is not declared.");
        return definition.CreateInstance(row);
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
        if (!Cards.TryGetValue(card.DefinitionId, out var definition))
            throw new KeyNotFoundException($"Card definition '{card.DefinitionId}' is not declared.");
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
