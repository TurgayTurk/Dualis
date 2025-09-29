using Dualis.CQRS.Commands;
using Dualis.CQRS.Queries;
using Dualis.Notifications;

namespace Dualis;

/// <summary>
/// Coordinates the dispatch of CQRS commands and queries to their registered handlers.
/// </summary>
/// <remarks>
/// Provides explicit <c>CommandAsync</c>/<c>QueryAsync</c> methods as well as unified <c>SendAsync</c> overloads
/// for convenience. Implementations are expected to resolve handlers via dependency injection.
/// </remarks>
public interface IDualizor
{
    /// <summary>
    /// Sends a query that returns a response to its corresponding handler.
    /// </summary>
    /// <typeparam name="TResponse">The query response type.</typeparam>
    /// <param name="query">The query instance to process.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that completes with the query response.</returns>
    Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command that returns a response to its corresponding handler.
    /// </summary>
    /// <typeparam name="TResponse">The command response type.</typeparam>
    /// <param name="command">The command instance to process.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that completes with the command response.</returns>
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command that does not produce a response to its corresponding handler.
    /// </summary>
    /// <param name="command">The command instance to process.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    Task SendAsync(ICommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a query that does not produce a response to its corresponding handler.
    /// </summary>
    /// <param name="query">The query instance to process.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    Task SendAsync(IQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a command that returns a response to its handler.
    /// </summary>
    /// <typeparam name="TResponse">The command response type.</typeparam>
    /// <param name="command">The command instance to process.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that completes with the command response.</returns>
    Task<TResponse> CommandAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a command that does not produce a response to its handler.
    /// </summary>
    /// <param name="command">The command instance to process.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    Task CommandAsync(ICommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a query that returns a response to its handler.
    /// </summary>
    /// <typeparam name="TResponse">The query response type.</typeparam>
    /// <param name="query">The query instance to process.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that completes with the query response.</returns>
    Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a query that does not produce a response to its handler.
    /// </summary>
    /// <param name="query">The query instance to process.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    Task QueryAsync(IQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a notification to all its handlers. Exceptions are aggregated.
    /// </summary>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(INotification notification, CancellationToken cancellationToken = default);
}
