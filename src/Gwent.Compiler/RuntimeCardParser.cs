using Gwent.Engine;

namespace Gwent.Compiler;

public sealed record RuntimeCardDefinition(
    string Id,
    string Name,
    RuntimeExpression Power,
    string Faction,
    string Type,
    string Rank,
    IReadOnlyList<string> Ranges,
    string Description,
    IReadOnlyList<RuntimeEffectInvocation> Activations)
{
    public int EvaluateBasePower()
    {
        var context = new RuntimeExecutionContext(
            new Player("__compiler"),
            new Card("__compiler", "__compiler", 0, Row.Melee),
            instructionLimit: 1_000);
        return Power.Evaluate(context).AsNumber();
    }

    public Gwent.Engine.CardDefinition ToEngineDefinition()
    {
        var rows = Ranges.Select(range => range switch
        {
            "Melee" => Row.Melee,
            "Range" or "Ranged" => Row.Ranged,
            "Siege" => Row.Siege,
            _ => throw new InvalidOperationException($"Unknown card row '{range}'.")
        });
        return new Gwent.Engine.CardDefinition(Id, Name, EvaluateBasePower(),
            Faction, Type, Rank, rows, Description);
    }

    public Gwent.Engine.Card CreateInstance(Row row) => ToEngineDefinition().CreateInstance(row);
}

public sealed record RuntimeCardCompilationResult(
    IReadOnlyList<RuntimeCardDefinition> Cards,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Success => Diagnostics.Count == 0;
}

