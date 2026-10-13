using Testcontainers.Kafka;

namespace Messaging.Dapper.Tests.Integration.Infrastructure;

public class KafkaFixture : IAsyncLifetime
{
    private readonly KafkaContainer _kafkaContainer = new KafkaBuilder("confluentinc/cp-kafka:7.6.0").Build();

    public string BootstrapAddress => _kafkaContainer.GetBootstrapAddress();

    public async Task InitializeAsync()
    {
        await _kafkaContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _kafkaContainer.DisposeAsync();
    }
}
