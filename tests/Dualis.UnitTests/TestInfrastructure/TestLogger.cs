using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Dualis.UnitTests.TestInfrastructure;

public sealed class TestLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentBag<LogEntry> entries = [];

    public IReadOnlyCollection<LogEntry> Entries => entries;

    public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, entries);

    public void Dispose() { }

    public sealed record LogEntry(string Category, LogLevel Level, EventId EventId, Exception? Exception, string Message);

    private sealed class TestLogger(string category, ConcurrentBag<LogEntry> sink) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => sink.Add(new LogEntry(category, logLevel, eventId, exception, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
