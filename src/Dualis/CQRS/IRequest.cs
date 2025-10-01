namespace Dualis.CQRS;

/// <summary>
/// Marker interface for Dualis requests (covers both commands and queries).
/// </summary>
public interface IRequest;

/// <summary>
/// Marker interface for Dualis requests that produce a response (covers both commands and queries).
/// </summary>
/// <typeparam name="T">The response type returned by the handler.</typeparam>
#pragma warning disable S2326 // Unused type parameters should be removed
public interface IRequest<out T> : IRequest;
#pragma warning restore S2326 // Unused type parameters should be removed
