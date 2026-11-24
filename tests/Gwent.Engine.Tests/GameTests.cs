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
    public void GameRejectsPlayersWithDuplicateIds()
    {
        var first = new Player("same-id");
        var second = new Player("same-id");

        Assert.Throws<ArgumentException>(() => new Game(first, second));
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
    public void PlayerCanPlayOnlyOneCardPerTurn()
    {
        var first = new Player("first");
        var game = new Game(first, new Player("second"));
        var firstCard = new Card("first", "First", 4, Row.Melee);
        var secondCard = new Card("second", "Second", 3, Row.Ranged);
        game.AddToDeck(first, firstCard);
        game.AddToDeck(first, secondCard);
        game.Draw(first);
        game.Draw(first);
        game.Play(first, secondCard);

        Assert.Throws<InvalidOperationException>(() => game.Play(first, firstCard));
        Assert.Single(first.Field);
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
        Assert.Equal(0, game.FirstRoundsWon);
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
            if (ReferenceEquals(game.CurrentPlayer, second)) game.Pass(second);
            game.Draw(first);
            game.Play(first, card);
            game.Pass(first);
            if (!game.IsFinished) game.Pass(second);
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
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var firstCopy = new Card("unit", "Unit", 5, Row.Melee, "boost");
        var secondCopy = new Card("unit", "Unit", 5, Row.Melee, "boost");
        game.AddToDeck(first, firstCopy);
        game.AddToDeck(first, secondCopy);
        game.Draw(first);
        game.Play(first, secondCopy);
        game.ApplyEffect(first, secondCopy, new CardEffect("boost", 3));
        Assert.Equal(8, secondCopy.Power);
        Assert.Equal(5, firstCopy.Power);
    }

    [Fact]
    public void PersistentEffectAffectsBothPlayersAndFutureCardsInItsRow()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var weather = new Card("weather", "Weather", 0, Row.Melee, "frost");
        var firstUnit = new Card("first-unit", "First unit", 5, Row.Melee);
        var secondUnit = new Card("second-unit", "Second unit", 4, Row.Melee);
        var laterUnit = new Card("later-unit", "Later unit", 6, Row.Melee);

        game.AddToDeck(first, firstUnit);
        game.AddToDeck(first, weather);
        game.AddToDeck(second, laterUnit);
        game.AddToDeck(second, secondUnit);
        game.Draw(first);
        game.Play(first, weather);
        game.ApplyEffect(first, weather, new CardEffect(
            "frost", 1, EffectDuration.WhileSourceOnField,
            EffectOperation.Set, EffectTargetScope.BothPlayersInRow));
        game.EndTurn();
        game.Draw(second);
        game.Play(second, secondUnit);

        Assert.Equal(0, first.Board.Score);
        Assert.Equal(1, second.Board.Score);

        game.EndTurn();
        game.EndTurn();
        game.Draw(second);
        game.Play(second, laterUnit);
        Assert.Equal(1, laterUnit.Power);
    }

    [Fact]
    public void PersistentEffectIsRemovedWhenItsSourceIsDestroyed()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var source = new Card("horn", "Horn", 0, Row.Ranged, "aura");
        var unit = new Card("unit", "Unit", 5, Row.Ranged);
        game.AddToDeck(first, unit);
        game.AddToDeck(first, source);
        game.Draw(first);
        game.Play(first, source);
        game.ApplyEffect(first, source, new CardEffect(
            "aura", 2, EffectDuration.WhileSourceOnField,
            EffectOperation.Multiply, EffectTargetScope.OwnRow));
        game.EndTurn();
        game.EndTurn();
        game.Draw(first);
        game.Play(first, unit);
        Assert.Equal(10, unit.Power);

        game.Destroy(first, source);
        Assert.Equal(5, unit.Power);
        Assert.Single(first.Graveyard);
        Assert.Equal(CardZone.Graveyard, source.Zone);
    }

    [Fact]
    public void RemovingOnePersistentSourcePreservesModifiersFromOtherSources()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var firstAura = new Card("aura-one", "Aura One", 0, Row.Melee, "aura");
        var secondAura = new Card("aura-two", "Aura Two", 0, Row.Melee, "aura");
        var unit = new Card("unit", "Unit", 5, Row.Melee);

        game.AddToDeck(first, unit);
        game.AddToDeck(first, secondAura);
        game.AddToDeck(first, firstAura);
        game.Draw(first);
        game.Draw(first);
        game.Play(first, firstAura);
        game.ApplyEffect(first, firstAura, new CardEffect(
            "aura", 2, EffectDuration.WhileSourceOnField,
            EffectOperation.Add, EffectTargetScope.OwnRow));

        game.EndTurn();
        game.EndTurn();
        game.Play(first, secondAura);
        game.ApplyEffect(first, secondAura, new CardEffect(
            "aura", 3, EffectDuration.WhileSourceOnField,
            EffectOperation.Add, EffectTargetScope.OwnRow));

        game.EndTurn();
        game.EndTurn();
        game.Draw(first);
        game.Play(first, unit);

        Assert.Equal(10, unit.Power);

        game.Destroy(first, firstAura);

        Assert.Equal(8, unit.Power);
        Assert.Equal(CardZone.Graveyard, firstAura.Zone);
        Assert.Equal(CardZone.Field, secondAura.Zone);
    }

    [Fact]
    public void InstantEffectRemainsAfterItsSourceLeavesTheField()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var source = new Card("boost", "Boost", 0, Row.Melee, "instant");
        game.AddToDeck(first, source);
        game.Draw(first);
        game.Play(first, source);
        game.ApplyEffect(first, source, new CardEffect("instant", 3));
        game.Destroy(first, source);

        Assert.Equal(3, source.Power);
        Assert.Equal(CardZone.Graveyard, source.Zone);
    }

    [Fact]
    public void PersistentEffectsAreRemovedWhenRoundEnds()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var source = new Card("weather", "Weather", 0, Row.Siege, "storm");
        var unit = new Card("unit", "Unit", 5, Row.Siege);
        game.AddToDeck(first, unit);
        game.AddToDeck(first, source);
        game.Draw(first);
        game.Play(first, source);
        game.ApplyEffect(first, source, new CardEffect(
            "storm", 1, EffectDuration.WhileSourceOnField,
            EffectOperation.Set, EffectTargetScope.OwnRow));
        game.EndTurn();
        game.EndTurn();
        game.Draw(first);
        game.Play(first, unit);
        Assert.Equal(1, unit.Power);
        game.Pass(first);
        game.Pass(second);

        Assert.Equal(CardZone.Graveyard, source.Zone);
        Assert.Equal(CardZone.Graveyard, unit.Zone);
        Assert.Equal(5, unit.Power);
    }


    [Fact]
    public void LeavingTheFieldRemovesIncomingPersistentModifiersFromTheCard()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var weather = new Card("weather", "Weather", 0, Row.Melee, "frost");
        var unit = new Card("unit", "Unit", 5, Row.Melee);
        game.AddToDeck(first, weather);
        game.AddToDeck(second, unit);
        game.Draw(first);
        game.Play(first, weather);
        game.ApplyEffect(first, weather, new CardEffect(
            "frost", 1, EffectDuration.WhileSourceOnField,
            EffectOperation.Set, EffectTargetScope.BothPlayersInRow));
        game.EndTurn();
        game.Draw(second);
        game.Play(second, unit);
        Assert.Equal(1, unit.Power);

        game.Destroy(second, unit);

        Assert.Equal(CardZone.Graveyard, unit.Zone);
        Assert.Equal(5, unit.Power);
    }





    [Fact]
    public void RoundBoundModifierSurvivesLeavingFieldAndExpiresAtRoundEnd()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var card = new Card("unit", "Unit", 5, Row.Melee);
        game.AddToDeck(first, card);
        game.Draw(first);
        game.Play(first, card);
        game.ApplyPowerModifier(first, card, "temporary-buff",
            EffectOperation.Add, 3, EffectDuration.UntilRoundEnd);

        game.MoveCard(card, first, CardZone.Graveyard);

        Assert.Equal(8, card.Power);
        game.Pass(first);
        game.Pass(second);

        Assert.Equal(CardZone.Graveyard, card.Zone);
        Assert.Equal(5, card.Power);
    }

    [Fact]
    public void RoundBoundModifierSurvivesDestroyUntilRoundCleanup()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var card = new Card("unit", "Unit", 5, Row.Melee);
        game.AddToDeck(first, card);
        game.Draw(first);
        game.Play(first, card);
        game.ApplyPowerModifier(first, card, "temporary-buff",
            EffectOperation.Add, 3, EffectDuration.UntilRoundEnd);

        game.Destroy(first, card);

        Assert.Equal(8, card.Power);
        game.Pass(first);
        game.Pass(second);

        Assert.Equal(5, card.Power);
    }



    [Fact]
    public void ActivePlayerCanDestroyAnOpponentCard()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var destroyer = new Card("destroyer", "Destroyer", 1, Row.Melee);
        var target = new Card("target", "Target", 5, Row.Ranged);
        game.AddToDeck(first, destroyer);
        game.AddToDeck(second, target);

        game.Draw(first);
        game.Play(first, destroyer);
        game.EndTurn();
        game.Draw(second);
        game.Play(second, target);
        game.EndTurn();

        game.Destroy(first, target);

        Assert.Equal(CardZone.Graveyard, target.Zone);
        Assert.DoesNotContain(target, second.Field);
        Assert.Contains(target, second.Graveyard);
        Assert.Empty(first.Graveyard);
        game.ValidateCardZones();
    }



    [Fact]
    public void DestroyingOpponentAuraRemovesItsModifiersFromBothFields()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var target = new Card("target", "Target", 5, Row.Melee);
        var aura = new Card("aura", "Aura", 0, Row.Melee, "frost");
        game.AddToDeck(first, target);
        game.AddToDeck(second, aura);

        game.Draw(first);
        game.Play(first, target);
        game.EndTurn();
        game.Draw(second);
        game.Play(second, aura);
        game.ApplyEffect(second, aura, new CardEffect(
            "frost", 1, EffectDuration.WhileSourceOnField,
            EffectOperation.Set, EffectTargetScope.BothPlayersInRow));
        Assert.Equal(1, target.Power);
        game.EndTurn();

        game.Destroy(first, aura);

        Assert.Equal(5, target.Power);
        Assert.Equal(CardZone.Graveyard, aura.Zone);
        Assert.Contains(aura, second.Graveyard);
        game.ValidateCardZones();
    }



    [Fact]
    public void PlayerChoosesOneOfTheCardAllowedRowsWhenPlaying()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var definition = new CardDefinition(
            "multi-row", "Multi-row Unit", 4, "Northern", "Unit", "Silver",
            [Row.Melee, Row.Ranged], "Can enter either row");
        var card = definition.CreateInstance(Row.Melee);
        game.AddToDeck(player, card);
        game.Draw(player);

        game.Play(player, card, Row.Ranged);

        Assert.Equal(Row.Ranged, card.Row);
        Assert.Empty(player.Board.GetRow(Row.Melee));
        Assert.Contains(card, player.Board.GetRow(Row.Ranged));
        game.ValidateCardZones();
    }

    [Fact]
    public void PlayingInAnUnallowedRowDoesNotMoveTheCardOrConsumeTheTurn()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var definition = new CardDefinition(
            "melee-only", "Melee Unit", 4, "Northern", "Unit", "Silver",
            [Row.Melee], "Melee only");
        var card = definition.CreateInstance(Row.Melee);
        game.AddToDeck(player, card);
        game.Draw(player);

        Assert.Throws<InvalidOperationException>(() => game.Play(player, card, Row.Siege));

        Assert.Equal(CardZone.Hand, card.Zone);
        Assert.Contains(card, player.Hand);
        Assert.Empty(player.Field);
        game.ValidateCardZones();
    }



    [Fact]
    public void PersistentRowEffectsDoNotModifyGoldUnits()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var silverDefinition = new CardDefinition(
            "silver", "Silver Unit", 5, "Northern", "Unit", "Silver",
            [Row.Melee], "Silver unit");
        var goldDefinition = new CardDefinition(
            "gold", "Gold Unit", 10, "Northern", "Unit", "Gold",
            [Row.Melee], "Gold unit");
        var source = new Card("weather", "Weather", 0, Row.Melee, "weather");
        var silver = silverDefinition.CreateInstance(Row.Melee);
        var gold = goldDefinition.CreateInstance(Row.Melee);
        game.AddToDeck(first, source);
        game.AddToDeck(first, silver);
        game.AddToDeck(second, gold);
        game.MoveCard(source, first, CardZone.Field);
        game.MoveCard(silver, first, CardZone.Field);
        game.MoveCard(gold, second, CardZone.Field);

        game.ApplyEffect(first, source, new CardEffect(
            "weather", 1, EffectDuration.WhileSourceOnField,
            EffectOperation.Set, EffectTargetScope.BothPlayersInRow));

        Assert.Equal(1, silver.Power);
        Assert.Equal(10, gold.Power);
        game.ValidateCardZones();
    }



    [Fact]
    public void WeatherZoneSourceAppliesAcrossBothFieldsUntilItLeavesWeatherZone()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var unitDefinition = new CardDefinition(
            "unit", "Silver Unit", 5, "Northern", "Unit", "Silver",
            [Row.Melee], "Silver unit");
        var weatherDefinition = new CardDefinition(
            "weather", "Rain", 0, "Neutral", "Weather", "Special",
            [Row.Melee], "Melee weather", "rain");
        var firstUnit = unitDefinition.CreateInstance(Row.Melee);
        var secondUnit = unitDefinition.CreateInstance(Row.Melee);
        var weather = weatherDefinition.CreateInstance(Row.Melee);
        game.AddToDeck(first, firstUnit);
        game.AddToDeck(first, weather);
        game.AddToDeck(second, secondUnit);

        game.Draw(first);
        game.Draw(first);
        game.Play(first, firstUnit);
        game.EndTurn();
        game.Draw(second);
        game.Play(second, secondUnit);
        game.EndTurn();
        game.Play(first, weather);
        game.ApplyEffect(first, weather, new CardEffect(
            "rain", 1, EffectDuration.WhileSourceOnField,
            EffectOperation.Set, EffectTargetScope.BothPlayersInRow));

        Assert.Equal(1, firstUnit.Power);
        Assert.Equal(1, secondUnit.Power);
        Assert.Contains(weather, first.Weather);

        game.MoveCard(weather, first, CardZone.Graveyard);

        Assert.Equal(5, firstUnit.Power);
        Assert.Equal(5, secondUnit.Power);
        Assert.Contains(weather, first.Graveyard);
        game.ValidateCardZones();
    }

    [Fact]
    public void UpgradeZoneSourceOnlyAffectsItsOwnersRowAndExpiresWhenRemoved()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var unitDefinition = new CardDefinition(
            "unit", "Silver Unit", 5, "Northern", "Unit", "Silver",
            [Row.Melee], "Silver unit");
        var upgradeDefinition = new CardDefinition(
            "horn", "Horn", 0, "Neutral", "Upgrade", "Special",
            [Row.Melee], "Melee boost", "horn");
        var firstUnit = unitDefinition.CreateInstance(Row.Melee);
        var secondUnit = unitDefinition.CreateInstance(Row.Melee);
        var upgrade = upgradeDefinition.CreateInstance(Row.Melee);
        game.AddToDeck(first, firstUnit);
        game.AddToDeck(first, upgrade);
        game.AddToDeck(second, secondUnit);

        game.Draw(first);
        game.Draw(first);
        game.Play(first, firstUnit);
        game.EndTurn();
        game.Draw(second);
        game.Play(second, secondUnit);
        game.EndTurn();
        game.Play(first, upgrade);
        game.ApplyEffect(first, upgrade, new CardEffect(
            "horn", 2, EffectDuration.WhileSourceOnField,
            EffectOperation.Add, EffectTargetScope.OwnRow));

        Assert.Equal(7, firstUnit.Power);
        Assert.Equal(5, secondUnit.Power);
        Assert.Contains(upgrade, first.Upgrades);

        game.MoveCard(upgrade, first, CardZone.Graveyard);

        Assert.Equal(5, firstUnit.Power);
        Assert.Equal(5, secondUnit.Power);
        Assert.Contains(upgrade, first.Graveyard);
        game.ValidateCardZones();
    }



    [Fact]
    public void PlayingWeatherInSameRowReplacesExistingWeatherAndRoundCleanupGravesIt()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var definition = new CardDefinition(
            "weather", "Rain", 0, "Neutral", "Weather", "Special",
            [Row.Melee], "Melee weather");
        var firstWeather = definition.CreateInstance(Row.Melee);
        var secondWeather = definition.CreateInstance(Row.Melee);
        game.AddToDeck(first, firstWeather);
        game.AddToDeck(second, secondWeather);

        game.Draw(first);
        game.Play(first, firstWeather);
        game.EndTurn();
        game.Draw(second);
        game.Play(second, secondWeather);

        Assert.Empty(first.Weather);
        Assert.Contains(firstWeather, first.Graveyard);
        Assert.Contains(secondWeather, second.Weather);
        game.ValidateCardZones();

        game.Pass(second);
        game.Pass(first);

        Assert.Empty(second.Weather);
        Assert.Contains(secondWeather, second.Graveyard);
        game.ValidateCardZones();
    }

    [Fact]
    public void PlayingUpgradeReplacesOnlySameOwnersUpgradeAndRoundCleanupGravesIt()
    {
        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var definition = new CardDefinition(
            "horn", "Horn", 0, "Neutral", "Upgrade", "Special",
            [Row.Melee], "Melee upgrade");
        var firstUpgrade = definition.CreateInstance(Row.Melee);
        var replacement = definition.CreateInstance(Row.Melee);
        var filler = new Card("filler", "Filler", 1, Row.Melee);
        game.AddToDeck(first, firstUpgrade);
        game.AddToDeck(first, replacement);
        game.AddToDeck(second, filler);

        game.Draw(first);
        game.Draw(first);
        game.Play(first, firstUpgrade);
        game.EndTurn();
        game.Draw(second);
        game.Play(second, filler);
        game.EndTurn();
        game.Play(first, replacement);

        Assert.Empty(first.Field);
        Assert.Contains(firstUpgrade, first.Graveyard);
        Assert.Contains(replacement, first.Upgrades);
        Assert.Empty(second.Upgrades);
        game.ValidateCardZones();

        game.Pass(first);
        game.Pass(second);

        Assert.Empty(first.Upgrades);
        Assert.Contains(replacement, first.Graveyard);
        game.ValidateCardZones();
    }


}
