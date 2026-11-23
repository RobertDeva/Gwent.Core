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
