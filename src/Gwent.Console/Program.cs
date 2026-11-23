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

var human = new Player("Player 1");
var opponent = new Player("Player 2");
var game = new Game(human, opponent);

foreach (var definition in compilation.Cards.Values)
{
    var card = definition.CreateCard();
    game.Play(human, card);
    if (card.EffectId is not null)
        compilation.Effects[card.EffectId].Execute(card);
    Console.WriteLine($"{card.Name,-22} {card.Row,-7} {card.Power,2} power");
}

var score = Game.Score(human);
Console.WriteLine($"\n{human.Id}: {score}");
Console.WriteLine($"{opponent.Id}: {Game.Score(opponent)}");
Console.WriteLine(game.Winner is null ? "Result: draw" : $"Winner: {game.Winner.Id}");
return 0;
