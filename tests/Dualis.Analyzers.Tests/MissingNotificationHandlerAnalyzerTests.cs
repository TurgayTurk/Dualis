using System.Collections.Immutable;
using Dualis.Analyzer.Analyzers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Dualis.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="MissingNotificationHandlerAnalyzer"/>.
/// Verifies that publishing a notification without any registered handler triggers DULIS006.
/// </summary>
public sealed class MissingNotificationHandlerAnalyzerTests
{
    private static Task<ImmutableArray<Diagnostic>> Run(string code) => AnalyzerTestHost.RunAsync(code, new MissingNotificationHandlerAnalyzer());

    [Fact]
    public async Task DiagnosticWhenPublishNotificationWithoutHandler()
    {
        string code = """
        using Dualis;
        using Dualis.Notifications;
        using System.Threading;
        
        sealed record E() : INotification;
        
        class C
        {
            async System.Threading.Tasks.Task M(IDualizor d, CancellationToken ct)
            {
                await d.Publish(new E(), ct);
            }
        }
        """;

        ImmutableArray<Diagnostic> diags = await Run(code);
        Assert.Contains(diags, d => d.Id == "DULIS006");
    }
}
