namespace Gwent.Compiler;

public sealed class ExpressionParser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly List<Diagnostic> _diagnostics = [];
    private int _position;

    public ExpressionParser(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lexer = new Lexer(source);
        _tokens = lexer.Tokenize();
        _diagnostics.AddRange(lexer.Diagnostics);
    }

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics.AsReadOnly();

    public RuntimeExpression? Parse()
    {
        var expression = ParseExpression();
        if (Current.Kind != TokenKind.EndOfFile)
            Error(Current, "Unexpected token after expression.");
        return _diagnostics.Count == 0 ? expression : null;
    }

    private RuntimeExpression? ParseExpression(int minimumPrecedence = 0)
    {
        var left = ParsePrefix();
        if (left is null) return null;

        left = ParsePostfix(left);
        while (TryGetBinaryOperator(Current.Kind, out var op, out var precedence, out var rightAssociative) &&
               precedence >= minimumPrecedence)
        {
            Advance();
            var right = ParseExpression(rightAssociative ? precedence : precedence + 1);
            if (right is null)
            {
                Error(Current, "Expected an expression after the operator.");
                return null;
            }
            left = new BinaryExpression(left, op, right);
        }

        if (minimumPrecedence == 0 && Match(TokenKind.Assign))
        {
            var assignedValue = ParseExpression();
            if (assignedValue is null) return null;
            if (left is not VariableExpression variable)
            {
                Error(Previous, "Only variables can be assigned by an expression.");
                return null;
            }
            return new AssignmentExpression(variable.Name, assignedValue);
        }
        return left;
    }

    private RuntimeExpression? ParsePrefix()
    {
        var token = Advance();
        switch (token.Kind)
        {
            case TokenKind.Integer:
                if (int.TryParse(token.Text, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var number))
                    return new LiteralExpression(RuntimeValue.From(number));
                Error(token, "Integer literal is outside the supported range.");
                return null;
            case TokenKind.String:
                return new LiteralExpression(RuntimeValue.From(token.Text));
            case TokenKind.Identifier:
                if (token.Text.Equals("true", StringComparison.OrdinalIgnoreCase))
                    return new LiteralExpression(RuntimeValue.From(true));
                if (token.Text.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return new LiteralExpression(RuntimeValue.From(false));
                return new VariableExpression(token.Text);
            case TokenKind.OpenParen:
                if (Current.Kind == TokenKind.Identifier &&
                    Peek(1).Kind == TokenKind.CloseParen &&
                    Peek(2).Kind == TokenKind.Arrow)
                {
                    var parameter = Advance().Text;
                    Advance();
                    Advance();
                    var body = ParseExpression();
                    return body is null ? null : new LambdaExpression(parameter, body);
                }
                var nested = ParseExpression();
                if (!Match(TokenKind.CloseParen)) Error(Current, "Expected ')' to close expression.");
                return nested;
            case TokenKind.Minus:
                var operand = ParseExpression(8);
                return operand is null ? null :
                    new BinaryExpression(new LiteralExpression(RuntimeValue.From(0)),
                        RuntimeBinaryOperator.Subtract, operand);
            default:
                Error(token, "Expected a literal, identifier, or parenthesized expression.");
                return null;
        }
    }

    private RuntimeExpression ParsePostfix(RuntimeExpression expression)
    {
        while (true)
        {
            if (Match(TokenKind.Dot))
            {
                if (Current.Kind != TokenKind.Identifier)
                {
                    Error(Current, "Expected a property or method name after '.'.");
                    return expression;
                }
                var name = Advance().Text;
                if (Match(TokenKind.OpenParen))
                {
                    var arguments = ParseArguments();
                    expression = new CollectionMethodExpression(expression, name, arguments);
                }
                else if (Enum.TryParse<CardProperty>(name, true, out var property))
                    expression = new CardPropertyExpression(expression, property);
                else
                {
                    Error(Previous, $"Unknown card property '{name}'.");
                    return expression;
                }
                continue;
            }

            if (Match(TokenKind.OpenBracket))
            {
                var index = ParseExpression();
                if (index is null) return expression;
                if (!Match(TokenKind.CloseBracket)) Error(Current, "Expected ']' after index.");
                expression = new IndexExpression(expression, index);
                continue;
            }
            return expression;
        }
    }

    private IReadOnlyList<RuntimeExpression> ParseArguments()
    {
        var arguments = new List<RuntimeExpression>();
        if (Match(TokenKind.CloseParen)) return arguments.AsReadOnly();
        do
        {
            var argument = ParseExpression();
            if (argument is not null) arguments.Add(argument);
            else break;
        } while (Match(TokenKind.Comma));

        if (!Match(TokenKind.CloseParen)) Error(Current, "Expected ')' after arguments.");
        return arguments.AsReadOnly();
    }

    private static bool TryGetBinaryOperator(
        TokenKind kind, out RuntimeBinaryOperator op, out int precedence, out bool rightAssociative)
    {
        rightAssociative = false;
        (op, precedence) = kind switch
        {
            TokenKind.OrOr => (RuntimeBinaryOperator.Or, 1),
            TokenKind.AndAnd => (RuntimeBinaryOperator.And, 2),
            TokenKind.EqualEqual => (RuntimeBinaryOperator.Equal, 3),
            TokenKind.BangEqual => (RuntimeBinaryOperator.NotEqual, 3),
            TokenKind.Greater => (RuntimeBinaryOperator.Greater, 4),
            TokenKind.GreaterEqual => (RuntimeBinaryOperator.GreaterOrEqual, 4),
            TokenKind.Less => (RuntimeBinaryOperator.Less, 4),
            TokenKind.LessEqual => (RuntimeBinaryOperator.LessOrEqual, 4),
            TokenKind.Plus => (RuntimeBinaryOperator.Add, 5),
            TokenKind.Minus => (RuntimeBinaryOperator.Subtract, 5),
            TokenKind.Star => (RuntimeBinaryOperator.Multiply, 6),
            TokenKind.Slash => (RuntimeBinaryOperator.Divide, 6),
            TokenKind.Percent => (RuntimeBinaryOperator.Modulo, 6),
            TokenKind.Caret => (RuntimeBinaryOperator.Power, 7),
            _ => (default, -1)
        };
        rightAssociative = kind == TokenKind.Caret;
        return precedence >= 0;
    }

    private Token Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
    private Token Peek(int offset) => _tokens[Math.Min(_position + offset, _tokens.Count - 1)];
    private Token Previous => _tokens[Math.Max(0, _position - 1)];
    private Token Advance()
    {
        var token = Current;
        if (Current.Kind != TokenKind.EndOfFile) _position++;
        return token;
    }
    private bool Match(TokenKind kind)
    {
        if (Current.Kind != kind) return false;
        Advance();
        return true;
    }
    private void Error(Token token, string message) =>
        _diagnostics.Add(new Diagnostic(token.Line, message, token.Column));
}
