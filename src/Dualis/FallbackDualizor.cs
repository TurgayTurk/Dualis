using System.Reflection;
using System.Runtime.ExceptionServices;
using Dualis.CQRS;
using Dualis.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Dualis;

/// <summary>
/// Runtime fallback mediator used when the source-generated Dualizor is not available.
/// Uses reflection to resolve and invoke handlers registered in DI.
/// </summary>
internal sealed class FallbackDualizor(
    IServiceProvider serviceProvider,
    INotificationPublisher publisher,
    NotificationPublishContext publishContext) : IDualizor, ISender, IPublisher
{
    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        Type reqType = request.GetType();
        Type handlerType = typeof(IRequestHandler<,>).MakeGenericType(reqType, typeof(TResponse));
        object handler = serviceProvider.GetRequiredService(handlerType);
        MethodInfo mi = handlerType.GetMethod("Handle")!;

        try
        {
            return await (Task<TResponse>)mi.Invoke(handler, [request, cancellationToken])!;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            (bool handled, TResponse response) = await TryHandleRequestException<TResponse>(request, tie.InnerException, cancellationToken);
            if (handled)
            {
                return response;
            }

            await TryExecuteExceptionActions(request, tie.InnerException, cancellationToken);
            ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw;
        }
        catch (Exception ex)
        {
            (bool handled, TResponse response) = await TryHandleRequestException<TResponse>(request, ex, cancellationToken);
            if (handled)
            {
                return response;
            }

            await TryExecuteExceptionActions(request, ex, cancellationToken);
            throw;
        }
    }

    public async Task Send(IRequest request, CancellationToken cancellationToken = default)
    {
        Type reqType = request.GetType();
        Type handlerType = typeof(IRequestHandler<>).MakeGenericType(reqType);
        object handler = serviceProvider.GetRequiredService(handlerType);
        MethodInfo mi = handlerType.GetMethod("Handle")!;

        try
        {
            await (Task)mi.Invoke(handler, [request, cancellationToken])!;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            await TryExecuteExceptionActions(request, tie.InnerException, cancellationToken);
            ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw;
        }
        catch (Exception ex)
        {
            await TryExecuteExceptionActions(request, ex, cancellationToken);
            throw;
        }
    }

    public async Task Publish(INotification notification, CancellationToken cancellationToken = default)
    {
        // Use publisher + context when there are handlers
        Type noteType = notification.GetType();
        Type handlerType = typeof(INotificationHandler<>).MakeGenericType(noteType);
        Type enumerableType = typeof(IEnumerable<>).MakeGenericType(handlerType);
        object? resolved = serviceProvider.GetService(enumerableType);

        if (resolved is System.Collections.IEnumerable handlers)
        {
            // Cast handlers to IEnumerable<INotificationHandler<T>> for the publisher
            Type iHandlerT = typeof(INotificationHandler<>).MakeGenericType(noteType);
            var handlersArray = Array.CreateInstance(iHandlerT, handlers.Cast<object>().Count());
            int idx = 0;
            foreach (object h in handlers)
            {
                handlersArray.SetValue(h, idx++);
            }

            MethodInfo castMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast))!.MakeGenericMethod(iHandlerT);
            object? list = castMethod.Invoke(null, [handlersArray]);
            MethodInfo asEnumMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.AsEnumerable))!.MakeGenericMethod(iHandlerT);
            object? enumerableHandlers = asEnumMethod.Invoke(null, [list!]);

            MethodInfo genericPublish = typeof(INotificationPublisher)
                .GetMethod(nameof(INotificationPublisher.Publish))!
                .MakeGenericMethod(noteType);

            await (Task)genericPublish.Invoke(publisher, [notification, enumerableHandlers!, publishContext, cancellationToken])!;
        }
    }

    private async Task<(bool handled, TResponse response)> TryHandleRequestException<TResponse>(IRequest<TResponse> request, Exception exception, CancellationToken cancellationToken)
    {
        Type requestType = request.GetType();
        Type responseType = typeof(TResponse);
        RequestExceptionState<TResponse> state = new();

        foreach (Type exceptionType in EnumerateExceptionTypes(exception.GetType()))
        {
            Type handlerServiceType = typeof(IRequestExceptionHandler<,,>).MakeGenericType(requestType, responseType, exceptionType);
            Type enumerableType = typeof(IEnumerable<>).MakeGenericType(handlerServiceType);
            object? resolved = serviceProvider.GetService(enumerableType);
            if (resolved is not System.Collections.IEnumerable handlers)
            {
                continue;
            }

            MethodInfo handleMethod = handlerServiceType.GetMethod(nameof(IRequestExceptionHandler<,,>.Handle))!;
            foreach (object handler in handlers)
            {
                await (Task)handleMethod.Invoke(handler, [request, exception, state, cancellationToken])!;
                if (state.Handled)
                {
                    return (true, state.Response!);
                }
            }
        }

        return (false, default!);
    }

    private async Task TryExecuteExceptionActions(IRequest request, Exception exception, CancellationToken cancellationToken)
    {
        Type requestType = request.GetType();

        foreach (Type exceptionType in EnumerateExceptionTypes(exception.GetType()))
        {
            Type actionServiceType = typeof(IRequestExceptionAction<,>).MakeGenericType(requestType, exceptionType);
            Type enumerableType = typeof(IEnumerable<>).MakeGenericType(actionServiceType);
            object? resolved = serviceProvider.GetService(enumerableType);
            if (resolved is not System.Collections.IEnumerable actions)
            {
                continue;
            }

            MethodInfo executeMethod = actionServiceType.GetMethod(nameof(IRequestExceptionAction<,>.Execute))!;
            foreach (object action in actions)
            {
                await (Task)executeMethod.Invoke(action, [request, exception, cancellationToken])!;
            }
        }
    }

    private static IEnumerable<Type> EnumerateExceptionTypes(Type exceptionType)
    {
        for (Type? current = exceptionType; current is not null && typeof(Exception).IsAssignableFrom(current); current = current.BaseType)
        {
            yield return current;
        }
    }
}
