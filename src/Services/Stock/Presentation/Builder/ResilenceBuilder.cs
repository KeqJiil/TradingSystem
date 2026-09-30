using Microsoft.Data.SqlClient;
using Polly;
using Polly.Retry;

namespace Stock.Presentation.Builder;

public static class ResilenceBuilder
{
    public static void AddResilence(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ResiliencePipeline>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ResiliencePipeline));

            var pipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = args =>
                    {
                        if (args.Outcome.Exception is not { } ex) return ValueTask.FromResult(false);

                        var retry = ex is SqlException sqlEx && IsTransient(sqlEx);
                        logger.LogWarning(ex,
                            "Resilience attempt {Attempt} failed with {ExceptionType} (SqlNumber: {SqlNumber}), retry: {Retry}",
                            args.AttemptNumber, ex.GetType().Name, (ex as SqlException)?.Number, retry);
                        return ValueTask.FromResult(retry);
                    },
                    OnRetry = args =>
                    {
                        logger.LogWarning("Resilience retry {RetryNumber} after {Delay} ms",
                            args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds);
                        return default;
                    },
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(200)
                })
                .Build();
            return pipeline;
        });
    }

    private static bool IsTransient(SqlException ex)
    {
        return ex.Number is -2 or 1205 or 20 or 64 or 233 or 10053 or 10054 or 10060 or 4060
               || ex.Number == 2601 && ex.Message.Contains("idx_event_store_version", StringComparison.Ordinal);
    }
}
