using Gwent.Engine;
using Xunit;

namespace Gwent.Engine.Tests;

public sealed class ZoneMovementTests
{
    [Fact]
    public void MovesCardsBetweenZonesAndTransfersOwnership()
    {
        var first = new Player("p1");
        var second = new Player("p2");
        var game = new Game(first, second);
        var card = new Card("unit", "Unit", 3, Row.Melee);
        game.AddToDeck(first, card);
        game.MoveCard(card, second, CardZone.Hand);

        Assert.Empty(first.Deck);
        Assert.Contains(card, second.Hand);
        Assert.Equal("p2", card.OwnerId);
        Assert.Equal(CardZone.Hand, card.Zone);
    }

    [Fact]
    public void MovesFieldCardOutAndRemovesItsSourceBoundEffects()
    {
        var first = new Player("p1");
        var second = new Player("p2");
        var game = new Game(first, second);
        var source = new Card("source", "Source", 2, Row.Melee, "aura");
        var target = new Card("target", "Target", 3, Row.Melee);
        game.AddToDeck(first, source);
        game.AddToDeck(first, target);
        game.Draw(first);
        game.Draw(first);
        game.Play(first, source);
        game.EndTurn();
        game.EndTurn();
        game.Play(first, target);
        game.ApplyEffect(first, source,
            new CardEffect("aura", 2, EffectDuration.WhileSourceOnField,
                EffectOperation.Add, EffectTargetScope.OwnRow));
        Assert.Equal(5, target.Power);

        game.MoveCard(source, first, CardZone.Graveyard);

        Assert.Equal(CardZone.Graveyard, source.Zone);
        Assert.Equal(3, target.Power);
    }

    [Fact]
    public void PopCanDetachThenPushCardIntoAnotherZone()
    {
        var first = new Player("p1");
        var game = new Game(first, new Player("p2"));
        var card = new Card("unit", "Unit", 3, Row.Melee);
        game.AddToDeck(first, card);

        game.MoveCard(card, first, CardZone.Unassigned);
        Assert.Equal(CardZone.Unassigned, card.Zone);
        game.MoveCard(card, first, CardZone.Hand);

        Assert.Contains(card, first.Hand);
        Assert.Equal(CardZone.Hand, card.Zone);
    }
}
