namespace Gwent.Compiler;
public sealed class Lexer
{
    private readonly string _source;
    private readonly List<Token> _tokens = [];
    private readonly List<Diagnostic> _diagnostics = [];
    private int _position, _line = 1, _column = 1;
    public Lexer(string source) => _source = source ?? throw new ArgumentNullException(nameof(source));
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics.AsReadOnly();
    public IReadOnlyList<Token> Tokenize()
    {
        while (!AtEnd)
        {
            var c = Current;
            if (c is ' ' or '\t' or '\r') { Advance(); continue; }
            if (c == '\n') { _tokens.Add(new(TokenKind.NewLine, "\n", _line, _column)); AdvanceLine(); continue; }
            if (c == '#') { while (!AtEnd && Current != '\n') Advance(); continue; }
            if (char.IsLetter(c) || c == '_') { ReadIdentifier(); continue; }
            if (char.IsDigit(c)) { ReadInteger(); continue; }
            if (c == '"') { ReadString(); continue; }
            if (ReadSymbol()) continue;
            _diagnostics.Add(new Diagnostic(_line, $"Unexpected character '{c}'.", _column));
            Advance();
        }
        _tokens.Add(new(TokenKind.EndOfFile, string.Empty, _line, _column));
        return _tokens.AsReadOnly();
    }
    private bool AtEnd => _position >= _source.Length;
    private char Current => _source[_position];
    private void ReadIdentifier()
    {
        var line = _line; var column = _column; var start = _position;
        while (!AtEnd && (char.IsLetterOrDigit(Current) || Current == '_' || Current == '-')) Advance();
        _tokens.Add(new(TokenKind.Identifier, _source[start.._position], line, column));
    }
    private void ReadInteger()
    {
        var line = _line; var column = _column; var start = _position;
        while (!AtEnd && char.IsDigit(Current)) Advance();
        _tokens.Add(new(TokenKind.Integer, _source[start.._position], line, column));
    }
    private void ReadString()
    {
        var line = _line; var column = _column; Advance();
        var value = new System.Text.StringBuilder();
        while (!AtEnd && Current != '"')
        {
            if (Current == '\n') { _diagnostics.Add(new Diagnostic(line, "Unterminated string.", column)); _tokens.Add(new Token(TokenKind.NewLine, "\n", _line, _column)); AdvanceLine(); return; }
            if (Current == '\\')
            {
                Advance();
                if (AtEnd) break;
                var escaped = Current switch { '"' => '"', '\\' => '\\', 'n' => '\n', 't' => '\t', _ => '\0' };
                if (escaped == '\0') { _diagnostics.Add(new Diagnostic(_line, "Unsupported escape sequence.", _column)); value.Append(Current); }
                else value.Append(escaped);
                Advance(); continue;
            }
            value.Append(Current); Advance();
        }
        if (AtEnd) { _diagnostics.Add(new Diagnostic(line, "Unterminated string.", column)); return; }
        Advance();
        _tokens.Add(new(TokenKind.String, value.ToString(), line, column));
    }
    private bool ReadSymbol()
    {
        var line = _line;
        var column = _column;
        var first = Current;
        var second = _position + 1 < _source.Length ? _source[_position + 1] : '\\0';
        TokenKind? kind = first switch
        {
            ':' => TokenKind.Colon,
            ',' => TokenKind.Comma,
            ';' => TokenKind.Semicolon,
            '{' => TokenKind.OpenBrace,
            '}' => TokenKind.CloseBrace,
            '[' => TokenKind.OpenBracket,
            ']' => TokenKind.CloseBracket,
            '(' => TokenKind.OpenParen,
            ')' => TokenKind.CloseParen,
            '.' => TokenKind.Dot,
            '+' => TokenKind.Plus,
            '-' when second != '>' => TokenKind.Minus,
            '*' => TokenKind.Star,
            '/' => TokenKind.Slash,
            '%' => TokenKind.Percent,
            '^' => TokenKind.Caret,
            '=' when second == '>' => TokenKind.Arrow,
            '=' when second == '=' => TokenKind.EqualEqual,
            '=' => TokenKind.Assign,
            '!' when second == '=' => TokenKind.BangEqual,
            '>' when second == '=' => TokenKind.GreaterEqual,
            '>' => TokenKind.Greater,
            '<' when second == '=' => TokenKind.LessEqual,
            '<' => TokenKind.Less,
            '&' when second == '&' => TokenKind.AndAnd,
            '|' when second == '|' => TokenKind.OrOr,
            _ => null
        };
        if (kind is null) return false;

        var length = kind is TokenKind.Arrow or TokenKind.EqualEqual or TokenKind.BangEqual or
            TokenKind.GreaterEqual or TokenKind.LessEqual or TokenKind.AndAnd or TokenKind.OrOr ? 2 : 1;
        var text = _source.Substring(_position, length);
        for (var i = 0; i < length; i++) Advance();
        _tokens.Add(new Token(kind.Value, text, line, column));
        return true;
    }

    private void Advance() { _position++; _column++; }
    private void AdvanceLine() { _position++; _line++; _column = 1; }
}
