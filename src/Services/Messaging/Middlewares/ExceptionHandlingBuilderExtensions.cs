namespace Messaging.Middlewares;

public static class ExceptionHandlingBuilderExtensions
{
    public static IMessagingBuilder AddExceptionHandling(this IMessagingBuilder builder)
    {
        return builder.AddDeliveryMiddleware<DefaultExceptionHandlingMiddleware>();
    }
}
