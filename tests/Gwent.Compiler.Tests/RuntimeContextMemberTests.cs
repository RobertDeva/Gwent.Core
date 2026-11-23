using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class RuntimeContextMemberTests
{
    [Fact]
    public void ExposesLegacyContextAndPlayerCollectionsThroughTypedMembers()
    {
        var first = new Player("p1");
        var second = new Player("p2");
        var game = new Game(first, second);
        var card = new Card("unit", "Unit", 4, Row.Melee);
        game.AddToDeck(first, card);
        game.Draw(first);

        var context = new RuntimeExecutionContext(
            first, new Card("source", "Source", 1, Row.Melee), game);
        context.Scope.Define("context", RuntimeValue.From(context));
        var parser = new ExpressionParser("context.TriggerPlayer.Hand[0].Power");
        var expression = parser.Parse();

        Assert.Empty(parser.Diagnostics);
        Assert.Equal(4, expression!.Evaluate(context).AsNumber());
    }

    [Fact]
    public void ExposesOpponentCollectionsOnlyWhenGameContextExists()
    {
        var first = new Player("p1");
        var second = new Player("p2");
        var game = new Game(first, second);
        var card = new Card("enemy", "Enemy", 2, Row.Ranged);
        game.AddToDeck(second, card);
        game.EndTurn();
        game.Draw(second);

        var context = new RuntimeExecutionContext(
            first, new Card("source", "Source", 1, Row.Melee), game);
        context.Scope.Define("context", RuntimeValue.From(context));
        var parser = new ExpressionParser("context.OtherHand[0].Name");
        var expression = parser.Parse();

        Assert.Empty(parser.Diagnostics);
        Assert.Equal("Enemy", expression!.Evaluate(context).AsString());
    }
}
