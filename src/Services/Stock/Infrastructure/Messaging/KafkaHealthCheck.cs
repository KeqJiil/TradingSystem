using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Stock.Infrastructure.Messaging;

public sealed class KafkaHealthCheck(IAdminClient adminClient)
    : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            adminClient.GetMetadata(
                TimeSpan.FromSeconds(3));

            return Task.FromResult(
                HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "Kafka is unavailable.",
                    ex));
        }
    }
}