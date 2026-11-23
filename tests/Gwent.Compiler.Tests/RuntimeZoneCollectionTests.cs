using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class RuntimeZoneCollectionTests
{
    [Fact]
    public void PopAndPushTransferCardBetweenRealGameZones()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var card = new Card("unit", "Unit", 3, Row.Melee);
        game.AddToDeck(player, card);
        game.Draw(player);
        var context = new RuntimeExecutionContext(player,
            new Card("source", "Source", 1, Row.Melee), game);
        context.Scope.Define("card", RuntimeValue.From(card));

        var popped = new ExpressionParser("context.Hand.Pop()").Parse()!.Evaluate(context).AsCard();
        Assert.Equal(CardZone.Unassigned, popped.Zone);
        Assert.Empty(player.Hand);

        new ExpressionParser("context.Deck.Push(card)").Parse()!.Evaluate(context);
        Assert.Equal(CardZone.Deck, card.Zone);
        Assert.Contains(card, player.Deck);
    }

    [Fact]
    public void AddAndSendBottomMutateDeckOrderThroughEngine()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var bottom = new Card("bottom", "Bottom", 1, Row.Melee);
        var top = new Card("top", "Top", 2, Row.Melee);
        game.AddToDeck(player, top);
        game.AddToDeck(player, bottom);
        var context = new RuntimeExecutionContext(player,
            new Card("source", "Source", 1, Row.Melee), game);
        context.Scope.Define("bottom", RuntimeValue.From(bottom));

        new ExpressionParser("context.Deck.SendBottom(bottom)").Parse()!.Evaluate(context);

        Assert.Same(bottom, player.Deck[0]);
        Assert.Same(top, player.Deck[^1]);
    }

    [Fact]
    public void FieldCollectionAddSummonsCardAndRefreshesBoardState()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var card = new Card("unit", "Unit", 4, Row.Ranged);
        game.AddToDeck(player, card);
        var context = new RuntimeExecutionContext(player,
            new Card("source", "Source", 1, Row.Melee), game);
        context.Scope.Define("unit", RuntimeValue.From(card));

        new ExpressionParser("context.Field.Add(unit)").Parse()!.Evaluate(context);

        Assert.Equal(CardZone.Field, card.Zone);
        Assert.Contains(card, player.Field);
        Assert.Equal(4, player.Board.Score);
    }


    [Fact]
    public void RemoveMovesCardFromHandToGraveyard()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var card = new Card("unit", "Unit", 3, Row.Melee);
        game.AddToDeck(player, card);
        game.Draw(player);
        var context = new RuntimeExecutionContext(player,
            new Card("source", "Source", 1, Row.Melee), game);
        context.Scope.Define("card", RuntimeValue.From(card));

        new ExpressionParser("context.Hand.Remove(card)").Parse()!.Evaluate(context);

        Assert.Equal(CardZone.Graveyard, card.Zone);
        Assert.Empty(player.Hand);
        Assert.Contains(card, player.Graveyard);
    }

    [Fact]
    public void ForEachCanRemoveCardsFromTheLiveZoneWithoutInvalidatingIteration()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var cards = Enumerable.Range(0, 3)
            .Select(index => new Card($"unit-{index}", $"Unit {index}", index + 1, Row.Melee))
            .ToArray();
        foreach (var card in cards)
        {
            game.AddToDeck(player, card);
            game.Draw(player);
        }

        var context = new RuntimeExecutionContext(player,
            new Card("source", "Source", 1, Row.Melee), game);
        var hand = new RuntimeMemberExpression(new VariableExpression("context"), "Hand");
        var removeCurrent = new EvaluateStatement(new CollectionMethodExpression(
            hand, "Remove", [new VariableExpression("unit")]));
        var loop = new ForEachCardStatement("unit", hand, [removeCurrent]);

        new RuntimeInterpreter().Execute([loop], context);

        Assert.Empty(player.Hand);
        Assert.Equal(3, player.Graveyard.Count);
        Assert.All(cards, card => Assert.Equal(CardZone.Graveyard, card.Zone));
    }

    [Fact]
    public void ShuffleUsesExecutionScopedRandomSource()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var cards = Enumerable.Range(0, 8)
            .Select(index => new Card($"c{index}", $"Card {index}", index, Row.Melee))
            .ToArray();
        foreach (var card in cards) game.AddToDeck(player, card);
        var context = new RuntimeExecutionContext(player,
            new Card("source", "Source", 1, Row.Melee), game, random: new Random(123));

        new ExpressionParser("context.Deck.Shuffle()").Parse()!.Evaluate(context);

        Assert.Equal(8, player.Deck.Count);
        Assert.False(cards.Select(card => card.InstanceId)
            .SequenceEqual(player.Deck.Select(card => card.InstanceId)));
    }
}
