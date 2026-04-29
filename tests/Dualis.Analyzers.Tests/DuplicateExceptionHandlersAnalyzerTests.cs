using System.Collections.Immutable;
using Dualis.Analyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Dualis.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="DuplicateExceptionHandlersAnalyzer"/>.
/// </summary>
public sealed class DuplicateExceptionHandlersAnalyzerTests
{
    private static Task<ImmutableArray<Diagnostic>> Run(string code) => AnalyzerTestHost.RunAsync(code, new DuplicateExceptionHandlersAnalyzer());

    [Fact]
    public async Task DiagnosticWhenDuplicateExceptionHandlersForSameContract()
    {
        string code = """
        using Dualis.CQRS;
        using System;
        using System.Threading;
        using System.Threading.Tasks;

        sealed record A() : IRequest<int>;

        sealed class EH1 : IRequestExceptionHandler<A, int, InvalidOperationException>
        {
            public Task Handle(A request, InvalidOperationException exception, RequestExceptionState<int> state, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }

        sealed class EH2 : IRequestExceptionHandler<A, int, InvalidOperationException>
        {
            public Task Handle(A request, InvalidOperationException exception, RequestExceptionState<int> state, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }
        """;

        ImmutableArray<Diagnostic> diags = await Run(code);
        Assert.Contains(diags, d => d.Id == "DULIS015");
    }
}
