using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class ExpressionTokenBoundaryTests
{
    [Fact]
    public void SubtractionDoesNotMergeAdjacentIdentifiers()
    {
        var parser = new ExpressionParser("left-right");
        var expression = parser.Parse();
        var context = new RuntimeExecutionContext(new Player("p1"),
            new Card("source", "Source", 1, Row.Melee));
        context.Scope.Define("left", RuntimeValue.From(9));
        context.Scope.Define("right", RuntimeValue.From(4));

        Assert.Empty(parser.Diagnostics);
        Assert.Equal(5, expression!.Evaluate(context).AsNumber());
    }

    [Fact]
    public void HyphenatedEffectMetadataRemainsOneIdentifier()
    {
        var tokens = new Lexer("effect frost boost 1 duration while-source-on-field").Tokenize();

        Assert.Contains(tokens, token =>
            token.Kind == TokenKind.Identifier && token.Text == "while-source-on-field");
    }
}
