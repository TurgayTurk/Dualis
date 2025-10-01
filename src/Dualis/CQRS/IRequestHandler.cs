using System.Threading;
using System.Threading.Tasks;

namespace Dualis.CQRS;

/// <summary>
/// Handles a request that does not produce a response.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
public interface IRequestHandler<in TRequest>
    where TRequest : IRequest
{
    /// <summary>
    /// Processes the specified request.
    /// </summary>
    /// <param name="request">The request instance to handle.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    Task HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles a request that produces a response.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Processes the specified request and returns a response.
    /// </summary>
    /// <param name="request">The request instance to handle.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that completes with the request response.</returns>
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
