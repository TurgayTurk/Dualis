namespace Dualis.Pipeline;

/// <summary>
/// Represents the absence of a value. Use as the response type for fire-and-forget
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
    public static bool operator ==(Unit left, Unit right) => left.Equals(right);

    /// <summary>
    /// Tests two <see cref="Unit"/> values for inequality.
    /// </summary>
    public static bool operator !=(Unit left, Unit right) => !(left == right);
}

/// <summary>
/// Unified pipeline behavior for any message (request or notification).
/// Implement <see cref="Handle(TMessage, RequestHandlerDelegate{TResponse}, CancellationToken)"/>.
/// </summary>
/// <typeparam name="TMessage">The message type being processed.</typeparam>
/// <typeparam name="TResponse">The response type produced by the pipeline.</typeparam>
public interface IPipelineBehaviour<in TMessage, TResponse>
{
    /// <summary>
    /// Handles the specified <paramref name="message"/> with a mandatory token.
    /// </summary>
    /// <param name="message">The message instance to process.</param>
    /// <param name="next">Delegate to invoke the next pipeline component.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that completes with the pipeline response.</returns>
    Task<TResponse> Handle(TMessage message, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

/// <summary>
/// Pipeline behavior for requests that produce a response.
/// Implement <see cref="Handle(TRequest, RequestHandlerDelegate{TResponse}, CancellationToken)"/>.
/// </summary>
/// <typeparam name="TRequest">The request type being processed.</typeparam>
/// <typeparam name="TResponse">The response type produced by the pipeline.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
{
    /// <summary>
    /// Handles the specified <paramref name="request"/> with a mandatory token.
    /// </summary>
    /// <param name="request">The request instance to process.</param>
    /// <param name="next">Delegate to invoke the next pipeline component.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that completes with the pipeline response.</returns>
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

/// <summary>
/// Pipeline behavior for requests that do not produce a response.
/// Implement <see cref="Handle(TRequest, RequestHandlerDelegate, CancellationToken)"/>.
/// </summary>
/// <typeparam name="TRequest">The request type being processed.</typeparam>
public interface IPipelineBehavior<in TRequest>
{
    /// <summary>
    /// Handles the specified <paramref name="request"/> with a mandatory token.
    /// </summary>
    /// <param name="request">The request instance to process.</param>
    /// <param name="next">Delegate to invoke the next pipeline component.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task representing the operation.</returns>
    Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken);
}
