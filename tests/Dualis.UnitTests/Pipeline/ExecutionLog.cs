using System.Collections.Concurrent;

namespace Dualis.UnitTests.Pipeline;

/// <summary>
/// Simple in-memory collector used by pipeline tests to assert execution order.
/// </summary>
public sealed class ExecutionLog
{
    private readonly ConcurrentQueue<string> entries = new();

    /// <summary>
    /// Appends an entry to the log.
    /// </summary>
    public void Add(string entry) => entries.Enqueue(entry);

    /// <summary>
    /// Returns a snapshot of the entries in FIFO order.
    /// </summary>
    public IReadOnlyList<string> Snapshot() => [.. entries];
}
