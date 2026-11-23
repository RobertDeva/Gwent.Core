using System.Collections.ObjectModel;

namespace Gwent.Compiler;

public enum RuntimeParameterType { Boolean, Number, String }

public sealed record RuntimeParameterDefinition(string Name, RuntimeParameterType Type);

public sealed record RuntimeEffectInvocation(
    string EffectId,
    IReadOnlyDictionary<string, RuntimeExpression> Arguments,
    RuntimeSelector Selector,
    RuntimeEffectInvocation? PostAction = null);

public sealed class RuntimeEffectDefinition
{
    public RuntimeEffectDefinition(
        string id,
        IEnumerable<RuntimeParameterDefinition> parameters,
        IEnumerable<RuntimeStatement> action)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Effect id is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(action);

        Id = id;
        Parameters = Array.AsReadOnly(parameters.ToArray());
        Action = Array.AsReadOnly(action.ToArray());

        var duplicate = Parameters.GroupBy(parameter => parameter.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Parameter '{duplicate.Key}' is declared more than once.", nameof(parameters));
        if (Parameters.Any(parameter => string.IsNullOrWhiteSpace(parameter.Name)))
            throw new ArgumentException("Parameter names cannot be empty.", nameof(parameters));
        if (Parameters.Any(parameter => parameter.Name is "targets" or "context"))
            throw new ArgumentException("Parameter names 'targets' and 'context' are reserved.", nameof(parameters));
    }

    public string Id { get; }
    public IReadOnlyList<RuntimeParameterDefinition> Parameters { get; }
    public IReadOnlyList<RuntimeStatement> Action { get; }
}

public sealed class RuntimeEffectCatalog
{
    private readonly IReadOnlyDictionary<string, RuntimeEffectDefinition> _effects;

    public RuntimeEffectCatalog(IEnumerable<RuntimeEffectDefinition> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        var catalog = new Dictionary<string, RuntimeEffectDefinition>(StringComparer.Ordinal);
        foreach (var effect in effects)
        {
            ArgumentNullException.ThrowIfNull(effect);
            if (!catalog.TryAdd(effect.Id, effect))
                throw new ArgumentException($"Effect '{effect.Id}' is declared more than once.", nameof(effects));
        }
        _effects = new ReadOnlyDictionary<string, RuntimeEffectDefinition>(catalog);
    }

    public RuntimeEffectDefinition Resolve(string id) =>
        _effects.TryGetValue(id, out var effect)
            ? effect
            : throw new KeyNotFoundException($"Effect '{id}' is not declared.");
}

public sealed class RuntimeEffectExecutor
{
    private readonly RuntimeEffectCatalog _catalog;
    private readonly RuntimeInterpreter _interpreter;

    public RuntimeEffectExecutor(RuntimeEffectCatalog catalog, RuntimeInterpreter? interpreter = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _interpreter = interpreter ?? new RuntimeInterpreter();
    }

    public IReadOnlyList<Gwent.Engine.Card> Execute(
        RuntimeEffectInvocation invocation,
        RuntimeExecutionContext context,
        IReadOnlyList<Gwent.Engine.Card>? parentTargets = null)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(invocation.Selector);
        context.ConsumeInstruction();

        var definition = _catalog.Resolve(invocation.EffectId);
        var values = BindArguments(definition, invocation.Arguments, context);
        var targets = invocation.Selector.Select(context, parentTargets);

        context.EnterScope();
        try
        {
            foreach (var parameter in values)
                context.Scope.Define(parameter.Key, parameter.Value);
            context.Scope.Define("targets", RuntimeValue.FromCards(targets));
            context.Scope.Define("context", RuntimeValue.From(context));
            _interpreter.Execute(definition.Action, context);

            if (invocation.PostAction is not null)
                Execute(invocation.PostAction, context, targets);
        }
        finally { context.ExitScope(); }

        return targets;
    }

    private static IReadOnlyDictionary<string, RuntimeValue> BindArguments(
        RuntimeEffectDefinition definition,
        IReadOnlyDictionary<string, RuntimeExpression> arguments,
        RuntimeExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var expected = definition.Parameters.ToDictionary(parameter => parameter.Name, StringComparer.Ordinal);

        foreach (var supplied in arguments.Keys)
            if (!expected.ContainsKey(supplied))
                throw new ArgumentException(
                    $"Effect '{definition.Id}' does not declare parameter '{supplied}'.", nameof(arguments));

        var values = new Dictionary<string, RuntimeValue>(StringComparer.Ordinal);
        foreach (var parameter in definition.Parameters)
        {
            if (!arguments.TryGetValue(parameter.Name, out var expression))
                throw new ArgumentException(
                    $"Effect '{definition.Id}' requires parameter '{parameter.Name}'.", nameof(arguments));

            var value = expression.Evaluate(context);
            var valid = parameter.Type switch
            {
                RuntimeParameterType.Boolean => value.Kind == RuntimeValueKind.Boolean,
                RuntimeParameterType.Number => value.Kind == RuntimeValueKind.Number,
                RuntimeParameterType.String => value.Kind == RuntimeValueKind.String,
                _ => false
            };
            if (!valid)
                throw new ArgumentException(
                    $"Parameter '{parameter.Name}' of effect '{definition.Id}' expects '{parameter.Type}', but received '{value.Kind}'.",
                    nameof(arguments));
            values.Add(parameter.Name, value);
        }
        return values;
    }
}
