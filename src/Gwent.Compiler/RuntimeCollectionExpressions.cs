namespace Gwent.Compiler;

public sealed record IndexExpression(RuntimeExpression Collection, RuntimeExpression Index)
    : RuntimeExpression
{
    public override RuntimeValue Evaluate(RuntimeExecutionContext context)
    {
        context.ConsumeInstruction();
        var collection = Collection.Evaluate(context).AsCards();
        var index = Index.Evaluate(context).AsNumber();
        if (index < 0 || index >= collection.Count)
            throw new ArgumentOutOfRangeException(nameof(Index), index, "Card collection index is out of range.");
        return RuntimeValue.From(collection[index]);
    }
}

public sealed record CollectionMethodExpression(
    RuntimeExpression Collection,
    string MethodName,
    IReadOnlyList<RuntimeExpression> Arguments) : RuntimeExpression
{
    public override RuntimeValue Evaluate(RuntimeExecutionContext context)
    {
        context.ConsumeInstruction();
        var collection = Collection.Evaluate(context).AsCardCollection();

        switch (MethodName)
        {
            case "Find":
                RequireArgumentCount(1);
                var predicate = Arguments[0].Evaluate(context).AsLambda();
                return RuntimeValue.FromCards(collection.Cards.Where(card =>
                    predicate.Invoke(context, RuntimeValue.From(card)).AsBoolean()));
            case "Add":
                RequireArgumentCount(1);
                collection.Add(Arguments[0].Evaluate(context).AsCard());
                break;
            case "Push":
                RequireArgumentCount(1);
                collection.Push(Arguments[0].Evaluate(context).AsCard());
                break;
            case "SendBottom":
                RequireArgumentCount(1);
                collection.SendBottom(Arguments[0].Evaluate(context).AsCard());
                break;
            case "Remove":
                RequireArgumentCount(1);
                collection.Remove(Arguments[0].Evaluate(context).AsCard());
                break;
            case "Pop":
                RequireArgumentCount(0);
                var popped = collection.Pop();
                return popped is null ? RuntimeValue.Null() : RuntimeValue.From(popped);
            case "Shuffle":
                RequireArgumentCount(0);
                collection.Shuffle(context.Random);
                break;
            case "Count":
                RequireArgumentCount(0);
                return RuntimeValue.From(collection.Count);
            default:
                throw new InvalidOperationException($"Unknown collection operation '{MethodName}'.");
        }

        return RuntimeValue.FromCardCollection(collection);
    }

    private void RequireArgumentCount(int expected)
    {
        if (Arguments.Count != expected)
            throw new ArgumentException($"Expected {expected} collection argument(s), received {Arguments.Count}.");
    }
}
