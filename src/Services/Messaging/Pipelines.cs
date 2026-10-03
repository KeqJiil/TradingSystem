using Messaging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging;

internal static class MessagingPipeline
{
    public static Task<ConsumeOutcome> RunDelivery<TMessage>(
        DeliveryContext<TMessage> ctx, IServiceProvider services,
        DeliveryDelegate terminal, CancellationToken ct)
    {
        var middlewares = services
            .GetKeyedServices<IMessageDeliveryMiddleware>("MessagingDeliveryMiddleware").ToList();

        var next = terminal;
        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = middlewares[i];
            var inner = next;
            next = () => middleware.OnDeliveryAsync(ctx, inner, ct);
        }

        return next();
    }
}

internal static class MessagingPublishPipeline
{
    public static Task RunPublish<TMessage>(
        PublishContext<TMessage> ctx, IServiceProvider services,
        PublishDelegate terminal, CancellationToken ct)
    {
        var middlewares = services
            .GetKeyedServices<IMessagePublishMiddleware>("MessagingPublishMiddleware").ToList();

        var next = terminal;
        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = middlewares[i];
            var inner = next;
            next = () => middleware.OnPublishAsync(ctx, inner, ct);
        }

        return next();
    }
}