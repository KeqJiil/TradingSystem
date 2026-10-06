namespace Messaging.Middlewares;

public static class TracingBuilderExtensions
{
    public static IMessagingBuilder AddTracing(this IMessagingBuilder builder)
    {
        return builder
            .AddDeliveryMiddleware<TracingDeliveryMiddleware>()
            .AddPublishMiddleware<TracingPublishMiddleware>();
    }
}
