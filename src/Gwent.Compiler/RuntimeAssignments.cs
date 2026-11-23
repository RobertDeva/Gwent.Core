namespace Gwent.Compiler;

public sealed record AssignmentExpression(string Name, RuntimeExpression ValueExpression)
    : RuntimeExpression
{
    public override RuntimeValue Evaluate(RuntimeExecutionContext context)
    {
        context.ConsumeInstruction();
        var value = ValueExpression.Evaluate(context);
        context.Scope.Assign(Name, value);
        return value;
    }
}
