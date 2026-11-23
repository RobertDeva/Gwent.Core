using Gwent.Engine;

namespace Gwent.Compiler;

public enum RuntimeValueKind
{
    Null,
    Boolean,
    Number,
    String,
    Card,
    CardCollection,
    Lambda,
    Player,
    Game,
    ExecutionContext
}

/// <summary>A closed, typed value representation used by the effect interpreter.</summary>
public sealed class RuntimeValue
{
    private RuntimeValue(RuntimeValueKind kind, object? value)
    {
        Kind = kind;
        Value = value;
    }

    public RuntimeValueKind Kind { get; }
    public object? Value { get; }

    public static RuntimeValue Null() => new(RuntimeValueKind.Null, null);
    public static RuntimeValue From(bool value) => new(RuntimeValueKind.Boolean, value);
    public static RuntimeValue From(int value) => new(RuntimeValueKind.Number, value);
    public static RuntimeValue From(string value) =>
        new(RuntimeValueKind.String, value ?? throw new ArgumentNullException(nameof(value)));
    public static RuntimeValue From(Card value) =>
        new(RuntimeValueKind.Card, value ?? throw new ArgumentNullException(nameof(value)));
    public static RuntimeValue FromCards(IEnumerable<Card> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new RuntimeValue(RuntimeValueKind.CardCollection,
            Array.AsReadOnly(value.ToArray()));
    }
    public static RuntimeValue From(RuntimeLambda value) =>
        new(RuntimeValueKind.Lambda, value ?? throw new ArgumentNullException(nameof(value)));
    public static RuntimeValue From(Player value) =>
        new(RuntimeValueKind.Player, value ?? throw new ArgumentNullException(nameof(value)));
    public static RuntimeValue From(Game value) =>
        new(RuntimeValueKind.Game, value ?? throw new ArgumentNullException(nameof(value)));
    public static RuntimeValue From(RuntimeExecutionContext value) =>
        new(RuntimeValueKind.ExecutionContext, value ?? throw new ArgumentNullException(nameof(value)));

    public bool AsBoolean() => Kind == RuntimeValueKind.Boolean
        ? (bool)Value!
        : throw TypeError(RuntimeValueKind.Boolean);

    public int AsNumber() => Kind == RuntimeValueKind.Number
        ? (int)Value!
        : throw TypeError(RuntimeValueKind.Number);

    public string AsString() => Kind == RuntimeValueKind.String
        ? (string)Value!
        : throw TypeError(RuntimeValueKind.String);

    public Card AsCard() => Kind == RuntimeValueKind.Card
        ? (Card)Value!
        : throw TypeError(RuntimeValueKind.Card);

    public IReadOnlyList<Card> AsCards() => Kind == RuntimeValueKind.CardCollection
        ? (IReadOnlyList<Card>)Value!
        : throw TypeError(RuntimeValueKind.CardCollection);

    public RuntimeLambda AsLambda() => Kind == RuntimeValueKind.Lambda
        ? (RuntimeLambda)Value!
        : throw TypeError(RuntimeValueKind.Lambda);
    public Player AsPlayer() => Kind == RuntimeValueKind.Player
        ? (Player)Value!
        : throw TypeError(RuntimeValueKind.Player);
    public Game AsGame() => Kind == RuntimeValueKind.Game
        ? (Game)Value!
        : throw TypeError(RuntimeValueKind.Game);
    public RuntimeExecutionContext AsContext() => Kind == RuntimeValueKind.ExecutionContext
        ? (RuntimeExecutionContext)Value!
        : throw TypeError(RuntimeValueKind.ExecutionContext);

    private InvalidOperationException TypeError(RuntimeValueKind expected) =>
        new($"Expected runtime value '{expected}', but received '{Kind}'.");
}

/// <summary>Lexical variable environment. Each execution owns its own scope chain.</summary>
public sealed class RuntimeScope
{
    private readonly Dictionary<string, RuntimeValue> _variables =
        new(StringComparer.Ordinal);

    public RuntimeScope(RuntimeScope? parent = null) => Parent = parent;

    public RuntimeScope? Parent { get; }

    public void Define(string name, RuntimeValue value)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(value);
        if (!_variables.TryAdd(name, value))
            throw new InvalidOperationException($"Variable '{name}' is already defined in this scope.");
    }

    public void Assign(string name, RuntimeValue value)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(value);

        if (_variables.ContainsKey(name))
        {
            _variables[name] = value;
            return;
        }

        if (Parent is not null)
        {
            Parent.Assign(name, value);
            return;
        }

        throw new KeyNotFoundException($"Variable '{name}' is not defined.");
    }

    public RuntimeValue Resolve(string name)
    {
        ValidateName(name);
        if (_variables.TryGetValue(name, out var value)) return value;
        if (Parent is not null) return Parent.Resolve(name);
        throw new KeyNotFoundException($"Variable '{name}' is not defined.");
    }

    public bool TryResolve(string name, out RuntimeValue? value)
    {
        ValidateName(name);
        if (_variables.TryGetValue(name, out value)) return true;
        if (Parent is not null) return Parent.TryResolve(name, out value);
        value = null;
        return false;
    }

    public RuntimeScope CreateChild() => new(this);

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variable name is required.", nameof(name));
    }
}

/// <summary>Per-invocation state; no mutable execution data is shared between effects.</summary>
public sealed class RuntimeExecutionContext
{
    private int _instructionsExecuted;

    public RuntimeExecutionContext(
        Player triggerPlayer,
        Card sourceCard,
        Game? game = null,
        int instructionLimit = 10_000)
    {
        TriggerPlayer = triggerPlayer ?? throw new ArgumentNullException(nameof(triggerPlayer));
        SourceCard = sourceCard ?? throw new ArgumentNullException(nameof(sourceCard));
        Game = game;
        if (game is not null && !ReferenceEquals(game.First, triggerPlayer) &&
            !ReferenceEquals(game.Second, triggerPlayer))
            throw new ArgumentException("Trigger player does not belong to the supplied game.", nameof(triggerPlayer));
        if (instructionLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(instructionLimit));

        InstructionLimit = instructionLimit;
        Scope = new RuntimeScope();
    }

    public Player TriggerPlayer { get; }
    public Card SourceCard { get; }
    public Game? Game { get; }
    public int InstructionLimit { get; }
    public int InstructionsExecuted => _instructionsExecuted;
    public RuntimeScope Scope { get; private set; }

    public RuntimeScope EnterScope()
    {
        Scope = Scope.CreateChild();
        return Scope;
    }

    public void ExitScope()
    {
        if (Scope.Parent is null)
            throw new InvalidOperationException("Cannot exit the root execution scope.");
        Scope = Scope.Parent;
    }

    public void ConsumeInstruction()
    {
        if (_instructionsExecuted >= InstructionLimit)
            throw new InvalidOperationException(
                $"Effect execution exceeded the instruction limit of {InstructionLimit}.");
        _instructionsExecuted++;
    }
}
