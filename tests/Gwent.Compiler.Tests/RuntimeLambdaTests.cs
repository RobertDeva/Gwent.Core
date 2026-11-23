using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class RuntimeLambdaTests
{
    [Fact]
    public void ParsesAndExecutesLegacyPredicateLambda()
    {
        var parser = new ExpressionParser("cards.Find((unit) => unit.Power > 3)");
        var expression = parser.Parse();
        var context = new RuntimeExecutionContext(new Player("p1"),
            new Card("source", "Source", 1, Row.Melee));
        var small = new Card("small", "Small", 2, Row.Melee);
        var large = new Card("large", "Large", 5, Row.Ranged);
        context.Scope.Define("cards", RuntimeValue.FromCards([small, large]));

        Assert.Empty(parser.Diagnostics);
        var result = expression!.Evaluate(context).AsCards();
        Assert.Single(result);
        Assert.Same(large, result[0]);
    }
}
