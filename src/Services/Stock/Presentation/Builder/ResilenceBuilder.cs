using Microsoft.Data.SqlClient;
using Polly;
using Polly.Retry;

namespace Stock.Presentation.Builder;

public static class ResilenceBuilder
{
    public static void AddResilence(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ResiliencePipeline>(_ =>
        {
            var pipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<SqlException>(IsTransient),
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
        return ex.Number is -2 or 1205 or 20 or 64 or 233 or 10053 or 10054 or 10060 or 4060;
    }
}