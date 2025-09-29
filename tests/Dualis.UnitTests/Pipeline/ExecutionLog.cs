using System.Collections.Concurrent;

namespace Dualis.UnitTests.Pipeline;

public sealed class ExecutionLog
{
    private readonly ConcurrentQueue<string> entries = new();

    public void Add(string entry) => entries.Enqueue(entry);

    public IReadOnlyList<string> Snapshot() => [.. entries];
}
