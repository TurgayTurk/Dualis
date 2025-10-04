using System.Diagnostics.CodeAnalysis;
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

        // No generated registration found in host assemblies; use runtime registration.
        return DependencyInjection.ServiceCollectionExtensions.AddDualizor(services, configure);
    }

    [SuppressMessage("Security", "S3011", Justification = "Intentional access to internal generated AddDualis method in the host assembly.")]
    private static IServiceCollection? TryInvokeGenerated(Assembly? asm, IServiceCollection services, Action<DualizorOptions>? configure)
    {
        if (asm is null)
        {
            return null;
        }

#pragma warning disable S3011
        // Only look for the new non-extension internal method generated under Dualis.Generated
        Type? type = asm.GetType("Dualis.Generated.ServiceCollectionExtensions", throwOnError: false, ignoreCase: false);
        if (type is null)
        {
            return null;
        }

        MethodInfo? mi = type.GetMethod(
            name: "AddDualis",
            bindingAttr: BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(IServiceCollection), typeof(Action<DualizorOptions>)],
            modifiers: null);
        // Fallback: find by name and first parameter type IServiceCollection
        mi ??= type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                 .FirstOrDefault(m => string.Equals(m.Name, "AddDualis", StringComparison.Ordinal) &&
                                      m.GetParameters() is { Length: 2 } p &&
                                      typeof(IServiceCollection).IsAssignableFrom(p[0].ParameterType));
#pragma warning restore S3011

        return mi is null ? null : (IServiceCollection?)mi.Invoke(null, [services, configure]);
    }
}
