using Stock.Infrastructure;

namespace Stock.Presentation.Http.Middlewares;

public class CorrelationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Guid.TryParse(context.Request.Headers["X-Correlation-ID"].FirstOrDefault(), out var parsed)
            ? parsed
            : Guid.NewGuid();

        CorrelationContext.CorrelationId = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-ID"] = correlationId.ToString();
            return Task.CompletedTask;
        });

        await next(context);
    }
}