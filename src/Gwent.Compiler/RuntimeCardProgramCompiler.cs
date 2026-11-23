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
            new Dictionary<string, RuntimeCardDefinition>(cards, StringComparer.Ordinal));
    }

    public RuntimeEffectCatalog Effects { get; }
    public IReadOnlyDictionary<string, RuntimeCardDefinition> Cards { get; }
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

        var effectResult = new RuntimeEffectCompiler().Compile(effectSource);
        var cardResult = new RuntimeCardParser(cardSource).Parse();
        var diagnostics = effectResult.Diagnostics.Concat(cardResult.Diagnostics).ToList();
        if (effectResult.Catalog is null || diagnostics.Count > 0)
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
            diagnostics.AddRange(new RuntimeTypeAnalyzer()
                .AnalyzeActivations(effectResult.Catalog, card.Activations));
            foreach (var range in card.Ranges)
                if (range is not ("Melee" or "Range" or "Ranged" or "Siege"))
                    diagnostics.Add(new Diagnostic(1, $"Card '{card.Id}' has unknown range '{range}'."));
        }

        if (diagnostics.Count > 0)
            return new RuntimeCardProgramCompilationResult(null, diagnostics.AsReadOnly());

        return new RuntimeCardProgramCompilationResult(
            new RuntimeCardProgram(effectResult.Catalog, cards), diagnostics.AsReadOnly());
    }
}
