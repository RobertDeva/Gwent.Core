namespace Gwent.Compiler;

public enum RuntimeType
{
    Unknown,
    Null,
    Boolean,
    Number,
    String,
    Card,
    CardCollection,
    Lambda,
    Player,
    Game,
    Context
}

public sealed class RuntimeTypeAnalyzer
{
    private readonly List<Diagnostic> _diagnostics = [];

    public IReadOnlyList<Diagnostic> AnalyzeExpression(
        RuntimeExpression expression,
        RuntimeType expectedType,
        IReadOnlyDictionary<string, RuntimeType>? variables = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        _diagnostics.Clear();
        var actualType = Infer(expression,
            variables ?? new Dictionary<string, RuntimeType>(StringComparer.Ordinal), "card");
        if (actualType != expectedType && actualType != RuntimeType.Unknown)
            Add($"Expression expects '{expectedType}', but received '{actualType}'.");
        return _diagnostics.AsReadOnly();
    }

    public IReadOnlyList<Diagnostic> Analyze(RuntimeEffectCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _diagnostics.Clear();

        foreach (var effect in GetEffects(catalog))
        {
            var variables = new Dictionary<string, RuntimeType>(StringComparer.Ordinal)
            {
                ["targets"] = RuntimeType.CardCollection,
                ["context"] = RuntimeType.Context
            };
            foreach (var parameter in effect.Parameters)
                variables[parameter.Name] = ToRuntimeType(parameter.Type);

            AnalyzeStatements(effect.Action, variables, effect.Id);
        }
        return _diagnostics.AsReadOnly();
    }

    public IReadOnlyList<Diagnostic> AnalyzeActivations(
        RuntimeEffectCatalog catalog,
        IEnumerable<RuntimeEffectInvocation> activations)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(activations);
        _diagnostics.Clear();

