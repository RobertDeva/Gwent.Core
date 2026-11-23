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

    private static RuntimeEffectInvocation Invoke(string id,
        IReadOnlyDictionary<string, RuntimeExpression>? args = null,
        RuntimeSelector? selector = null,
        RuntimeEffectInvocation? postAction = null) =>
        new(id, args ?? new Dictionary<string, RuntimeExpression>(),
            selector ?? AllHand(), postAction);

    [Fact]
    public void BindsParametersByNameAndEnforcesTheirTypes()
    {
        var definition = new RuntimeEffectDefinition(
            "rally",
            [new RuntimeParameterDefinition("amount", RuntimeParameterType.Number)],
            [new AssignVariableStatement("result", new VariableExpression("amount"))]);
        var executor = new RuntimeEffectExecutor(new RuntimeEffectCatalog([definition]));
        var context = Context();
        context.Scope.Define("result", RuntimeValue.From(0));

        executor.Execute(Invoke("rally", new Dictionary<string, RuntimeExpression>
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
            []);
        var executor = new RuntimeEffectExecutor(new RuntimeEffectCatalog([definition]));

        Assert.Throws<ArgumentException>(() => executor.Execute(Invoke("rally"), Context()));
        Assert.Throws<ArgumentException>(() => executor.Execute(Invoke("rally",
            new Dictionary<string, RuntimeExpression>
            {
                ["unknown"] = new LiteralExpression(RuntimeValue.From(1))
            }), Context()));
        Assert.Throws<ArgumentException>(() => executor.Execute(Invoke("rally",
            new Dictionary<string, RuntimeExpression>
            {
                ["amount"] = new LiteralExpression(RuntimeValue.From("four"))
            }), Context()));
    }

    [Fact]
    public void PostActionCanSelectParentTargetsExplicitly()
    {
        var context = Context();
        context.Scope.Define("childCount", RuntimeValue.From(0));
        var player = context.TriggerPlayer;
        var card = new Card("unit", "Unit", 3, Row.Melee);
        context.Game!.AddToDeck(player, card);
        context.Game.Draw(player);

        var child = new RuntimeEffectDefinition(
            "child", [],
            [new AssignVariableStatement("childCount", new LiteralExpression(RuntimeValue.From(1)))]);
        var parent = new RuntimeEffectDefinition("parent", [], []);
        var executor = new RuntimeEffectExecutor(new RuntimeEffectCatalog([parent, child]));

        var invocation = Invoke("parent", selector: AllHand(),
            postAction: Invoke("child", selector:
                new RuntimeSelector("parent", new LiteralExpression(RuntimeValue.From(false)))));
        var targets = executor.Execute(invocation, context);

        Assert.Same(card, targets.Single());
        Assert.Equal(1, context.Scope.Resolve("childCount").AsNumber());
    }
}
