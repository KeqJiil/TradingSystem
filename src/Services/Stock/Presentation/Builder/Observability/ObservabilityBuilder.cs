using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Stock.Infrastructure.Observability;

namespace Stock.Presentation.Builder.Observability;

public static class ObservabilityBuilder
{
    public static void AddObservability(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(res => res.AddService("Stock"))
            .WithTracing(tracing =>
            {
                tracing.AddSource(StockTelemetry.SourceName)
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation();

                tracing.SetSampler(new ParentBasedSampler(new DropParentlessClientSampler()));
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(StockTelemetry.SourceName)
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