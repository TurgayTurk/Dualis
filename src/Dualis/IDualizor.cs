using Dualis.CQRS;
using Dualis.Notifications;

namespace Dualis;

/// <summary>
/// Coordinates the dispatch of requests to their registered handlers and publishes notifications.
/// </summary>
public interface IDualizor
{
    /// <summary>
    /// Sends a request that returns a response to its corresponding handler.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request instance to process.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that completes with the response.</returns>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request that does not return a response to its corresponding handler.
    /// </summary>
    /// <param name="request">The request instance to process.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    Task Send(IRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a notification to all its handlers. Exceptions are aggregated.
    /// </summary>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Publish(INotification notification, CancellationToken cancellationToken = default);
}
