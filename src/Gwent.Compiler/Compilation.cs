using System.Collections.ObjectModel;
using System.Globalization;
using Gwent.Engine;
namespace Gwent.Compiler;
public sealed record Diagnostic(int Line, string Message, int Column = 0)
{
    public override string ToString() => Column > 0
        ? $"Line {Line}, column {Column}: {Message}"
        : $"Line {Line}: {Message}";
}
public sealed record CompiledEffect(string Id, int PowerIncrease, EffectDuration Duration = EffectDuration.Instant, EffectOperation Operation = EffectOperation.Add, EffectTargetScope TargetScope = EffectTargetScope.SourceCard)
{
    public CardEffect ToEngineEffect() => new(Id, PowerIncrease, Duration, Operation, TargetScope);
}
public sealed class CardDefinition
{
    public CardDefinition(string id, string name, int power, Row row, string? effectId) { Id=id; Name=name; Power=power; Row=row; EffectId=effectId; }
    public string Id { get; }
    public string Name { get; }
    public int Power { get; }
    public Row Row { get; }
    public string? EffectId { get; }
    public Card CreateCard() => new(Id, Name, Power, Row, EffectId);
}
public sealed class CompilationResult
{
    internal CompilationResult(IReadOnlyDictionary<string,CardDefinition> cards, IReadOnlyDictionary<string,CompiledEffect> effects, IReadOnlyList<Diagnostic> diagnostics)
    {
        Cards=new ReadOnlyDictionary<string,CardDefinition>(new Dictionary<string,CardDefinition>(cards,StringComparer.Ordinal));
        Effects=new ReadOnlyDictionary<string,CompiledEffect>(new Dictionary<string,CompiledEffect>(effects,StringComparer.Ordinal));
        Diagnostics=Array.AsReadOnly(diagnostics.ToArray());
    }
    public IReadOnlyDictionary<string,CardDefinition> Cards { get; }
    public IReadOnlyDictionary<string,CompiledEffect> Effects { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }
    public bool Success => Diagnostics.Count==0;
    public bool ApplyCardEffect(Game game, Player player, Card card) =>
        new EffectExecutor().Execute(this, game, player, card);
}
public sealed class DslCompiler
{
    private readonly SemanticAnalyzer _semanticAnalyzer;

    public DslCompiler() : this(new SemanticAnalyzer()) { }

    public DslCompiler(SemanticAnalyzer semanticAnalyzer) =>
        _semanticAnalyzer = semanticAnalyzer ?? throw new ArgumentNullException(nameof(semanticAnalyzer));

    public CompilationResult Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var syntax = new Parser(tokens).Parse();
        return _semanticAnalyzer.Analyze(syntax, lexer.Diagnostics);
    }

    public RuntimeCardProgramCompilationResult CompileRuntimeProgram(
        string effectSource,
        string cardSource) =>
        new RuntimeCardProgramCompiler().Compile(effectSource, cardSource);
}
