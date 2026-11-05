using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry;
using Order.Infrastructure.Observability;

namespace Order.Presentation;

public static class ObservabilityBuilder
{
    public static void AddObservability(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(res => res.AddService("Order"))
            .WithTracing(tracing =>
            {
                tracing.AddSource(OrderTelemetry.SourceName, global::Messaging.MessagingTelemetry.SourceName)
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation();

                tracing.SetSampler(new ParentBasedSampler(new DropParentlessClientSampler()));
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(OrderTelemetry.SourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithLogging(logging => { logging.AddProcessor(new CorrelationLogProcessor()); },
                options =>
                {
                    options.IncludeScopes = true;
                    options.IncludeFormattedMessage = true;
                })
            .UseOtlpExporter();
    }
}