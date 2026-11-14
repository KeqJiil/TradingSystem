using System.Diagnostics;
using Messaging.Abstractions;

namespace Order.Presentation;

public class CorrelationMiddleware(RequestDelegate next)
{
    public const string Header = "x-correlation-id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Guid.TryParse(context.Request.Headers[Header].FirstOrDefault(), out var parsed)
            ? parsed.ToString()
            : Guid.NewGuid().ToString();

        MessagingCorrelation.CorrelationId = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = correlationId;
            return Task.CompletedTask;
        });

        await next(context);
    }
}
