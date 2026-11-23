using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class RuntimeInterpreterTests
{
    private static RuntimeExecutionContext CreateContext(int limit = 100) =>
        new(new Player("p1"), new Card("source", "Source", 1, Row.Melee), instructionLimit: limit);

    [Fact]
    public void EvaluatesTypedArithmeticAndShortCircuitsBooleanExpressions()
    {
        var context = CreateContext();
        var expression = new BinaryExpression(
            new LiteralExpression(RuntimeValue.From(5)), RuntimeBinaryOperator.Add,
            new BinaryExpression(new LiteralExpression(RuntimeValue.From(2)),
                RuntimeBinaryOperator.Multiply, new LiteralExpression(RuntimeValue.From(3))));
        Assert.Equal(11, expression.Evaluate(context).AsNumber());

        var shortCircuit = new BinaryExpression(
            new LiteralExpression(RuntimeValue.From(false)), RuntimeBinaryOperator.And,
            new VariableExpression("undefined"));
        Assert.False(shortCircuit.Evaluate(context).AsBoolean());
    }

    [Fact]
    public void WhileLoopExecutesAssignmentsInLexicalScope()
    {
        var context = CreateContext();
        context.Scope.Define("counter", RuntimeValue.From(0));
        var loop = new WhileStatement(
            new BinaryExpression(new VariableExpression("counter"), RuntimeBinaryOperator.Less,
                new LiteralExpression(RuntimeValue.From(3))),
            new RuntimeStatement[]
            {
                new AssignVariableStatement("counter",
                    new BinaryExpression(new VariableExpression("counter"), RuntimeBinaryOperator.Add,
                        new LiteralExpression(RuntimeValue.From(1))))
            });
        new RuntimeInterpreter().Execute([loop], context);
        Assert.Equal(3, context.Scope.Resolve("counter").AsNumber());
    }

    [Fact]
    public void WhileLoopCannotExceedExecutionBudget()
    {
        var context = CreateContext(limit: 12);
        context.Scope.Define("counter", RuntimeValue.From(0));
        var loop = new WhileStatement(new LiteralExpression(RuntimeValue.From(true)),
            new RuntimeStatement[]
            {
                new AssignVariableStatement("counter",
                    new BinaryExpression(new VariableExpression("counter"), RuntimeBinaryOperator.Add,
                        new LiteralExpression(RuntimeValue.From(1))))
            });
        Assert.Throws<InvalidOperationException>(() => new RuntimeInterpreter().Execute([loop], context));
        Assert.True(context.Scope.Resolve("counter").AsNumber() > 0);
    }

    [Fact]
    public void ForEachUsesStableCardSnapshotAndIterationScopes()
    {
        var context = CreateContext();
        var first = new Card("one", "One", 1, Row.Melee);
        var second = new Card("two", "Two", 2, Row.Ranged);
        context.Scope.Define("cards", RuntimeValue.FromCards([first, second]));
        context.Scope.Define("sum", RuntimeValue.From(0));
        var loop = new ForEachCardStatement("unit", new VariableExpression("cards"),
            new RuntimeStatement[]
            {
                new AssignVariableStatement("sum",
                    new BinaryExpression(new VariableExpression("sum"), RuntimeBinaryOperator.Add,
                        new CardPropertyExpression(new VariableExpression("unit"), CardProperty.Power)))
            });
        new RuntimeInterpreter().Execute([loop], context);
        Assert.Equal(3, context.Scope.Resolve("sum").AsNumber());
        Assert.False(context.Scope.TryResolve("unit", out _));
    }
}
