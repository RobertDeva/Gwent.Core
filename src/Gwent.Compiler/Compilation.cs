using System.Collections.ObjectModel;
using System.Globalization;
using Gwent.Engine;
namespace Gwent.Compiler;
public sealed record Diagnostic(int Line, string Message)
{
    public override string ToString() => $"Line {Line}: {Message}";
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
    public bool ApplyCardEffect(Game game, Player player, Card card)
    {
        ArgumentNullException.ThrowIfNull(game); ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(card);
        if (!Cards.TryGetValue(card.DefinitionId,out var definition)) throw new ArgumentException($"Card definition '{card.DefinitionId}' does not belong to this compilation.",nameof(card));
        if (!StringComparer.Ordinal.Equals(card.Name,definition.Name) || card.BasePower!=definition.Power || card.Row!=definition.Row || !StringComparer.Ordinal.Equals(card.EffectId,definition.EffectId))
            throw new ArgumentException("Card instance does not match its compiled definition.",nameof(card));
        if (definition.EffectId is null) return false;
        if (!Effects.TryGetValue(definition.EffectId,out var effect)) throw new InvalidOperationException($"Compiled effect '{definition.EffectId}' is missing from the catalog.");
        game.ApplyEffect(player,card,effect.ToEngineEffect()); return true;
    }
}
public sealed class DslCompiler
{
    public CompilationResult Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lexer=new Lexer(source); var tokens=lexer.Tokenize(); var syntax=new Parser(tokens).Parse();
        var diagnostics=lexer.Diagnostics.Concat(syntax.Diagnostics).ToList();
        var effects=new Dictionary<string,CompiledEffect>(StringComparer.Ordinal);
        var cards=new Dictionary<string,CardDefinition>(StringComparer.Ordinal);
        foreach(var declaration in syntax.Declarations.OfType<EffectDeclarationSyntax>())
        {
            if(!TryParseNonNegativeInteger(declaration.Amount,out var amount)) { diagnostics.Add(new Diagnostic(declaration.Line,$"Effect '{declaration.Id}' has a value outside the supported integer range.")); continue; }
            var duration=declaration.Duration?.ToLowerInvariant() switch { "while-source-on-field"=>EffectDuration.WhileSourceOnField, null or "instant"=>EffectDuration.Instant, _=>(EffectDuration?)null };
            var operation=declaration.Operation?.ToLowerInvariant() switch { "set"=>EffectOperation.Set, "multiply"=>EffectOperation.Multiply, null or "add"=>EffectOperation.Add, _=>(EffectOperation?)null };
            var target=declaration.Target?.ToLowerInvariant() switch { "own-row"=>EffectTargetScope.OwnRow, "both-players-in-row"=>EffectTargetScope.BothPlayersInRow, null or "source"=>EffectTargetScope.SourceCard, _=>(EffectTargetScope?)null };
            if(duration is null || operation is null || target is null) { diagnostics.Add(new Diagnostic(declaration.Line,$"Effect '{declaration.Id}' contains unsupported metadata.")); continue; }
            if(!effects.TryAdd(declaration.Id,new CompiledEffect(declaration.Id,amount,duration.Value,operation.Value,target.Value)))
                diagnostics.Add(new Diagnostic(declaration.Line,$"Duplicate effect id '{declaration.Id}'."));
        }
        foreach(var declaration in syntax.Declarations.OfType<CardDeclarationSyntax>())
        {
            if(cards.ContainsKey(declaration.Id)) { diagnostics.Add(new Diagnostic(declaration.Line,$"Duplicate card id '{declaration.Id}'.")); continue; }
            if(!TryParseNonNegativeInteger(declaration.Power,out var power)) { diagnostics.Add(new Diagnostic(declaration.Line,$"Card '{declaration.Id}' has a power outside the supported integer range.")); continue; }
            if(!Enum.TryParse<Row>(declaration.Row,true,out var row) || !Enum.IsDefined(row)) { diagnostics.Add(new Diagnostic(declaration.Line,$"Card '{declaration.Id}' has an invalid row.")); continue; }
            if(declaration.EffectId is not null && !effects.ContainsKey(declaration.EffectId)) { diagnostics.Add(new Diagnostic(declaration.Line,$"Card '{declaration.Id}' references unknown effect '{declaration.EffectId}'.")); continue; }
            cards.Add(declaration.Id,new CardDefinition(declaration.Id,declaration.Name,power,row,declaration.EffectId));
        }
        if(diagnostics.Count>0) return new CompilationResult(new Dictionary<string,CardDefinition>(),new Dictionary<string,CompiledEffect>(),diagnostics.AsReadOnly());
        return new CompilationResult(cards,effects,diagnostics.AsReadOnly());
    }
    private static bool TryParseNonNegativeInteger(string value,out int result)=>int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out result);
}
