using Gwent.Compiler;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class RuntimeEffectProgramCompilerTests
{
    [Fact]
    public void CompilesEffectDefinitionsAndCardActivationsTogether()
    {
        var effects = """
            effect {
                Name: "rally",
                Params: { amount: Number, },
                Action: (targets, context) => {
                    for unit in targets {
                        let result = unit.Power + amount;
                    };
                }
            }
            """;
        var activations = """
            OnActivation: [
                {
                    Type: "rally",
                    Params: { amount: 3, },
                    Selector: {
                        Source: "board";
                        Single: false;
                        Predicate: (unit) => unit.Power > 1;
                    },
                },
            ]
            """;

        var result = new RuntimeProgramCompiler().Compile(effects, activations);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal("rally", result.Program!.Activations.Single().EffectId);
    }

    [Fact]
    public void RejectsActivationWithWrongParameterTypeBeforeRuntime()
    {
        var effects = """
            effect {
                Name: "rally",
                Params: { amount: Number, },
                Action: (targets, context) => { }
            }
            """;
        var activations = """
            OnActivation: [
                { Type: "rally", Params: { amount: "three", } }
            ]
            """;

        var result = new RuntimeProgramCompiler().Compile(effects, activations);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("expects 'Number'", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsUnknownVariablesAndNonBooleanPredicates()
    {
        var effects = """
            effect {
                Name: "rally",
                Action: (targets, context) => {
                    let result = missingValue + 1;
                }
            }
            """;
        var activations = """
            OnActivation: [
                { Type: "rally", Selector: {
                    Source: "board";
                    Single: false;
                    Predicate: (unit) => unit.Power + 1;
                } }
            ]
            """;

        var result = new RuntimeProgramCompiler().Compile(effects, activations);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("Unknown identifier 'missingValue'", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("must return Boolean", StringComparison.Ordinal));
    }
}
