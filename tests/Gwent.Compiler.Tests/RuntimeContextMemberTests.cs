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
        var parser = new ExpressionParser("context.TriggerPlayer.Hand[0].Power");
        var expression = parser.Parse();

        Assert.Empty(parser.Diagnostics);
        Assert.Equal(4, expression!.Evaluate(context).AsNumber());
    }



    [Fact]
    public void ExposesCardOwnerAndAllowedRangesWithTheirLegacyTypes()
    {
        var first = new Player("p1");
        var game = new Game(first, new Player("p2"));
        var definition = new CardDefinition(
            "unit", "Unit", 4, "Northern", "Unit", "Silver",
            [Row.Melee, Row.Ranged], "A unit");
        var card = definition.CreateInstance(Row.Melee);
        game.AddToDeck(first, card);
        game.Draw(first);

        var context = new RuntimeExecutionContext(
            first, new Card("source", "Source", 1, Row.Melee), game);

        var ownerParser = new ExpressionParser("context.Hand[0].Owner.Id");
        var rangeParser = new ExpressionParser("context.Hand[0].Range[1]");
        var countParser = new ExpressionParser("context.Hand[0].Range.Count");

        Assert.Equal("p1", ownerParser.Parse()!.Evaluate(context).AsString());
        Assert.Equal("Range", rangeParser.Parse()!.Evaluate(context).AsString());
        Assert.Equal(2, countParser.Parse()!.Evaluate(context).AsNumber());
    }

    [Fact]
    public void ExposesOtherPlayerAsATypedContextMember()
    {
        var first = new Player("p1");
        var second = new Player("p2");
        var game = new Game(first, second);
        var context = new RuntimeExecutionContext(
            first, new Card("source", "Source", 1, Row.Melee), game);
        var parser = new ExpressionParser("context.OtherPlayer.Id");
        var expression = parser.Parse();

        Assert.Empty(parser.Diagnostics);
        Assert.Equal("p2", expression!.Evaluate(context).AsString());
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
        var parser = new ExpressionParser("context.OtherHand[0].Name");
        var expression = parser.Parse();

        Assert.Empty(parser.Diagnostics);
        Assert.Equal("Enemy", expression!.Evaluate(context).AsString());
    }
}
