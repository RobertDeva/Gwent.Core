using System.Globalization;
using Gwent.Engine;

namespace Gwent.Compiler;

public sealed class SemanticAnalyzer
{
    public CompilationResult Analyze(
        SyntaxTree syntax,
        IEnumerable<Diagnostic>? priorDiagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(syntax);

        var diagnostics = (priorDiagnostics ?? []).Concat(syntax.Diagnostics).ToList();
        var effects = new Dictionary<string, CompiledEffect>(StringComparer.Ordinal);
        var cards = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);

        foreach (var declaration in syntax.Declarations.OfType<EffectDeclarationSyntax>())
        {
            if (!TryParseNonNegativeInteger(declaration.Amount, out var amount))
            {
                diagnostics.Add(new Diagnostic(declaration.Line,
                    $"Effect '{declaration.Id}' has a value outside the supported integer range."));
                continue;
            }

            var duration = declaration.Duration?.ToLowerInvariant() switch
            {
                "while-source-on-field" => EffectDuration.WhileSourceOnField,
                null or "instant" => EffectDuration.Instant,
                _ => (EffectDuration?)null
            };
            var operation = declaration.Operation?.ToLowerInvariant() switch
            {
                "set" => EffectOperation.Set,
                "multiply" => EffectOperation.Multiply,
                null or "add" => EffectOperation.Add,
                _ => (EffectOperation?)null
            };
            var target = declaration.Target?.ToLowerInvariant() switch
            {
                "own-row" => EffectTargetScope.OwnRow,
                "both-players-in-row" => EffectTargetScope.BothPlayersInRow,
                null or "source" => EffectTargetScope.SourceCard,
                _ => (EffectTargetScope?)null
            };

            if (duration is null || operation is null || target is null)
            {
                diagnostics.Add(new Diagnostic(declaration.Line,
                    $"Effect '{declaration.Id}' contains unsupported metadata."));
                continue;
            }

            if (!effects.TryAdd(declaration.Id,
                    new CompiledEffect(declaration.Id, amount, duration.Value, operation.Value, target.Value)))
                diagnostics.Add(new Diagnostic(declaration.Line, $"Duplicate effect id '{declaration.Id}'."));
        }

        foreach (var declaration in syntax.Declarations.OfType<CardDeclarationSyntax>())
        {
            if (cards.ContainsKey(declaration.Id))
            {
                diagnostics.Add(new Diagnostic(declaration.Line, $"Duplicate card id '{declaration.Id}'."));
                continue;
            }

            if (!TryParseNonNegativeInteger(declaration.Power, out var power))
            {
                diagnostics.Add(new Diagnostic(declaration.Line,
                    $"Card '{declaration.Id}' has a power outside the supported integer range."));
                continue;
            }

            if (!Enum.TryParse<Row>(declaration.Row, true, out var row) || !Enum.IsDefined(row))
            {
                diagnostics.Add(new Diagnostic(declaration.Line, $"Card '{declaration.Id}' has an invalid row."));
                continue;
            }

            if (declaration.EffectId is not null && !effects.ContainsKey(declaration.EffectId))
            {
                diagnostics.Add(new Diagnostic(declaration.Line,
                    $"Card '{declaration.Id}' references unknown effect '{declaration.EffectId}'."));
                continue;
            }

            cards.Add(declaration.Id,
                new CardDefinition(declaration.Id, declaration.Name, power, row, declaration.EffectId));
        }

        if (diagnostics.Count > 0)
            return new CompilationResult(new Dictionary<string, CardDefinition>(),
                new Dictionary<string, CompiledEffect>(), diagnostics.AsReadOnly());

        return new CompilationResult(cards, effects, diagnostics.AsReadOnly());
    }

    private static bool TryParseNonNegativeInteger(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
}
