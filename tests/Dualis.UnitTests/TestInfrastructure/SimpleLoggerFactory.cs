using Microsoft.Extensions.Logging;

namespace Dualis.UnitTests.TestInfrastructure;

public sealed class SimpleLoggerFactory : ILoggerFactory
{
    private readonly List<ILoggerProvider> providers = [];

    public SimpleLoggerFactory(params ILoggerProvider[] providers)
    {
        if (providers is { Length: > 0 })
        {
            this.providers.AddRange(providers);
        }
    }

    public void AddProvider(ILoggerProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        providers.Add(provider);
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (providers.Count == 0)
        {
            return Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        }
        // For simplicity, return first provider's logger
        return providers[0].CreateLogger(categoryName);
    }

    public void Dispose()
    {
        foreach (ILoggerProvider p in providers)
        {
            p.Dispose();
        }
    }
}
