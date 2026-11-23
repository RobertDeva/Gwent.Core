namespace Gwent.Compiler;

public enum RuntimeUnaryOperator { Not, Negate }

public sealed record UnaryExpression(
    RuntimeUnaryOperator Operator,
    RuntimeExpression Operand) : RuntimeExpression
{
    public override RuntimeValue Evaluate(RuntimeExecutionContext context)
    {
        context.ConsumeInstruction();
        var value = Operand.Evaluate(context);
        return Operator switch
        {
            RuntimeUnaryOperator.Not => RuntimeValue.From(!value.AsBoolean()),
            RuntimeUnaryOperator.Negate => RuntimeValue.From(checked(-value.AsNumber())),
            _ => throw new InvalidOperationException($"Unsupported unary operator '{Operator}'.")
        };
    }
}

public sealed record IncrementExpression(string VariableName, int Delta) : RuntimeExpression
{
    public override RuntimeValue Evaluate(RuntimeExecutionContext context)
    {
        context.ConsumeInstruction();
        if (Delta is not (1 or -1))
            throw new ArgumentOutOfRangeException(nameof(Delta));
        var current = context.Scope.Resolve(VariableName).AsNumber();
        var updated = RuntimeValue.From(checked(current + Delta));
        context.Scope.Assign(VariableName, updated);
        return updated;
    }
}
