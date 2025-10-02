using Dualis.Notifications;
using Dualis.UnitTests.TestInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dualis.UnitTests.Notifications;

/// <summary>
/// Tests for <see cref="ParallelWhenAllNotificationPublisher"/> covering concurrency limits and failure policies.
/// </summary>
public sealed class ParallelWhenAllNotificationPublisherTests
{
    private sealed record TestNote(int Value) : INotification;

    /// <summary>
    /// Validates that the publisher respects <see cref="NotificationPublishContext.MaxDegreeOfParallelism"/>.
    /// </summary>
    /// <remarks>
    /// Arrange: Create 16 handlers that record concurrent activity and set DOP=2.
    /// Act: Publish a note and capture the maximum observed concurrency.
    /// Assert: The maximum does not exceed the configured DOP.
    /// </remarks>
    [Fact]
    public async Task Honors_MaxDegreeOfParallelism()
    {
        int dop = 2;
        int active = 0;
        int maxObserved = 0;

        // 16 handlers with small delays to overlap
        List<INotificationHandler<TestNote>> handlers = new(capacity: 16);
        for (int i = 0; i < 16; i++)
        {
            handlers.Add(new DelegateNotificationHandler<TestNote>(async (_, ct) =>
            {
                int now = Interlocked.Increment(ref active);
                int snapshot;
                while (true)
                {
                    int currentMax = maxObserved;
                    snapshot = Math.Max(currentMax, now);
                    if (Interlocked.CompareExchange(ref maxObserved, snapshot, currentMax) == currentMax)
                    {
                        break;
                    }
                }

                await Task.Delay(50, ct);
                Interlocked.Decrement(ref active);
            }));
        }

        ParallelWhenAllNotificationPublisher publisher = new(NullLogger<ParallelWhenAllNotificationPublisher>.Instance, NullLoggerFactory.Instance);
        NotificationPublishContext context = new(NotificationFailureBehavior.ContinueAndAggregate, dop);

        await publisher.Publish(new TestNote(1), handlers, context, CancellationToken.None);

        maxObserved.Should().BeLessOrEqualTo(dop, "publisher should constrain concurrency to MaxDegreeOfParallelism");
    }

    /// <summary>
    /// Ensures <c>ContinueAndAggregate</c> aggregates all failures and throws a single <see cref="AggregateException"/>.
    /// </summary>
    /// <remarks>
    /// Arrange: Two failing handlers and one successful handler.
    /// Act: Publish with <c>ContinueAndAggregate</c>.
    /// Assert: Thrown exception contains both inner exceptions.
    /// </remarks>
    [Fact]
    public async Task ContinueAndAggregate_aggregates_all_failures()
    {
        List<INotificationHandler<TestNote>> handlers =
        [
            new DelegateNotificationHandler<TestNote>((_, _) => throw new InvalidOperationException("A")),
            new DelegateNotificationHandler<TestNote>((_, _) => Task.CompletedTask),
            new DelegateNotificationHandler<TestNote>((_, _) => throw new ApplicationException("B")),
        ];

        ParallelWhenAllNotificationPublisher publisher = new();
        NotificationPublishContext context = new(NotificationFailureBehavior.ContinueAndAggregate, Environment.ProcessorCount);

        Func<Task> act = () => publisher.Publish(new TestNote(42), handlers, context, CancellationToken.None);

        FluentAssertions.Specialized.ExceptionAssertions<AggregateException> ex = await act.Should().ThrowAsync<AggregateException>();
        ex.Which.InnerExceptions.Should().HaveCount(2);
        ex.Which.InnerExceptions.Select(e => e.Message).Should().Contain(["A", "B"]);
    }

    /// <summary>
    /// Verifies <c>ContinueAndLog</c> logs failures and does not throw.
    /// </summary>
    /// <remarks>
    /// Arrange: Two failing handlers and a test logger to capture entries.
    /// Act: Publish with <c>ContinueAndLog</c>.
    /// Assert: Exactly two error log entries are recorded.
    /// </remarks>
    [Fact]
    public async Task ContinueAndLog_logs_and_swallows()
    {
        TestLoggerProvider provider = new();
        using SimpleLoggerFactory lf = new(provider);

        ILogger<ParallelWhenAllNotificationPublisher> logger = lf.CreateLogger<ParallelWhenAllNotificationPublisher>();
        ParallelWhenAllNotificationPublisher publisher = new(logger, lf);

        List<INotificationHandler<TestNote>> handlers =
        [
            new DelegateNotificationHandler<TestNote>((_, _) => throw new InvalidOperationException("oops-1")),
            new DelegateNotificationHandler<TestNote>((_, _) => Task.CompletedTask),
            new DelegateNotificationHandler<TestNote>((_, _) => throw new InvalidOperationException("oops-2")),
        ];

        NotificationPublishContext context = new(NotificationFailureBehavior.ContinueAndLog, Environment.ProcessorCount);

        await publisher.Publish(new TestNote(7), handlers, context, CancellationToken.None);

        provider.Entries.Count(e => e.Level >= LogLevel.Error && e.Exception is not null).Should().Be(2);
    }

    /// <summary>
    /// Ensures <c>StopOnFirstException</c> falls back to sequential execution and stops at the first failure.
    /// </summary>
    /// <remarks>
    /// Arrange: Three handlers where the second throws.
    /// Act: Publish with <c>StopOnFirstException</c>.
    /// Assert: An exception is thrown and only the first two handlers are invoked.
    /// </remarks>
    [Fact]
    public async Task StopOnFirstException_runs_sequentially_and_stops_early()
    {
        int invoked = 0;
        List<INotificationHandler<TestNote>> handlers =
        [
            new DelegateNotificationHandler<TestNote>((_, _) => { Interlocked.Increment(ref invoked); return Task.CompletedTask; }),
            new DelegateNotificationHandler<TestNote>((_, _) => { Interlocked.Increment(ref invoked); throw new InvalidOperationException("stop"); }),
            new DelegateNotificationHandler<TestNote>((_, _) => { Interlocked.Increment(ref invoked); return Task.CompletedTask; }),
        ];

        ParallelWhenAllNotificationPublisher publisher = new();
        NotificationPublishContext context = new(NotificationFailureBehavior.StopOnFirstException, Environment.ProcessorCount);

        Func<Task> act = () => publisher.Publish(new TestNote(0), handlers, context, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        invoked.Should().Be(2, "sequential fallback should stop after first failure");
    }
}
