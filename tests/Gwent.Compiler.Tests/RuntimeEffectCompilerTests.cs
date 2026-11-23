using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class RuntimeEffectCompilerTests
{
    [Fact]
    public void CompilesLegacyEffectDeclarationWithTypedParametersAndLoops()
    {
        var source = """
            effect {
                Name: "rally",
                Params: {
                    amount: Number,
                    enabled: Bool,
                },
                Action: (targets, context) => {
                    let counter = 0;
                    while (counter < amount) {
                        counter = counter + 1;
                    };
                    for unit in targets {
                        counter = counter + unit.Power;
                    };
                }
            }
            """;

        var result = new RuntimeEffectCompiler().Compile(source);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var effect = result.Catalog!.Resolve("rally");
        Assert.Equal(2, effect.Parameters.Count);
        Assert.Equal(3, effect.Action.Count);
        Assert.IsType<WhileStatement>(effect.Action[1]);
        Assert.IsType<ForEachCardStatement>(effect.Action[2]);
    }


    [Fact]
    public void RejectsFindPredicatesThatDoNotReturnBoolean()
    {
        var effect = new RuntimeEffectDefinition("find", [], [
            new EvaluateStatement(new CollectionMethodExpression(
                new VariableExpression("targets"),
                "Find",
                [new LambdaExpression("unit",
                    new CardPropertyExpression(new VariableExpression("unit"), CardProperty.Power))]))
        ]);

        var diagnostics = new RuntimeTypeAnalyzer().Analyze(new RuntimeEffectCatalog([effect]));

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Message.Contains("Find predicate", StringComparison.Ordinal) &&
            diagnostic.Message.Contains("Boolean", StringComparison.Ordinal));
    }

    [Fact]
    public void AcceptsBooleanFindPredicates()
    {
        var effect = new RuntimeEffectDefinition("find", [], [
            new EvaluateStatement(new CollectionMethodExpression(
                new VariableExpression("targets"),
                "Find",
                [new LambdaExpression("unit",
                    new BinaryExpression(
                        new CardPropertyExpression(new VariableExpression("unit"), CardProperty.Power),
                        RuntimeBinaryOperator.Greater,
                        new LiteralExpression(RuntimeValue.From(0))))]))
        ]);

        var diagnostics = new RuntimeTypeAnalyzer().Analyze(new RuntimeEffectCatalog([effect]));

        Assert.Empty(diagnostics);
    }


    [Fact]
    public void RejectsAssignmentsThatChangeVariableType()
    {
        var effect = new RuntimeEffectDefinition("assign", [], [
            new DefineVariableStatement("counter",
                new LiteralExpression(RuntimeValue.From(1))),
            new EvaluateStatement(new AssignmentExpression("counter",
                new LiteralExpression(RuntimeValue.From("one"))))
        ]);

        var diagnostics = new RuntimeTypeAnalyzer().Analyze(new RuntimeEffectCatalog([effect]));

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Message.Contains("Cannot assign", StringComparison.Ordinal) &&
            diagnostic.Message.Contains("counter", StringComparison.Ordinal));
    }



    [Fact]
    public void TypeChecksLegacyPlayerZoneHelperFunctions()
    {
        var source = """
            effect {
                Name: "zones",
                Action: (targets, context) => {
                    let hand = HandOfPlayer(context.TriggerPlayer);
                    let deck = DeckOfPlayer(context.OtherPlayer);
                    let field = FieldOfPlayer(context.TriggerPlayer);
                    let graveyard = GraveyardOfPlayer(context.OtherPlayer);
                }
            }
            """;

        var result = new RuntimeEffectCompiler().Compile(source);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
    }

    [Fact]
    public void RejectsLegacyPlayerZoneHelpersWithNonPlayerArguments()
    {
        var source = """
            effect {
                Name: "zones",
                Action: (targets, context) => {
                    let hand = HandOfPlayer("p1");
                }
            }
            """;

        var result = new RuntimeEffectCompiler().Compile(source);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("requires a Player argument", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsUnknownIdentifiersDuringEffectCompilation()
    {
        var source = """
            effect {
                Name: "broken",
                Action: (targets, context) => {
                    let amount = missing;
                }
            }
            """;

        var result = new RuntimeEffectCompiler().Compile(source);

        Assert.False(result.Success);
        Assert.Null(result.Catalog);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("Unknown identifier 'missing'", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsInvalidCardPowerAssignmentsDuringEffectCompilation()
    {
        var source = """
            effect {
                Name: "broken",
                Action: (targets, context) => {
                    for unit in targets {
                        unit.Power = "strong";
                    };
                }
            }
            """;

        var result = new RuntimeEffectCompiler().Compile(source);

        Assert.False(result.Success);
        Assert.Null(result.Catalog);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("numeric value", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsDuplicateEffectIdentifiers()
    {
        var source = """
            effect {
                Name: "rally",
                Action: (targets, context) => { }
            }
            effect {
                Name: "rally",
                Action: (targets, context) => { }
            }
            """;

        var result = new RuntimeEffectCompiler().Compile(source);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("declared more than once", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsUnknownParameterTypes()
    {
        var source = """
            effect {
                Name: "rally",
                Params: { amount: Card, },
                Action: (targets, context) => { }
            }
            """;

        var result = new RuntimeEffectCompiler().Compile(source);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("Unsupported parameter type", StringComparison.Ordinal));
    }
}
