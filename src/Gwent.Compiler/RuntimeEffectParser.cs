namespace Gwent.Compiler;

public sealed record RuntimeEffectCompilationResult(
    RuntimeEffectCatalog? Catalog,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Success => Catalog is not null && Diagnostics.Count == 0;
}

public sealed class RuntimeEffectCompiler
{
    public RuntimeEffectCompilationResult Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new RuntimeEffectParser(tokens, lexer.Diagnostics);
        return parser.Parse();
    }
}

public sealed class RuntimeEffectParser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly List<Diagnostic> _diagnostics;
    private int _position;

    public RuntimeEffectParser(
        IReadOnlyList<Token> tokens,
        IEnumerable<Diagnostic>? lexicalDiagnostics = null)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _diagnostics = lexicalDiagnostics?.ToList() ?? [];
    }

    public RuntimeEffectCompilationResult Parse()
    {
        var effects = new List<RuntimeEffectDefinition>();
        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind == TokenKind.NewLine || Current.Kind == TokenKind.Semicolon)
            {
                Advance();
                continue;
            }

            if (!IsIdentifier("effect"))
            {
                Error(Current, "Expected an 'effect' declaration.");
                RecoverToNextEffect();
                continue;
            }

            var effect = ParseEffect();
            if (effect is not null) effects.Add(effect);
        }

        if (_diagnostics.Count > 0)
            return new RuntimeEffectCompilationResult(null, _diagnostics.AsReadOnly());

        try
        {
            return new RuntimeEffectCompilationResult(
                new RuntimeEffectCatalog(effects), _diagnostics.AsReadOnly());
        }
        catch (ArgumentException exception)
        {
            Error(Previous, exception.Message);
            return new RuntimeEffectCompilationResult(null, _diagnostics.AsReadOnly());
        }
    }

    private RuntimeEffectDefinition? ParseEffect()
    {
        Advance(); // effect
        if (!Match(TokenKind.OpenBrace))
        {
            Error(Current, "Expected '{' after 'effect'.");
            return null;
        }

        if (!ExpectIdentifier("Name") || !Expect(TokenKind.Colon, "':' after Name") ||
            !Expect(TokenKind.String, "quoted effect name"))
            return null;
        var id = Previous.Text;
        if (!Match(TokenKind.Comma))
            Error(Current, "Expected ',' after effect Name.");

        var parameters = new List<RuntimeParameterDefinition>();
        if (IsIdentifier("Params"))
        {
            Advance();
            if (Expect(TokenKind.Colon, "':' after Params") &&
                Expect(TokenKind.OpenBrace, "'{' after Params"))
            {
                while (Current.Kind is not (TokenKind.CloseBrace or TokenKind.EndOfFile))
                {
                    if (!Expect(TokenKind.Identifier, "parameter name")) break;
                    var name = Previous.Text;
                    if (!Expect(TokenKind.Colon, "':' after parameter name") ||
                        !Expect(TokenKind.Identifier, "parameter type")) break;
                    var type = Previous.Text switch
                    {
                        "Bool" or "Boolean" or "bool" or "boolean" => RuntimeParameterType.Boolean,
                        "Number" or "number" => RuntimeParameterType.Number,
                        "String" or "string" => RuntimeParameterType.String,
                        _ => (RuntimeParameterType?)null
                    };
                    if (type is null)
                        Error(Previous, $"Unsupported parameter type '{Previous.Text}'.");
                    else
                        parameters.Add(new RuntimeParameterDefinition(name, type.Value));

                    if (!Match(TokenKind.Comma) && !Match(TokenKind.Semicolon) &&
                        Current.Kind != TokenKind.CloseBrace)
                    {
                        Error(Current, "Expected ',' or '}' after parameter declaration.");
                        break;
                    }
                }
                Expect(TokenKind.CloseBrace, "'}' after Params");
            }
            if (!Match(TokenKind.Comma))
                Error(Current, "Expected ',' after Params.");
        }

        if (!ExpectIdentifier("Action") || !Expect(TokenKind.Colon, "':' after Action") ||
            !Expect(TokenKind.OpenParen, "'(' before Action arguments"))
            return null;
        if (!ExpectIdentifier("targets") || !Expect(TokenKind.Comma, "',' between Action arguments") ||
            !ExpectIdentifier("context") || !Expect(TokenKind.CloseParen, "')' after Action arguments") ||
            !Expect(TokenKind.Arrow, "'=>' before Action body") ||
            !Expect(TokenKind.OpenBrace, "'{' before Action body"))
            return null;

        var body = ParseStatements();
        Expect(TokenKind.CloseBrace, "'}' after Action body");
        Match(TokenKind.Comma);
        Expect(TokenKind.CloseBrace, "'}' after effect declaration");

        try { return new RuntimeEffectDefinition(id, parameters, body); }
        catch (ArgumentException exception)
        {
            Error(Previous, exception.Message);
            return null;
        }
    }

    private IReadOnlyList<RuntimeStatement> ParseStatements()
    {
        var statements = new List<RuntimeStatement>();
        while (Current.Kind is not (TokenKind.CloseBrace or TokenKind.EndOfFile))
        {
            if (Match(TokenKind.Semicolon) || Match(TokenKind.NewLine)) continue;

            if (IsIdentifier("while"))
            {
                Advance();
                if (!Expect(TokenKind.OpenParen, "'(' after while")) break;
                var condition = ParseExpressionUntil(TokenKind.CloseParen);
                if (condition is null || !Expect(TokenKind.CloseParen, "')' after while condition") ||
                    !Expect(TokenKind.OpenBrace, "'{' before while body")) break;
                statements.Add(new WhileStatement(condition, ParseStatements()));
                Expect(TokenKind.CloseBrace, "'}' after while body");
                Match(TokenKind.Semicolon);
                continue;
            }

            if (IsIdentifier("for"))
            {
                Advance();
                if (!Expect(TokenKind.Identifier, "loop variable")) break;
                var variable = Previous.Text;
                if (!ExpectIdentifier("in")) break;
                var collection = ParseExpressionUntil(TokenKind.OpenBrace);
                if (collection is null || !Expect(TokenKind.OpenBrace, "'{' before for body")) break;
                statements.Add(new ForEachCardStatement(variable, collection, ParseStatements()));
                Expect(TokenKind.CloseBrace, "'}' after for body");
                Match(TokenKind.Semicolon);
                continue;
            }

            if (IsIdentifier("let"))
            {
                Advance();
                if (!Expect(TokenKind.Identifier, "variable name")) break;
                var name = Previous.Text;
                if (!Expect(TokenKind.Assign, "'=' after variable name")) break;
                var value = ParseExpressionUntil(TokenKind.Semicolon);
                if (value is null || !Expect(TokenKind.Semicolon, "';' after variable declaration")) break;
                statements.Add(new DefineVariableStatement(name, value));
                continue;
            }

            var expression = ParseExpressionUntil(TokenKind.Semicolon);
            if (expression is null || !Expect(TokenKind.Semicolon, "';' after expression")) break;
            statements.Add(new EvaluateStatement(expression));
        }
        return statements.AsReadOnly();
    }

    private RuntimeExpression? ParseExpressionUntil(TokenKind terminator)
    {
        var expressionTokens = new List<Token>();
        var parentheses = 0;
        var brackets = 0;

        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (parentheses == 0 && brackets == 0 && Current.Kind == terminator)
                break;
            if (parentheses == 0 && brackets == 0 &&
                terminator == TokenKind.Semicolon && Current.Kind == TokenKind.CloseBrace)
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

        var source = string.Join(" ", expressionTokens.Select(FormatToken));
        var parser = new ExpressionParser(source);
        var expression = parser.Parse();
        foreach (var diagnostic in parser.Diagnostics)
            Error(expressionTokens[0], diagnostic.Message);
        return expression;
    }

    private static string FormatToken(Token token) => token.Kind == TokenKind.String
        ? "\"" + token.Text.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\n", "\\n").Replace("\t", "\\t") + "\""
        : token.Text;

    private void RecoverToNextEffect()
    {
        while (Current.Kind != TokenKind.EndOfFile && !IsIdentifier("effect"))
            Advance();
    }

    private bool IsIdentifier(string value) =>
        Current.Kind == TokenKind.Identifier &&
        Current.Text.Equals(value, StringComparison.OrdinalIgnoreCase);

    private bool ExpectIdentifier(string expected)
    {
        if (IsIdentifier(expected)) { Advance(); return true; }
        Error(Current, $"Expected '{expected}'.");
        return false;
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
