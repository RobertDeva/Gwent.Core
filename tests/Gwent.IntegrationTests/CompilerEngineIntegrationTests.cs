using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.IntegrationTests;

public sealed class CompilerEngineIntegrationTests
{
    [Fact]
    public void CompiledEffectRunsInsideEngineAndChangesLiveGameScore()
    {
        var result = new DslCompiler().Compile("""
            effect rally boost 3
            card vanguard "Vanguard" power 4 row melee effect rally
            """);
        Assert.True(result.Success);

        var player = new Player("human");
        var game = new Game(player, new Player("opponent"));
        var card = result.Cards["vanguard"].CreateCard();
        game.AddToDeck(player, card);
        Assert.Same(card, game.Draw(player));
        game.Play(player, card);

        game.ApplyEffect(player, card, result.Effects["rally"].ToEngineEffect());

        Assert.Equal(7, card.Power);
        Assert.Equal(7, Game.Score(player));
        Assert.Throws<InvalidOperationException>(() =>
            game.ApplyEffect(player, card, result.Effects["rally"].ToEngineEffect()));
    }

    [Fact]
    public void TwoCopiesOfSameDefinitionCanBeDrawnAndPlayedIndependently()
    {
        var result = new DslCompiler().Compile("""
            card vanguard "Vanguard" power 4 row melee
            """);
        Assert.True(result.Success);

        var player = new Player("human");
        var game = new Game(player, new Player("opponent"));
        var definition = result.Cards["vanguard"];
        var first = definition.CreateCard();
        var second = definition.CreateCard();
        game.AddToDeck(player, first);
        game.AddToDeck(player, second);

        Assert.Same(second, game.Draw(player));
        game.Play(player, second);
        game.EndTurn();
        game.EndTurn();
        Assert.Same(first, game.Draw(player));
        game.Play(player, first);

        Assert.Equal(2, player.Field.Count);
        Assert.Equal(8, Game.Score(player));
        Assert.NotEqual(first.InstanceId, second.InstanceId);
    }
}
