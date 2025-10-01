namespace Dualis.CQRS;

/// <summary>
/// Handles a command that does not produce a response.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Processes the specified command.
    /// </summary>
    /// <param name="command">The command instance to handle.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles a command that produces a response.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Processes the specified command and returns a response.
    /// </summary>
    /// <param name="command">The command instance to handle.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    /// <returns>A task that completes with the command response.</returns>
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
