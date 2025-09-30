using System.Collections.Immutable;
using System.Text;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace SourceGen.Tests;

/// <summary>
/// Verifies generator gating (host-only enablement) and the duplicate-type guard for <c>Dualis.Dualizor</c>.
/// </summary>
/// <remarks>
/// Scenarios covered:
/// - Guard prevents generating <c>Dualis.Dualizor</c> when a referenced assembly already contains the type.
/// - Generators run when explicitly enabled via MSBuild property or assembly attribute.
/// - Generators do not run when explicitly disabled via MSBuild property.
/// </remarks>
public sealed class GeneratorGatingTests
{
    private static CSharpCompilation CreateCompilation(string source, params MetadataReference[]? extraRefs)
    {
        MetadataReference mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        MetadataReference systemRuntime = MetadataReference.CreateFromFile(typeof(Task).Assembly.Location);
        MetadataReference dualisAbstractions = MetadataReference.CreateFromFile(typeof(Dualis.CQRS.Commands.ICommand).Assembly.Location);

        var refs = ImmutableArray.Create(mscorlib, systemRuntime, dualisAbstractions);
        if (extraRefs is not null && extraRefs.Length > 0)
        {
            refs = refs.AddRange(extraRefs);
        }

        return CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static PortableExecutableReference CreateKernelDualizorAssembly()
    {
        // Minimal type presence; no need to implement interfaces for the guard.
        string kernel = "namespace Dualis { public sealed class Dualizor { } }";
        var comp = CSharpCompilation.Create(
            "Shared.Kernel",
            [CSharpSyntaxTree.ParseText(kernel, new CSharpParseOptions(LanguageVersion.Preview))],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var pe = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult emit = comp.Emit(pe);
        emit.Success.Should().BeTrue("shared kernel stub should compile");
        pe.Position = 0;
        return MetadataReference.CreateFromImage(pe.ToArray());
    }

    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content, Encoding.UTF8);
    }

    private static InMemoryAdditionalText CreateEditorConfig(bool enable)
    {
        // Prefer .globalconfig so values flow into GlobalOptions reliably.
        string content = string.Join(Environment.NewLine,
            "is_global = true",
            $"build_property.DualisEnableGenerator = {(enable ? "true" : "false")}"
        );
        return new InMemoryAdditionalText("/.globalconfig", content);
    }

    /// <summary>
    /// Ensures the generator does not emit <c>Dualis.Dualizor</c> when that type already exists.
    /// </summary>
    /// <remarks>
    /// Arrange: Create a referenced in-memory assembly named <c>Shared.Kernel</c> that defines <c>Dualis.Dualizor</c> and a source with a simple handler.
    /// Act: Run the generator with the assembly opt-in attribute enabled.
    /// Assert: No <c>Dualizor.g.cs</c> is generated.
    /// </remarks>
    [Fact]
    public void DoesNotGenerateDualizorWhenTypeAlreadyExists()
    {
        // Arrange: a simple handler so generation would normally happen
        string src = """
        using Dualis;
        using Dualis.CQRS.Queries;
        [assembly: EnableDualisGeneration]
        public sealed record Q : IQuery<string>;
        public sealed class H : Dualis.CQRS.Queries.IQueryHandler<Q, string>
        {
            public System.Threading.Tasks.Task<string> HandleAsync(Q q, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.FromResult("ok");
        }
        """;
        PortableExecutableReference kernel = CreateKernelDualizorAssembly();
        CSharpCompilation compilation = CreateCompilation(src, kernel);

        IIncrementalGenerator gen = new DualisGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(gen.AsSourceGenerator())
            .WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.Preview));

        // Act
        GeneratorDriverRunResult runResult = driver.RunGenerators(compilation).GetRunResult();

        // Assert: should not contain Dualizor.g.cs
        runResult.GeneratedTrees.Any(t => t.FilePath.EndsWith("Dualizor.g.cs", StringComparison.OrdinalIgnoreCase)).Should().BeFalse();
        // And no generator errors
        runResult.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error).Should().BeFalse();
    }

    /// <summary>
    /// Ensures both generators run and produce sources when opt-in attribute is present.
    /// </summary>
    /// <remarks>
    /// Arrange: Provide a simple <c>ICommand</c> and handler in the compilation and add the assembly attribute.
    /// Act: Run the <c>DualisGenerator</c> and <c>ServiceCollectionExtensionGenerator</c>.
    /// Assert: The generated trees contain both <c>Dualizor.g.cs</c> and <c>ServiceCollectionExtensions.g.cs</c> and there are no generator errors.
    /// </remarks>
    [Fact]
    public void GeneratesInHostWhenEnabled()
    {
        // Arrange
        string src = """
        using Dualis;
        using Dualis.CQRS.Commands;
        [assembly: EnableDualisGeneration]
        public sealed record C : ICommand;
        public sealed class H : Dualis.CQRS.Commands.ICommandHandler<C>
        {
            public System.Threading.Tasks.Task HandleAsync(C c, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
        }
        """;
        CSharpCompilation compilation = CreateCompilation(src);

        IIncrementalGenerator gen1 = new DualisGenerator();
        IIncrementalGenerator gen2 = new ServiceCollectionExtensionGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: [gen1.AsSourceGenerator(), gen2.AsSourceGenerator()],
                additionalTexts: [CreateEditorConfig(true)])
            .WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.Preview));

        // Act
        GeneratorDriverRunResult runResult = driver.RunGenerators(compilation).GetRunResult();

        // Assert
        runResult.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error).Should().BeFalse();
        runResult.GeneratedTrees.Any(t => t.FilePath.EndsWith("Dualizor.g.cs", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        runResult.GeneratedTrees.Any(t => t.FilePath.EndsWith("ServiceCollectionExtensions.g.cs", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    /// <summary>
    /// Ensures no Dualis-generated sources are produced when host gating is disabled via MSBuild property.
    /// </summary>
    /// <remarks>
    /// Arrange: Provide a simple <c>ICommand</c> and handler.
    /// Act: Run the generators with the gating property set to <c>false</c>.
    /// Assert: No <c>Dualizor.g.cs</c> or <c>ServiceCollectionExtensions.g.cs</c> files are generated.
    /// </remarks>
    [Fact]
    public void DoesNotGenerateWhenDisabledViaBuildProperty()
    {
        // Arrange
        string src = """
        using Dualis.CQRS.Commands;
        public sealed record C : ICommand;
        public sealed class H : Dualis.CQRS.Commands.ICommandHandler<C>
        {
            public System.Threading.Tasks.Task HandleAsync(C c, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
        }
        """;
        CSharpCompilation compilation = CreateCompilation(src);

        IIncrementalGenerator gen1 = new DualisGenerator();
        IIncrementalGenerator gen2 = new ServiceCollectionExtensionGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: [gen1.AsSourceGenerator(), gen2.AsSourceGenerator()],
                additionalTexts: [CreateEditorConfig(false)])
            .WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.Preview));

        // Act
        GeneratorDriverRunResult result = driver.RunGenerators(compilation).GetRunResult();

        // Assert: no Dualis-generated sources
        bool hasDualis = result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Dualizor.g.cs", StringComparison.OrdinalIgnoreCase)
            || t.FilePath.EndsWith("ServiceCollectionExtensions.g.cs", StringComparison.OrdinalIgnoreCase));
        hasDualis.Should().BeFalse();
        result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error).Should().BeFalse();
    }
}
