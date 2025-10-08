using System.Collections.Immutable;
using Dualis.Analyzer.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Dualis.Analyzer.Analyzers;

/// <summary>
/// Reports when ISender/IDualizor.Send is called with a type that does not implement IRequest/IRequest&lt;T&gt;.
/// Uses IOperation for low overhead and accurate argument analysis.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidSendArgumentAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [Descriptors.DULIS004_InvalidSendArgument];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            Compilation compilation = compilationContext.Compilation;
            INamedTypeSymbol? isender = compilation.GetTypeByMetadataName("Dualis.ISender");
            INamedTypeSymbol? idualizor = compilation.GetTypeByMetadataName("Dualis.IDualizor");
            INamedTypeSymbol? irequestT = compilation.GetTypeByMetadataName("Dualis.CQRS.IRequest`1");
            INamedTypeSymbol? irequest = compilation.GetTypeByMetadataName("Dualis.CQRS.IRequest");
            if (isender is null || idualizor is null || irequestT is null || irequest is null)
            {
                return;
            }

            compilationContext.RegisterOperationAction(ctx =>
            {
                if (ctx.Operation is not IInvocationOperation op)
                {
                    return;
                }

                IMethodSymbol method = op.TargetMethod;
                if (!string.Equals(method.Name, "Send", StringComparison.Ordinal))
                {
                    return;
                }

                INamedTypeSymbol? containing = method.ContainingType;
                if (containing is null)
                {
                    return;
                }

                if (!SymbolEqualityComparer.Default.Equals(containing, isender) && !SymbolEqualityComparer.Default.Equals(containing, idualizor))
                {
                    return;
                }

                if (op.Arguments.Length == 0)
                {
                    return;
                }

                // Be resilient to conversions and incomplete code by inspecting operand/converted types when available.
                ITypeSymbol? argType = GetArgType(op.Arguments[0].Value);
                if (argType is null)
                {
                    return;
                }

                bool ok = false;
                if (argType is INamedTypeSymbol nts && (nts.Equals(irequest, SymbolEqualityComparer.Default) || nts.OriginalDefinition.Equals(irequestT, SymbolEqualityComparer.Default)))
                {
                    ok = true;
                }

                if (!ok)
                {
                    ImmutableArray<INamedTypeSymbol> ifaces = argType.AllInterfaces;
                    for (int i = 0; i < ifaces.Length; i++)
                    {
                        INamedTypeSymbol iface = ifaces[i];
                        if (iface.OriginalDefinition.Equals(irequestT, SymbolEqualityComparer.Default) || iface.Equals(irequest, SymbolEqualityComparer.Default))
                        {
                            ok = true;
                            break;
                        }
                    }
                }

                if (!ok)
                {
                    string display = argType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                    ctx.ReportDiagnostic(Diagnostic.Create(Descriptors.DULIS004_InvalidSendArgument, op.Syntax.GetLocation(), display));
                }
            }, OperationKind.Invocation);
        });
    }

    private static ITypeSymbol? GetArgType(IOperation op)
    {
        // Use explicit patterns to satisfy style rules; no switch expression.
        if (op is IArgumentOperation arg)
        {
            return GetArgType(arg.Value);
        }

        if (op is IConversionOperation conv && conv.Operand is not null)
        {
            return conv.Operand.Type ?? conv.Type;
        }

        return op.Type;
    }
}
