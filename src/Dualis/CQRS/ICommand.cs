namespace Dualis.CQRS;

/// <summary>
/// Marker interface for CQRS commands.
/// </summary>
/// <remarks>
/// Commands represent intent to change system state. They may or may not return a response.
/// </remarks>
public interface ICommand : IRequest;

/// <summary>
/// Marker interface for CQRS commands that produce a response.
/// </summary>
/// <typeparam name="T">The response type returned by the command handler.</typeparam>
#pragma warning disable S2326 // Unused type parameters should be removed
public interface ICommand<out T> : ICommand, IRequest<T>;
#pragma warning restore S2326 // Unused type parameters should be removed
