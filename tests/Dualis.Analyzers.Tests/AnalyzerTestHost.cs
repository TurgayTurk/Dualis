using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dualis.Analyzers.Tests;

internal static class AnalyzerTestHost
{
    private static ImmutableArray<MetadataReference> GetFrameworkReferences()
    {
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrEmpty(tpa))
        {
            return [];
        }

        ImmutableArray<MetadataReference>.Builder builder = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (string path in tpa.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                builder.Add(MetadataReference.CreateFromFile(path));
            }
        }
        return builder.ToImmutable();
    }

    public static async Task<ImmutableArray<Diagnostic>> RunAsync(string code, DiagnosticAnalyzer analyzer)
    {
        CSharpParseOptions parse = new(LanguageVersion.Preview);
        CSharpCompilationOptions opts = new(OutputKind.DynamicallyLinkedLibrary);

        ImmutableArray<MetadataReference> refs = GetFrameworkReferences()
            .Add(MetadataReference.CreateFromFile(typeof(IDualizor).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerTestsHost",
            syntaxTrees: [CSharpSyntaxTree.ParseText(code, parse)],
            references: refs,
            options: opts);

        CompilationWithAnalyzers cwa = compilation.WithAnalyzers([analyzer]);
        ImmutableArray<Diagnostic> diagnostics = await cwa.GetAnalyzerDiagnosticsAsync();
        return diagnostics;
    }
}
