using Testcontainers.Kafka;

namespace Messaging.Dapper.Tests.Integration.Infrastructure;

public class KafkaFixture : IAsyncLifetime
{
    private readonly KafkaContainer _kafkaContainer = new KafkaBuilder("confluentinc/cp-kafka:latest").Build();

    public string? ConnectionString = null;

    public async Task InitializeAsync()
    {
        await _kafkaContainer.StartAsync();
        ConnectionString = _kafkaContainer.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        await _kafkaContainer.DisposeAsync();
    }
}