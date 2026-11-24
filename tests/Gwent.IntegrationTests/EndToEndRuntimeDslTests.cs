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



    [Fact]
    public void DslEffectCanDestroyAnOpponentUnitIntoItsOwnersGraveyard()
    {
        var source = """
            effect {
                Name: "slay",
                Params: { },
                Action: (targets, context) => {
                    for unit in targets {
                        unit.Owner.Graveyard.Add(unit);
                    };
                }
            }

            card filler {
                Name: "Filler";
                Power: 1;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee"];
                description: "Filler";
                OnActivation: [];
            }

            card slayer {
                Name: "Slayer";
                Power: 2;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee"];
                description: "Slayer";
                OnActivation: [
                    {
                        Type: "slay",
                        Params: { },
                        Selector: { Source: "otherField"; Single: false; },
                    },
                ];
            }
            """;
        var compiled = new DslCompiler().CompileRuntimeProgram(source);
        Assert.True(compiled.Success, string.Join(Environment.NewLine, compiled.Diagnostics));

        var first = new Player("first");
        var second = new Player("second");
        var game = new Game(first, second);
        var filler = compiled.Program!.CreateCardInstance("filler", Row.Melee);
        var slayer = compiled.Program.CreateCardInstance("slayer", Row.Melee);
        var enemy = new Card("enemy", "Enemy", 5, Row.Melee);
        game.AddToDeck(first, filler);
        game.AddToDeck(first, slayer);
        game.AddToDeck(second, enemy);

        game.Draw(first);
        game.Draw(first);
        game.Play(first, filler);
        game.EndTurn();
        game.Draw(second);
        game.Play(second, enemy);
        game.EndTurn();
        compiled.Program.Play(game, first, slayer);

        Assert.Equal(CardZone.Graveyard, enemy.Zone);
        Assert.Contains(enemy, second.Graveyard);
        Assert.DoesNotContain(enemy, first.Graveyard);
        game.ValidateCardZones();
    }



    [Theory]
    [InlineData("Weather")]
    [InlineData("Upgrade")]
    public void SpecialZoneCardsCanActivateCompiledDslEffects(string cardType)
    {
        var source = $$"""
            effect {
                Name: "boost",
                Params: { amount: Number, },
                Action: (targets, context) => {
                    for unit in targets {
                        unit.Power = unit.Power + amount;
                    };
                }
            }

            card special {
                Name: "Special Source";
                Power: 0;
                Faction: Neutral;
                Type: {{cardType}};
                Rank: Special;
                Range: ["Melee"];
                description: "Special source";
                OnActivation: [
                    {
                        Type: "boost",
                        Params: { amount: 2, },
                        Selector: { Source: "field"; Single: false; },
                    },
                ];
            }

            card unit {
                Name: "Unit";
                Power: 3;
                Faction: Northern;
                Type: Unit;
                Rank: Silver;
                Range: ["Melee"];
                description: "Unit";
                OnActivation: [];
            }
            """;
        var compiled = new DslCompiler().CompileRuntimeProgram(source);
        Assert.True(compiled.Success, string.Join(Environment.NewLine, compiled.Diagnostics));

        var player = new Player("first");
        var game = new Game(player, new Player("second"));
        var special = compiled.Program!.CreateCardInstance("special", Row.Melee);
        var unit = compiled.Program.CreateCardInstance("unit", Row.Melee);
        game.AddToDeck(player, special);
        game.AddToDeck(player, unit);
        game.Draw(player);
        game.Draw(player);
        game.MoveCard(unit, player, CardZone.Field);

        compiled.Program.Play(game, player, special);

        Assert.Equal(5, unit.Power);
        if (cardType == "Weather")
        {
            Assert.Contains(special, player.Weather);
            Assert.Empty(player.Upgrades);
        }
        else
        {
            Assert.Contains(special, player.Upgrades);
            Assert.Empty(player.Weather);
        }
        game.ValidateCardZones();
    }


}
