using System.Text;

namespace Gwent.Compiler;

/// <summary>Splits one source file into top-level effect and card declarations.</summary>
public sealed class RuntimeSourceProgramCompiler
{
    public RuntimeCardProgramCompilationResult Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var diagnostics = lexer.Diagnostics.ToList();
        var effects = new List<Token>();
        var cards = new List<Token>();

        for (var position = 0; position < tokens.Count - 1;)
        {
            var token = tokens[position];
            if (token.Kind is TokenKind.NewLine or TokenKind.Semicolon)
            {
                position++;
                continue;
            }

            List<Token>? destination = token.Kind == TokenKind.Identifier &&
                token.Text.Equals("effect", StringComparison.OrdinalIgnoreCase) ? effects :
                token.Kind == TokenKind.Identifier &&
                token.Text.Equals("card", StringComparison.OrdinalIgnoreCase) ? cards : null;

            if (destination is null)
            {
                diagnostics.Add(new Diagnostic(token.Line,
                    $"Unexpected top-level declaration '{token.Text}'.", token.Column));
                position++;
                continue;
            }

            var end = FindDeclarationEnd(tokens, position);
            if (end < 0)
            {
                diagnostics.Add(new Diagnostic(token.Line,
                    "Unterminated top-level declaration.", token.Column));
                break;
            }

            for (var i = position; i < end; i++) destination.Add(tokens[i]);
            destination.Add(new Token(TokenKind.NewLine, "\n",
                tokens[end - 1].Line, tokens[end - 1].Column + tokens[end - 1].Text.Length));
            position = end;
        }

        if (diagnostics.Count > 0)
            return new RuntimeCardProgramCompilationResult(null, diagnostics.AsReadOnly());

        var effectSource = Reconstruct(effects);
        var cardSource = Reconstruct(cards);
        return new RuntimeCardProgramCompiler().Compile(effectSource, cardSource);
    }

    private static int FindDeclarationEnd(IReadOnlyList<Token> tokens, int start)
    {
        var opening = start;
        while (opening < tokens.Count && tokens[opening].Kind != TokenKind.OpenBrace)
            opening++;
        if (opening >= tokens.Count) return -1;

        var depth = 0;
        for (var i = opening; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == TokenKind.OpenBrace) depth++;
            else if (tokens[i].Kind == TokenKind.CloseBrace)
            {
                depth--;
                if (depth == 0) return i + 1;
            }
        }
        return -1;
    }

    private static string Reconstruct(IReadOnlyList<Token> tokens)
    {
        var builder = new StringBuilder();
        var line = tokens.Count == 0 ? 1 : tokens[0].Line;
        foreach (var token in tokens)
        {
            if (token.Kind == TokenKind.NewLine)
            {
                builder.AppendLine();
                line = token.Line + 1;
                continue;
            }

            while (line < token.Line)
            {
                builder.AppendLine();
                line++;
            }
            if (builder.Length > 0 && builder[^1] != '\n') builder.Append(' ');
            builder.Append(token.Kind == TokenKind.String ? Quote(token.Text) : token.Text);
        }
        return builder.ToString();
    }

    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
}
