using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace SourceGen.Tests;

/// <summary>
/// Tests for the source generator that creates dependency injection extensions for Dualis.
/// </summary>
public sealed class ServiceCollectionGeneratorTests
{
    /// <summary>
    /// Ensures the generator produces the expected service collection extension when Dualis types are present and generation is enabled.
    /// </summary>
    /// <remarks>
    /// Arrange: Create a Roslyn compilation that includes query, command, notification handlers and the assembly opt-in attribute.
    /// Act: Run the <see cref="ServiceCollectionExtensionGenerator"/> and capture its generated trees.
    /// Assert: The driver contains generated syntax trees (i.e., the extension was produced).
    /// </remarks>
    [Fact]
    public void GeneratesAddDualisExtensionWithMediatorRegistrations()
    {
        // Arrange
        string source = """
        using Dualis;
        using Dualis.CQRS.Queries;
        using Dualis.CQRS.Commands;
        using Dualis.Notifications;
        using Dualis.Pipeline;
        
        [assembly: EnableDualisGeneration]
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

        CSharpCompilation compilation = CreateCompilation(source);
        ServiceCollectionExtensionGenerator generator = new();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators: [generator.AsSourceGenerator()], additionalTexts: [CreateEditorConfig(enable: true)])
            .WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.Preview));

        // Act
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

        // Assert
        driver.GetRunResult().GeneratedTrees.Should().NotBeEmpty();
    }

    /// <summary>
    /// Creates a minimal Roslyn <see cref="CSharpCompilation"/> containing the provided source and references to Dualis abstractions.
    /// </summary>
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

    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content, System.Text.Encoding.UTF8);
    }

    /// <summary>
    /// Creates an in-memory .globalconfig that toggles the Dualis generator.
    /// </summary>
    private static InMemoryAdditionalText CreateEditorConfig(bool enable)
    {
        string content = string.Join(Environment.NewLine,
            "is_global = true",
            $"build_property.DualisEnableGenerator = {(enable ? "true" : "false")}"
        );
        return new InMemoryAdditionalText("/.globalconfig", content);
    }
}
