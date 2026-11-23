namespace Gwent.Compiler;

public sealed record RuntimeLambda(string ParameterName, RuntimeExpression Body)
{
    public RuntimeValue Invoke(RuntimeExecutionContext context, RuntimeValue argument)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(argument);
        context.EnterScope();
        try
        {
            context.Scope.Define(ParameterName, argument);
            return Body.Evaluate(context);
        }
        finally { context.ExitScope(); }
    }
}

public sealed record LambdaExpression(string ParameterName, RuntimeExpression Body)
    : RuntimeExpression
{
    public override RuntimeValue Evaluate(RuntimeExecutionContext context)
    {
        context.ConsumeInstruction();
        return RuntimeValue.From(new RuntimeLambda(ParameterName, Body));
    }
}
