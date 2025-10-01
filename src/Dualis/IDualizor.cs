using Dualis.CQRS;
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
    // SendAsync overloads (keep adjacent)
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
    /// Unified request overload that dispatches either a command or a query with response.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        => request switch
        {
            ICommand<TResponse> c => SendAsync(c, cancellationToken),
            IQuery<TResponse> q => SendAsync(q, cancellationToken),
            _ => Task.FromException<TResponse>(new InvalidOperationException($"Unknown request type: {request.GetType().Name}")),
        };

    /// <summary>
    /// Unified request overload that dispatches either a command or a query without response.
    /// </summary>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(IRequest request, CancellationToken cancellationToken = default)
        => request switch
        {
            ICommand c => SendAsync(c, cancellationToken),
            IQuery q => SendAsync(q, cancellationToken),
            _ => Task.FromException(new InvalidOperationException($"Unknown request type: {request.GetType().Name}")),
        };

    // Explicit verbs (separate from Send/SendAsync)
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

    // Send overloads (keep adjacent)
    /// <summary>
    /// Sends a command that produces a response to its registered handler.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command that does not produce a response to its registered handler.
    /// </summary>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Send(ICommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a query that produces a response to its registered handler.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="query">The query instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a query that does not produce a response to its registered handler.
    /// </summary>
    /// <param name="query">The query instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Send(IQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Migration convenience: synchronous-named unified request overloads.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        => SendAsync(request, cancellationToken);

    /// <summary>
    /// Migration convenience: synchronous-named unified request overload.
    /// </summary>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Send(IRequest request, CancellationToken cancellationToken = default)
        => SendAsync(request, cancellationToken);

    /// <summary>
    /// Publishes a notification to all its handlers. Exceptions are aggregated.
    /// </summary>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(INotification notification, CancellationToken cancellationToken = default);
}
