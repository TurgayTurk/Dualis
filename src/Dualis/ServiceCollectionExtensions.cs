using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dualis;

/// <summary>
/// Public entry point for configuring Dualis services.
/// Delegates to the source-generated registration in the calling assembly when available,
/// otherwise falls back to minimal infrastructure registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Dualis services and discovered handlers/behaviors.
    /// If the source generator ran in the calling assembly, this delegates to the generated internal method.
    /// Otherwise, it registers the baseline infrastructure so the application still functions.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configure">Optional configuration for Dualis options.</param>
    /// <returns>The same service collection instance.</returns>
    public static IServiceCollection AddDualis(this IServiceCollection services, Action<DualizorOptions>? configure = null)
    {
        var calling = Assembly.GetCallingAssembly();
        IServiceCollection? result = TryInvokeGenerated(calling, services, configure);
        if (result is not null)
        {
            return result;
        }

        var entry = Assembly.GetEntryAssembly();
        result = TryInvokeGenerated(entry, services, configure);
        if (result is not null)
        {
            return result;
        }

        IServiceCollection? fromScan = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(asm => TryInvokeGenerated(asm, services, configure))
            .FirstOrDefault(sc => sc is not null);
        if (fromScan is not null)
        {
            return fromScan;
        }

        return DependencyInjection.ServiceCollectionExtensions.AddDualizor(services, configure);
    }

    [SuppressMessage("Security", "S3011", Justification = "Intentional access to internal generated AddDualis method in the app assembly.")]
    private static IServiceCollection? TryInvokeGenerated(Assembly? asm, IServiceCollection services, Action<DualizorOptions>? configure)
    {
        if (asm is null)
        {
            return null;
        }

        // Try new namespace first, then fallback to old for backward compatibility
        Type? type = asm.GetType("Dualis.Generated.ServiceCollectionExtensions", throwOnError: false, ignoreCase: false)
                   ?? asm.GetType("Dualis.ServiceCollectionExtensions", throwOnError: false, ignoreCase: false);
        if (type is null)
        {
            return null;
        }

#pragma warning disable S3011
        // The generated method is 'internal static IServiceCollection AddDualis(this IServiceCollection, Action<DualizorOptions>?)'
        MethodInfo? mi = type.GetMethod(
            name: "AddDualis",
            bindingAttr: BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(IServiceCollection), typeof(Action<DualizorOptions>)],
            modifiers: null);
#pragma warning restore S3011

        if (mi is null)
        {
#pragma warning disable S3011
            MethodInfo? candidate = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .FirstOrDefault(m => string.Equals(m.Name, "AddDualis", StringComparison.Ordinal) &&
                                     m.GetParameters() is { Length: 2 } p &&
                                     typeof(IServiceCollection).IsAssignableFrom(p[0].ParameterType));
#pragma warning restore S3011
            return candidate is null
                ? null
                : (IServiceCollection?)candidate.Invoke(null, [services, configure]);
        }

        return (IServiceCollection?)mi.Invoke(null, [services, configure]);
    }
}
