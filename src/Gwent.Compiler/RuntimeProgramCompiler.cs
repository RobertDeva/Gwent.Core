namespace Gwent.Compiler;

public sealed record RuntimeEffectProgram(
    RuntimeEffectCatalog Effects,
    IReadOnlyList<RuntimeEffectInvocation> Activations);

public sealed record RuntimeProgramCompilationResult(
    RuntimeEffectProgram? Program,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Success => Program is not null && Diagnostics.Count == 0;
}

public sealed class RuntimeProgramCompiler
{
    public RuntimeProgramCompilationResult Compile(string effectSource, string activationSource)
    {
        ArgumentNullException.ThrowIfNull(effectSource);
        ArgumentNullException.ThrowIfNull(activationSource);

        var effectResult = new RuntimeEffectCompiler().Compile(effectSource);
        var activationResult = new RuntimeActivationParser(activationSource).Parse();
        var diagnostics = effectResult.Diagnostics.Concat(activationResult.Diagnostics).ToList();
        if (effectResult.Catalog is null || diagnostics.Count > 0)
            return new RuntimeProgramCompilationResult(null, diagnostics.AsReadOnly());

        var analyzer = new RuntimeTypeAnalyzer();
        diagnostics.AddRange(analyzer.Analyze(effectResult.Catalog));
        diagnostics.AddRange(analyzer.AnalyzeActivations(effectResult.Catalog, activationResult.Activations));

        if (diagnostics.Count > 0)
            return new RuntimeProgramCompilationResult(null, diagnostics.AsReadOnly());

        return new RuntimeProgramCompilationResult(
            new RuntimeEffectProgram(effectResult.Catalog, activationResult.Activations),
            diagnostics.AsReadOnly());
    }
}
