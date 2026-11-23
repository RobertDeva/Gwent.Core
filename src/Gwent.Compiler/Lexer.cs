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
            _diagnostics.Add(new Diagnostic(_line, $"Unexpected character '{c}' at column {_column}."));
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
            if (Current == '\n') { _diagnostics.Add(new Diagnostic(line, $"Unterminated string beginning at column {column}.")); AdvanceLine(); return; }
            if (Current == '\\')
            {
                Advance();
                if (AtEnd) break;
                var escaped = Current switch { '"' => '"', '\\' => '\\', 'n' => '\n', 't' => '\t', _ => '\0' };
                if (escaped == '\0') { _diagnostics.Add(new Diagnostic(_line, $"Unsupported escape sequence at column {_column}.")); value.Append(Current); }
                else value.Append(escaped);
                Advance(); continue;
            }
            value.Append(Current); Advance();
        }
        if (AtEnd) { _diagnostics.Add(new Diagnostic(line, $"Unterminated string beginning at column {column}.")); return; }
        Advance();
        _tokens.Add(new(TokenKind.String, value.ToString(), line, column));
    }
    private void Advance() { _position++; _column++; }
    private void AdvanceLine() { _position++; _line++; _column = 1; }
}
