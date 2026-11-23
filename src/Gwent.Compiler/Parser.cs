namespace Gwent.Compiler;
public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly List<Diagnostic> _diagnostics = [];
    private int _position;
    public Parser(IReadOnlyList<Token> tokens) => _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    public SyntaxTree Parse()
    {
        var declarations = new List<DeclarationSyntax>();
        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (Match(TokenKind.NewLine)) continue;
            var start = _position;
            var declaration = ParseDeclaration();
            if (declaration is not null) declarations.Add(declaration);
            if (_position == start) Advance();
            while (Current.Kind is not (TokenKind.NewLine or TokenKind.EndOfFile)) Advance();
            Match(TokenKind.NewLine);
        }
        return new SyntaxTree(declarations.AsReadOnly(), _diagnostics.AsReadOnly());
    }
    private DeclarationSyntax? ParseDeclaration()
    {
        if (!IsIdentifier("effect") && !IsIdentifier("card")) { Error(Current, "Expected 'effect' or 'card' declaration."); return null; }
        var keyword = Advance();
        return keyword.Text.Equals("effect", StringComparison.OrdinalIgnoreCase) ? ParseEffect(keyword.Line) : ParseCard(keyword.Line);
    }
    private EffectDeclarationSyntax? ParseEffect(int line)
    {
        if (!ExpectIdentifier("effect id")) return null;
        var id = Previous.Text;
        if (!ExpectIdentifierText("boost") || !Expect(TokenKind.Integer, "non-negative integer after 'boost'")) return null;
        var amount = Previous.Text;
        string? duration = null, operation = null, target = null;
        if (IsIdentifier("duration")) { Advance(); if (!ExpectOneOf(["instant", "while-source-on-field"], "effect duration")) return null; duration = Previous.Text; }
        if (IsIdentifier("operation")) { Advance(); if (!ExpectOneOf(["add", "set", "multiply"], "effect operation")) return null; operation = Previous.Text; }
        if (IsIdentifier("target")) { Advance(); if (!ExpectOneOf(["source", "own-row", "both-players-in-row"], "effect target")) return null; target = Previous.Text; }
        if (!AtLineEnd) Error(Current, "Unexpected token after effect declaration.");
        return new EffectDeclarationSyntax(line, id, amount, duration, operation, target);
    }
    private CardDeclarationSyntax? ParseCard(int line)
    {
        if (!ExpectIdentifier("card id")) return null;
        var id = Previous.Text;
        if (!Expect(TokenKind.String, "quoted card display name")) return null;
        var name = Previous.Text;
        if (!ExpectIdentifierText("power") || !Expect(TokenKind.Integer, "non-negative card power")) return null;
        var power = Previous.Text;
        if (!ExpectIdentifierText("row") || !ExpectOneOf(["melee", "ranged", "siege"], "card row")) return null;
        var row = Previous.Text;
        string? effectId = null;
        if (IsIdentifier("effect")) { Advance(); if (!ExpectIdentifier("effect id")) return null; effectId = Previous.Text; }
        if (!AtLineEnd) Error(Current, "Unexpected token after card declaration.");
        return new CardDeclarationSyntax(line, id, name, power, row, effectId);
    }
    private bool ExpectIdentifier(string description) { if (Current.Kind == TokenKind.Identifier) { Advance(); return true; } Error(Current, $"Expected {description}."); return false; }
    private bool ExpectIdentifierText(string expected) { if (IsIdentifier(expected)) { Advance(); return true; } Error(Current, $"Expected '{expected}'."); return false; }
    private bool Expect(TokenKind kind, string description) { if (Current.Kind == kind) { Advance(); return true; } Error(Current, $"Expected {description}."); return false; }
    private bool ExpectOneOf(IReadOnlyCollection<string> values, string description)
    {
        if (Current.Kind == TokenKind.Identifier && values.Contains(Current.Text, StringComparer.OrdinalIgnoreCase)) { Advance(); return true; }
        Error(Current, $"Expected {description}: {string.Join(", ", values)}."); return false;
    }
    private bool IsIdentifier(string value) => Current.Kind == TokenKind.Identifier && Current.Text.Equals(value, StringComparison.OrdinalIgnoreCase);
    private bool AtLineEnd => Current.Kind is TokenKind.NewLine or TokenKind.EndOfFile;
    private Token Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
    private Token Previous => _tokens[Math.Max(0, _position - 1)];
    private Token Advance() { var token = Current; if (Current.Kind != TokenKind.EndOfFile) _position++; return token; }
    private bool Match(TokenKind kind) { if (Current.Kind != kind) return false; Advance(); return true; }
    private void Error(Token token, string message) => _diagnostics.Add(new Diagnostic(token.Line, message, token.Column));
}
