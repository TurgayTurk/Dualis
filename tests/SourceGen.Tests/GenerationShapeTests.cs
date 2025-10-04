using Dualis.SourceGen;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace SourceGen.Tests;

/// <summary>
/// Verifies the shape and gating behavior of the source-generated AddDualis code.
/// Ensures the method is internal, non-extension, and emitted under Dualis.Generated only when enabled.
/// </summary>
public sealed class GenerationShapeTests
{
    /// <summary>
    /// When the generator is disabled via editorconfig/globalconfig, no sources must be produced.
    /// </summary>
    [Fact]
    public void DisabledPropertyProducesNoAddDualisSource()
    {
        string src = """
        using Dualis;
        using Dualis.CQRS;
        
        public sealed record Q : IRequest<string>;
        public sealed class QHandler : Dualis.CQRS.IRequestHandler<Q, string>
        {
            public System.Threading.Tasks.Task<string> Handle(Q request, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult("ok");
        }
        """;

        CSharpCompilation comp = CreateCompilation(src);
        ServiceCollectionExtensionGenerator gen = new();
        // Disabled via editorconfig
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators: [gen.AsSourceGenerator()], additionalTexts: [CreateEditorConfig(enable: false)])
            .WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.Preview));

        driver = driver.RunGenerators(comp);
        GeneratorDriverRunResult result = driver.GetRunResult();
        // No sources should be generated when disabled
        result.GeneratedTrees.Should().BeEmpty();
    }

    /// <summary>
    /// With assembly-level opt-in, the generator must emit AddDualis as an internal non-extension method within Dualis.Generated.
    /// </summary>
    [Fact]
    public void GeneratedAddDualisIsInternalNonExtensionUnderDualisGenerated()
    {
        string src = """
        using Dualis;
        using Dualis.CQRS;
        
        [assembly: EnableDualisGeneration]
        public sealed record Q : IRequest<string>;
        public sealed class QHandler : Dualis.CQRS.IRequestHandler<Q, string>
        {
            public System.Threading.Tasks.Task<string> Handle(Q request, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult("ok");
        }
        """;

        CSharpCompilation comp = CreateCompilation(src);
        ServiceCollectionExtensionGenerator gen = new();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(gen.AsSourceGenerator())
            .WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.Preview));

        driver = driver.RunGenerators(comp);
        GeneratorDriverRunResult result = driver.GetRunResult();
        string combined = string.Join("\n\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        combined.Should().Contain("namespace Dualis.Generated;");
        combined.Should().Contain("internal static IServiceCollection AddDualis(Microsoft.Extensions.DependencyInjection.IServiceCollection services");
        combined.Should().NotContain("this Microsoft.Extensions.DependencyInjection.IServiceCollection");
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references: [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Dualis.CQRS.IRequest).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation;
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
