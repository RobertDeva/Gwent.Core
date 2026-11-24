using Gwent.Engine;
using Xunit;

namespace Gwent.Engine.Tests;

public sealed class DeckDefinitionTests
{
    private static CardDefinition CreateLeader(string faction = "Northern", string type = "Leader") =>
        new("leader", "Leader", 0, faction, type, "Special", [Row.Melee], "Leader ability");

    private static CardDefinition CreateUnit(string faction = "Northern") =>
        new("unit", "Unit", 4, faction, "Unit", "Silver", [Row.Melee, Row.Ranged], "A unit");

    private static DeckDefinition CreateDeck(int count = DeckDefinition.MinimumCardCount)
    {
        var unit = CreateUnit();
        return new DeckDefinition("northern-deck", CreateLeader(),
            Enumerable.Range(0, count)
                .Select(index => new DeckCardEntry(unit, index % 2 == 0 ? Row.Melee : Row.Ranged)));
    }

    [Fact]
    public void DeckRequiresAtLeastTwentyFiveCards()
    {
        var unit = CreateUnit();
        var exception = Assert.Throws<ArgumentException>(() =>
            new DeckDefinition("short", CreateLeader(),
                Enumerable.Range(0, 24).Select(_ => new DeckCardEntry(unit, Row.Melee))));
        Assert.Contains("at least 25 cards", exception.Message);
    }

    [Fact]
    public void DeckRequiresLeaderAndCardsFromTheSameFaction()
    {
        var unit = CreateUnit("Nilfgaard");
        var exception = Assert.Throws<ArgumentException>(() =>
            new DeckDefinition("mixed", CreateLeader("Northern"),
                Enumerable.Range(0, 25).Select(_ => new DeckCardEntry(unit, Row.Melee))));
        Assert.Contains("not leader faction", exception.Message);
    }

    [Fact]
    public void DeckLeaderMustHaveLeaderTypeAndCannotAppearAmongMainDeckCards()
    {
        var unit = CreateUnit();
        Assert.Throws<ArgumentException>(() =>
            new DeckDefinition("invalid-leader", CreateLeader(type: "Unit"),
                Enumerable.Range(0, 25).Select(_ => new DeckCardEntry(unit, Row.Melee))));

        var leader = CreateLeader();
        Assert.Throws<ArgumentException>(() =>
            new DeckDefinition("leader-in-pile", leader,
                Enumerable.Range(0, 24)
                    .Select(_ => new DeckCardEntry(unit, Row.Melee))
                    .Append(new DeckCardEntry(leader, Row.Melee))));
    }

    [Fact]
    public void RepeatedDefinitionsCreateIndependentPhysicalCardInstances()
    {
        var instances = CreateDeck().CreateCardInstances();
        Assert.Equal(25, instances.Count);
        Assert.All(instances, card => Assert.Equal("unit", card.DefinitionId));
        Assert.Equal(25, instances.Select(card => card.InstanceId).Distinct().Count());
        Assert.Equal(13, instances.Count(card => card.Row == Row.Melee));
        Assert.Equal(12, instances.Count(card => card.Row == Row.Ranged));
        Assert.All(instances, card => Assert.Equal(CardZone.Unassigned, card.Zone));
    }

    [Fact]
    public void GameLoadsValidatedDeckAndKeepsLeaderOutsideTheCardPile()
    {
        var first = new Player("first");
        var game = new Game(first, new Player("second"));
        var deck = CreateDeck();
        game.LoadDeck(first, deck);

        Assert.Same(deck.Leader, first.LeaderDefinition);
        Assert.Equal(25, first.Deck.Count);
        Assert.Empty(first.Hand);
        Assert.All(first.Deck, card => Assert.Equal(CardZone.Deck, card.Zone));
        game.ValidateCardZones();
    }

    [Fact]
    public void LoadingDeckIntoNonEmptyPlayerFailsWithoutChangingExistingCards()
    {
        var first = new Player("first");
        var game = new Game(first, new Player("second"));
        var existing = new Card("existing", "Existing", 1, Row.Melee);
        game.AddToDeck(first, existing);

        Assert.Throws<InvalidOperationException>(() => game.LoadDeck(first, CreateDeck()));

        Assert.Same(existing, Assert.Single(first.Deck));
        Assert.Null(first.LeaderDefinition);
        game.ValidateCardZones();
    }
}
