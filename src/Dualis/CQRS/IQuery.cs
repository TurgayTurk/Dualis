namespace Dualis.CQRS;

/// <summary>
/// Marker interface for CQRS queries.
/// </summary>
/// <remarks>
/// Queries are read-only operations that must not mutate state. They may or may not return a response.
/// </remarks>
public interface IQuery : IRequest;

/// <summary>
/// Marker interface for CQRS queries that produce a response.
/// </summary>
/// <typeparam name="T">The response type returned by the query handler.</typeparam>
#pragma warning disable S2326 // Unused type parameters should be removed
public interface IQuery<out T> : IQuery, IRequest<T>;
#pragma warning restore S2326 // Unused type parameters should be removed
