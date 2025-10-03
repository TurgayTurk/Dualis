using System.Reflection;
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
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        Type reqType = request.GetType();
        Type handlerType = typeof(IRequestHandler<,>).MakeGenericType(reqType, typeof(TResponse));
        object handler = serviceProvider.GetRequiredService(handlerType);
        MethodInfo mi = handlerType.GetMethod("Handle")!;
        return (Task<TResponse>)mi.Invoke(handler, [request, cancellationToken])!;
    }

    public Task Send(IRequest request, CancellationToken cancellationToken = default)
    {
        Type reqType = request.GetType();
        Type handlerType = typeof(IRequestHandler<>).MakeGenericType(reqType);
        object handler = serviceProvider.GetRequiredService(handlerType);
        MethodInfo mi = handlerType.GetMethod("Handle")!;
        return (Task)mi.Invoke(handler, [request, cancellationToken])!;
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
}
