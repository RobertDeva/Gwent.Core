using System.Collections.Generic;
using Gwent.Compiler;
using Gwent.Engine;
using Xunit;

namespace Gwent.Compiler.Tests;

public sealed class CompilerTests
{
    [Fact]
    public void CompilesCardsAndEffects()
    {
        var result = new DslCompiler().Compile("""
            effect rally boost 2
            card vanguard "Vanguard" power 4 row melee effect rally
            """);
        Assert.True(result.Success);
        Assert.Equal(Row.Melee, result.Cards["vanguard"].Row);
        Assert.Equal(2, result.Effects["rally"].PowerIncrease);
    }

    [Fact]
    public void InvalidProgramDoesNotExposePartialCatalog()
    {
        var result = new DslCompiler().Compile("""
            card valid "Valid" power 4 row melee
            this is invalid
            """);
        Assert.False(result.Success);
        Assert.Empty(result.Cards);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void IntegerOverflowProducesDiagnosticInsteadOfThrowing()
    {
        var result = new DslCompiler().Compile("""
            effect rally boost 999999999999999999999999
            card vanguard "Vanguard" power 4 row melee
            """);

        Assert.False(result.Success);
        Assert.Empty(result.Cards);
        Assert.Empty(result.Effects);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("integer range", StringComparison.Ordinal));
    }

    [Fact]
    public void CompilationCatalogCannotBeModifiedThroughReadOnlyView()
    {
        var result = new DslCompiler().Compile("""
            card vanguard "Vanguard" power 4 row melee
            """);
        var cards = Assert.IsAssignableFrom<IDictionary<string, CardDefinition>>(result.Cards);

        Assert.Throws<NotSupportedException>(() =>
            cards.Add("other", new CardDefinition("other", "Other", 1, Row.Melee, null)));
        Assert.Single(result.Cards);
    }
}
