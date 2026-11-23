using System.Collections.Generic;
using Gwent.Compiler;
using Gwent.Engine;
using Xunit;
namespace Gwent.Compiler.Tests;
public sealed class CompilerTests
{
    [Fact] public void LexerProducesTokensWithSourceLocations()
    {
        var tokens=new Lexer("card unit \"Unit\" power 3 row melee").Tokenize();
        Assert.Equal(TokenKind.Identifier,tokens[0].Kind); Assert.Equal("card",tokens[0].Text); Assert.Equal(1,tokens[0].Line); Assert.Equal(1,tokens[0].Column);
        Assert.Equal(TokenKind.String,tokens[2].Kind); Assert.Equal("Unit",tokens[2].Text); Assert.Equal(TokenKind.EndOfFile,tokens[^1].Kind);
    }
    [Fact] public void LexerHandlesCommentsEscapesAndNewlines()
    {
        var tokens=new Lexer("# comment\ncard unit \"Unit\\nOne\" power 3 row melee").Tokenize();
        Assert.Contains(tokens,token=>token.Kind==TokenKind.NewLine);
        Assert.Contains(tokens,token=>token.Kind==TokenKind.String && token.Text=="Unit\nOne");
    }
    [Fact] public void LexerReportsUnsupportedCharactersAndUnterminatedStrings()
    {
        var lexer=new Lexer("card @ \"broken"); lexer.Tokenize(); Assert.Equal(2,lexer.Diagnostics.Count);
    }
    [Fact] public void ParserBuildsTypedDeclarations()
    {
        var tokens=new Lexer("""
            effect rally boost 2 duration instant operation add target source
            card vanguard "Vanguard" power 4 row melee effect rally
            """).Tokenize();
        var syntax=new Parser(tokens).Parse(); Assert.Empty(syntax.Diagnostics);
        Assert.Collection(syntax.Declarations,e=>Assert.IsType<EffectDeclarationSyntax>(e),c=>Assert.IsType<CardDeclarationSyntax>(c));
    }
    [Fact] public void ParserReportsMalformedDeclarationsAndContinues()
    {
        var tokens=new Lexer("""
            card broken "Broken" power row melee
            card valid "Valid" power 4 row melee
            """).Tokenize();
        var syntax=new Parser(tokens).Parse(); Assert.NotEmpty(syntax.Diagnostics); Assert.Single(syntax.Declarations);
        Assert.True(syntax.Diagnostics[0].Column > 0);
        Assert.Equal("valid",Assert.IsType<CardDeclarationSyntax>(syntax.Declarations[0]).Id);
    }
    [Fact] public void CompilesCardsAndEffects()
    {
        var result=new DslCompiler().Compile("""
            effect rally boost 2
            card vanguard "Vanguard" power 4 row melee effect rally
            """);
        Assert.True(result.Success); Assert.Equal(Row.Melee,result.Cards["vanguard"].Row); Assert.Equal(2,result.Effects["rally"].PowerIncrease);
    }
    [Fact] public void InvalidProgramDoesNotExposePartialCatalog()
    {
        var result=new DslCompiler().Compile("""
            card valid "Valid" power 4 row melee
            this is invalid
            """);
        Assert.False(result.Success); Assert.Empty(result.Cards); Assert.Empty(result.Effects);
    }
    [Fact] public void IntegerOverflowProducesDiagnosticInsteadOfThrowing()
    {
        var result=new DslCompiler().Compile("""
            effect rally boost 999999999999999999999999
            card vanguard "Vanguard" power 4 row melee
            """);
        Assert.False(result.Success); Assert.Empty(result.Cards); Assert.Empty(result.Effects);
        Assert.Contains(result.Diagnostics,d=>d.Message.Contains("integer range",StringComparison.Ordinal));
    }
    [Fact] public void CompilationCatalogCannotBeModifiedThroughReadOnlyView()
    {
        var result=new DslCompiler().Compile("card vanguard \"Vanguard\" power 4 row melee");
        var cards=Assert.IsAssignableFrom<IDictionary<string,CardDefinition>>(result.Cards);
        Assert.Throws<NotSupportedException>(()=>cards.Add("other",new CardDefinition("other","Other",1,Row.Melee,null)));
        Assert.Single(result.Cards);
    }
    [Fact] public void CardDefinitionCreatesDistinctInstances()
    {
        var result=new DslCompiler().Compile("card vanguard \"Vanguard\" power 4 row melee");
        var definition=result.Cards["vanguard"]; var first=definition.CreateCard(); var second=definition.CreateCard();
        Assert.NotEqual(first.InstanceId,second.InstanceId); Assert.Equal(first.DefinitionId,second.DefinitionId);
    }
    [Fact] public void CompilesFieldBoundRowEffectMetadata()
    {
        var result=new DslCompiler().Compile("""
            effect frost boost 1 duration while-source-on-field operation set target both-players-in-row
            card blizzard "Blizzard" power 0 row melee effect frost
            """);
        Assert.True(result.Success); var effect=result.Effects["frost"].ToEngineEffect();
        Assert.Equal(EffectDuration.WhileSourceOnField,effect.Duration); Assert.Equal(EffectOperation.Set,effect.Operation);
        Assert.Equal(EffectTargetScope.BothPlayersInRow,effect.TargetScope);
    }
    [Fact] public void CardCanReferenceEffectDeclaredLater()
    {
        var result=new DslCompiler().Compile("""
            card vanguard "Vanguard" power 4 row melee effect rally
            effect rally boost 2
            """);
        Assert.True(result.Success); Assert.Equal("rally",result.Cards["vanguard"].EffectId);
    }
    [Fact]
    public void SemanticAnalyzerRejectsUnknownEffectReferences()
    {
        var syntax = new Parser(new Lexer("card unit \"Unit\" power 3 row melee effect missing").Tokenize()).Parse();
        var result = new SemanticAnalyzer().Analyze(syntax);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("unknown effect", StringComparison.Ordinal));
    }

    [Fact]
    public void CompilerCanUseInjectedSemanticAnalyzer()
    {
        var analyzer = new SemanticAnalyzer();
        var compiler = new DslCompiler(analyzer);

        var result = compiler.Compile("card unit \"Unit\" power 3 row melee");

        Assert.True(result.Success);
    }

    [Fact]
    public void ParserRecoversAfterUnterminatedStringOnPreviousLine()
    {
        var tokens = new Lexer("card broken \"Broken\\ncard valid \"Valid\" power 3 row melee").Tokenize();
        var syntax = new Parser(tokens).Parse();

        Assert.NotEmpty(syntax.Diagnostics);
        Assert.Single(syntax.Declarations);
        Assert.Equal("valid", Assert.IsType<CardDeclarationSyntax>(syntax.Declarations[0]).Id);
    }

}
