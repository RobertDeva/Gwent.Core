using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Gwent.Engine;

namespace Gwent.Compiler;

public sealed record Diagnostic(int Line, string Message)
{
    public override string ToString() => $"Line {Line}: {Message}";
}

// Compiled effect data is intentionally separate from engine execution.
public sealed record CompiledEffect(
    string Id,
    int PowerIncrease,
    EffectDuration Duration = EffectDuration.Instant,
    EffectOperation Operation = EffectOperation.Add,
    EffectTargetScope TargetScope = EffectTargetScope.SourceCard)
{
    public CardEffect ToEngineEffect() => new(Id, PowerIncrease, Duration, Operation, TargetScope);
}

public sealed class CardDefinition
{
    public CardDefinition(string id, string name, int power, Row row, string? effectId)
    {
        Id = id;
        Name = name;
        Power = power;
        Row = row;
        EffectId = effectId;
    }

    public string Id { get; }
    public string Name { get; }
    public int Power { get; }
    public Row Row { get; }
    public string? EffectId { get; }

    public Card CreateCard() => new(Id, Name, Power, Row, EffectId);
}

public sealed class CompilationResult
{
    internal CompilationResult(
        IReadOnlyDictionary<string, CardDefinition> cards,
        IReadOnlyDictionary<string, CompiledEffect> effects,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        Cards = new ReadOnlyDictionary<string, CardDefinition>(
            new Dictionary<string, CardDefinition>(cards, StringComparer.Ordinal));
        Effects = new ReadOnlyDictionary<string, CompiledEffect>(
            new Dictionary<string, CompiledEffect>(effects, StringComparer.Ordinal));
        Diagnostics = diagnostics;
    }

    public IReadOnlyDictionary<string, CardDefinition> Cards { get; }
    public IReadOnlyDictionary<string, CompiledEffect> Effects { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }
    public bool Success => Diagnostics.Count == 0;
}

public sealed class DslCompiler
{
    private static readonly Regex CardPattern = new(
        @"^card\s+(?<id>[A-Za-z_][A-Za-z0-9_]*)\s+""(?<name>[^""]+)""\s+power\s+(?<power>\d+)\s+row\s+(?<row>melee|ranged|siege)(?:\s+effect\s+(?<effect>[A-Za-z_][A-Za-z0-9_]*))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex EffectPattern = new(
        @"^effect\s+(?<id>[A-Za-z_][A-Za-z0-9_]*)\s+boost\s+(?<amount>\d+)(?:\s+duration\s+(?<duration>instant|while-source-on-field))?(?:\s+operation\s+(?<operation>add|set|multiply))?(?:\s+target\s+(?<target>source|own-row|both-players-in-row))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public CompilationResult Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var cards = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
        var effects = new Dictionary<string, CompiledEffect>(StringComparer.Ordinal);
        var diagnostics = new List<Diagnostic>();
        var cardLines = new List<(int Line, Match Match)>();

        var lines = source.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

            var effectMatch = EffectPattern.Match(line);
            if (effectMatch.Success)
            {
                var id = effectMatch.Groups["id"].Value;
                if (!TryParseNonNegativeInteger(effectMatch.Groups["amount"].Value, out var amount))
                {
                    diagnostics.Add(new Diagnostic(index + 1, $"Effect '{id}' has a value outside the supported integer range."));
                    continue;
                }

                var duration = effectMatch.Groups["duration"].Value.ToLowerInvariant() switch
                {
                    "while-source-on-field" => EffectDuration.WhileSourceOnField,
                    _ => EffectDuration.Instant
                };
                var operation = effectMatch.Groups["operation"].Value.ToLowerInvariant() switch
                {
                    "set" => EffectOperation.Set,
                    "multiply" => EffectOperation.Multiply,
                    _ => EffectOperation.Add
                };
                var targetScope = effectMatch.Groups["target"].Value.ToLowerInvariant() switch
                {
                    "own-row" => EffectTargetScope.OwnRow,
                    "both-players-in-row" => EffectTargetScope.BothPlayersInRow,
                    _ => EffectTargetScope.SourceCard
                };

                if (!effects.TryAdd(id, new CompiledEffect(id, amount, duration, operation, targetScope)))
                    diagnostics.Add(new Diagnostic(index + 1, $"Duplicate effect id '{id}'."));
                continue;
            }

            var cardMatch = CardPattern.Match(line);
            if (cardMatch.Success)
            {
                cardLines.Add((index + 1, cardMatch));
                continue;
            }

            diagnostics.Add(new Diagnostic(index + 1, "Unrecognized declaration."));
        }

        foreach (var (line, match) in cardLines)
        {
            var id = match.Groups["id"].Value;
            var effectId = match.Groups["effect"].Success ? match.Groups["effect"].Value : null;
            if (cards.ContainsKey(id))
            {
                diagnostics.Add(new Diagnostic(line, $"Duplicate card id '{id}'."));
                continue;
            }

            if (!TryParseNonNegativeInteger(match.Groups["power"].Value, out var power))
            {
                diagnostics.Add(new Diagnostic(line, $"Card '{id}' has a power outside the supported integer range."));
                continue;
            }

            if (effectId is not null && !effects.ContainsKey(effectId))
            {
                diagnostics.Add(new Diagnostic(line, $"Card '{id}' references unknown effect '{effectId}'."));
                continue;
            }

            var row = Enum.Parse<Row>(match.Groups["row"].Value, ignoreCase: true);
            cards.Add(id, new CardDefinition(id, match.Groups["name"].Value, power, row, effectId));
        }

        if (diagnostics.Count > 0)
        {
            return new CompilationResult(
                new Dictionary<string, CardDefinition>(),
                new Dictionary<string, CompiledEffect>(),
                diagnostics.AsReadOnly());
        }

        return new CompilationResult(cards, effects, diagnostics.AsReadOnly());
    }

    private static bool TryParseNonNegativeInteger(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
}
