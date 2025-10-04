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

public sealed class CrossAssemblyDiscoveryTests
{
    [Fact]
    public void AddDualisRegistersFromHostAndReferencedAssembliesForAllCategories()
    {
        // Arrange: referenced library with query/command/notification handlers and pipeline behaviors
        PortableExecutableReference libRef = CreateLibraryWithHandlersAndBehaviors();

        // Host compilation with its own types
        string hostSrc = """
        using Dualis;
        using Dualis.CQRS;
        using Dualis.Notifications;
        using Dualis.Pipeline;

        [assembly: EnableDualisGeneration]

        namespace Host;

        public sealed record Q2 : IRequest<string>;
        public sealed class Q2Handler : IRequestHandler<Q2, string>
        {
            public System.Threading.Tasks.Task<string> Handle(Q2 q, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.FromResult("ok-host-q");
        }

        public sealed record C2 : IRequest;
        public sealed class C2Handler : IRequestHandler<C2>
        {
            public System.Threading.Tasks.Task Handle(C2 c, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
        }

        public sealed record N2 : INotification;
        public sealed class N2Handler : Dualis.Notifications.INotificationHandler<N2>
        {
            public System.Threading.Tasks.Task Handle(N2 note, System.Threading.CancellationToken ct = default) => System.Threading.Tasks.Task.CompletedTask;
        }

        public sealed class Order2<TReq, TRes> : IPipelineBehavior<TReq, TRes>
        {
            public System.Threading.Tasks.Task<TRes> Handle(TReq request, RequestHandlerDelegate<TRes> next, System.Threading.CancellationToken ct) => next(ct);
        }

        public sealed class VoidOrder2<TReq> : IPipelineBehavior<TReq>
        {
            public System.Threading.Tasks.Task Handle(TReq request, RequestHandlerDelegate next, System.Threading.CancellationToken ct) => next(ct);
        }
        """;

        CSharpCompilation host = CreateHostCompilation(hostSrc, libRef);

        IIncrementalGenerator gen = new ServiceCollectionExtensionGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: [gen.AsSourceGenerator()],
                additionalTexts: [CreateEditorConfig(enable: true)])
            .WithUpdatedParseOptions(new CSharpParseOptions(LanguageVersion.Preview));

        // Act
        GeneratorDriverRunResult run = driver.RunGenerators(host).GetRunResult();

        // Assert
        run.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error).Should().BeFalse();

        string addDualis = run.Results
            .SelectMany(r => r.GeneratedSources)
            .Where(gs => gs.HintName.EndsWith("ServiceCollectionExtensions.g.cs", StringComparison.OrdinalIgnoreCase))
            .Select(gs => gs.SourceText.ToString())
            .FirstOrDefault() ?? string.Empty;

        addDualis.Should().NotBeNullOrEmpty("the DI extension should be generated when enabled");

        // Host registrations
        addDualis.Should().Contain("IRequestHandler<global::Host.Q2, string>, global::Host.Q2Handler>");
        addDualis.Should().Contain("IRequestHandler<global::Host.C2>, global::Host.C2Handler>");
        addDualis.Should().Contain("INotificationHandler<global::Host.N2>, global::Host.N2Handler>");
        addDualis.Should().Contain("ServiceDescriptor.Scoped(typeof(global::Dualis.Pipeline.IPipelineBehavior<,>), typeof(global::Host.Order2<,>))");
        addDualis.Should().Contain("ServiceDescriptor.Scoped(typeof(global::Dualis.Pipeline.IPipelineBehavior<>), typeof(global::Host.VoidOrder2<>))");

        // Referenced library registrations
        addDualis.Should().Contain("IRequestHandler<global::Lib.Q, string>, global::Lib.QHandler>");
        addDualis.Should().Contain("IRequestHandler<global::Lib.C>, global::Lib.CHandler>");
        addDualis.Should().Contain("INotificationHandler<global::Lib.N>, global::Lib.NHandler>");
        addDualis.Should().Contain("ServiceDescriptor.Scoped(typeof(global::Dualis.Pipeline.IPipelineBehavior<,>), typeof(global::Lib.Order<,>))");
        addDualis.Should().Contain("ServiceDescriptor.Scoped(typeof(global::Dualis.Pipeline.IPipelineBehavior<>), typeof(global::Lib.VoidOrder<>))");
    }

    private static ImmutableArray<MetadataReference> GetFrameworkReferences()
    {
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrEmpty(tpa))
        {
            return [];
        }

        ImmutableArray<MetadataReference>.Builder builder = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (string path in tpa.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                builder.Add(MetadataReference.CreateFromFile(path));
            }
        }
        return builder.ToImmutable();
    }

    private static CSharpCompilation CreateHostCompilation(string source, params MetadataReference[] extraRefs)
    {
        ImmutableArray<MetadataReference> frameworkRefs = GetFrameworkReferences();
        MetadataReference dualisAbstractions = MetadataReference.CreateFromFile(typeof(IRequest).Assembly.Location);

        ImmutableArray<MetadataReference> refs = frameworkRefs.Add(dualisAbstractions);
        if (extraRefs is not null && extraRefs.Length > 0)
        {
            refs = refs.AddRange(extraRefs);
        }

        return CSharpCompilation.Create(
            assemblyName: "Host",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static PortableExecutableReference CreateLibraryWithHandlersAndBehaviors()
    {
        string libSrc = """
        using System.Threading;
        using System.Threading.Tasks;
        using Dualis.Pipeline;

        namespace Lib;

        public sealed record Q : Dualis.CQRS.IRequest<string>;
        public sealed class QHandler : Dualis.CQRS.IRequestHandler<Q, string>
        {
            public Task<string> Handle(Q q, CancellationToken cancellationToken = default) => Task.FromResult("ok-lib-q");
        }

        public sealed record C : Dualis.CQRS.IRequest;
        public sealed class CHandler : Dualis.CQRS.IRequestHandler<C>
        {
            public Task Handle(C c, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        public sealed record N : Dualis.Notifications.INotification;
        public sealed class NHandler : Dualis.Notifications.INotificationHandler<N>
        {
            public Task Handle(N n, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        // Open generic behaviors so generator emits open registrations
        public sealed class Order<TReq, TRes> : IPipelineBehavior<TReq, TRes>
        {
            public Task<TRes> Handle(TReq request, RequestHandlerDelegate<TRes> next, CancellationToken ct) => next(ct);
        }

        public sealed class VoidOrder<TReq> : IPipelineBehavior<TReq>
        {
            public Task Handle(TReq request, RequestHandlerDelegate next, CancellationToken ct) => next(ct);
        }
        """;

        ImmutableArray<MetadataReference> frameworkRefs = GetFrameworkReferences();
        MetadataReference dualisAbstractions = MetadataReference.CreateFromFile(typeof(IRequest).Assembly.Location);

        var lib = CSharpCompilation.Create(
            assemblyName: "Lib",
            syntaxTrees: [CSharpSyntaxTree.ParseText(libSrc, new CSharpParseOptions(LanguageVersion.Preview))],
            references: frameworkRefs.Add(dualisAbstractions),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using MemoryStream pe = new();
        Microsoft.CodeAnalysis.Emit.EmitResult emit = lib.Emit(pe);
        emit.Success.Should().BeTrue("Library compile failed. Diagnostics:\n" + string.Join("\n", lib.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)));
        pe.Position = 0;
        return MetadataReference.CreateFromImage(pe.ToArray());
    }

    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText GetText(CancellationToken cancellationToken = default)
            => SourceText.From(content, Encoding.UTF8);
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
