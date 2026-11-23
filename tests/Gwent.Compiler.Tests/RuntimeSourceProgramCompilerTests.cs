using Gwent.Compiler;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class RuntimeSourceProgramCompilerTests
{
    [Fact]
    public void CompilesEffectsAndCardsFromOneSourceFile()
    {
        var source = """
            effect {
                Name: "rally",
                Params: { amount: Number, },
                Action: (targets, context) => {
                    for unit in targets {
                        let score = unit.Power + amount;
                    };
                }
            }

            card vanguard {
                Name: "Vanguard";
                Power: 4;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee", "Range"];
                description: "A vanguard";
                OnActivation: [
                    { Type: "rally", Params: { amount: 2, } }
                ];
            }
            """;

        var result = new RuntimeSourceProgramCompiler().Compile(source);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.True(result.Program!.Cards.ContainsKey("vanguard"));
        Assert.Single(result.Program.Effects.Effects);
    }

    [Fact]
    public void ReportsUnknownTopLevelDeclarations()
    {
        var result = new RuntimeSourceProgramCompiler().Compile("leader { Name: \"unknown\" }");

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("Unexpected top-level declaration", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsUnterminatedTopLevelDeclarations()
    {
        var result = new RuntimeSourceProgramCompiler().Compile(
            "effect { Name: \"rally\", Action: (targets, context) => { }");

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("Unterminated", StringComparison.Ordinal));
    }
}
