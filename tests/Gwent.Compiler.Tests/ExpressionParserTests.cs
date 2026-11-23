using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class ExpressionParserTests
{
    [Fact]
    public void ParsesOperatorPrecedenceAndEvaluatesExpression()
    {
        var parser = new ExpressionParser("5 + 2 * 3");
        var expression = parser.Parse();

        Assert.Empty(parser.Diagnostics);
        var context = new RuntimeExecutionContext(new Player("p1"),
            new Card("source", "Source", 1, Row.Melee));
        Assert.Equal(11, expression!.Evaluate(context).AsNumber());
    }

    [Fact]
    public void ParsesCardPropertyPredicateAndBooleanLiterals()
    {
        var parser = new ExpressionParser("it.Power >= 4 && true");
        var expression = parser.Parse();
        var context = new RuntimeExecutionContext(new Player("p1"),
            new Card("source", "Source", 1, Row.Melee));
        context.Scope.Define("it", RuntimeValue.From(new Card("unit", "Unit", 5, Row.Melee)));

        Assert.Empty(parser.Diagnostics);
        Assert.True(expression!.Evaluate(context).AsBoolean());
    }

    [Fact]
    public void ReportsMalformedExpressionsWithSourcePosition()
    {
        var parser = new ExpressionParser("4 + )");

        Assert.Null(parser.Parse());
        Assert.NotEmpty(parser.Diagnostics);
        Assert.True(parser.Diagnostics[0].Column > 0);
    }

    [Fact]
    public void ParsesCollectionIndexerAndMethods()
    {
        var parser = new ExpressionParser("cards[0].Power");
        var expression = parser.Parse();
        var context = new RuntimeExecutionContext(new Player("p1"),
            new Card("source", "Source", 1, Row.Melee));
        context.Scope.Define("cards", RuntimeValue.FromCards([
            new Card("unit", "Unit", 7, Row.Melee)
        ]));

        Assert.Empty(parser.Diagnostics);
        Assert.Equal(7, expression!.Evaluate(context).AsNumber());
    }
}
