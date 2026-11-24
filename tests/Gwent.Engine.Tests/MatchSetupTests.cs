using Gwent.Engine;
using Xunit;

namespace Gwent.Engine.Tests;

public sealed class MatchSetupTests
{
    private static DeckDefinition CreateDeck(string prefix, string faction = "Northern")
    {
        var leader = new CardDefinition(
            $"{prefix}-leader", $"{prefix} Leader", 0, faction,
            "Leader", "Special", [Row.Melee], "Leader");
        var unit = new CardDefinition(
            $"{prefix}-unit", $"{prefix} Unit", 4, faction,
            "Unit", "Silver", [Row.Melee, Row.Ranged], "Unit");
        return new DeckDefinition($"{prefix}-deck", leader,
            Enumerable.Range(0, DeckDefinition.MinimumCardCount)
                .Select(index => new DeckCardEntry(unit,
                    index % 2 == 0 ? Row.Melee : Row.Ranged)));
    }

    private static (Game Game, Player First, Player Second) CreateInitializedGame()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        game.LoadDeck(first, CreateDeck("first"));
        game.LoadDeck(second, CreateDeck("second"));
        game.InitializeMatch(new Random(123));
        return (game, first, second);
    }

    [Fact]
    public void InitializationShufflesAndDealsTenCardsToEachPlayer()
    {
        var (game, first, second) = CreateInitializedGame();

        Assert.True(game.IsInitialized);
        Assert.Equal(10, first.Hand.Count);
        Assert.Equal(10, second.Hand.Count);
        Assert.Equal(15, first.Deck.Count);
        Assert.Equal(15, second.Deck.Count);
        Assert.All(first.Hand.Concat(second.Hand), card => Assert.Equal(CardZone.Hand, card.Zone));
        game.ValidateCardZones();
    }

    [Fact]
    public void InitializationRequiresBothValidatedDecksAndCanOnlyRunOnce()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);

        Assert.Throws<InvalidOperationException>(() => game.InitializeMatch());

        game.LoadDeck(first, CreateDeck("first"));
        game.LoadDeck(second, CreateDeck("second"));
        game.InitializeMatch(new Random(1));

        Assert.Throws<InvalidOperationException>(() => game.InitializeMatch());
        Assert.Throws<InvalidOperationException>(() => game.LoadDeck(first, CreateDeck("replacement")));
    }

    [Fact]
    public void MulliganReplacesUpToTwoCardsAndPreservesZoneIntegrity()
    {
        var (game, first, _) = CreateInitializedGame();
        var selected = first.Hand.Take(2).ToArray();

        var replaced = game.Mulligan(first, selected, new Random(7));

        Assert.Equal(2, replaced);
        Assert.Equal(10, first.Hand.Count);
        Assert.Equal(15, first.Deck.Count);
        Assert.All(selected, card => Assert.Contains(card.Zone, new[] { CardZone.Deck, CardZone.Hand }));
        game.ValidateCardZones();
    }

    [Fact]
    public void MulliganLimitIsTwoPerPlayerAcrossCalls()
    {
        var (game, first, second) = CreateInitializedGame();

        game.Mulligan(first, [first.Hand[0]], new Random(1));
        game.Mulligan(first, [first.Hand[0]], new Random(2));
        game.Mulligan(second, [second.Hand[0], second.Hand[1]], new Random(3));

        Assert.Throws<ArgumentException>(() =>
            game.Mulligan(first, [first.Hand[0]], new Random(4)));
        Assert.Equal(10, first.Hand.Count);
        Assert.Equal(10, second.Hand.Count);
        game.ValidateCardZones();
    }

    [Fact]
    public void MulliganRejectsDuplicateAndForeignSelectionsWithoutMutation()
    {
        var (game, first, second) = CreateInitializedGame();
        var selected = first.Hand[0];
        var handBefore = first.Hand.ToArray();
        var deckBefore = first.Deck.ToArray();

        Assert.Throws<ArgumentException>(() => game.Mulligan(first, [selected, selected]));
        Assert.Throws<ArgumentException>(() => game.Mulligan(first, [second.Hand[0]]));

        Assert.Equal(handBefore, first.Hand);
        Assert.Equal(deckBefore, first.Deck);
        game.ValidateCardZones();
    }

    [Fact]
    public void NormalTurnActionClosesMulliganPhaseAndHandCannotExceedTen()
    {
        var (game, first, _) = CreateInitializedGame();

        Assert.Null(game.Draw(first));
        Assert.Equal(15, first.Deck.Count);
        Assert.Throws<InvalidOperationException>(() =>
            game.Mulligan(first, [first.Hand[0]]));

        Assert.Equal(10, first.Hand.Count);
        game.ValidateCardZones();
    }
}
