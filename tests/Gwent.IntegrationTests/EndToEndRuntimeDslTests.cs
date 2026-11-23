using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class EndToEndRuntimeDslTests
{
    [Fact]
    public void CompilesAndExecutesCardActivationAgainstTheEngine()
    {
        var source = """
            effect {
                Name: "rally",
                Params: { amount: Number, },
                Action: (targets, context) => {
                    for unit in targets {
                        unit.Power = unit.Power + amount;
                    };
                }
            }

            card vanguard {
                Name: "Vanguard";
                Power: 4;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee"];
                description: "A vanguard";
                OnActivation: [
                    {
                        Type: "rally",
                        Params: { amount: 2, },
                        Selector: {
                            Source: "field";
                            Single: false;
                            Predicate: (unit) => unit.Power > 0;
                        },
                    },
                ];
            }
            """;

        var compiled = new DslCompiler().CompileRuntimeProgram(source);
        Assert.True(compiled.Success, string.Join(Environment.NewLine, compiled.Diagnostics));

        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var card = new Card("vanguard", "Vanguard", 4, Row.Melee);
        game.AddToDeck(player, card);
        game.Draw(player);
        game.Play(player, card);

        var context = new RuntimeExecutionContext(player, card, game);
        var invocation = compiled.Program!.Cards["vanguard"].Activations.Single();
        new RuntimeEffectExecutor(compiled.Program.Effects).Execute(invocation, context);

        Assert.Equal(6, card.Power);
        game.ValidateCardZones();
    }
}
