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

var interactive = args.Contains("--interactive", StringComparer.Ordinal);
var sourcePath = args.FirstOrDefault(argument =>
    !StringComparer.Ordinal.Equals(argument, "--interactive"));
var source = sourcePath is null ? sampleDsl : File.ReadAllText(sourcePath);
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

if (interactive)
{
    foreach (var player in new[] { first, second })
    {
        Console.WriteLine($"{player.Id}: mulligan up to two hand indexes (space/comma separated), or Enter to keep.");
        var selection = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(selection)) continue;

        var tokens = selection.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 2 ||
            tokens.Any(token => !int.TryParse(token, out var index) ||
                                index < 0 || index >= player.Hand.Count))
        {
            Console.WriteLine("Invalid mulligan selection; keeping the current hand.");
            continue;
        }

        var indexes = tokens.Select(token => int.Parse(token)).ToArray();
        try
        {
            game.Mulligan(player, indexes.Select(index => player.Hand[index]));
        }
        catch (ArgumentException exception)
        {
            Console.WriteLine(exception.Message);
        }
    }

    Console.WriteLine("Commands: hand, board, status, draw, play <index>, end, pass, help, quit");
    while (!game.IsFinished)
    {
        var player = game.CurrentPlayer;
        Console.Write($"{player.Id}> ");
        var input = Console.ReadLine();
        if (input is null) break;

        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) continue;

        switch (parts[0].ToLowerInvariant())
        {
            case "help":
                Console.WriteLine("hand | board | status | draw | play <index> | end | pass | quit");
                break;
            case "hand":
                for (var index = 0; index < player.Hand.Count; index++)
                {
                    var card = player.Hand[index];
                    Console.WriteLine($"{index}: {card.Name} ({card.Row}, {card.Power} power)");
                }
                if (player.Hand.Count == 0) Console.WriteLine("Hand is empty.");
                break;
            case "board":
                foreach (var participant in new[] { first, second })
                {
                    Console.WriteLine($"{participant.Id} — {participant.Board.Score} points");
                    foreach (var row in Enum.GetValues<Row>())
                    {
                        var cards = participant.Board.GetRow(row);
                        Console.WriteLine($"  {row}: {string.Join(", ", cards.Select(card => $"{card.Name} ({card.Power})"))}");
                    }
                }
                break;
            case "status":
                Console.WriteLine($"Round {game.RoundNumber}; rounds {game.FirstRoundsWon}-{game.SecondRoundsWon}; " +
                                  $"score {first.Board.Score}-{second.Board.Score}");
                break;
            case "draw":
                var drawn = game.Draw(player);
                Console.WriteLine(drawn is null ? "No card drawn." : $"Drew {drawn.Name}.");
                break;
            case "play":
                if (parts.Length < 2 || !int.TryParse(parts[1], out var cardIndex) ||
                    cardIndex < 0 || cardIndex >= player.Hand.Count)
                {
                    Console.WriteLine("Use: play <valid hand index>");
                    break;
                }
                try
                {
                    var card = player.Hand[cardIndex];
                    program.Play(game, player, card);
                    Console.WriteLine($"Played {card.Name} ({card.Power} power).");
                    game.EndTurn();
                }
                catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
                {
                    Console.WriteLine(exception.Message);
                }
                break;
            case "end":
                game.EndTurn();
                break;
            case "pass":
                game.Pass(player);
                break;
            case "quit":
                return 0;
            default:
                Console.WriteLine("Unknown command. Enter 'help' to see available commands.");
                break;
        }
    }

    Console.WriteLine($"Final rounds: {game.FirstRoundsWon} - {game.SecondRoundsWon}");
    if (!game.IsFinished)
        Console.WriteLine("Match stopped before completion.");
    else if (game.Winner is null)
        Console.WriteLine("Match ended in a tie.");
    else
        Console.WriteLine($"Match winner: {game.Winner.Id}");
    return 0;
}

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
