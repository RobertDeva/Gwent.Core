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
        var cards = Collection.Evaluate(context).AsCards().ToList();
        var values = Arguments.Select(argument => argument.Evaluate(context)).ToArray();

        switch (MethodName)
        {
            case "Find":
                RequireArgumentCount(values, 1);
                throw new InvalidOperationException("Find requires a selector predicate and is evaluated through RuntimeSelector.");
            case "Add":
                RequireArgumentCount(values, 1);
                cards.Add(values[0].AsCard());
                break;
            case "Push":
                RequireArgumentCount(values, 1);
                MoveToEnd(cards, values[0].AsCard());
                break;
            case "SendBottom":
                RequireArgumentCount(values, 1);
                MoveToBeginning(cards, values[0].AsCard());
                break;
            case "Remove":
                RequireArgumentCount(values, 1);
                cards.RemoveAll(card => card.InstanceId == values[0].AsCard().InstanceId);
                break;
            case "Pop":
                RequireArgumentCount(values, 0);
                if (cards.Count == 0) return RuntimeValue.Null();
                var last = cards[^1];
                return RuntimeValue.From(last);
            case "Count":
                RequireArgumentCount(values, 0);
                return RuntimeValue.From(cards.Count);
            default:
                throw new InvalidOperationException($"Unknown collection operation '{MethodName}'.");
        }

        return RuntimeValue.FromCards(cards);
    }

    private static void MoveToEnd(List<Gwent.Engine.Card> cards, Gwent.Engine.Card card)
    {
        cards.RemoveAll(existing => existing.InstanceId == card.InstanceId);
        cards.Add(card);
    }

    private static void MoveToBeginning(List<Gwent.Engine.Card> cards, Gwent.Engine.Card card)
    {
        cards.RemoveAll(existing => existing.InstanceId == card.InstanceId);
        cards.Insert(0, card);
    }

    private static void RequireArgumentCount(IReadOnlyCollection<RuntimeValue> values, int expected)
    {
        if (values.Count != expected)
            throw new ArgumentException($"Expected {expected} collection argument(s), received {values.Count}.");
    }
}
