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

        Assert.True(result.ApplyCardEffect(game, player, card));

        Assert.Equal(7, card.Power);
        Assert.Equal(7, Game.Score(player));
        Assert.Throws<InvalidOperationException>(() =>
            game.ApplyEffect(player, card, result.Effects["rally"].ToEngineEffect()));
    }

    [Fact]
    public void CompiledCardWithoutEffectDoesNotMutateTheGame()
    {
        var result = new DslCompiler().Compile("""
            card recruit "Recruit" power 2 row ranged
            """);
        Assert.True(result.Success);

        var player = new Player("human");
        var game = new Game(player, new Player("opponent"));
        var card = result.Cards["recruit"].CreateCard();
        game.AddToDeck(player, card);
        game.Draw(player);
        game.Play(player, card);

        Assert.False(result.ApplyCardEffect(game, player, card));
        Assert.Equal(2, card.Power);
    }

    [Fact]
    public void CompiledCatalogRejectsCardWithMismatchedDefinitionData()
    {
        var result = new DslCompiler().Compile("""
            card recruit "Recruit" power 2 row ranged
            """);
        Assert.True(result.Success);

        var player = new Player("human");
        var game = new Game(player, new Player("opponent"));
        var foreignCard = new Card("recruit", "Impostor", 2, Row.Ranged);
        game.AddToDeck(player, foreignCard);
        game.Draw(player);
        game.Play(player, foreignCard);

        Assert.Throws<ArgumentException>(() => result.ApplyCardEffect(game, player, foreignCard));
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

    [Fact]
    public void CompiledPersistentEffectChangesAnOpponentCardPlayedLater()
    {
        var result = new DslCompiler().Compile("""
            effect frost boost 1 duration while-source-on-field operation set target both-players-in-row
            card blizzard "Blizzard" power 0 row melee effect frost
            card unit "Unit" power 5 row melee
            """);
        Assert.True(result.Success);

        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var weather = result.Cards["blizzard"].CreateCard();
        var unit = result.Cards["unit"].CreateCard();
        game.AddToDeck(first, weather);
        game.AddToDeck(second, unit);
        game.Draw(first);
        game.Play(first, weather);
        Assert.True(result.ApplyCardEffect(game, first, weather));

        game.EndTurn();
        game.Draw(second);
        game.Play(second, unit);

        Assert.Equal(1, unit.Power);
        Assert.Equal(1, Game.Score(second));
    }

}
