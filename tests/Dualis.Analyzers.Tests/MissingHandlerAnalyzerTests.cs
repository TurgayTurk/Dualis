using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Dualis.Analyzer.Analyzers;
using Xunit;

namespace Dualis.Analyzers.Tests;

/// <summary>
/// Tests for <see cref="MissingHandlerAnalyzer"/> ensuring warnings are emitted when requests lack handlers.
/// </summary>
public class MissingHandlerAnalyzerTests
{
    private static CSharpAnalyzerTest<MissingHandlerAnalyzer, DefaultVerifier> Create(string code)
    {
        CSharpAnalyzerTest<MissingHandlerAnalyzer, DefaultVerifier> test = new()
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
    /// Verifies that a warning diagnostic (DULIS002) is reported when a request is sent without a matching handler.
    /// </summary>
    [Fact]
    public async Task ReportsWarningWhenNoHandler()
    {
        string code = """
        using System.Threading;
        using Dualis;
        using Dualis.CQRS;
        
        sealed record P(string Text) : IRequest<string>;
        
        class C
        {
            async System.Threading.Tasks.Task M(ISender sender, CancellationToken ct)
            {
                _ = await sender.Send(new P("x"), ct);
            }
        }
        """;

        DiagnosticResult expected = new DiagnosticResult("DULIS002", DiagnosticSeverity.Warning).WithSpan(11, 19, 11, 46);
        CSharpAnalyzerTest<MissingHandlerAnalyzer, DefaultVerifier> test = Create(code);
        test.ExpectedDiagnostics.Add(expected);
        bool single = test.ExpectedDiagnostics.Count == 1;
        Assert.True(single);
        await test.RunAsync();
    }
}
