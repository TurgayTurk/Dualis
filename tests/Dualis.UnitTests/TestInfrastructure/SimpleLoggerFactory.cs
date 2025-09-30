using Microsoft.Extensions.Logging;

namespace Dualis.UnitTests.TestInfrastructure;

/// <summary>
/// Minimal logger factory to capture logs in tests using a provided <see cref="ILoggerProvider"/>.
/// </summary>
public sealed class SimpleLoggerFactory : ILoggerFactory
{
    private readonly List<ILoggerProvider> providers = [];

    /// <summary>
    /// Initializes the factory with an optional set of providers.
    /// </summary>
    public SimpleLoggerFactory(params ILoggerProvider[] providers)
    {
        if (providers is { Length: > 0 })
        {
            this.providers.AddRange(providers);
        }
    }

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        providers.Add(provider);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        if (providers.Count == 0)
        {
            return Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        }
        // For simplicity, return first provider's logger
        return providers[0].CreateLogger(categoryName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (ILoggerProvider p in providers)
        {
            p.Dispose();
        }
    }
}
