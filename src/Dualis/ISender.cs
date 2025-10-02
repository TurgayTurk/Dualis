using Dualis.CQRS;

namespace Dualis;

/// <summary>
/// Mediator interface focused on request/response dispatch, similar to MediatR's <c>ISender</c>.
/// </summary>
public interface ISender
{
    /// <summary>
    /// Sends a request that produces a response to its registered handler.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request that does not produce a response to its registered handler.
    /// </summary>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Send(IRequest request, CancellationToken cancellationToken = default);
}
