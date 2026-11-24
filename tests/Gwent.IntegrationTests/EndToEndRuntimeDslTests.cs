using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class EndToEndRuntimeDslTests
{

    [Fact]
    public void PlayingCompiledCardExecutesEveryActivationInOrder()
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
                    { Type: "rally", Params: { amount: 2, }, Selector: { Source: "field"; Single: true; } },
                    { Type: "rally", Params: { amount: 3, }, Selector: { Source: "field"; Single: true; } },
                ];
            }
            """;

        var compiled = new DslCompiler().CompileRuntimeProgram(source);
        Assert.True(compiled.Success, string.Join(Environment.NewLine, compiled.Diagnostics));

        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var card = compiled.Program!.CreateCardInstance("vanguard", Row.Melee);
        game.AddToDeck(player, card);
        game.Draw(player);

        var results = compiled.Program.Play(game, player, card);

        Assert.Equal(2, results.Count);
        Assert.Equal(9, card.Power);
        game.ValidateCardZones();
    }

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
        var card = compiled.Program!.CreateCardInstance("vanguard", Row.Melee);
        game.AddToDeck(player, card);
        game.Draw(player);
        compiled.Program.Play(game, player, card);

        Assert.Equal(6, card.Power);
        game.ValidateCardZones();
    }

    [Fact]
    public void CompiledPostActionExecutesAgainstExactlyTheParentTargets()
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

            effect {
                Name: "follow-up",
                Params: { amount: Number, },
                Action: (targets, context) => {
                    for unit in targets {
                        unit.Power = unit.Power + amount;
                    };
                }
            }

            card ally {
                Name: "Ally";
                Power: 2;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee"];
                description: "An ally";
            }

            card commander {
                Name: "Commander";
                Power: 1;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee"];
                description: "A commander";
                OnActivation: [
                    {
                        Type: "rally",
                        Params: { amount: 1, },
                        Selector: {
                            Source: "field";
                            Single: false;
                            Predicate: (unit) => unit.Power == 2;
                        },
                        PostAction: {
                            Type: "follow-up",
                            Params: { amount: 2, },
                        },
                    },
                ];
            }
            """;

        var compiled = new DslCompiler().CompileRuntimeProgram(source);
        Assert.True(compiled.Success, string.Join(Environment.NewLine, compiled.Diagnostics));

        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var ally = compiled.Program!.CreateCardInstance("ally", Row.Melee);
        var commander = compiled.Program.CreateCardInstance("commander", Row.Melee);
        game.AddToDeck(player, ally);
        game.AddToDeck(player, commander);
        game.MoveCard(ally, player, CardZone.Field);
        game.Draw(player);

        var result = compiled.Program.Play(game, player, commander);

        Assert.Single(result);
        Assert.Equal(5, ally.Power);
        Assert.Equal(1, commander.Power);
        game.ValidateCardZones();
    }


    [Fact]
    public void NestedPostActionsInheritAncestorParametersAndParentTargets()
    {
        var source = """
            effect {
                Name: "first",
                Params: { rootAmount: Number, },
                Action: (targets, context) => {
                    for unit in targets { unit.Power = unit.Power + rootAmount; };
                }
            }

            effect {
                Name: "second",
                Params: { childAmount: Number, },
                Action: (targets, context) => {
                    for unit in targets { unit.Power = unit.Power + childAmount; };
                }
            }

            effect {
                Name: "third",
                Params: { grandAmount: Number, },
                Action: (targets, context) => {
                    for unit in targets { unit.Power = unit.Power + grandAmount; };
                }
            }

            effect {
                Name: "fourth",
                Params: { finalAmount: Number, },
                Action: (targets, context) => {
                    for unit in targets { unit.Power = unit.Power + finalAmount; };
                }
            }

            card ally {
                Name: "Ally";
                Power: 2;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee"];
                description: "An ally";
            }

            card commander {
                Name: "Commander";
                Power: 1;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee"];
                description: "A commander";
                OnActivation: [
                    {
                        Type: "first",
                        Params: { rootAmount: 1, },
                        Selector: {
                            Source: "field";
                            Single: false;
                            Predicate: (unit) => unit.Power == 2;
                        },
                        PostAction: {
                            Type: "second",
                            Params: { childAmount: rootAmount + 1, },
                            PostAction: {
                                Type: "third",
                                Params: { grandAmount: childAmount + 1, },
                                PostAction: {
                                    Type: "fourth",
                                    Params: { finalAmount: grandAmount + 1, },
                                },
                            },
                        },
                    },
                ];
            }
            """;

        var compiled = new DslCompiler().CompileRuntimeProgram(source);
        Assert.True(compiled.Success, string.Join(Environment.NewLine, compiled.Diagnostics));

        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var ally = compiled.Program!.CreateCardInstance("ally", Row.Melee);
        var commander = compiled.Program.CreateCardInstance("commander", Row.Melee);
        game.AddToDeck(player, ally);
        game.AddToDeck(player, commander);
        game.MoveCard(ally, player, CardZone.Field);
        game.Draw(player);

        compiled.Program.Play(game, player, commander);

        Assert.Equal(12, ally.Power);
        Assert.Equal(1, commander.Power);
        game.ValidateCardZones();
    }



    [Fact]
    public void PostActionCanReadTopLevelVariablesDeclaredByParentAction()
    {
        var source = """
            effect {
                Name: "prepare",
                Params: { baseAmount: Number, },
                Action: (targets, context) => {
                    let bonus = baseAmount + 2;
                }
            }

            effect {
                Name: "follow-up",
                Params: { amount: Number, },
                Action: (targets, context) => {
                    for unit in targets { unit.Power = unit.Power + amount; };
                }
            }

            card ally {
                Name: "Ally";
                Power: 2;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee"];
                description: "An ally";
            }

            card commander {
                Name: "Commander";
                Power: 1;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee"];
                description: "A commander";
                OnActivation: [
                    {
                        Type: "prepare",
                        Params: { baseAmount: 1, },
                        Selector: { Source: "field"; Single: false; },
                        PostAction: {
                            Type: "follow-up",
                            Params: { amount: bonus + 1, },
                        },
                    },
                ];
            }
            """;

        var compiled = new DslCompiler().CompileRuntimeProgram(source);
        Assert.True(compiled.Success, string.Join(Environment.NewLine, compiled.Diagnostics));

        var player = new Player("p1");
        var game = new Game(player, new Player("p2"));
        var ally = compiled.Program!.CreateCardInstance("ally", Row.Melee);
        var commander = compiled.Program.CreateCardInstance("commander", Row.Melee);
        game.AddToDeck(player, ally);
        game.AddToDeck(player, commander);
        game.MoveCard(ally, player, CardZone.Field);
        game.Draw(player);

        compiled.Program.Play(game, player, commander);

        Assert.Equal(6, ally.Power);
        game.ValidateCardZones();
    }

    [Fact]
    public void PostActionCannotReadVariablesDeclaredInsideParentLoop()
    {
        var source = """
            effect {
                Name: "prepare",
                Params: { },
                Action: (targets, context) => {
                    for unit in targets {
                        let loopBonus = 2;
                    };
                }
            }

            effect {
                Name: "follow-up",
                Params: { amount: Number, },
                Action: (targets, context) => { }
            }

            card commander {
                Name: "Commander";
                Power: 1;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee"];
                description: "A commander";
                OnActivation: [
                    {
                        Type: "prepare",
                        Params: { },
                        Selector: { Source: "field"; Single: false; },
                        PostAction: {
                            Type: "follow-up",
                            Params: { amount: loopBonus, },
                        },
                    },
                ];
            }
            """;

        var compiled = new DslCompiler().CompileRuntimeProgram(source);

        Assert.False(compiled.Success);
        Assert.Contains(compiled.Diagnostics,
            diagnostic => diagnostic.Message.Contains("Unknown identifier 'loopBonus'", StringComparison.Ordinal));
    }


}
