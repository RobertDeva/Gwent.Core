using Gwent.Engine;

namespace Gwent.Compiler;

public abstract record RuntimeExpression
{
    public int SourceLine { get; init; } = 1;
    public int SourceColumn { get; init; }
    public abstract RuntimeValue Evaluate(RuntimeExecutionContext context);
}

public sealed record LiteralExpression(RuntimeValue Literal) : RuntimeExpression
{
    public override RuntimeValue Evaluate(RuntimeExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ConsumeInstruction();
        return Literal ?? throw new InvalidOperationException("Literal value is missing.");
    }
}

public sealed record VariableExpression(string Name) : RuntimeExpression
{
    public override RuntimeValue Evaluate(RuntimeExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ConsumeInstruction();
        return context.Scope.Resolve(Name);
    }
}


public sealed record RuntimeFunctionExpression(
    string FunctionName,
    IReadOnlyList<RuntimeExpression> Arguments) : RuntimeExpression
{
    public override RuntimeValue Evaluate(RuntimeExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ConsumeInstruction();
        if (Arguments.Count != 1)
            throw new InvalidOperationException($"Function '{FunctionName}' requires one Player argument.");

        var player = Arguments[0].Evaluate(context).AsPlayer();
        var game = context.Game;
        if (game is null)
        {
            if (!ReferenceEquals(player, context.TriggerPlayer))
                throw new InvalidOperationException($"Function '{FunctionName}' requires a game to access another player.");
        }
        else if (!ReferenceEquals(player, game.First) && !ReferenceEquals(player, game.Second))
            throw new InvalidOperationException("Player argument does not belong to the current game.");

        var zone = FunctionName.ToLowerInvariant() switch
        {
            "handofplayer" => CardZone.Hand,
            "deckofplayer" => CardZone.Deck,
            "fieldofplayer" => CardZone.Field,
            "graveyardofplayer" => CardZone.Graveyard,
            _ => throw new InvalidOperationException($"Unknown runtime function '{FunctionName}'.")
        };
        return RuntimeValue.FromZoneCards(context, player, zone);
    }
}

public enum CardProperty { Name, Power, OwnerId, Row, DefinitionId }

public sealed record CardPropertyExpression(RuntimeExpression Target, CardProperty Property)
    : RuntimeExpression
{
    public override RuntimeValue Evaluate(RuntimeExecutionContext context)
    {
        context.ConsumeInstruction();
        var card = Target.Evaluate(context).AsCard();
        return Property switch
        {
            CardProperty.Name => RuntimeValue.From(card.Name),
            CardProperty.Power => RuntimeValue.From(card.Power),
            CardProperty.OwnerId => RuntimeValue.From(card.OwnerId ?? string.Empty),
            CardProperty.Row => RuntimeValue.From(card.Row.ToString()),
            CardProperty.DefinitionId => RuntimeValue.From(card.DefinitionId),
            _ => throw new InvalidOperationException($"Unsupported card property '{Property}'.")
        };
    }
}

public enum RuntimeBinaryOperator
{
    Add, Subtract, Multiply, Divide, Modulo, Power,
    Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual,
    And, Or, Concatenate, ConcatenateWithSpace
}

