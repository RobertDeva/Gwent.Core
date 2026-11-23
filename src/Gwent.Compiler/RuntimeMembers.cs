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
            RuntimeValueKind.Player => ReadPlayer(target.AsPlayer(), context),
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
        "Faction" => RuntimeValue.From(card.Faction),
        "Type" => RuntimeValue.From(card.Type),
        "Rank" => RuntimeValue.From(card.Rank),
        "EffectId" => RuntimeValue.From(card.EffectId ?? string.Empty),
        _ => throw UnknownMember()
    };

    private RuntimeValue ReadPlayer(Player player, RuntimeExecutionContext context) => MemberName switch
    {
        "Id" => RuntimeValue.From(player.Id),
        "Hand" => RuntimeValue.FromZoneCards(context, player, CardZone.Hand),
        "Deck" => RuntimeValue.FromZoneCards(context, player, CardZone.Deck),
        "Field" => RuntimeValue.FromZoneCards(context, player, CardZone.Field),
        "Graveyard" => RuntimeValue.FromZoneCards(context, player, CardZone.Graveyard),
        "Board" => RuntimeValue.FromZoneCards(context, player, CardZone.Field),
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
        "Winner" => game.Winner is null ? RuntimeValue.Null() : RuntimeValue.From(game.Winner!),
        _ => throw UnknownMember()
    };

    private RuntimeValue ReadContext(RuntimeExecutionContext context) => MemberName switch
    {
        "TriggerPlayer" => RuntimeValue.From(context.TriggerPlayer),
        "OtherPlayer" => RuntimeValue.From(OtherPlayer(context)),
        "SourceCard" => RuntimeValue.From(context.SourceCard),
        "Game" => context.Game is null ? RuntimeValue.Null() : RuntimeValue.From(context.Game!),
        "Hand" => RuntimeValue.FromZoneCards(context, context.TriggerPlayer, CardZone.Hand),
        "Deck" => RuntimeValue.FromZoneCards(context, context.TriggerPlayer, CardZone.Deck),
        "Field" => RuntimeValue.FromZoneCards(context, context.TriggerPlayer, CardZone.Field),
        "Graveyard" => RuntimeValue.FromZoneCards(context, context.TriggerPlayer, CardZone.Graveyard),
        "Board" => RuntimeValue.FromCards(context.Game is null
            ? context.TriggerPlayer.Field
            : context.Game.First.Field.Concat(context.Game.Second.Field)),
        "OtherHand" => RuntimeValue.FromZoneCards(context, OtherPlayer(context), CardZone.Hand),
        "OtherDeck" => RuntimeValue.FromZoneCards(context, OtherPlayer(context), CardZone.Deck),
        "OtherField" => RuntimeValue.FromZoneCards(context, OtherPlayer(context), CardZone.Field),
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
