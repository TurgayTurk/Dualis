using Dualis.CQRS.Commands;
using Dualis.CQRS.Queries;

namespace Dualis;

/// <summary>
/// Mediator interface focused on request/response (commands and queries), similar to MediatR's <c>ISender</c>.
/// </summary>
public interface ISender
{
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
}
