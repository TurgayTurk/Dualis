using Dualis.Notifications;

namespace Dualis.Pipeline;

/// <summary>
/// Represents the absence of a value. Use as the response type for "fire-and-forget"
/// requests and notifications.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// The singleton Unit value.
    /// </summary>
    public static readonly Unit Value;

    /// <inheritdoc />
    public override string ToString() => "()";

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc />
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <summary>
    /// Tests two <see cref="Unit"/> values for equality.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if both are equal; otherwise <see langword="false"/>.</returns>
    public static bool operator ==(Unit left, Unit right) => left.Equals(right);

    /// <summary>
    /// Tests two <see cref="Unit"/> values for inequality.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if both are not equal; otherwise <see langword="false"/>.</returns>
    public static bool operator !=(Unit left, Unit right) => !(left == right);
}

/// <summary>
/// Defines a unified pipeline behavior for any message (request or notification).
/// For requests that produce a response, set <typeparamref name="TResponse"/> to the response type.
/// For void commands/requests and notifications, use <see cref="Unit"/> as the response type.
/// </summary>
/// <typeparam name="TMessage">The message type being processed (request or notification).</typeparam>
/// <typeparam name="TResponse">The response type produced by the pipeline.</typeparam>
public interface IPipelineBehaviour<in TMessage, TResponse>
{
    /// <summary>
    /// Handles the specified <paramref name="message"/> and invokes the <paramref name="next"/> delegate to continue the pipeline.
    /// </summary>
    /// <param name="message">The message instance to process.</param>
    /// <param name="next">A delegate to invoke the next component in the pipeline.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that completes with the <typeparamref name="TResponse"/> produced by the pipeline.</returns>
    Task<TResponse> Handle(TMessage message, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

/// <summary>
/// Defines a pipeline behavior for requests that produce a response.
/// Behaviors can run before and/or after the next component in the pipeline.
/// </summary>
/// <typeparam name="TRequest">The request type being processed.</typeparam>
/// <typeparam name="TResponse">The response type produced by the pipeline.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
{
    /// <summary>
    /// Handles the specified <paramref name="request"/> and invokes the <paramref name="next"/> delegate to continue the pipeline.
    /// </summary>
    /// <param name="request">The request instance to process.</param>
    /// <param name="next">A delegate to invoke the next component in the pipeline.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that completes with the <typeparamref name="TResponse"/> produced by the pipeline.</returns>
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

/// <summary>
/// Defines a pipeline behavior for requests that do not produce a response.
/// Behaviors can run before and/or after the next component in the pipeline.
/// </summary>
/// <typeparam name="TRequest">The request type being processed.</typeparam>
public interface IPipelineBehavior<in TRequest>
{
    /// <summary>
    /// Handles the specified <paramref name="request"/> and invokes the <paramref name="next"/> delegate to continue the pipeline.
    /// </summary>
    /// <param name="request">The request instance to process.</param>
    /// <param name="next">A delegate to invoke the next component in the pipeline.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that completes when the pipeline processing finishes.</returns>
    Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken);
}

/// <summary>
/// Defines a pipeline behavior for notifications being published.
/// Behaviors can run before and/or after the next component in the pipeline.
/// Deprecated: use <see cref="IPipelineBehaviour{TNotification, Unit}"/> instead.
/// </summary>
/// <typeparam name="TNotification">The notification type being published.</typeparam>
#pragma warning disable S1133 // Do not forget to remove this deprecated code someday.
[Obsolete("INotificationBehavior is deprecated. Use IPipelineBehaviour<TNotification, Unit> instead.")]
public interface INotificationBehavior<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles the specified <paramref name="notification"/> and invokes the <paramref name="next"/> delegate to continue the pipeline.
    /// </summary>
    /// <param name="notification">The notification instance to process.</param>
    /// <param name="next">A delegate to invoke the next component in the pipeline.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that completes when the pipeline processing finishes.</returns>
    Task Handle(TNotification notification, NotificationPublishDelegate next, CancellationToken cancellationToken);
}
#pragma warning restore S1133 // Do not forget to remove this deprecated code someday.
