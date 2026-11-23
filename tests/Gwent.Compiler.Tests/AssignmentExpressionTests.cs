using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class AssignmentExpressionTests
{
    [Fact]
    public void ParsesAndExecutesVariableAssignment()
    {
        var parser = new ExpressionParser("score = score + 4");
        var expression = parser.Parse();
        var context = new RuntimeExecutionContext(new Player("p1"),
            new Card("source", "Source", 1, Row.Melee));
        context.Scope.Define("score", RuntimeValue.From(3));

        Assert.Empty(parser.Diagnostics);
        Assert.Equal(7, expression!.Evaluate(context).AsNumber());
        Assert.Equal(7, context.Scope.Resolve("score").AsNumber());
    }

    [Fact]
    public void RejectsAssignmentToNonVariableExpression()
    {
        var parser = new ExpressionParser("1 + 2 = 3");

        Assert.Null(parser.Parse());
        Assert.Contains(parser.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("Assignments are supported", StringComparison.Ordinal));
    }
}
