using Gwent.Engine;
using Xunit;

namespace Gwent.Engine.Tests;

public sealed class GameTests
{
    [Fact]
    public void PlayingCardsUpdatesScoreAndWinner()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        game.Play(first, new Card("unit", "Unit", 5, Row.Melee));
        game.Play(second, new Card("enemy", "Enemy", 3, Row.Ranged));
        Assert.Equal(5, Game.Score(first));
        Assert.Same(first, game.Winner);
    }

    [Fact]
    public void RejectsPlayerOutsideGame()
    {
        var game = new Game(new Player("a"), new Player("b"));
        Assert.Throws<InvalidOperationException>(() => game.Play(new Player("c"), new Card("x", "X", 1, Row.Siege)));
    }

    [Fact]
    public void SamePhysicalCardCannotBePlayedTwiceInOneGame()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var card = new Card("unit", "Unit", 5, Row.Melee);

        game.Play(first, card);

        Assert.Throws<InvalidOperationException>(() => game.Play(second, card));
        Assert.Single(first.Field);
        Assert.Empty(second.Field);
    }

    [Fact]
    public void DifferentInstancesOfSameDefinitionCanBePlayedTogether()
    {
        var player = new Player("first");
        var game = new Game(player, new Player("second"));
        var firstCopy = new Card("unit", "Unit", 5, Row.Melee);
        var secondCopy = new Card("unit", "Unit", 5, Row.Melee);

        game.Play(player, firstCopy);
        game.Play(player, secondCopy);

        Assert.NotEqual(firstCopy.InstanceId, secondCopy.InstanceId);
        Assert.Equal(firstCopy.DefinitionId, secondCopy.DefinitionId);
        Assert.Equal(2, player.Field.Count);
        Assert.Equal(10, Game.Score(player));
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

    [Fact]
    public void FieldCannotBeModifiedThroughReadOnlyView()
    {
        var player = new Player("first");
        var field = Assert.IsAssignableFrom<IList<Card>>(player.Field);

        Assert.Throws<NotSupportedException>(() => field.Add(new Card("unit", "Unit", 1, Row.Melee)));
        Assert.Empty(player.Field);
    }
}
