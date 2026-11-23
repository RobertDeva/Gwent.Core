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
    public void RuntimeCardProgramSnapshotsMutableDefinitionCollections()
    {
        var ranges = new List<string> { "Melee" };
        var activations = new List<RuntimeEffectInvocation>();
        var definition = new RuntimeCardDefinition(
            "unit", "Unit", new LiteralExpression(RuntimeValue.From(1)),
            "Northern", "Unit", "Silver", ranges, "Unit", activations);
        var program = new RuntimeCardProgram(
            new RuntimeEffectCatalog([]),
            new Dictionary<string, RuntimeCardDefinition> { ["unit"] = definition });

        ranges.Add("Range");
        activations.Add(new RuntimeEffectInvocation(
            "noop", new Dictionary<string, RuntimeExpression>(),
            new RuntimeSelector("field", new LiteralExpression(RuntimeValue.From(false)))));

        Assert.Equal(["Melee"], program.Cards["unit"].Ranges);
        Assert.Empty(program.Cards["unit"].Activations);
    }

    [Fact]
    public void RuntimeCardProgramRejectsMismatchedCatalogKeys()
    {
        var definition = new RuntimeCardDefinition(
            "unit", "Unit", new LiteralExpression(RuntimeValue.From(1)),
            "Northern", "Unit", "Silver", ["Melee"], "Unit", []);
        var effects = new RuntimeEffectCatalog([]);

        Assert.Throws<ArgumentException>(() =>
            new RuntimeCardProgram(effects,
                new Dictionary<string, RuntimeCardDefinition> { ["other"] = definition }));
    }

    [Fact]
    public void ActivationRequiresTheOwnedCardToBeOnTheField()
    {
        var player = new Gwent.Engine.Player("p1");
        var game = new Gwent.Engine.Game(player, new Gwent.Engine.Player("p2"));
        var card = new Gwent.Engine.Card("unit", "Unit", 1, Gwent.Engine.Row.Melee);
        game.AddToDeck(player, card);
        var invocation = new RuntimeEffectInvocation(
            "noop", new Dictionary<string, RuntimeExpression>(),
            new RuntimeSelector("field", new LiteralExpression(RuntimeValue.From(false))));
        var definition = new RuntimeCardDefinition(
            "unit", "Unit", new LiteralExpression(RuntimeValue.From(1)),
            "Northern", "Unit", "Silver", ["Melee"], "Unit", [invocation]);
        var program = new RuntimeCardProgram(
            new RuntimeEffectCatalog([new RuntimeEffectDefinition("noop", [], [])]),
            new Dictionary<string, RuntimeCardDefinition> { ["unit"] = definition });
        var context = new RuntimeExecutionContext(player, card, game);

        Assert.Throws<InvalidOperationException>(() => program.Activate(card, 0, context));
    }


    [Fact]
    public void ActivationRejectsCardsFromAnotherGame()
    {
        var firstPlayer = new Gwent.Engine.Player("p1");
        var firstGame = new Gwent.Engine.Game(firstPlayer, new Gwent.Engine.Player("p2"));
        var foreignPlayer = new Gwent.Engine.Player("p1");
        var foreignGame = new Gwent.Engine.Game(foreignPlayer, new Gwent.Engine.Player("p2"));
        var card = new Gwent.Engine.Card("unit", "Unit", 1, Gwent.Engine.Row.Melee);
        foreignGame.AddToDeck(foreignPlayer, card);
        foreignGame.MoveCard(card, foreignPlayer, Gwent.Engine.CardZone.Field);
        var invocation = new RuntimeEffectInvocation(
            "noop", new Dictionary<string, RuntimeExpression>(),
            new RuntimeSelector("field", new LiteralExpression(RuntimeValue.From(false))));
        var definition = new RuntimeCardDefinition(
            "unit", "Unit", new LiteralExpression(RuntimeValue.From(1)),
            "Northern", "Unit", "Silver", ["Melee"], "Unit", [invocation]);
        var program = new RuntimeCardProgram(
            new RuntimeEffectCatalog([new RuntimeEffectDefinition("noop", [], [])]),
            new Dictionary<string, RuntimeCardDefinition> { ["unit"] = definition });
        var context = new RuntimeExecutionContext(firstPlayer, card, firstGame);

        Assert.Throws<ArgumentException>(() => program.Activate(card, 0, context));
    }

    [Fact]
    public void ActivationRejectsATriggerPlayerWhoDoesNotOwnTheCard()
    {
        var first = new Gwent.Engine.Player("p1");
        var second = new Gwent.Engine.Player("p2");
        var game = new Gwent.Engine.Game(first, second);
        var card = new Gwent.Engine.Card("unit", "Unit", 1, Gwent.Engine.Row.Melee);
        game.AddToDeck(second, card);
        game.MoveCard(card, second, Gwent.Engine.CardZone.Field);
        var invocation = new RuntimeEffectInvocation(
            "noop", new Dictionary<string, RuntimeExpression>(),
            new RuntimeSelector("field", new LiteralExpression(RuntimeValue.From(false))));
        var definition = new RuntimeCardDefinition(
            "unit", "Unit", new LiteralExpression(RuntimeValue.From(1)),
            "Northern", "Unit", "Silver", ["Melee"], "Unit", [invocation]);
        var program = new RuntimeCardProgram(
            new RuntimeEffectCatalog([new RuntimeEffectDefinition("noop", [], [])]),
            new Dictionary<string, RuntimeCardDefinition> { ["unit"] = definition });
        var context = new RuntimeExecutionContext(first, card, game);

        Assert.Throws<ArgumentException>(() => program.Activate(card, 0, context));
    }


    [Fact]
    public void SameCompiledDefinitionCreatesDistinctPhysicalCardInstances()
    {
        var cards = """
            card unit {
                Name: "Unit";
                Power: 4;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee", "Range"];
                description: "Unit";
                OnActivation: [];
            }
            """;
        var result = new RuntimeCardProgramCompiler().Compile(Effects, cards);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));

        var first = result.Program!.CreateCardInstance("unit", Gwent.Engine.Row.Melee);
        var second = result.Program.CreateCardInstance("unit", Gwent.Engine.Row.Ranged);

        Assert.Equal(first.DefinitionId, second.DefinitionId);
        Assert.NotEqual(first.InstanceId, second.InstanceId);
        Assert.NotSame(first, second);
        Assert.Equal(Gwent.Engine.Row.Melee, first.Row);
        Assert.Equal(Gwent.Engine.Row.Ranged, second.Row);
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
