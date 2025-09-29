namespace Dualis.Notifications;

/// <summary>
/// Context passed to notification publishers to guide publish semantics.
/// </summary>
public sealed class NotificationPublishContext(
    NotificationFailureBehavior failureBehavior,
    int? maxDegreeOfParallelism)
{
    /// <summary>
    /// Gets the configured behavior to apply when a notification handler throws an exception.
    /// </summary>
    public NotificationFailureBehavior FailureBehavior { get; } = failureBehavior;

    /// <summary>
    /// Gets the optional maximum degree of parallelism for publishing.
    /// If <c>null</c>, the publisher selects a default value.
    /// </summary>
    public int? MaxDegreeOfParallelism { get; } = maxDegreeOfParallelism;
}