        var callerVariables = new Dictionary<string, RuntimeType>(StringComparer.Ordinal)
        {
            ["context"] = RuntimeType.Context,
            ["card"] = RuntimeType.Card,
            ["targets"] = RuntimeType.CardCollection
        };
        foreach (var activation in activations)
            AnalyzeInvocation(catalog, activation, callerVariables, isPostAction: false);
        return _diagnostics.AsReadOnly();
    }

    private void AnalyzeInvocation(
        RuntimeEffectCatalog catalog,
        RuntimeEffectInvocation invocation,
        IReadOnlyDictionary<string, RuntimeType> callerVariables,
        bool isPostAction)
    {
        RuntimeEffectDefinition definition;
        try { definition = catalog.Resolve(invocation.EffectId); }
        catch (KeyNotFoundException exception)
        {
            Add(exception.Message);
            return;
        }

        var expected = definition.Parameters.ToDictionary(parameter => parameter.Name, StringComparer.Ordinal);
        foreach (var argument in invocation.Arguments)
        {
            if (!expected.TryGetValue(argument.Key, out var parameter))
            {
                Add($"Effect '{definition.Id}' does not declare parameter '{argument.Key}'.");
                continue;
            }

            var actualType = Infer(argument.Value, callerVariables, definition.Id);
            var expectedType = ToRuntimeType(parameter.Type);
            if (actualType != RuntimeType.Unknown && actualType != expectedType)
                Add($"Parameter '{parameter.Name}' of effect '{definition.Id}' expects '{expectedType}', but received '{actualType}'.");
        }

        foreach (var parameter in definition.Parameters)
            if (!invocation.Arguments.ContainsKey(parameter.Name))
                Add($"Effect '{definition.Id}' requires parameter '{parameter.Name}'.");

        ValidateSelector(invocation.Selector, callerVariables, definition.Id, isPostAction);
        if (invocation.PostAction is not null)
            AnalyzeInvocation(catalog, invocation.PostAction, callerVariables, isPostAction: true);
    }

    private void ValidateSelector(
        RuntimeSelector selector,
        IReadOnlyDictionary<string, RuntimeType> variables,
        string effectId,
        bool isPostAction)
    {
        var allowedSources = new HashSet<string>(
            ["board", "field", "otherField", "hand", "otherHand", "deck", "otherDeck", "parent"],
            StringComparer.Ordinal);
        if (!allowedSources.Contains(selector.Source))
            Add($"Effect '{effectId}' uses unknown selector source '{selector.Source}'.");
        if (selector.Source == "parent" && !isPostAction)
            Add($"Effect '{effectId}' can use source 'parent' only in a PostAction.");

        var singleType = Infer(selector.Single, variables, effectId);
        if (singleType != RuntimeType.Boolean && singleType != RuntimeType.Unknown)
            Add($"Selector Single in effect '{effectId}' must be Boolean.");

        if (selector.Predicate is null) return;
        if (selector.Predicate is not LambdaExpression lambda)
        {
            Add($"Selector Predicate in effect '{effectId}' must be a lambda expression.");
            return;
        }

        var predicateVariables = CopyVariables(variables);
        predicateVariables[lambda.ParameterName] = RuntimeType.Card;
        var predicateType = Infer(lambda.Body, predicateVariables, effectId);
        if (predicateType != RuntimeType.Boolean && predicateType != RuntimeType.Unknown)
            Add($"Selector Predicate in effect '{effectId}' must return Boolean.");
    }

    private void AnalyzeStatements(
        IEnumerable<RuntimeStatement> statements,
        Dictionary<string, RuntimeType> variables,
        string effectId)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case DefineVariableStatement define:
                    var definedType = Infer(define.Value, variables, effectId);
                    if (!variables.TryAdd(define.Name, definedType))
                        Add($"Variable '{define.Name}' is already defined in effect '{effectId}'.");
                    break;
                case AssignVariableStatement assign:
                    var assignedType = Infer(assign.Value, variables, effectId);
                    if (!variables.TryGetValue(assign.Name, out var existingType))
                        Add($"Variable '{assign.Name}' is not defined in effect '{effectId}'.");
                    else if (assignedType != RuntimeType.Unknown && existingType != RuntimeType.Unknown &&
                             assignedType != existingType)
                        Add($"Cannot assign '{assignedType}' to variable '{assign.Name}' of type '{existingType}'.");
                    break;
                case EvaluateStatement evaluate:
                    _ = Infer(evaluate.Expression, variables, effectId);
                    break;
                case WhileStatement loop:
                    var conditionType = Infer(loop.Condition, variables, effectId);
                    if (conditionType != RuntimeType.Boolean && conditionType != RuntimeType.Unknown)
                        Add($"While condition in effect '{effectId}' must be Boolean.");
                    AnalyzeStatements(loop.Body,
                        new Dictionary<string, RuntimeType>(variables, StringComparer.Ordinal), effectId);
                    break;
                case ForEachCardStatement loop:
                    var collectionType = Infer(loop.Collection, variables, effectId);
                    if (collectionType != RuntimeType.CardCollection && collectionType != RuntimeType.Unknown)
                        Add($"For loop in effect '{effectId}' requires a card collection.");
                    var loopVariables = new Dictionary<string, RuntimeType>(variables, StringComparer.Ordinal)
                    {
                        [loop.VariableName] = RuntimeType.Card
                    };
                    AnalyzeStatements(loop.Body, loopVariables, effectId);
                    break;
                default:
                    Add($"Unsupported statement '{statement.GetType().Name}' in effect '{effectId}'.");
                    break;
            }
        }
    }

    private RuntimeType Infer(
        RuntimeExpression expression,
        IReadOnlyDictionary<string, RuntimeType> variables,
        string effectId)
    {
        switch (expression)
        {
            case LiteralExpression literal:
                return literal.Literal.Kind switch
                {
                    RuntimeValueKind.Boolean => RuntimeType.Boolean,
                    RuntimeValueKind.Number => RuntimeType.Number,
                    RuntimeValueKind.String => RuntimeType.String,
                    RuntimeValueKind.Card => RuntimeType.Card,
                    RuntimeValueKind.CardCollection => RuntimeType.CardCollection,
                    RuntimeValueKind.Lambda => RuntimeType.Lambda,
                    RuntimeValueKind.Player => RuntimeType.Player,
                    RuntimeValueKind.Game => RuntimeType.Game,
                    RuntimeValueKind.ExecutionContext => RuntimeType.Context,
                    RuntimeValueKind.Null => RuntimeType.Null,
                    _ => RuntimeType.Unknown
                };
            case VariableExpression variable:
                if (variables.TryGetValue(variable.Name, out var type)) return type;
                Add($"Unknown identifier '{variable.Name}' in effect '{effectId}'.");
                return RuntimeType.Unknown;
            case CardPropertyExpression property:
                _ = Infer(property.Target, variables, effectId);
                return property.Property switch
                {
                    CardProperty.Power => RuntimeType.Number,
                    _ => RuntimeType.String
                };
            case RuntimeMemberExpression member:
                return InferMember(member, variables, effectId);
            case BinaryExpression binary:
                return InferBinary(binary, variables, effectId);
            case AssignmentExpression assignment:
                if (!variables.ContainsKey(assignment.Name))
                    Add($"Unknown identifier '{assignment.Name}' in effect '{effectId}'.");
                return Infer(assignment.ValueExpression, variables, effectId);
            case LambdaExpression lambda:
                var lambdaVariables = new Dictionary<string, RuntimeType>(variables, StringComparer.Ordinal)
                {
                    [lambda.ParameterName] = RuntimeType.Card
                };
                _ = Infer(lambda.Body, lambdaVariables, effectId);
                return RuntimeType.Lambda;
            case IndexExpression index:
                var collection = Infer(index.Collection, variables, effectId);
                var indexType = Infer(index.Index, variables, effectId);
                if (collection != RuntimeType.CardCollection && collection != RuntimeType.Unknown)
                    Add($"Indexer in effect '{effectId}' requires a card collection.");
                if (indexType != RuntimeType.Number && indexType != RuntimeType.Unknown)
                    Add($"Collection index in effect '{effectId}' must be numeric.");
                return RuntimeType.Card;
            case CollectionMethodExpression method:
                var receiver = Infer(method.Collection, variables, effectId);
                if (receiver != RuntimeType.CardCollection && receiver != RuntimeType.Unknown)
                    Add($"Collection operation '{method.MethodName}' in effect '{effectId}' requires a card collection.");
                foreach (var argument in method.Arguments)
                    _ = Infer(argument, variables, effectId);
                return method.MethodName switch
                {
                    "Find" or "Add" or "Push" or "SendBottom" or "Remove" => RuntimeType.CardCollection,
                    "Pop" => RuntimeType.Card,
                    "Count" => RuntimeType.Number,
                    _ => UnknownCollectionMethod(method.MethodName, effectId)
                };
            default:
                Add($"Unsupported expression '{expression.GetType().Name}' in effect '{effectId}'.");
                return RuntimeType.Unknown;
        }
    }

    private RuntimeType InferMember(
        RuntimeMemberExpression member,
        IReadOnlyDictionary<string, RuntimeType> variables,
        string effectId)
    {
        var targetType = Infer(member.Target, variables, effectId);
        var name = member.MemberName;
        return targetType switch
        {
            RuntimeType.Card => name switch
            {
                "Power" => RuntimeType.Number,
                "Name" or "Owner" or "OwnerId" or "Range" or "Row" or "DefinitionId" or "EffectId" => RuntimeType.String,
                _ => UnknownMember(name, effectId)
            },
            RuntimeType.Context => name switch
            {
                "TriggerPlayer" or "OtherPlayer" => RuntimeType.Player,
                "SourceCard" => RuntimeType.Card,
                "Game" => RuntimeType.Game,
                "Hand" or "Deck" or "Field" or "Graveyard" or "Board" or "OtherHand" or "OtherDeck" or "OtherField" => RuntimeType.CardCollection,
                _ => UnknownMember(name, effectId)
            },
            RuntimeType.Player => name switch
            {
                "Id" => RuntimeType.String,
                "Hand" or "Deck" or "Field" or "Graveyard" or "Board" => RuntimeType.CardCollection,
                _ => UnknownMember(name, effectId)
            },
            RuntimeType.Game => name switch
            {
                "First" or "Second" or "CurrentPlayer" or "Winner" => RuntimeType.Player,
                "RoundNumber" or "FirstRoundsWon" or "SecondRoundsWon" => RuntimeType.Number,
                "IsFinished" => RuntimeType.Boolean,
                _ => UnknownMember(name, effectId)
            },
            RuntimeType.Unknown => RuntimeType.Unknown,
            _ => UnknownMember(name, effectId)
        };
    }

    private RuntimeType InferBinary(
        BinaryExpression expression,
        IReadOnlyDictionary<string, RuntimeType> variables,
        string effectId)
    {
        var left = Infer(expression.Left, variables, effectId);
        var right = Infer(expression.Right, variables, effectId);
        switch (expression.Operator)
        {
            case RuntimeBinaryOperator.Add:
            case RuntimeBinaryOperator.Subtract:
            case RuntimeBinaryOperator.Multiply:
            case RuntimeBinaryOperator.Divide:
            case RuntimeBinaryOperator.Modulo:
            case RuntimeBinaryOperator.Power:
                RequireTypes(left, right, RuntimeType.Number, expression.Operator.ToString(), effectId);
                return RuntimeType.Number;
            case RuntimeBinaryOperator.Greater:
            case RuntimeBinaryOperator.GreaterOrEqual:
            case RuntimeBinaryOperator.Less:
            case RuntimeBinaryOperator.LessOrEqual:
                RequireTypes(left, right, RuntimeType.Number, expression.Operator.ToString(), effectId);
                return RuntimeType.Boolean;
            case RuntimeBinaryOperator.And:
            case RuntimeBinaryOperator.Or:
                RequireTypes(left, right, RuntimeType.Boolean, expression.Operator.ToString(), effectId);
                return RuntimeType.Boolean;
            case RuntimeBinaryOperator.Concatenate:
                RequireTypes(left, right, RuntimeType.String, expression.Operator.ToString(), effectId);
                return RuntimeType.String;
            case RuntimeBinaryOperator.Equal:
            case RuntimeBinaryOperator.NotEqual:
                if (left != RuntimeType.Unknown && right != RuntimeType.Unknown && left != right)
                    Add($"Equality operands in effect '{effectId}' have incompatible types '{left}' and '{right}'.");
                return RuntimeType.Boolean;
            default:
                return RuntimeType.Unknown;
        }
    }

    private void RequireTypes(RuntimeType left, RuntimeType right, RuntimeType expected, string operation, string effectId)
    {
        if ((left != expected && left != RuntimeType.Unknown) ||
            (right != expected && right != RuntimeType.Unknown))
            Add($"Operator '{operation}' in effect '{effectId}' requires '{expected}' operands.");
    }

    private RuntimeType UnknownCollectionMethod(string name, string effectId)
    {
        Add($"Unknown collection operation '{name}' in effect '{effectId}'.");
        return RuntimeType.Unknown;
    }

    private RuntimeType UnknownMember(string name, string effectId)
    {
        Add($"Unknown member '{name}' in effect '{effectId}'.");
        return RuntimeType.Unknown;
    }

    private static Dictionary<string, RuntimeType> CopyVariables(
        IReadOnlyDictionary<string, RuntimeType> variables) =>
        variables.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private void Add(string message) => _diagnostics.Add(new Diagnostic(1, message));

    private static RuntimeType ToRuntimeType(RuntimeParameterType type) => type switch
    {
        RuntimeParameterType.Boolean => RuntimeType.Boolean,
        RuntimeParameterType.Number => RuntimeType.Number,
        RuntimeParameterType.String => RuntimeType.String,
        _ => RuntimeType.Unknown
    };

    private static IEnumerable<RuntimeEffectDefinition> GetEffects(RuntimeEffectCatalog catalog)
    {
        // Catalog deliberately exposes only lookup; validation is performed by the compiler at registration.
        return catalog.Effects;
    }
}

