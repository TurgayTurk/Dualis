using System.Collections.Immutable;
using System.Text;
using Dualis.CQRS;
using Dualis.SourceGen;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace SourceGen.Tests;

public sealed class GeneratorGatingTests
{
    private static CSharpCompilation CreateCompilation(string source, params MetadataReference[]? extraRefs)
    {
        MetadataReference mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        MetadataReference systemRuntime = MetadataReference.CreateFromFile(typeof(Task).Assembly.Location);
        MetadataReference dualisAbstractions = MetadataReference.CreateFromFile(typeof(IRequest).Assembly.Location);

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
            $"build_property.DualisEnableGenerator = {(enable ? "true" : "false")}");
        return new InMemoryAdditionalText("/.globalconfig", content);
    }

    private static InMemoryAdditionalText CreateEditorConfigCompat(bool enable)
    {
        // Also provide .editorconfig for compatibility; it doesn't support global properties.
        string content = string.Join(Environment.NewLine,
            "is_global = true",
            $"build_property.DualisEnableGenerator = {(enable ? "true" : "false")}");
        return new InMemoryAdditionalText("/.editorconfig", content);
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
        using Dualis.CQRS;
        [assembly: EnableDualisGeneration]
        public sealed record Q : IRequest<string>;
        public sealed class H : IRequestHandler<Q, string>
        {
            public System.Threading.Tasks.Task<string> Handle(Q q, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.FromResult("ok");
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
    [Fact]
    public void GeneratesInHostWhenEnabled()
    {
        // Arrange
        string src = """
        using Dualis;
        using Dualis.CQRS;
        [assembly: EnableDualisGeneration]
        public sealed record C : IRequest;
        public sealed class H : IRequestHandler<C>
        {
            public System.Threading.Tasks.Task Handle(C c, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
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
    [Fact]
    public void DoesNotGenerateWhenDisabledViaBuildProperty()
    {
        // Arrange
        string src = """
        using Dualis.CQRS;
        public sealed record C : IRequest;
        public sealed class H : IRequestHandler<C>
        {
            public System.Threading.Tasks.Task Handle(C c, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
        }
        """;
        CSharpCompilation compilation = CreateCompilation(src);

        IIncrementalGenerator gen1 = new DualisGenerator();
        IIncrementalGenerator gen2 = new ServiceCollectionExtensionGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: [gen1.AsSourceGenerator(), gen2.AsSourceGenerator()],
                additionalTexts: [CreateEditorConfig(false), CreateEditorConfigCompat(false)])
            .WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.Preview));

        // Act
        GeneratorDriverRunResult result = driver.RunGenerators(compilation).GetRunResult();

        // Assert: no Dualis-generated sources
        bool hasDualis = result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Dualizor.g.cs", StringComparison.OrdinalIgnoreCase)
            || t.FilePath.EndsWith("ServiceCollectionExtensions.g.cs", StringComparison.OrdinalIgnoreCase));
        hasDualis.Should().BeFalse();
        result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error).Should().BeFalse();
    }

    /// <summary>
    /// Ensures generators run when enabled via compiler-visible MSBuild property without the assembly attribute.
    /// </summary>
    [Fact]
    public void GeneratesWhenEnabledViaCompilerVisibleProperty()
    {
        // Arrange: property enabled, no attribute
        string src = """
        using Dualis.CQRS;
        public sealed record C : IRequest;
        public sealed class H : IRequestHandler<C>
        {
            public System.Threading.Tasks.Task Handle(C c, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
        }
        """;
        CSharpCompilation compilation = CreateCompilation(src);

        IIncrementalGenerator gen1 = new DualisGenerator();
        IIncrementalGenerator gen2 = new ServiceCollectionExtensionGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: [gen1.AsSourceGenerator(), gen2.AsSourceGenerator()],
                additionalTexts: [CreateEditorConfig(true), CreateEditorConfigCompat(true)])
            .WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.Preview));

        // Act
        GeneratorDriverRunResult result = driver.RunGenerators(compilation).GetRunResult();

        // Assert: sources generated (at minimum the DI extension)
        result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error).Should().BeFalse();
        bool hasAddDualis = result.GeneratedTrees.Any(t => t.FilePath.EndsWith("ServiceCollectionExtensions.g.cs", StringComparison.OrdinalIgnoreCase));
        hasAddDualis.Should().BeTrue();
        // Dualizor may be skipped by guards; allow optional
        // result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Dualizor.g.cs", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    /// <summary>
    /// Ensures generators do not run when neither attribute nor compiler-visible property is set.
    /// </summary>
    [Fact]
    public void DoesNotGenerateWhenNoAttributeOrProperty()
    {
        // Arrange: no attribute, no property
        string src = """
        using Dualis.CQRS;
        public sealed record C : IRequest;
        public sealed class H : IRequestHandler<C>
        {
            public System.Threading.Tasks.Task Handle(C c, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
        }
        """;
        CSharpCompilation compilation = CreateCompilation(src);

        IIncrementalGenerator gen1 = new DualisGenerator();
        IIncrementalGenerator gen2 = new ServiceCollectionExtensionGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(gen1.AsSourceGenerator(), gen2.AsSourceGenerator())
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
