using Gwent.Engine;
using Xunit;

namespace Gwent.Engine.Tests;

public sealed class GameTests
{
    [Fact]
    public void DrawPlayAndDestroyMoveCardAcrossZones()
    {
        var player = new Player("first");
        var game = new Game(player, new Player("second"));
        var card = new Card("unit", "Unit", 5, Row.Melee);
        game.AddToDeck(player, card);

        Assert.Same(card, game.Draw(player));
        Assert.Empty(player.Deck);
        Assert.Single(player.Hand);

        game.Play(player, card);
        Assert.Empty(player.Hand);
        Assert.Single(player.Field);
        Assert.Equal(5, Game.Score(player));

        game.Destroy(player, card);
        Assert.Empty(player.Field);
        Assert.Single(player.Graveyard);
        Assert.Equal(0, Game.Score(player));
    }

    [Fact]
    public void DrawReturnsNullWhenDeckIsEmpty()
    {
        var player = new Player("first");
        var game = new Game(player, new Player("second"));

        Assert.Null(game.Draw(player));
    }

    [Fact]
    public void CannotPlayCardThatIsNotInPlayersHand()
    {
        var player = new Player("first");
        var game = new Game(player, new Player("second"));
        var card = new Card("unit", "Unit", 5, Row.Melee);

        Assert.Throws<InvalidOperationException>(() => game.Play(player, card));
        Assert.Empty(player.Field);
    }

    [Fact]
    public void CannotAddSameInstanceToTwoZonesOrPlayers()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var card = new Card("unit", "Unit", 5, Row.Melee);
        game.AddToDeck(first, card);

        Assert.Throws<InvalidOperationException>(() => game.AddToDeck(second, card));
        Assert.Single(first.Deck);
        Assert.Empty(second.Deck);
    }

    [Fact]
    public void SameDefinitionCanHaveMultipleIndependentInstancesInOneDeck()
    {
        var player = new Player("first");
        var game = new Game(player, new Player("second"));
        var firstCopy = new Card("unit", "Unit", 5, Row.Melee);
        var secondCopy = new Card("unit", "Unit", 5, Row.Melee);

        game.AddToDeck(player, firstCopy);
        game.AddToDeck(player, secondCopy);

        Assert.Equal(2, player.Deck.Count);
        Assert.NotEqual(firstCopy.InstanceId, secondCopy.InstanceId);
        Assert.Equal(firstCopy.DefinitionId, secondCopy.DefinitionId);
    }

    [Fact]
    public void CardInstanceCannotBeReusedInAnotherGame()
    {
        var card = new Card("unit", "Unit", 5, Row.Melee);
        var firstPlayer = new Player("first");
        new Game(firstPlayer, new Player("second")).AddToDeck(firstPlayer, card);

        var otherPlayer = new Player("other");
        var otherGame = new Game(otherPlayer, new Player("opponent"));

        Assert.Throws<InvalidOperationException>(() => otherGame.AddToDeck(otherPlayer, card));
    }

    [Fact]
    public void RejectsPlayerOutsideGame()
    {
        var game = new Game(new Player("a"), new Player("b"));
        Assert.Throws<InvalidOperationException>(() => game.Draw(new Player("c")));
    }

    [Fact]
    public void ZonesCannotBeModifiedThroughReadOnlyViews()
    {
        var player = new Player("first");
        var deck = Assert.IsAssignableFrom<IList<Card>>(player.Deck);

        Assert.Throws<NotSupportedException>(() => deck.Add(new Card("unit", "Unit", 1, Row.Melee)));
        Assert.Empty(player.Deck);
    }

    [Fact]
    public void ChangingOneCopyDoesNotChangeAnotherCopy()
    {
        var firstCopy = new Card("unit", "Unit", 5, Row.Melee);
        var secondCopy = new Card("unit", "Unit", 5, Row.Melee);

        firstCopy.IncreasePower(3);

        Assert.Equal(8, firstCopy.Power);
        Assert.Equal(5, secondCopy.Power);
    }
}
