using Dualis.SourceGen;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace SourceGen.Tests;

public sealed class DualisGeneratorErrorMessageTests
{
    [Fact]
    public void GeneratedDualizorUsesInterpolatedFullNameInUnknownMessages()
    {
        string source = """
        using Dualis;
        using Dualis.CQRS;
        [assembly: EnableDualisGeneration]
        public sealed record Dummy(int Id) : IRequest<int>;
        public sealed class DummyHandler : IRequestHandler<Dummy, int>
        {
            public Task<int> Handle(Dummy request, CancellationToken cancellationToken = default) => Task.FromResult(42);
        }
        """;

        var compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references: [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Dualis.CQRS.IRequest).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        DualisGenerator generator = new();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: [generator.AsSourceGenerator()],
                additionalTexts: [CreateEditorConfig(enable: true)])
            .WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.Preview));

        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        GeneratorDriverRunResult result = driver.GetRunResult();
        string allText = string.Join("\n\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        allText.Should().Contain("$\"Unknown request type: {request.GetType().FullName}\"");
    }

    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content, System.Text.Encoding.UTF8);
    }

    private static InMemoryAdditionalText CreateEditorConfig(bool enable)
    {
        string content = string.Join(Environment.NewLine,
            "is_global = true",
            $"build_property.DualisEnableGenerator = {(enable ? "true" : "false")}"
        );
        return new InMemoryAdditionalText("/.globalconfig", content);
    }
}
