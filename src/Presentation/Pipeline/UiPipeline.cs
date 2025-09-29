using Dualis.CQRS.Commands;
using Dualis.Pipeline;

namespace Presentation.Pipeline;

/// <summary>
/// Unified pipeline behaviors for the UI layer.
/// One name across shapes: request/response and void requests.
/// </summary>
public sealed class UiPipeline<TRequest, TResponse>(ILogger<UiPipeline<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        logger.LogDebug("[UI] Handling {Request} at {Now}", typeof(TRequest).Name, DateTime.UtcNow);
        try
        {
            TResponse response = await next(cancellationToken).ConfigureAwait(false);

            logger.LogDebug("[UI] Handled {Request} at {Now}", typeof(TRequest).Name, DateTime.UtcNow);
            return response;
        }
        catch (Exception ex) when (LogError(ex, typeof(TRequest).Name))
        {
            throw;
        }
    }

    private bool LogError(Exception ex, string name)
    {
        logger.LogError(ex, "[UI] Error handling {Request} at {Now}", name, DateTime.UtcNow);
        return false;
    }
}

public sealed class UiVoidPipeline<TRequest>(ILogger<UiVoidPipeline<TRequest>> logger)
    : IPipelineBehavior<TRequest>
{
    public async Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        logger.LogDebug("[UI] Handling {Request} at {Now}", typeof(TRequest).Name, DateTime.UtcNow);
        try
        {
            await next(cancellationToken).ConfigureAwait(false);
            logger.LogDebug("[UI] Handled {Request} at {Now}", typeof(TRequest).Name, DateTime.UtcNow);
        }
        catch (Exception ex) when (LogError(ex, typeof(TRequest).Name))
        {
            throw;
        }
    }

    private bool LogError(Exception ex, string name)
    {
        logger.LogError(ex, "[UI] Error in pipeline for {Name} at {Now}", name, DateTime.UtcNow);
        return false;
    }
}
