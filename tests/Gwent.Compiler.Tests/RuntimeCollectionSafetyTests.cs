using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class RuntimeCollectionSafetyTests
{
    [Fact]
    public void ZoneCollectionsWithoutGameContextAreReadOnly()
    {
        var player = new Player("p1");
        var card = new Card("unit", "Unit", 2, Row.Melee);
        var context = new RuntimeExecutionContext(player,
            new Card("source", "Source", 1, Row.Melee));
        context.Scope.Define("unit", RuntimeValue.From(card));

        var expression = new ExpressionParser("context.Hand.Add(unit)").Parse();

        Assert.Throws<InvalidOperationException>(() => expression!.Evaluate(context));
    }

    [Fact]
    public void EffectCannotLeaveRegisteredCardsOutsideGameZones()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var card = new Card("unit", "Unit", 2, Row.Melee);
        game.AddToDeck(player, card);
        var context = new RuntimeExecutionContext(player,
            new Card("source", "Source", 1, Row.Melee), game);
        var effect = new RuntimeEffectDefinition("pop", [], [
            new EvaluateStatement(new CollectionMethodExpression(
                new RuntimeMemberExpression(new VariableExpression("context"), "Deck"),
                "Pop", []))
        ]);
        var invocation = new RuntimeEffectInvocation(
            "pop", new Dictionary<string, RuntimeExpression>(),
            new RuntimeSelector("field", new LiteralExpression(RuntimeValue.From(false))));
        var executor = new RuntimeEffectExecutor(new RuntimeEffectCatalog([effect]));

        Assert.Throws<InvalidOperationException>(() => executor.Execute(invocation, context));
        Assert.Equal(CardZone.Unassigned, card.Zone);
    }
}
