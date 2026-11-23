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
}
