namespace Dualis.Pipeline;

/// <summary>
/// Delegate representing the next component in the request pipeline returning a response.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="cancellationToken">A token to observe while waiting for completion.</param>
/// <returns>A task that completes with the response.</returns>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);

/// <summary>
/// Delegate representing the next component in the request pipeline without a response.
/// </summary>
/// <param name="cancellationToken">A token to observe while waiting for completion.</param>
/// <returns>A task that completes when the pipeline continues.</returns>
public delegate Task RequestHandlerDelegate(CancellationToken cancellationToken = default);

/// <summary>
/// Delegate representing the next component in the notification pipeline.
/// </summary>
/// <param name="cancellationToken">A token to observe while waiting for completion.</param>
/// <returns>A task that completes when the pipeline continues.</returns>
public delegate Task NotificationPublishDelegate(CancellationToken cancellationToken = default);
