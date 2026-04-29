namespace Dualis.CQRS;

/// <summary>
/// Handles exceptions thrown while processing a request that produces a response.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type returned by the request.</typeparam>
/// <typeparam name="TException">The exception type this handler can process.</typeparam>
public interface IRequestExceptionHandler<in TRequest, TResponse, in TException>
    where TRequest : IRequest<TResponse>
    where TException : Exception
{
    /// <summary>
    /// Handles a request exception and optionally marks it as handled with a response.
    /// </summary>
    /// <param name="request">The request being processed when the exception occurred.</param>
    /// <param name="exception">The thrown exception.</param>
    /// <param name="state">The exception handling state used to mark as handled and set response.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Handle(TRequest request, TException exception, RequestExceptionState<TResponse> state, CancellationToken cancellationToken);
}

/// <summary>
/// Executes side effects when a request throws an exception, without handling it.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TException">The exception type this action can process.</typeparam>
public interface IRequestExceptionAction<in TRequest, in TException>
    where TRequest : IRequest
    where TException : Exception
{
    /// <summary>
    /// Executes the exception action.
    /// </summary>
    /// <param name="request">The request being processed when the exception occurred.</param>
    /// <param name="exception">The thrown exception.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Execute(TRequest request, TException exception, CancellationToken cancellationToken);
}
