using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Messaging.Dapper.Transactions;

public static class SqlTransientRetry
{
    public static bool IsKnownRolledBack(SqlException ex) => ex.Number == 1205;

    public static bool IsTransient(SqlException ex)
    {
        return ex.Number is -2 or 1205 or 20 or 64 or 233 or 10053 or 10054 or 10060 or 4060;
    }

    internal static ResiliencePipeline CreateDefault(ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = args => ValueTask.FromResult(
                    args.Outcome.Exception is SqlException sqlException && IsTransient(sqlException)),
                OnRetry = args =>
                {
                    logger.LogWarning(args.Outcome.Exception,
                        "Transaction attempt {Attempt} failed with a transient SQL error, retrying after {Delay} ms",
                        args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds);
                    return default;
                },
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(200)
            })
            .Build();
    }
}