public sealed class RuntimeCardParser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly List<Diagnostic> _diagnostics = [];
    private int _position;

    public RuntimeCardParser(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lexer = new Lexer(source);
        _tokens = lexer.Tokenize();
        _diagnostics.AddRange(lexer.Diagnostics);
    }

    public RuntimeCardCompilationResult Parse()
    {
        var cards = new List<RuntimeCardDefinition>();
        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (Match(TokenKind.Semicolon)) continue;
            if (!IsIdentifier("card"))
            {
                Error(Current, "Expected a 'card' declaration.");
                RecoverToNextCard();
                continue;
            }

            var card = ParseCard();
            if (card is not null) cards.Add(card);
        }
        return new RuntimeCardCompilationResult(cards.AsReadOnly(), _diagnostics.AsReadOnly());
    }

    private RuntimeCardDefinition? ParseCard()
    {
        Advance();
        if (!Expect(TokenKind.Identifier, "card identifier")) return null;
        var id = Previous.Text;
        if (!Expect(TokenKind.OpenBrace, "'{' after card identifier")) return null;

        string? name = null;
        RuntimeExpression? power = null;
        string? faction = null;
        string? type = null;
        string? rank = null;
        string? description = null;
        var ranges = new List<string>();
        IReadOnlyList<RuntimeEffectInvocation> activations = [];

        while (Current.Kind is not (TokenKind.CloseBrace or TokenKind.EndOfFile))
        {
            if (Match(TokenKind.Semicolon) || Match(TokenKind.Comma)) continue;

            if (IsIdentifier("Name"))
            {
                Advance();
                Expect(TokenKind.Colon, "':' after Name");
                if (Expect(TokenKind.String, "quoted card name")) name = Previous.Text;
            }
            else if (IsIdentifier("Power"))
            {
                Advance();
                Expect(TokenKind.Colon, "':' after Power");
                power = ParseExpressionUntil(TokenKind.Semicolon);
                if (power is null) break;
            }
            else if (IsIdentifier("Faction"))
            {
                Advance();
                Expect(TokenKind.Colon, "':' after Faction");
                faction = ParseTextOrIdentifier("faction");
            }
            else if (IsIdentifier("Type"))
            {
                Advance();
                Expect(TokenKind.Colon, "':' after Type");
                type = ParseTextOrIdentifier("card type");
            }
            else if (IsIdentifier("Rank"))
            {
                Advance();
                Expect(TokenKind.Colon, "':' after Rank");
                rank = ParseTextOrIdentifier("card rank");
            }
            else if (IsIdentifier("Range"))
            {
                Advance();
                Expect(TokenKind.Colon, "':' after Range");
                ParseRanges(ranges);
            }
            else if (IsIdentifier("description"))
            {
                Advance();
                Expect(TokenKind.Colon, "':' after description");
                if (Expect(TokenKind.String, "quoted card description")) description = Previous.Text;
            }
            else if (IsIdentifier("OnActivation"))
            {
                var activationTokens = ExtractActivationTokens();
                var activationParser = new RuntimeActivationParser(activationTokens);
                var result = activationParser.Parse();
                _diagnostics.AddRange(result.Diagnostics);
                activations = result.Activations;
                _position += activationParser.ConsumedTokenCount;
            }
            else
            {
                Error(Current, $"Unknown card field '{Current.Text}'.");
                RecoverToFieldSeparator();
            }

            if (!Match(TokenKind.Semicolon) && !Match(TokenKind.Comma) &&
                Current.Kind != TokenKind.CloseBrace)
            {
                Error(Current, "Expected ';' after card field.");
                RecoverToFieldSeparator();
            }
        }

        Expect(TokenKind.CloseBrace, "'}' after card declaration");
        if (name is null || power is null || faction is null || type is null ||
            rank is null || description is null || ranges.Count == 0)
        {
            Error(Previous, $"Card '{id}' is missing one or more required fields.");
            return null;
        }

        return new RuntimeCardDefinition(id, name, power, faction, type, rank,
            Array.AsReadOnly(ranges.ToArray()), description, activations);
    }

    private string? ParseTextOrIdentifier(string description)
    {
        if (Current.Kind is TokenKind.String or TokenKind.Identifier)
            return Advance().Text;
        Error(Current, $"Expected {description}.");
        return null;
    }

    private void ParseRanges(List<string> ranges)
    {
        if (!Expect(TokenKind.OpenBracket, "'[' before card ranges")) return;
        while (Current.Kind is not (TokenKind.CloseBracket or TokenKind.EndOfFile))
        {
            if (Expect(TokenKind.String, "quoted card range"))
                ranges.Add(Previous.Text);
            if (!Match(TokenKind.Comma) && Current.Kind != TokenKind.CloseBracket)
            {
                Error(Current, "Expected ',' or ']' after card range.");
                break;
            }
        }
        Expect(TokenKind.CloseBracket, "']' after card ranges");
    }

    private IReadOnlyList<Token> ExtractActivationTokens()
    {
        var start = _position;
        var bracketDepth = 0;
        var end = start;
        for (var i = start; i < _tokens.Count; i++)
        {
            if (_tokens[i].Kind == TokenKind.OpenBracket) bracketDepth++;
            if (_tokens[i].Kind == TokenKind.CloseBracket)
            {
                bracketDepth--;
                if (bracketDepth == 0)
                {
                    end = i + 1;
                    break;
                }
            }
        }

        if (end == start)
        {
            Error(Current, "Unterminated OnActivation list.");
            return [new Token(TokenKind.EndOfFile, string.Empty, Current.Line, Current.Column)];
        }

        var slice = _tokens.Skip(start).Take(end - start).ToList();
        var last = slice[^1];
        slice.Add(new Token(TokenKind.EndOfFile, string.Empty, last.Line, last.Column + last.Text.Length));
        return slice.AsReadOnly();
    }

    private RuntimeExpression? ParseExpressionUntil(TokenKind terminator)
    {
        var expressionTokens = new List<Token>();
        var parentheses = 0;
        var brackets = 0;
        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (parentheses == 0 && brackets == 0 && Current.Kind == terminator) break;
            var token = Advance();
            if (token.Kind == TokenKind.OpenParen) parentheses++;
            if (token.Kind == TokenKind.CloseParen) parentheses--;
            if (token.Kind == TokenKind.OpenBracket) brackets++;
            if (token.Kind == TokenKind.CloseBracket) brackets--;
            expressionTokens.Add(token);
        }

        if (expressionTokens.Count == 0)
        {
            Error(Current, "Expected an expression.");
            return null;
        }

        var text = string.Join(" ", expressionTokens.Select(token =>
            token.Kind == TokenKind.String
                ? "\"" + token.Text.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\t", "\\t") + "\""
                : token.Text));
        var parser = new ExpressionParser(text);
        var expression = parser.Parse();
        foreach (var diagnostic in parser.Diagnostics)
            Error(expressionTokens[0], diagnostic.Message);
        return expression;
    }

    private void RecoverToFieldSeparator()
    {
        while (Current.Kind is not (TokenKind.Semicolon or TokenKind.Comma or
            TokenKind.CloseBrace or TokenKind.EndOfFile))
            Advance();
    }

    private void RecoverToNextCard()
    {
        while (Current.Kind != TokenKind.EndOfFile && !IsIdentifier("card"))
            Advance();
    }

    private bool IsIdentifier(string value) =>
        Current.Kind == TokenKind.Identifier &&
        Current.Text.Equals(value, StringComparison.OrdinalIgnoreCase);

    private bool Expect(TokenKind kind, string description)
    {
        if (Current.Kind == kind) { Advance(); return true; }
        Error(Current, $"Expected {description}.");
        return false;
    }

    private bool Match(TokenKind kind)
    {
        if (Current.Kind != kind) return false;
        Advance();
        return true;
    }

    private Token Current
    {
        get
        {
            while (_position < _tokens.Count - 1 && _tokens[_position].Kind == TokenKind.NewLine)
                _position++;
            return _tokens[Math.Min(_position, _tokens.Count - 1)];
        }
    }
    private Token Previous => _tokens[Math.Max(0, _position - 1)];
    private Token Advance()
    {
        var token = Current;
        if (Current.Kind != TokenKind.EndOfFile) _position++;
        return token;
    }
    private void Error(Token token, string message) =>
        _diagnostics.Add(new Diagnostic(token.Line, message, token.Column));
}
