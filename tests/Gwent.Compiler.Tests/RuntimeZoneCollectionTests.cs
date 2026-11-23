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
    public void AddDoesNotReorderExistingZoneCardsButPushDoes()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var first = new Card("first", "First", 1, Row.Melee);
        var second = new Card("second", "Second", 2, Row.Melee);
        game.AddToDeck(player, first);
        game.AddToDeck(player, second);
        game.Draw(player);
        game.Draw(player);
        var context = new RuntimeExecutionContext(player,
            new Card("source", "Source", 1, Row.Melee), game);
        context.Scope.Define("first", RuntimeValue.From(first));
        context.Scope.Define("second", RuntimeValue.From(second));

        new ExpressionParser("context.Hand.Add(second)").Parse()!.Evaluate(context);
        Assert.Equal(new[] { second, first }, player.Hand);

        new ExpressionParser("context.Hand.Push(second)").Parse()!.Evaluate(context);
        Assert.Equal(new[] { first, second }, player.Hand);
    }

    [Fact]
    public void LocalPushMovesExistingCardToEndWhileAddAppends()
    {
        var first = new Card("first", "First", 1, Row.Melee);
        var second = new Card("second", "Second", 2, Row.Melee);
        var collection = new RuntimeCardCollection([first, second]);

        collection.Push(first);
        Assert.Equal(new[] { second, first }, collection.Cards);

        collection.Add(first);
        Assert.Equal(new[] { second, first, first }, collection.Cards);
    }

    [Fact]
    public void PushAndSendBottomPreserveOrderingInHandCollections()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var first = new Card("first", "First", 1, Row.Melee);
        var second = new Card("second", "Second", 2, Row.Melee);
        game.AddToDeck(player, first);
        game.AddToDeck(player, second);
        game.Draw(player);
        game.Draw(player);
        var context = new RuntimeExecutionContext(player,
            new Card("source", "Source", 1, Row.Melee), game);
        context.Scope.Define("first", RuntimeValue.From(first));
        context.Scope.Define("second", RuntimeValue.From(second));

        new ExpressionParser("context.Hand.SendBottom(first)").Parse()!.Evaluate(context);
        Assert.Equal(new[] { first, second }, player.Hand);

        new ExpressionParser("context.Hand.Push(first)").Parse()!.Evaluate(context);
        Assert.Equal(new[] { second, first }, player.Hand);
    }


    [Fact]
    public void PushAndSendBottomPreserveOrderingInFieldCollections()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var first = new Card("first", "First", 1, Row.Melee);
        var second = new Card("second", "Second", 2, Row.Melee);
        game.AddToDeck(player, first);
        game.AddToDeck(player, second);
        game.MoveCard(first, player, CardZone.Field);
        game.MoveCard(second, player, CardZone.Field);
        var context = new RuntimeExecutionContext(player,
            new Card("source", "Source", 1, Row.Melee), game);
        context.Scope.Define("first", RuntimeValue.From(first));
        context.Scope.Define("second", RuntimeValue.From(second));

        new ExpressionParser("context.Field.SendBottom(second)").Parse()!.Evaluate(context);
        Assert.Equal(new[] { second, first }, player.Field);

        new ExpressionParser("context.Field.Push(second)").Parse()!.Evaluate(context);
        Assert.Equal(new[] { first, second }, player.Field);
    }

    [Fact]
    public void PushAndSendBottomPreserveOrderingInGraveyardCollections()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var first = new Card("first", "First", 1, Row.Melee);
        var second = new Card("second", "Second", 2, Row.Melee);
        game.AddToDeck(player, first);
        game.AddToDeck(player, second);
        game.MoveCard(first, player, CardZone.Graveyard);
        game.MoveCard(second, player, CardZone.Graveyard);
        var context = new RuntimeExecutionContext(player,
            new Card("source", "Source", 1, Row.Melee), game);
        context.Scope.Define("first", RuntimeValue.From(first));
        context.Scope.Define("second", RuntimeValue.From(second));

        new ExpressionParser("context.Graveyard.SendBottom(second)").Parse()!.Evaluate(context);
        Assert.Equal(new[] { second, first }, player.Graveyard);

        new ExpressionParser("context.Graveyard.Push(second)").Parse()!.Evaluate(context);
        Assert.Equal(new[] { first, second }, player.Graveyard);
    }


    [Fact]
    public void ShuffleSupportsEveryAssignedPlayerZone()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var cards = Enumerable.Range(0, 4)
            .Select(index => new Card($"unit-{index}", $"Unit {index}", index + 1, Row.Melee))
            .ToArray();
        foreach (var card in cards)
        {
            game.AddToDeck(player, card);
            game.Draw(player);
        }

        var context = new RuntimeExecutionContext(player,
            new Card("source", "Source", 1, Row.Melee), game);

        new ExpressionParser("context.Hand.Shuffle()").Parse()!.Evaluate(context);
        Assert.Equal(4, player.Hand.Count);
        Assert.All(cards, card => Assert.Contains(card, player.Hand));

        foreach (var card in cards)
            game.MoveCard(card, player, CardZone.Field);
        new ExpressionParser("context.Field.Shuffle()").Parse()!.Evaluate(context);
        Assert.Equal(4, player.Field.Count);
        Assert.All(cards, card => Assert.Contains(card, player.Field));

        foreach (var card in cards)
            game.MoveCard(card, player, CardZone.Graveyard);
        new ExpressionParser("context.Graveyard.Shuffle()").Parse()!.Evaluate(context);
        Assert.Equal(4, player.Graveyard.Count);
        Assert.All(cards, card => Assert.Contains(card, player.Graveyard));
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
