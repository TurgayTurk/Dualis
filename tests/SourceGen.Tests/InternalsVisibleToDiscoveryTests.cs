using System.Reflection;
using Dualis.SourceGen;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace SourceGen.Tests;

/// <summary>
/// Verifies that cross-assembly handler/behavior discovery honors <c>InternalsVisibleTo</c>,
/// not just public visibility. Reproduces a real-world layered setup where handlers are declared
/// <c>internal</c> in an Application project and the host project (composition root) is granted
/// access via <c>[assembly: InternalsVisibleTo("Host")]</c>.
/// </summary>
public sealed class InternalsVisibleToDiscoveryTests
{
    [Fact]
    public void InternalHandlerVisibleViaInternalsVisibleToIsDiscoveredAcrossAssemblies()
    {
        const string librarySource = """
        using Dualis.CQRS;

        [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Host")]

        public sealed record Q : IRequest<string>;

        internal sealed class QHandler : IRequestHandler<Q, string>
        {
            public System.Threading.Tasks.Task<string> Handle(Q request, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult("ok");
        }
        """;

        CSharpCompilation libraryCompilation = CreateCompilation("Library", librarySource);
        byte[] libraryImage = EmitToBytes(libraryCompilation);

        const string hostSource = """
        using Dualis;

        [assembly: EnableDualisGeneration]
        """;

        CSharpCompilation hostCompilation = CreateCompilation(
            "Host",
            hostSource,
            extraReferences: [MetadataReference.CreateFromImage(libraryImage)]);

        ServiceCollectionExtensionGenerator gen = new();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(gen.AsSourceGenerator())
            .WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.Preview));

        driver = driver.RunGenerators(hostCompilation);
        GeneratorDriverRunResult result = driver.GetRunResult();
        string combined = string.Join("\n\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        combined.Should().Contain("QHandler", "an internal handler visible via InternalsVisibleTo must be discovered cross-assembly");
    }

    private static CSharpCompilation CreateCompilation(string assemblyName, string source, IEnumerable<MetadataReference>? extraReferences = null)
    {
        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Dualis.CQRS.IRequest).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
        ];
        if (extraReferences is not null)
        {
            references.AddRange(extraReferences);
        }

        return CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static byte[] EmitToBytes(CSharpCompilation compilation)
    {
        using MemoryStream stream = new();
        Microsoft.CodeAnalysis.Emit.EmitResult emitResult = compilation.Emit(stream);
        emitResult.Success.Should().BeTrue(string.Join(Environment.NewLine, emitResult.Diagnostics));
        return stream.ToArray();
    }
}
