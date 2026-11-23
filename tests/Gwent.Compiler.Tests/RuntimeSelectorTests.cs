using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class RuntimeSelectorTests
{
    [Fact]
    public void ResolvesTriggerPlayersHandAndOpponentHand()
    {
        var first = new Player("p1");
        var second = new Player("p2");
        var game = new Game(first, second);
        var ownCard = new Card("own", "Own", 2, Row.Melee);
        var enemyCard = new Card("enemy", "Enemy", 3, Row.Ranged);
        game.AddToDeck(first, ownCard);
        game.AddToDeck(second, enemyCard);
        game.Draw(first);
        game.EndTurn();
        game.Draw(second);

        var context = new RuntimeExecutionContext(
            first, new Card("source", "Source", 1, Row.Melee), game);

        Assert.Same(ownCard, RuntimeSourceResolver.Resolve(context, "hand").Single());
        Assert.Same(enemyCard, RuntimeSourceResolver.Resolve(context, "otherHand").Single());
    }

    [Fact]
    public void SelectorFiltersCardsWithTypedPredicateAndSupportsSingle()
    {
        var first = new Player("p1");
        var game = new Game(first, new Player("p2"));
        var low = new Card("low", "Low", 2, Row.Melee);
        var high = new Card("high", "High", 7, Row.Ranged);
        game.AddToDeck(first, low);
        game.AddToDeck(first, high);
        game.Draw(first);
        game.Draw(first);

        var context = new RuntimeExecutionContext(
            first, new Card("source", "Source", 1, Row.Melee), game);
        var predicate = new BinaryExpression(
            new CardPropertyExpression(new VariableExpression("it"), CardProperty.Power),
            RuntimeBinaryOperator.Greater,
            new LiteralExpression(RuntimeValue.From(4)));
        var selector = new RuntimeSelector(
            "hand",
            new LiteralExpression(RuntimeValue.From(false)),
            predicate);

        Assert.Same(high, selector.Select(context).Single());
    }

    [Fact]
    public void ParentSourceRequiresExplicitParentTargets()
    {
        var context = new RuntimeExecutionContext(
            new Player("p1"), new Card("source", "Source", 1, Row.Melee));

        Assert.Throws<InvalidOperationException>(() =>
            RuntimeSourceResolver.Resolve(context, "parent"));
    }
}
