using Gwent.Compiler;
using Gwent.Engine;

const string sampleDsl = """
effect rally boost 2
card vanguard "Northern Vanguard" power 4 row melee effect rally
card archer "Frontier Archer" power 3 row ranged
""";

var source = args.Length > 0 ? File.ReadAllText(args[0]) : sampleDsl;
var compilation = new DslCompiler().Compile(source);
if (!compilation.Success)
{
    foreach (var diagnostic in compilation.Diagnostics) Console.Error.WriteLine(diagnostic);
    return 1;
}

var first = new Player("Player 1");
var second = new Player("Player 2");
var game = new Game(first, second);

foreach (var definition in compilation.Cards.Values)
{
    game.AddToDeck(first, definition.CreateCard());
    game.AddToDeck(second, definition.CreateCard());
}

Console.WriteLine($"Round {game.RoundNumber}");
for (var turn = 0; turn < 2; turn++)
{
    var player = game.CurrentPlayer;
    var card = game.Draw(player);
    if (card is not null)
    {
        game.Play(player, card);
        if (card.EffectId is not null)
            game.ApplyEffect(player, card, compilation.Effects[card.EffectId].ToEngineEffect());

        Console.WriteLine($"{player.Id}: {card.Name} ({card.Row}, {card.Power} power)");
    }
    game.EndTurn();
}

game.Pass(game.CurrentPlayer);
game.Pass(game.CurrentPlayer);

Console.WriteLine($"\n{first.Id}: {first.Board.Score}");
Console.WriteLine($"{second.Id}: {second.Board.Score}");
Console.WriteLine(game.LastRoundWinner is null ? "Round result: draw" : $"Round winner: {game.LastRoundWinner.Id}");
Console.WriteLine($"Rounds: {game.FirstRoundsWon} - {game.SecondRoundsWon}");
return 0;
