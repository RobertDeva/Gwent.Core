using Gwent.Compiler;
using Gwent.Engine;

const string sampleDsl = """
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
    Name: "banner",
    Params: { factor: Number, },
    Action: (targets, context) => {
        for unit in targets {
            unit.Power = unit.Power * factor;
        };
    }
}

card northern_leader {
    Name: "Northern Leader";
    Power: 0;
    Faction: Northern;
    Type: Leader;
    Rank: Special;
    Range: ["Melee"];
    description: "Leader of the Northern faction";
    OnActivation: [];
}

card nilfgaard_leader {
    Name: "Nilfgaardian Leader";
    Power: 0;
    Faction: Nilfgaard;
    Type: Leader;
    Rank: Special;
    Range: ["Melee"];
    description: "Leader of Nilfgaard";
    OnActivation: [];
}

card northern_vanguard {
    Name: "Northern Vanguard";
    Power: 4;
    Faction: Northern;
    Type: Unit;
    Rank: Silver;
    Range: ["Melee", "Range"];
    description: "A flexible front-line unit";
    OnActivation: [
        {
            Type: "rally",
            Params: { amount: 2, },
            Selector: { Source: "field"; Single: false; },
        },
    ];
}

card northern_archer {
    Name: "Northern Archer";
    Power: 3;
    Faction: Northern;
    Type: Unit;
    Rank: Silver;
    Range: ["Range"];
    description: "A ranged unit";
    OnActivation: [
        {
            Type: "banner",
            Params: { factor: 2, },
            Selector: { Source: "board"; Single: false; },
        },
    ];
}

card nilfgaard_scout {
    Name: "Nilfgaardian Scout";
    Power: 4;
    Faction: Nilfgaard;
    Type: Unit;
    Rank: Silver;
    Range: ["Melee", "Range"];
    description: "A flexible scout";
    OnActivation: [
        {
            Type: "rally",
            Params: { amount: 2, },
            Selector: { Source: "otherField"; Single: false; },
        },
    ];
}

card nilfgaard_mage {
    Name: "Nilfgaardian Mage";
    Power: 3;
    Faction: Nilfgaard;
    Type: Unit;
    Rank: Silver;
    Range: ["Range"];
    description: "A ranged mage";
    OnActivation: [
        {
            Type: "banner",
            Params: { factor: 2, },
            Selector: { Source: "field"; Single: false; },
        },
    ];
}
""";

var source = args.Length > 0 ? File.ReadAllText(args[0]) : sampleDsl;
var compilation = new DslCompiler().CompileRuntimeProgram(source);
if (!compilation.Success)
{
    foreach (var diagnostic in compilation.Diagnostics)
        Console.Error.WriteLine(diagnostic);
    return 1;
}

var program = compilation.Program!;
var first = new Player("Player 1");
var second = new Player("Player 2");
var game = new Game(first, second);

var firstEntries = Enumerable.Range(0, DeckDefinition.MinimumCardCount)
    .Select(index => index % 2 == 0
        ? new RuntimeDeckCardEntry("northern_vanguard",
            index % 4 == 0 ? Row.Melee : Row.Ranged)
        : new RuntimeDeckCardEntry("northern_archer", Row.Ranged));
var secondEntries = Enumerable.Range(0, DeckDefinition.MinimumCardCount)
    .Select(index => index % 2 == 0
        ? new RuntimeDeckCardEntry("nilfgaard_scout",
            index % 4 == 0 ? Row.Melee : Row.Ranged)
        : new RuntimeDeckCardEntry("nilfgaard_mage", Row.Ranged));

game.LoadDeck(first, program.CreateDeckDefinition(
    "Northern deck", "northern_leader", firstEntries));
game.LoadDeck(second, program.CreateDeckDefinition(
    "Nilfgaard deck", "nilfgaard_leader", secondEntries));
game.InitializeMatch(new Random(42));

Console.WriteLine($"Round {game.RoundNumber}");
foreach (var player in new[] { first, second })
{
    var card = player.Hand[^1];
    program.Play(game, player, card);
    Console.WriteLine($"{player.Id}: {card.Name} ({card.Row}, {card.Power} power)");
    game.EndTurn();
}

var firstRoundScore = first.Board.Score;
var secondRoundScore = second.Board.Score;
game.Pass(game.CurrentPlayer);
game.Pass(game.CurrentPlayer);

Console.WriteLine($"\n{first.Id}: {firstRoundScore}");
Console.WriteLine($"{second.Id}: {secondRoundScore}");
Console.WriteLine(game.LastRoundWinner is null
    ? "Round result: draw"
    : $"Round winner: {game.LastRoundWinner.Id}");
Console.WriteLine($"Rounds: {game.FirstRoundsWon} - {game.SecondRoundsWon}");
return 0;
