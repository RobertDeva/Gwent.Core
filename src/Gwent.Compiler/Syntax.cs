namespace Gwent.Compiler;
public enum TokenKind { Identifier, Integer, String, NewLine, Colon, Comma, Semicolon, OpenBrace, CloseBrace, OpenBracket, CloseBracket, OpenParen, CloseParen, Dot, Plus, Minus, Increment, Decrement, Star, Slash, Percent, Caret, Concatenate, ConcatenateWithSpace, Assign, EqualEqual, Bang, BangEqual, Greater, GreaterEqual, Less, LessEqual, AndAnd, OrOr, Arrow, EndOfFile }
public sealed record Token(TokenKind Kind, string Text, int Line, int Column);
public abstract record DeclarationSyntax(int Line);
public sealed record EffectDeclarationSyntax(int SourceLine, string Id, string Amount, string? Duration, string? Operation, string? Target) : DeclarationSyntax(SourceLine);
public sealed record CardDeclarationSyntax(int SourceLine, string Id, string Name, string Power, string Row, string? EffectId) : DeclarationSyntax(SourceLine);
public sealed record SyntaxTree(IReadOnlyList<DeclarationSyntax> Declarations, IReadOnlyList<Diagnostic> Diagnostics);
