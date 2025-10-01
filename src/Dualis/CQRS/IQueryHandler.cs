namespace Dualis.CQRS;

/// <summary>
/// Handles a query that does not produce a response.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
public interface IQueryHandler<in TQuery>
    where TQuery : IQuery
{
    /// <summary>
    /// Processes the specified query.
    /// </summary>
    /// <param name="query">The query instance to handle.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    Task HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles a query that produces a response.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    /// <summary>
    /// Processes the specified query and returns a response.
    /// </summary>
    /// <param name="query">The query instance to handle.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that completes with the query response.</returns>
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
