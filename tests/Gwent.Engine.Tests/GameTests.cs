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
        Assert.Equal(CardZone.Hand, card.Zone);
        Assert.Empty(player.Deck);
        game.Play(player, card);
        Assert.Equal(CardZone.Field, card.Zone);
        Assert.Empty(player.Hand);
        Assert.Single(player.Field);
        Assert.Equal(5, Game.Score(player));
        game.Destroy(player, card);
        Assert.Equal(CardZone.Graveyard, card.Zone);
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
    public void CardsAreStoredAndScoredByTheirRows()
    {
        var player = new Player("first");
        var game = new Game(player, new Player("second"));
        var melee = new Card("swordsman", "Swordsman", 5, Row.Melee);
        var ranged = new Card("archer", "Archer", 3, Row.Ranged);
        game.AddToDeck(player, melee);
        game.AddToDeck(player, ranged);
        game.Draw(player);
        game.Play(player, ranged);
        game.EndTurn();
        game.EndTurn();
        game.Draw(player);
        game.Play(player, melee);
        Assert.Single(player.Board.GetRow(Row.Melee));
        Assert.Single(player.Board.GetRow(Row.Ranged));
        Assert.Empty(player.Board.GetRow(Row.Siege));
        Assert.Equal(5, Game.Score(player, Row.Melee));
        Assert.Equal(3, Game.Score(player, Row.Ranged));
        Assert.Equal(8, Game.Score(player));
    }

    [Fact]
    public void PassingTwiceResolvesRoundAndAwardsRoundPoint()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var card = new Card("unit", "Unit", 5, Row.Melee);
        game.AddToDeck(first, card);
        game.Draw(first);
        game.Play(first, card);
        game.Pass(first);
        game.Pass(second);

        Assert.Equal(1, game.FirstRoundsWon);
        Assert.Equal(0, game.SecondRoundsWon);
        Assert.Equal(2, game.RoundNumber);
        Assert.Empty(first.Field);
        Assert.Single(first.Graveyard);
        Assert.Same(second, game.CurrentPlayer);
    }

    [Fact]
    public void FirstPlayerCannotActAfterPassing()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        game.Pass(first);
        Assert.Throws<InvalidOperationException>(() => game.Draw(first));
        Assert.Same(second, game.CurrentPlayer);
    }

    [Fact]
    public void PlayerCanContinueAfterOpponentPassesUntilTheyPass()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        game.Pass(first);
        var card = new Card("unit", "Unit", 5, Row.Melee);
        game.AddToDeck(second, card);
        Assert.Same(card, game.Draw(second));
        game.Play(second, card);
        game.Pass(second);
        Assert.Equal(1, game.FirstRoundsWon);
        Assert.Equal(1, game.SecondRoundsWon);
    }

    [Fact]
    public void TwoRoundWinsFinishMatchAndExposeWinner()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        for (var round = 0; round < 2; round++)
        {
            var card = new Card($"unit{round}", "Unit", 5, Row.Melee);
            game.AddToDeck(first, card);
            game.Draw(first);
            game.Play(first, card);
            game.Pass(first);
            game.Pass(second);
        }
        Assert.True(game.IsFinished);
        Assert.Same(first, game.Winner);
        Assert.Throws<InvalidOperationException>(() => game.EndTurn());
    }

    [Fact]
    public void RowCollectionsCannotBeModifiedExternally()
    {
        var player = new Player("first");
        var row = Assert.IsAssignableFrom<IList<Card>>(player.Board.GetRow(Row.Melee));
        Assert.Throws<NotSupportedException>(() => row.Add(new Card("unit", "Unit", 1, Row.Melee)));
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
