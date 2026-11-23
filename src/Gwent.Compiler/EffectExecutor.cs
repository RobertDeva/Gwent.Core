using Gwent.Engine;

namespace Gwent.Compiler;

public sealed class EffectExecutor
{
    public bool Execute(CompilationResult program, Game game, Player player, Card card)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(card);

        if (!program.Success)
            throw new InvalidOperationException("A program with compilation diagnostics cannot be executed.");

        if (!program.Cards.TryGetValue(card.DefinitionId, out var definition))
            throw new ArgumentException($"Card definition '{card.DefinitionId}' does not belong to this program.", nameof(card));

        if (!StringComparer.Ordinal.Equals(card.Name, definition.Name) ||
            card.BasePower != definition.Power ||
            card.Row != definition.Row ||
            !StringComparer.Ordinal.Equals(card.EffectId, definition.EffectId))
            throw new ArgumentException("Card instance does not match its compiled definition.", nameof(card));

        if (definition.EffectId is null) return false;
        if (!program.Effects.TryGetValue(definition.EffectId, out var effect))
            throw new InvalidOperationException($"Compiled effect '{definition.EffectId}' is missing from the program.");

        game.ApplyEffect(player, card, effect.ToEngineEffect());
        return true;
    }
}
