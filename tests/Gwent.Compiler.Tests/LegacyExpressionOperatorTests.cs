using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class LegacyExpressionOperatorTests
{
    [Fact]
    public void SupportsLegacyStringConcatenationOperators()
    {
        var context = new RuntimeExecutionContext(new Player("p1"),
            new Card("source", "Source", 1, Row.Melee));
        var concat = new ExpressionParser("\"Gwent\" @ \"Core\"").Parse();
        var concatWithSpace = new ExpressionParser("\"Gwent\" @@ \"Core\"").Parse();

        Assert.Equal("GwentCore", concat!.Evaluate(context).AsString());
        Assert.Equal("Gwent Core", concatWithSpace!.Evaluate(context).AsString());
    }

    [Fact]
    public void SupportsLogicalNotAndVariableIncrementDecrement()
    {
        var context = new RuntimeExecutionContext(new Player("p1"),
            new Card("source", "Source", 1, Row.Melee));
        context.Scope.Define("counter", RuntimeValue.From(4));
        var not = new ExpressionParser("!false").Parse();
        var increment = new ExpressionParser("counter++").Parse();
        var decrement = new ExpressionParser("counter--").Parse();

        Assert.True(not!.Evaluate(context).AsBoolean());
        Assert.Equal(5, increment!.Evaluate(context).AsNumber());
        Assert.Equal(4, decrement!.Evaluate(context).AsNumber());
        Assert.Equal(4, context.Scope.Resolve("counter").AsNumber());
    }

    [Fact]
    public void SupportsUnaryNegativeNumbers()
    {
        var context = new RuntimeExecutionContext(new Player("p1"),
            new Card("source", "Source", 1, Row.Melee));
        var expression = new ExpressionParser("-4 + 9").Parse();

        Assert.Equal(5, expression!.Evaluate(context).AsNumber());
    }
}
