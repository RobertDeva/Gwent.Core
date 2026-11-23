using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class RuntimeEffectExecutorTests
{
    private static RuntimeExecutionContext Context()
    {
        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var card = new Card("source", "Source", 1, Row.Melee);
        return new RuntimeExecutionContext(player, card, game);
    }

    private static RuntimeSelector AllHand() =>
        new("hand", new LiteralExpression(RuntimeValue.From(false)));

    [Fact]
    public void BindsParametersByNameAndEnforcesTheirTypes()
    {
        var definition = new RuntimeEffectDefinition(
            "rally",
            [new RuntimeParameterDefinition("amount", RuntimeParameterType.Number)],
            AllHand(),
            [new AssignVariableStatement("result", new VariableExpression("amount"))]);
        var executor = new RuntimeEffectExecutor(new RuntimeEffectCatalog([definition]));
        var context = Context();
        context.Scope.Define("result", RuntimeValue.From(0));

        executor.Execute(new RuntimeEffectInvocation("rally",
            new Dictionary<string, RuntimeExpression>
            {
                ["amount"] = new LiteralExpression(RuntimeValue.From(4))
            }), context);

        Assert.Equal(4, context.Scope.Resolve("result").AsNumber());
    }

    [Fact]
    public void RejectsMissingUnknownAndWronglyTypedParameters()
    {
        var definition = new RuntimeEffectDefinition(
            "rally",
            [new RuntimeParameterDefinition("amount", RuntimeParameterType.Number)],
            AllHand(),
            []);
        var executor = new RuntimeEffectExecutor(new RuntimeEffectCatalog([definition]));

        Assert.Throws<ArgumentException>(() =>
            executor.Execute(new RuntimeEffectInvocation("rally",
                new Dictionary<string, RuntimeExpression>()), Context()));
        Assert.Throws<ArgumentException>(() =>
            executor.Execute(new RuntimeEffectInvocation("rally",
                new Dictionary<string, RuntimeExpression>
                {
                    ["unknown"] = new LiteralExpression(RuntimeValue.From(1))
                }), Context()));
        Assert.Throws<ArgumentException>(() =>
            executor.Execute(new RuntimeEffectInvocation("rally",
                new Dictionary<string, RuntimeExpression>
                {
                    ["amount"] = new LiteralExpression(RuntimeValue.From("four"))
                }), Context()));
    }

    [Fact]
    public void PostActionCanReceiveParentTargetsExplicitly()
    {
        var context = Context();
        context.Scope.Define("childCount", RuntimeValue.From(0));
        var player = context.TriggerPlayer;
        var card = new Card("unit", "Unit", 3, Row.Melee);
        context.Game!.AddToDeck(player, card);
        context.Game.Draw(player);

        var child = new RuntimeEffectDefinition(
            "child",
            [],
            new RuntimeSelector("parent", new LiteralExpression(RuntimeValue.From(false))),
            [new AssignVariableStatement("childCount",
                new LiteralExpression(RuntimeValue.From(1)))]);
        var parent = new RuntimeEffectDefinition(
            "parent",
            [],
            AllHand(),
            [],
            new RuntimeEffectInvocation("child", new Dictionary<string, RuntimeExpression>()));
        var executor = new RuntimeEffectExecutor(new RuntimeEffectCatalog([parent, child]));

        var targets = executor.Execute(new RuntimeEffectInvocation(
            "parent", new Dictionary<string, RuntimeExpression>()), context);

        Assert.Same(card, targets.Single());
        Assert.Equal(1, context.Scope.Resolve("childCount").AsNumber());
    }
}
