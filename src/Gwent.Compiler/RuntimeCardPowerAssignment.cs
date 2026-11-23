using Gwent.Engine;

namespace Gwent.Compiler;

public sealed record CardPowerAssignmentExpression(
    RuntimeExpression Target,
    RuntimeExpression ValueExpression) : RuntimeExpression
{
    public override RuntimeValue Evaluate(RuntimeExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ConsumeInstruction();
        var card = Target.Evaluate(context).AsCard();
        var value = ValueExpression.Evaluate(context).AsNumber();
        var game = context.Game ??
            throw new InvalidOperationException("Card power assignment requires an active game context.");
        game.ApplyPowerModifier(
            context.TriggerPlayer, card, "dsl-set-power",
            EffectOperation.Set, value, EffectDuration.UntilRoundEnd);
        return RuntimeValue.From(value);
    }
}
