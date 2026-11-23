using Gwent.Compiler;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class RuntimeActivationParserTests
{
    [Fact]
    public void ParsesActivationParametersSelectorsPredicatesAndPostActions()
    {
        var source = """
            OnActivation: [
                {
                    Type: "rally",
                    Params: { amount: 3, },
                    Selector: {
                        Source: "hand";
                        Single: false;
                        Predicate: (unit) => unit.Power > 2;
                    },
                    PostAction: {
                        Type: "follow-up",
                        Params: { enabled: true, },
                    },
                },
            ]
            """;

        var result = new RuntimeActivationParser(source).Parse();

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var activation = Assert.Single(result.Activations);
        Assert.Equal("rally", activation.EffectId);
        Assert.Equal(3, Assert.IsType<LiteralExpression>(activation.Arguments["amount"]).Literal.AsNumber());
        Assert.Equal("hand", activation.Selector.Source);
        Assert.IsType<LambdaExpression>(activation.Selector.Predicate);
        Assert.Equal("follow-up", activation.PostAction!.EffectId);
        Assert.Equal("parent", activation.PostAction.Selector.Source);
    }

    [Fact]
    public void DefaultsMainActivationToBoardAndPostActionToParent()
    {
        var result = new RuntimeActivationParser(
            """OnActivation: [{ Type: "rally", PostAction: { Type: "follow-up" } }]""").Parse();

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal("board", result.Activations[0].Selector.Source);
        Assert.Equal("parent", result.Activations[0].PostAction!.Selector.Source);
    }


    [Fact]
    public void RejectsDuplicateActivationAndSelectorFields()
    {
        var result = new RuntimeActivationParser(
            """OnActivation: [{ Type: "rally", Type: "follow-up", Selector: { Source: "field"; Source: "board"; } }]""").Parse();

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("Activation field 'Type'", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("Activation field 'Source'", StringComparison.Ordinal));
    }


    [Fact]
    public void ParsedActivationArgumentsAreReadOnly()
    {
        var result = new RuntimeActivationParser(
            """OnActivation: [{ Type: "rally", Params: { amount: 1, } }]""").Parse();
        var arguments = Assert.Single(result.Activations).Arguments;
        var mutableView = Assert.IsAssignableFrom<IDictionary<string, RuntimeExpression>>(arguments);

        Assert.Throws<NotSupportedException>(() =>
            mutableView.Add("extra", new LiteralExpression(RuntimeValue.From(2))));
    }

    [Fact]
    public void RejectsDuplicateParameterAssignments()
    {
        var result = new RuntimeActivationParser(
            """OnActivation: [{ Type: "rally", Params: { amount: 1, amount: 2, } }]""").Parse();

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("supplied more than once", StringComparison.Ordinal));
    }
}
