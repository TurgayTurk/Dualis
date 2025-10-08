using System.Collections.Immutable;
using Dualis.Analyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Dualis.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="DuplicateHandlersAnalyzer"/>.
/// Verifies that when multiple IRequestHandler implementations exist for the same request, DULIS003 is reported.
/// </summary>
public sealed class DuplicateHandlersAnalyzerTests
{
    private static Task<ImmutableArray<Diagnostic>> Run(string code) => AnalyzerTestHost.RunAsync(code, new DuplicateHandlersAnalyzer());

    [Fact]
    public async Task DiagnosticWhenDuplicateHandlersForSameRequest()
    {
        string code = """
        using Dualis.CQRS;
        using System.Threading;
        using System.Threading.Tasks;
        
        sealed record A() : IRequest;
        
        sealed class H1 : IRequestHandler<A>
        {
            public Task Handle(A r, CancellationToken ct) => Task.CompletedTask;
        }
        
        sealed class H2 : IRequestHandler<A>
        {
            public Task Handle(A r, CancellationToken ct) => Task.CompletedTask;
        }
        """;

        ImmutableArray<Diagnostic> diags = await Run(code);
        Assert.Contains(diags, d => d.Id == "DULIS003");
    }
}
