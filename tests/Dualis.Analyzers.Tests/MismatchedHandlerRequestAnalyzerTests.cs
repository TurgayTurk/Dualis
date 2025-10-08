using System.Collections.Immutable;
using Dualis.Analyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Dualis.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="MismatchedHandlerRequestAnalyzer"/>.
/// Verifies handlers use request types that implement the required IRequest shapes.
/// </summary>
public sealed class MismatchedHandlerRequestAnalyzerTests
{
    private static Task<ImmutableArray<Diagnostic>> Run(string code) => AnalyzerTestHost.RunAsync(code, new MismatchedHandlerRequestAnalyzer());

    [Fact]
    public async Task DiagnosticWhenHandler2RequestDoesNotImplementIRequestOfT()
    {
        string code = """
        using Dualis.CQRS;
        using System.Threading;
        using System.Threading.Tasks;
        
        sealed record A();
        
        sealed class H1 : IRequestHandler<A, int>
        {
            public Task<int> Handle(A r, CancellationToken ct) => Task.FromResult(0);
        }
        """;

        ImmutableArray<Diagnostic> diags = await Run(code);
        Assert.Contains(diags, d => d.Id == "DULIS005");
    }

    [Fact]
    public async Task DiagnosticWhenHandler1RequestDoesNotImplementIRequest()
    {
        string code = """
        using Dualis.CQRS;
        using System.Threading;
        using System.Threading.Tasks;
        
        sealed record A();
        
        sealed class H1 : IRequestHandler<A>
        {
            public Task Handle(A r, CancellationToken ct) => Task.CompletedTask;
        }
        """;

        ImmutableArray<Diagnostic> diags = await Run(code);
        Assert.Contains(diags, d => d.Id == "DULIS005");
    }
}
