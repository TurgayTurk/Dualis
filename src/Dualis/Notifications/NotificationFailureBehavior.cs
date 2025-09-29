namespace Dualis.Notifications;

/// <summary>
/// Controls how the publisher reacts to handler exceptions.
/// </summary>
public enum NotificationFailureBehavior
{
    /// <summary>
    /// Stops at the first handler that throws and rethrows that exception.
    /// </summary>
    StopOnFirstException = 0,

    /// <summary>
    /// Invokes every handler and aggregates exceptions into an <see cref="AggregateException"/>.
    /// </summary>
    ContinueAndAggregate = 1,

    /// <summary>
    /// Invokes every handler, logs exceptions (publisher dependent) and does not rethrow.
    /// </summary>
    ContinueAndLog = 2
}
