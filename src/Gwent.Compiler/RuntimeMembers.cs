using Gwent.Engine;

namespace Gwent.Compiler;

public sealed record RuntimeMemberExpression(RuntimeExpression Target, string MemberName)
    : RuntimeExpression
{
    public override RuntimeValue Evaluate(RuntimeExecutionContext context)
    {
        context.ConsumeInstruction();
        var target = Target.Evaluate(context);
        return target.Kind switch
        {
            RuntimeValueKind.Card => ReadCard(target.AsCard()),
            RuntimeValueKind.Player => ReadPlayer(target.AsPlayer()),
            RuntimeValueKind.Game => ReadGame(target.AsGame()),
            RuntimeValueKind.ExecutionContext => ReadContext(target.AsContext()),
            _ => throw new InvalidOperationException(
                $"Member '{MemberName}' cannot be read from runtime value '{target.Kind}'.")
        };
    }

    private RuntimeValue ReadCard(Card card) => MemberName switch
    {
        "Name" => RuntimeValue.From(card.Name),
        "Power" => RuntimeValue.From(card.Power),
        "Owner" or "OwnerId" => RuntimeValue.From(card.OwnerId ?? string.Empty),
        "Range" or "Row" => RuntimeValue.From(card.Row.ToString()),
        "DefinitionId" => RuntimeValue.From(card.DefinitionId),
        "EffectId" => RuntimeValue.From(card.EffectId ?? string.Empty),
        _ => throw UnknownMember()
    };

    private RuntimeValue ReadPlayer(Player player) => MemberName switch
    {
        "Id" => RuntimeValue.From(player.Id),
        "Hand" => RuntimeValue.FromCards(player.Hand),
        "Deck" => RuntimeValue.FromCards(player.Deck),
        "Field" => RuntimeValue.FromCards(player.Field),
        "Graveyard" => RuntimeValue.FromCards(player.Graveyard),
        "Board" => RuntimeValue.FromCards(player.Board.Cards),
        _ => throw UnknownMember()
    };

    private RuntimeValue ReadGame(Game game) => MemberName switch
    {
        "First" => RuntimeValue.From(game.First),
        "Second" => RuntimeValue.From(game.Second),
        "CurrentPlayer" => RuntimeValue.From(game.CurrentPlayer),
        "RoundNumber" => RuntimeValue.From(game.RoundNumber),
        "FirstRoundsWon" => RuntimeValue.From(game.FirstRoundsWon),
        "SecondRoundsWon" => RuntimeValue.From(game.SecondRoundsWon),
        "IsFinished" => RuntimeValue.From(game.IsFinished),
        "Winner" => game.Winner is null ? RuntimeValue.Null() : RuntimeValue.From(game.Winner),
        _ => throw UnknownMember()
    };

    private RuntimeValue ReadContext(RuntimeExecutionContext context) => MemberName switch
    {
        "TriggerPlayer" => RuntimeValue.From(context.TriggerPlayer),
        "SourceCard" => RuntimeValue.From(context.SourceCard),
        "Game" => context.Game is null ? RuntimeValue.Null() : RuntimeValue.From(context.Game),
        "Hand" => RuntimeValue.FromCards(context.TriggerPlayer.Hand),
        "Deck" => RuntimeValue.FromCards(context.TriggerPlayer.Deck),
        "Field" => RuntimeValue.FromCards(context.TriggerPlayer.Field),
        "Graveyard" => RuntimeValue.FromCards(context.TriggerPlayer.Graveyard),
        "Board" => RuntimeValue.FromCards(context.Game is null
            ? context.TriggerPlayer.Field
            : context.Game.First.Field.Concat(context.Game.Second.Field)),
        "OtherHand" => RuntimeValue.FromCards(OtherPlayer(context).Hand),
        "OtherDeck" => RuntimeValue.FromCards(OtherPlayer(context).Deck),
        "OtherField" => RuntimeValue.FromCards(OtherPlayer(context).Field),
        _ => throw UnknownMember()
    };

    private static Player OtherPlayer(RuntimeExecutionContext context)
    {
        var game = context.Game ??
            throw new InvalidOperationException("Other-player context requires a game.");
        return ReferenceEquals(context.TriggerPlayer, game.First) ? game.Second : game.First;
    }

    private InvalidOperationException UnknownMember() =>
        new($"Unknown member '{MemberName}'.");
}
