using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Messaging;
using Messaging.Abstractions;
using Messaging.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;
using Stock.Infrastructure.Cron;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Options;
using Stock.Infrastructure.Persistence;
using Stock.Presentation.Builder;
using Testcontainers.Kafka;
using Testcontainers.MsSql;
using Xunit;

namespace Stock.Tests.Infrastructure;

public class StockServiceHostFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _mssqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("St0ckP@ssw0rd")
        .Build();

    private readonly KafkaContainer _kafkaContainer = new KafkaBuilder("confluentinc/cp-kafka:7.6.0")
        .Build();

    private WebApplication _app = null!;

    public string BootstrapAddress => _kafkaContainer.GetBootstrapAddress();
    public string ConnectionString => _mssqlContainer.GetConnectionString();
    public IServiceProvider Services => _app.Services;

    public PublishedMessages Published =>
        new(ConnectionString, Services.GetRequiredService<IMessageSerializerResolver>());

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mssqlContainer.StartAsync(), _kafkaContainer.StartAsync());

        await EnsureTopicsExistAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionString,
            ["KafkaOptions:BootstrapServers"] = BootstrapAddress,
            ["KafkaOptions:ProducerClientId"] = "stock-tests-producer"
        });

        builder.AddResilence();
        builder.AddPersistence();
        builder.AddApplication();
        builder.AddJobs();
        builder.AddStockMessaging();
        builder.Services.AddSingleton<ISystemClock, SystemClock>();

        DbMigrator.ApplyMigrations(ConnectionString);

        _app = builder.Build();
        await _app.StartAsync();
    }

    public void Produce<TValue>(string topic, Guid key, TValue value)
    {
        using var stream = new MemoryStream();
        ProtoBuf.Serializer.Serialize(stream, value);

        using var producer = new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = BootstrapAddress })
            .Build();
        producer.Produce(topic, KafkaMessageFactory.Create(key.ToString(), stream.ToArray(), typeof(TValue).Name,
            Guid.NewGuid().ToString(), []));
        producer.Flush(TimeSpan.FromSeconds(10));
    }

    public static string DeadLetterTopic(string consumerGroup, string topic)
    {
        return $"{consumerGroup}.{topic}.dlq";
    }

    private async Task EnsureTopicsExistAsync()
    {
        var topics = TopicNames.All;

        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = BootstrapAddress })
            .Build();

        try
        {
            await admin.CreateTopicsAsync(topics.Select(t =>
                new TopicSpecification { Name = t, NumPartitions = 1, ReplicationFactor = 1 }));
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
        }
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        await _kafkaContainer.DisposeAsync();
        await _mssqlContainer.DisposeAsync();
    }
}
