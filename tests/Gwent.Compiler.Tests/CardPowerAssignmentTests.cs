using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class CardPowerAssignmentTests
{
    [Fact]
    public void AssignmentUpdatesCardPowerAndExpiresAtRoundCleanup()
    {
        var first = new Player("p1");
        var second = new Player("p2");
        var game = new Game(first, second);
        var source = new Card("source", "Source", 1, Row.Melee);
        var target = new Card("target", "Target", 3, Row.Melee);
        game.AddToDeck(first, source);
        game.AddToDeck(first, target);
        game.Draw(first);
        game.Draw(first);
        game.Play(first, source);
        game.EndTurn();
        game.EndTurn();
        game.Play(first, target);

        var context = new RuntimeExecutionContext(first, source, game);
        var expression = new ExpressionParser("unit.Power = 9").Parse();
        context.Scope.Define("unit", RuntimeValue.From(target));

        var result = expression!.Evaluate(context);

        Assert.Equal(9, result.AsNumber());
        Assert.Equal(9, target.Power);

        game.Pass(first);
        game.Pass(second);

        Assert.Equal(CardZone.Graveyard, target.Zone);
        Assert.Equal(3, target.Power);
    }

    [Fact]
    public void PowerAssignmentRequiresCardOnFieldAndGameContext()
    {
        var player = new Player("p1");
        var target = new Card("target", "Target", 3, Row.Melee);
        var context = new RuntimeExecutionContext(player, target);
        context.Scope.Define("unit", RuntimeValue.From(target));
        var expression = new ExpressionParser("unit.Power = 9").Parse();

        Assert.Throws<InvalidOperationException>(() => expression!.Evaluate(context));
    }

    [Fact]
    public void GameRejectsPowerMutationOnCardsOutsideField()
    {
        var first = new Player("p1");
        var second = new Player("p2");
        var game = new Game(first, second);
        var card = new Card("unit", "Unit", 2, Row.Melee);
        game.AddToDeck(first, card);

        Assert.Throws<InvalidOperationException>(() =>
            game.ApplyPowerModifier(first, card, "test", EffectOperation.Set, 7));
    }
}
