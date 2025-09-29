using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace SourceGen.Tests;

public sealed class ServiceCollectionGeneratorTests
{
    [Fact]
    public void GeneratesAddDualisExtensionWithMediatorRegistrations()
    {
        // Arrange
        string source = """
        using Dualis.CQRS.Queries;
        using Dualis.CQRS.Commands;
        using Dualis.Notifications;
        using Dualis.Pipeline;
        
        public sealed record Q : IQuery<string>;
        public sealed class QHandler : IQueryHandler<Q, string>
        {
            public Task<string> HandleAsync(Q query, CancellationToken cancellationToken = default) => Task.FromResult("ok");
        }
        
        public sealed record C : ICommand;
        public sealed class CHandler : ICommandHandler<C>
        {
            public Task HandleAsync(C command, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
        
        public sealed record N : INotification;
        public sealed class NHandler : INotificationHandler<N>
        {
            public Task HandleAsync(N note, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
        
        public sealed class Order<TReq, TRes> : IPipelineBehavior<TReq, TRes>
        {
            public Task<TRes> Handle(TReq request, RequestHandlerDelegate<TRes> next, CancellationToken cancellationToken) => next(cancellationToken);
        }
        """;

        // Act
        CSharpCompilation compilation = CreateCompilation(source);
        ServiceCollectionExtensionGenerator generator = new();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        driver.GetRunResult().GeneratedTrees.Should().NotBeEmpty();
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references: [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Dualis.CQRS.Commands.ICommand).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation;
    }
}
