using System.Collections.ObjectModel;

namespace Gwent.Compiler;

public sealed record RuntimeActivationCompilationResult(
    IReadOnlyList<RuntimeEffectInvocation> Activations,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Success => Diagnostics.Count == 0;
}

public sealed class RuntimeActivationParser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly List<Diagnostic> _diagnostics = [];
    private int _position;

    public RuntimeActivationParser(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lexer = new Lexer(source);
        _tokens = lexer.Tokenize();
        _diagnostics.AddRange(lexer.Diagnostics);
    }

    public RuntimeActivationParser(
        IReadOnlyList<Token> tokens,
        IEnumerable<Diagnostic>? lexicalDiagnostics = null)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _diagnostics.AddRange(lexicalDiagnostics ?? []);
    }

    public int ConsumedTokenCount { get; private set; }

    public RuntimeActivationCompilationResult Parse()
    {
        ExpectIdentifier("OnActivation");
        Expect(TokenKind.Colon, "':' after OnActivation");
        Expect(TokenKind.OpenBracket, "'[' before activation list");

        var activations = new List<RuntimeEffectInvocation>();
        while (Current.Kind is not (TokenKind.CloseBracket or TokenKind.EndOfFile))
        {
            if (Match(TokenKind.Comma) || Match(TokenKind.Semicolon)) continue;
            if (!Expect(TokenKind.OpenBrace, "'{' before card activation")) break;
            var activation = ParseInvocationBody(isPostAction: false);
            if (activation is not null) activations.Add(activation);
            Match(TokenKind.Comma);
        }

        Expect(TokenKind.CloseBracket, "']' after activation list");
        if (Current.Kind != TokenKind.EndOfFile)
            Error(Current, "Unexpected token after OnActivation list.");

        ConsumedTokenCount = _position;
        return new RuntimeActivationCompilationResult(
            activations.AsReadOnly(), _diagnostics.AsReadOnly());
    }

    private RuntimeEffectInvocation? ParseInvocationBody(bool isPostAction)
    {
        string? effectId = null;
        var arguments = new Dictionary<string, RuntimeExpression>(StringComparer.Ordinal);
        var source = isPostAction ? "parent" : "board";
        RuntimeExpression single = new LiteralExpression(RuntimeValue.From(false));
        RuntimeExpression? predicate = null;
        RuntimeEffectInvocation? postAction = null;
        var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (Current.Kind is not (TokenKind.CloseBrace or TokenKind.EndOfFile))
        {
            if (Match(TokenKind.Comma) || Match(TokenKind.Semicolon)) continue;
            if (IsIdentifier("Type"))
            {
                MarkField(seenFields, Advance(), "Type");
                Expect(TokenKind.Colon, "':' after Type");
                if (Expect(TokenKind.String, "quoted effect id")) effectId = Previous.Text;
            }
            else if (IsIdentifier("Params"))
            {
                MarkField(seenFields, Advance(), "Params");
                Expect(TokenKind.Colon, "':' after Params");
                Expect(TokenKind.OpenBrace, "'{' before activation parameters");
                ParseArguments(arguments);
                Expect(TokenKind.CloseBrace, "'}' after activation parameters");
            }
            else if (IsIdentifier("Selector"))
            {
                MarkField(seenFields, Advance(), "Selector");
                Expect(TokenKind.Colon, "':' after Selector");
                Expect(TokenKind.OpenBrace, "'{' before Selector");
                ParseSelector(ref source, ref single, ref predicate);
                Expect(TokenKind.CloseBrace, "'}' after Selector");
            }
            else if (IsIdentifier("PostAction"))
            {
                MarkField(seenFields, Advance(), "PostAction");
                Expect(TokenKind.Colon, "':' after PostAction");
                Expect(TokenKind.OpenBrace, "'{' before PostAction");
                postAction = ParseInvocationBody(isPostAction: true);
            }
            else
            {
                Error(Current, "Expected Type, Params, Selector, or PostAction in activation.");
                Advance();
            }
            Match(TokenKind.Comma);
        }

        Expect(TokenKind.CloseBrace, "'}' after activation");
        if (string.IsNullOrWhiteSpace(effectId))
        {
            Error(Previous, "Activation is missing its Type effect id.");
            return null;
        }

        return new RuntimeEffectInvocation(effectId,
            new ReadOnlyDictionary<string, RuntimeExpression>(
                new Dictionary<string, RuntimeExpression>(arguments, StringComparer.Ordinal)),
            new RuntimeSelector(source, single, predicate), postAction);
    }

    private void MarkField(HashSet<string> seenFields, Token token, string fieldName)
    {
        if (!seenFields.Add(fieldName))
            Error(token, $"Activation field '{fieldName}' is declared more than once.");
    }

    private void ParseArguments(IDictionary<string, RuntimeExpression> arguments)
    {
        while (Current.Kind is not (TokenKind.CloseBrace or TokenKind.EndOfFile))
        {
            if (!Expect(TokenKind.Identifier, "parameter name")) return;
            var name = Previous.Text;
            Expect(TokenKind.Colon, "':' after parameter name");
            var expression = ParseExpressionUntil(TokenKind.Comma, TokenKind.CloseBrace);
            if (expression is not null && !arguments.TryAdd(name, expression))
                Error(Previous, $"Parameter '{name}' is supplied more than once.");
            if (!Match(TokenKind.Comma) && Current.Kind != TokenKind.CloseBrace)
            {
                Error(Current, "Expected ',' or '}' after parameter value.");
                return;
            }
        }
    }

    private void ParseSelector(
        ref string source,
        ref RuntimeExpression single,
        ref RuntimeExpression? predicate)
    {
        var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (Current.Kind is not (TokenKind.CloseBrace or TokenKind.EndOfFile))
        {
            if (Match(TokenKind.Semicolon) || Match(TokenKind.Comma)) continue;
            if (IsIdentifier("Source"))
            {
                MarkField(seenFields, Advance(), "Source");
                Expect(TokenKind.Colon, "':' after Source");
                if (Expect(TokenKind.String, "quoted selector source")) source = Previous.Text;
            }
            else if (IsIdentifier("Single"))
            {
                MarkField(seenFields, Advance(), "Single");
                Expect(TokenKind.Colon, "':' after Single");
                single = ParseExpressionUntil(TokenKind.Semicolon, TokenKind.CloseBrace) ?? single;
            }
            else if (IsIdentifier("Predicate"))
            {
                MarkField(seenFields, Advance(), "Predicate");
                Expect(TokenKind.Colon, "':' after Predicate");
                if (IsIdentifier("null"))
                {
                    Advance();
                    predicate = null;
                }
                else
                    predicate = ParseExpressionUntil(TokenKind.Semicolon, TokenKind.CloseBrace);
            }
            else
            {
                Error(Current, "Expected Source, Single, or Predicate in Selector.");
                Advance();
            }
            Match(TokenKind.Semicolon);
            Match(TokenKind.Comma);
        }
    }

    private RuntimeExpression? ParseExpressionUntil(params TokenKind[] terminators)
    {
        var expressionTokens = new List<Token>();
        var parentheses = 0;
        var brackets = 0;

        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (parentheses == 0 && brackets == 0 && terminators.Contains(Current.Kind))
                break;

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

    private bool IsIdentifier(string value) =>
        Current.Kind == TokenKind.Identifier &&
        Current.Text.Equals(value, StringComparison.OrdinalIgnoreCase);

    private void ExpectIdentifier(string value)
    {
        if (IsIdentifier(value)) Advance();
        else Error(Current, $"Expected '{value}'.");
    }

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
