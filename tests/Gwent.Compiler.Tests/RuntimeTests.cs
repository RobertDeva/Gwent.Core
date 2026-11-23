using Gwent.Compiler;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class RuntimeTests
{
    [Fact]
    public void ScopeResolvesVariablesThroughParentScopes()
    {
        var root = new RuntimeScope();
        root.Define("amount", RuntimeValue.From(4));
        var child = root.CreateChild();
        child.Define("bonus", RuntimeValue.From(3));

        Assert.Equal(4, child.Resolve("amount").AsNumber());
        Assert.Equal(3, child.Resolve("bonus").AsNumber());
    }

    [Fact]
    public void AssignmentUpdatesNearestExistingBinding()
    {
        var root = new RuntimeScope();
        root.Define("amount", RuntimeValue.From(4));
        var child = root.CreateChild();
        child.Define("amount", RuntimeValue.From(8));

        child.Assign("amount", RuntimeValue.From(9));

        Assert.Equal(9, child.Resolve("amount").AsNumber());
        Assert.Equal(4, root.Resolve("amount").AsNumber());
    }

    [Fact]
    public void AssignmentCanUpdateAnEnclosingBinding()
    {
        var root = new RuntimeScope();
        root.Define("amount", RuntimeValue.From(4));
        var child = root.CreateChild();

        child.Assign("amount", RuntimeValue.From(6));

        Assert.Equal(6, root.Resolve("amount").AsNumber());
    }

    [Fact]
    public void DuplicateDefinitionsAndUnknownVariablesFailExplicitly()
    {
        var scope = new RuntimeScope();
        scope.Define("amount", RuntimeValue.From(4));

        Assert.Throws<InvalidOperationException>(() =>
            scope.Define("amount", RuntimeValue.From(5)));
        Assert.Throws<KeyNotFoundException>(() => scope.Resolve("missing"));
        Assert.Throws<KeyNotFoundException>(() =>
            scope.Assign("missing", RuntimeValue.From(5)));
    }

    [Fact]
    public void RuntimeValuesRejectImplicitTypeConversions()
    {
        var value = RuntimeValue.From(12);

        Assert.Equal(12, value.AsNumber());
        Assert.Throws<InvalidOperationException>(() => value.AsBoolean());
        Assert.Throws<InvalidOperationException>(() => value.AsString());
    }

    [Fact]
    public void ExecutionBudgetStopsUnboundedRuntimeWork()
    {
        var context = new RuntimeExecutionContext(
            new Gwent.Engine.Player("p1"),
            new Gwent.Engine.Card("unit", "Unit", 3, Gwent.Engine.Row.Melee),
            instructionLimit: 2);

        context.ConsumeInstruction();
        context.ConsumeInstruction();

        Assert.Equal(2, context.InstructionsExecuted);
        Assert.Throws<InvalidOperationException>(() => context.ConsumeInstruction());
    }

    [Fact]
    public void ExecutionContextsDoNotShareScopesOrInstructionCounters()
    {
        var player = new Gwent.Engine.Player("p1");
        var card = new Gwent.Engine.Card("unit", "Unit", 3, Gwent.Engine.Row.Melee);
        var first = new RuntimeExecutionContext(player, card);
        var second = new RuntimeExecutionContext(player, card);
        first.Scope.Define("temporary", RuntimeValue.From(1));
        first.ConsumeInstruction();

        Assert.False(second.Scope.TryResolve("temporary", out _));
        Assert.Equal(0, second.InstructionsExecuted);
    }

    [Fact]
    public void ExecutionScopesCanBeEnteredAndExitedSafely()
    {
        var context = new RuntimeExecutionContext(
            new Gwent.Engine.Player("p1"),
            new Gwent.Engine.Card("unit", "Unit", 3, Gwent.Engine.Row.Melee));
        context.Scope.Define("outer", RuntimeValue.From(1));
        context.EnterScope();
        context.Scope.Define("inner", RuntimeValue.From(2));

        Assert.Equal(1, context.Scope.Resolve("outer").AsNumber());
        Assert.Equal(2, context.Scope.Resolve("inner").AsNumber());
        context.ExitScope();

        Assert.Throws<KeyNotFoundException>(() => context.Scope.Resolve("inner"));
        Assert.Throws<InvalidOperationException>(() => context.ExitScope());
    }

    [Fact]
    public void CardCollectionsAreDefensiveSnapshots()
    {
        var card = new Gwent.Engine.Card("unit", "Unit", 3, Gwent.Engine.Row.Melee);
        var cards = new[] { card };
        var value = RuntimeValue.FromCards(cards);
        cards[0] = new Gwent.Engine.Card("other", "Other", 1, Gwent.Engine.Row.Ranged);

        Assert.Same(card, value.AsCards()[0]);
        Assert.Single(value.AsCards());
    }
}
