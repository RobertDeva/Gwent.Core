using Gwent.Compiler;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class RuntimeCardProgramCompilerTests
{
    private const string Effects = """
        effect {
            Name: "rally",
            Params: { amount: Number, },
            Action: (targets, context) => { }
        }
        """;

    [Fact]
    public void CompilesCardMetadataAndActivations()
    {
        var cards = """
            card vanguard {
                Name: "Vanguard";
                Power: 4 + 2;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee", "Range"];
                description: "Front line unit";
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
                ];
            }
            """;

        var result = new RuntimeCardProgramCompiler().Compile(Effects, cards);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var card = result.Program!.Cards["vanguard"];
        Assert.Equal("Vanguard", card.Name);
        Assert.Equal(["Melee", "Range"], card.Ranges);
        Assert.Single(card.Activations);
    }


    [Fact]
    public void RejectsDuplicateCardFieldsAndRanges()
    {
        var cards = """
            card unit {
                Name: "Unit";
                Name: "Renamed";
                Power: 1;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee", "Melee", "Range", "Ranged"];
                description: "Unit";
                OnActivation: [];
            }
            """;

        var result = new RuntimeCardProgramCompiler().Compile(Effects, cards);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("Card field 'Name'", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("declares range", StringComparison.Ordinal));
    }


    [Fact]
    public void RejectsBlankRequiredCardMetadataBeforeInstanceCreation()
    {
        var cards = """
            card unit {
                Name: "";
                Power: 1;
                Faction: "";
                Type: "";
                Rank: "";
                Range: ["Melee"];
                description: "";
                OnActivation: [];
            }
            """;

        var result = new RuntimeCardProgramCompiler().Compile(Effects, cards);

        Assert.False(result.Success);
        Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("non-empty Name", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("non-empty Faction", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("non-empty Type", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("non-empty Rank", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsCardActivationReferencingUnknownEffect()
    {
        var cards = """
            card unit {
                Name: "Unit";
                Power: 1;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee"];
                description: "Unit";
                OnActivation: [{ Type: "missing" }];
            }
            """;

        var result = new RuntimeCardProgramCompiler().Compile(Effects, cards);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("not declared", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsNonNumericCardPowerAndUnknownRanges()
    {
        var cards = """
            card unit {
                Name: "Unit";
                Power: "strong";
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Air"];
                description: "Unit";
                OnActivation: [];
            }
            """;

        var result = new RuntimeCardProgramCompiler().Compile(Effects, cards);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("expects 'Number'", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("unknown range", StringComparison.Ordinal));
    }
}
