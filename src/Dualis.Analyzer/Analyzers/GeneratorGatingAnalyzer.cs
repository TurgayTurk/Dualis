using System.Collections.Immutable;
using Dualis.Analyzer.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dualis.Analyzer.Analyzers;

/// <summary>
/// Emits an informational diagnostic if the host project does not appear to have the Dualis source generator enabled.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GeneratorGatingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [Descriptors.DULIS001_GeneratorNotEnabled];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        Compilation comp = context.Compilation;
        bool hasAttr = comp.Assembly.GetAttributes()
            .Select(a => a.AttributeClass)
            .Any(cls =>
            {
                if (cls is null)
                {
                    return false;
                }
                string name = cls.Name;
                string ns = cls.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                return (name == "EnableDualisGenerationAttribute" || name == "EnableDualisGeneration") && ns == "global::Dualis";
            });

        if (!hasAttr)
        {
            Location loc = comp.Assembly.Locations.FirstOrDefault() ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.DULIS001_GeneratorNotEnabled, loc));
        }
    }
}
