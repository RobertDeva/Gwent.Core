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
            RuntimeValueKind.Card => ReadCard(target.AsCard(), context),
            RuntimeValueKind.CardCollection => ReadCardCollection(target.AsCardCollection()),
            RuntimeValueKind.StringCollection => ReadStringCollection(target.AsStrings()),
            RuntimeValueKind.Player => ReadPlayer(target.AsPlayer(), context),
            RuntimeValueKind.Game => ReadGame(target.AsGame()),
            RuntimeValueKind.ExecutionContext => ReadContext(target.AsContext()),
            _ => throw new InvalidOperationException(
                $"Member '{MemberName}' cannot be read from runtime value '{target.Kind}'.")
        };
    }

    private RuntimeValue ReadCard(Card card, RuntimeExecutionContext context) => MemberName switch
    {
        "Name" => RuntimeValue.From(card.Name),
        "Description" => RuntimeValue.From(card.Description),
        "Power" => RuntimeValue.From(card.Power),
        "Owner" => RuntimeValue.From(ResolveOwner(card, context)),
        "OwnerId" => RuntimeValue.From(card.OwnerId ?? string.Empty),
        "Range" => RuntimeValue.FromStrings(card.AllowedRows.Select(ToDslRange)),
        "Row" => RuntimeValue.From(ToDslRange(card.Row)),
        "DefinitionId" => RuntimeValue.From(card.DefinitionId),
        "Faction" => RuntimeValue.From(card.Faction),
        "Type" => RuntimeValue.From(card.Type),
        "Rank" => RuntimeValue.From(card.Rank),
        "EffectId" => RuntimeValue.From(card.EffectId ?? string.Empty),
        _ => throw UnknownMember()
    };

    private RuntimeValue ReadCardCollection(RuntimeCardCollection collection) => MemberName switch
    {
        "Count" => RuntimeValue.From(collection.Count),
        _ => throw UnknownMember()
    };

    private RuntimeValue ReadStringCollection(IReadOnlyList<string> values) => MemberName switch
    {
        "Count" => RuntimeValue.From(values.Count),
        _ => throw UnknownMember()
    };

    private static Player ResolveOwner(Card card, RuntimeExecutionContext context)
    {
        var ownerId = card.OwnerId ??
            throw new InvalidOperationException("An unassigned card does not have an owner.");

        if (StringComparer.Ordinal.Equals(ownerId, context.TriggerPlayer.Id))
            return context.TriggerPlayer;

        if (context.Game is not null)
        {
            if (StringComparer.Ordinal.Equals(ownerId, context.Game.First.Id))
                return context.Game.First;
            if (StringComparer.Ordinal.Equals(ownerId, context.Game.Second.Id))
                return context.Game.Second;
        }

        throw new InvalidOperationException("Card owner is not available in the current execution context.");
    }

    private static string ToDslRange(Row row) => row switch
    {
        Row.Melee => "Melee",
        Row.Ranged => "Range",
        Row.Siege => "Siege",
        _ => throw new ArgumentOutOfRangeException(nameof(row))
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
