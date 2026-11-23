using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.IntegrationTests;

public sealed class CompilerEngineIntegrationTests
{
    [Fact]
    public void CompiledEffectChangesLiveGameStateAndScore()
    {
        var result = new DslCompiler().Compile("""
            effect rally boost 3
            card vanguard "Vanguard" power 4 row melee effect rally
            """);
        Assert.True(result.Success);

        var player = new Player("human");
        var opponent = new Player("opponent");
        var game = new Game(player, opponent);
        var card = result.Cards["vanguard"].CreateCard();

        game.Play(player, card);
        var effect = result.Effects[card.EffectId!];
        card.IncreasePower(effect.PowerIncrease);

        Assert.Equal(7, card.Power);
        Assert.Equal(7, Game.Score(player));
        Assert.Same(player, game.Winner);
    }

    [Fact]
    public void MultipleCopiesFromDslDefinitionCanExistInSameGame()
    {
        var result = new DslCompiler().Compile("""
            card vanguard "Vanguard" power 4 row melee
            """);
        Assert.True(result.Success);

        var player = new Player("human");
        var game = new Game(player, new Player("opponent"));
        var definition = result.Cards["vanguard"];

        game.Play(player, definition.CreateCard());
        game.Play(player, definition.CreateCard());

        Assert.Equal(2, player.Field.Count);
        Assert.Equal(8, Game.Score(player));
    }
}
