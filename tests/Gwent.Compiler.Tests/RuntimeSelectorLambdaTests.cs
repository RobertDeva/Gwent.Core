using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class RuntimeSelectorLambdaTests
{
    [Fact]
    public void SelectorAcceptsLegacyLambdaPredicate()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var low = new Card("low", "Low", 2, Row.Melee);
        var high = new Card("high", "High", 8, Row.Ranged);
        game.AddToDeck(player, low);
        game.AddToDeck(player, high);
        game.Draw(player);
        game.Draw(player);

        var context = new RuntimeExecutionContext(
            player, new Card("source", "Source", 1, Row.Melee), game);
        var parsed = new ExpressionParser("(unit) => unit.Power >= 5").Parse();
        var selector = new RuntimeSelector(
            "hand",
            new LiteralExpression(RuntimeValue.From(false)),
            parsed);

        Assert.Same(high, selector.Select(context).Single());
    }
}
