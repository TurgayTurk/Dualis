using System.Collections.Immutable;
using Dualis.Analyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Dualis.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="MismatchedExceptionContractRequestAnalyzer"/>.
/// </summary>
public sealed class MismatchedExceptionContractRequestAnalyzerTests
{
    private static Task<ImmutableArray<Diagnostic>> Run(string code) => AnalyzerTestHost.RunAsync(code, new MismatchedExceptionContractRequestAnalyzer());

    [Fact]
    public async Task DiagnosticWhenExceptionHandlerRequestDoesNotImplementIRequestOfT()
    {
        string code = """
        using Dualis.CQRS;
        using System;
        using System.Threading;
        using System.Threading.Tasks;

        sealed record A();

        sealed class EH : IRequestExceptionHandler<A, int, InvalidOperationException>
        {
            public Task Handle(A request, InvalidOperationException exception, RequestExceptionState<int> state, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }
        """;

        ImmutableArray<Diagnostic> diags = await Run(code);
        Assert.Contains(diags, d => d.Id == "DULIS014");
    }

    [Fact]
    public async Task DiagnosticWhenExceptionActionRequestDoesNotImplementIRequest()
    {
        string code = """
        using Dualis.CQRS;
        using System;
        using System.Threading;
        using System.Threading.Tasks;

        sealed record A();

        sealed class EA : IRequestExceptionAction<A, InvalidOperationException>
        {
            public Task Execute(A request, InvalidOperationException exception, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }
        """;

        ImmutableArray<Diagnostic> diags = await Run(code);
        Assert.Contains(diags, d => d.Id == "DULIS014");
    }
}