public sealed record BinaryExpression(
    RuntimeExpression Left,
    RuntimeBinaryOperator Operator,
    RuntimeExpression Right) : RuntimeExpression
{
    public override RuntimeValue Evaluate(RuntimeExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ConsumeInstruction();
        var left = Left.Evaluate(context);

        if (Operator == RuntimeBinaryOperator.And && !left.AsBoolean())
            return RuntimeValue.From(false);
        if (Operator == RuntimeBinaryOperator.Or && left.AsBoolean())
            return RuntimeValue.From(true);

        var right = Right.Evaluate(context);
        return Operator switch
        {
            RuntimeBinaryOperator.Add => RuntimeValue.From(checked(left.AsNumber() + right.AsNumber())),
            RuntimeBinaryOperator.Subtract => RuntimeValue.From(checked(left.AsNumber() - right.AsNumber())),
            RuntimeBinaryOperator.Multiply => RuntimeValue.From(checked(left.AsNumber() * right.AsNumber())),
            RuntimeBinaryOperator.Divide => RuntimeValue.From(left.AsNumber() / right.AsNumber()),
            RuntimeBinaryOperator.Modulo => RuntimeValue.From(left.AsNumber() % right.AsNumber()),
            RuntimeBinaryOperator.Power => RuntimeValue.From(Power(left.AsNumber(), right.AsNumber(), context)),
            RuntimeBinaryOperator.Equal => RuntimeValue.From(Equals(left.Value, right.Value)),
            RuntimeBinaryOperator.NotEqual => RuntimeValue.From(!Equals(left.Value, right.Value)),
            RuntimeBinaryOperator.Greater => RuntimeValue.From(left.AsNumber() > right.AsNumber()),
            RuntimeBinaryOperator.GreaterOrEqual => RuntimeValue.From(left.AsNumber() >= right.AsNumber()),
            RuntimeBinaryOperator.Less => RuntimeValue.From(left.AsNumber() < right.AsNumber()),
            RuntimeBinaryOperator.LessOrEqual => RuntimeValue.From(left.AsNumber() <= right.AsNumber()),
            RuntimeBinaryOperator.And => RuntimeValue.From(right.AsBoolean()),
            RuntimeBinaryOperator.Or => RuntimeValue.From(right.AsBoolean()),
            RuntimeBinaryOperator.Concatenate => RuntimeValue.From(left.AsString() + right.AsString()),
            RuntimeBinaryOperator.ConcatenateWithSpace => RuntimeValue.From(left.AsString() + " " + right.AsString()),
            _ => throw new InvalidOperationException($"Unsupported binary operator '{Operator}'.")
        };
    }

    private static int Power(int value, int exponent, RuntimeExecutionContext context)
    {
        if (exponent < 0) throw new ArgumentOutOfRangeException(nameof(exponent), "Exponent cannot be negative.");
        var result = 1;
        for (var i = 0; i < exponent; i++)
        {
            context.ConsumeInstruction();
            result = checked(result * value);
        }
        return result;
    }
}

public abstract record RuntimeStatement;
public sealed record DefineVariableStatement(string Name, RuntimeExpression Value) : RuntimeStatement;
public sealed record AssignVariableStatement(string Name, RuntimeExpression Value) : RuntimeStatement;
public sealed record EvaluateStatement(RuntimeExpression Expression) : RuntimeStatement;
public sealed record WhileStatement(RuntimeExpression Condition, IReadOnlyList<RuntimeStatement> Body) : RuntimeStatement;
public sealed record ForEachCardStatement(
    string VariableName,
    RuntimeExpression Collection,
    IReadOnlyList<RuntimeStatement> Body) : RuntimeStatement;

public sealed class RuntimeInterpreter
{
    public void Execute(IEnumerable<RuntimeStatement> statements, RuntimeExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(context);
        ExecuteBlock(statements, context);
    }

    private void ExecuteBlock(IEnumerable<RuntimeStatement> statements, RuntimeExecutionContext context)
    {
        foreach (var statement in statements)
        {
            context.ConsumeInstruction();
            switch (statement)
            {
                case DefineVariableStatement define:
                    context.Scope.Define(define.Name, define.Value.Evaluate(context));
                    break;
                case AssignVariableStatement assign:
                    context.Scope.Assign(assign.Name, assign.Value.Evaluate(context));
                    break;
                case EvaluateStatement evaluate:
                    _ = evaluate.Expression.Evaluate(context);
                    break;
                case WhileStatement loop:
                    ExecuteWhile(loop, context);
                    break;
                case ForEachCardStatement loop:
                    ExecuteForEach(loop, context);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported runtime statement '{statement.GetType().Name}'.");
            }
        }
    }

    private void ExecuteWhile(WhileStatement loop, RuntimeExecutionContext context)
    {
        while (loop.Condition.Evaluate(context).AsBoolean())
        {
            context.ConsumeInstruction();
            context.EnterScope();
            try { ExecuteBlock(loop.Body, context); }
            finally { context.ExitScope(); }
        }
    }

    private void ExecuteForEach(ForEachCardStatement loop, RuntimeExecutionContext context)
    {
        var cards = loop.Collection.Evaluate(context).AsCards().ToArray();
        foreach (var card in cards)
        {
            context.ConsumeInstruction();
            context.EnterScope();
            try
            {
                context.Scope.Define(loop.VariableName, RuntimeValue.From(card));
                ExecuteBlock(loop.Body, context);
            }
            finally { context.ExitScope(); }
        }
    }
}
