using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Dualis.Analyzer.Analyzers;
using Xunit;

namespace Dualis.Analyzers.Tests;

/// <summary>
/// Tests for <see cref="GeneratorGatingAnalyzer"/> ensuring a diagnostic is emitted when the
/// Dualis generator is not enabled in the host project.
/// </summary>
public class GeneratorGatingAnalyzerTests
{
    private static CSharpAnalyzerTest<GeneratorGatingAnalyzer, DefaultVerifier> Create(string code)
    {
        CSharpAnalyzerTest<GeneratorGatingAnalyzer, DefaultVerifier> test = new()
        {
            TestCode = code,
            ReferenceAssemblies = new ReferenceAssemblies("net9.0").WithPackages([
                new PackageIdentity("Microsoft.NETCore.App.Ref", "9.0.0"),
            ]),
        };
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(CQRS.IRequest).Assembly.Location));
        return test;
    }

    /// <summary>
    /// Verifies that an informational diagnostic (DULIS001) is reported when the enabling attribute is missing.
    /// </summary>
    [Fact]
    public async Task ReportsInfoWhenAttributeMissing()
    {
        string code = """
        using Dualis;
        
        class C
        {
            void M()
            {
            }
        }
        """;

        DiagnosticResult expected = new DiagnosticResult("DULIS001", DiagnosticSeverity.Info).WithSpan(1, 1, 8, 2);
        CSharpAnalyzerTest<GeneratorGatingAnalyzer, DefaultVerifier> test = Create(code);
        test.ExpectedDiagnostics.Add(expected);
        bool single = test.ExpectedDiagnostics.Count == 1;
        Assert.True(single);
        await test.RunAsync();
    }
}
