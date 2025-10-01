using Dualis.CQRS;

namespace Dualis;

/// <summary>
/// Mediator interface focused on request/response (commands and queries), similar to MediatR's <c>ISender</c>.
/// </summary>
public interface ISender
{
    // SendAsync overloads (keep adjacent)
    /// <summary>
    /// Sends a command that produces a response to its registered handler.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command that does not produce a response to its registered handler.
    /// </summary>
    /// <param name="command">The command instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(ICommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a query that produces a response to its registered handler.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="query">The query instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a query that does not produce a response to its registered handler.
    /// </summary>
    /// <param name="query">The query instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
}
