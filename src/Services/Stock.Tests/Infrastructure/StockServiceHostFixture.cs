using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Options;
using Stock.Infrastructure.Persistence;
using Stock.Presentation.Builder;
using Stock.Presentation.Kafka;
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
        builder.AddKafka();
        builder.AddMessaging();
        builder.Services.AddSingleton<ISystemClock, SystemClock>();

        DbMigrator.ApplyMigrations(ConnectionString);

        _app = builder.Build();
        await _app.StartAsync();
    }

    private async Task EnsureTopicsExistAsync()
    {
        var dlqSuffix = new DeadLetterOptions().TopicSuffix;
        string[] sourceTopics =
        [
            TopicNames.StockCreated, TopicNames.StockStatusToggled, TopicNames.Price, TopicNames.PriceChangeRequested
        ];

        var topics = sourceTopics
            .SelectMany(t => new[] { t, t + dlqSuffix + ".retry", t + dlqSuffix + ".fatal" })
            .ToArray();

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
