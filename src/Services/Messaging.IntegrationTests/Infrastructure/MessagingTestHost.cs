using Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messaging.IntegrationTests.Infrastructure;

public sealed class MessagingTestHost : IAsyncDisposable
{
    private readonly IHost _host;
    private bool _stopped;

    private MessagingTestHost(IHost host, KafkaTestClient kafka)
    {
        _host = host;
        Kafka = kafka;
    }

    public IServiceProvider Services => _host.Services;

    public KafkaTestClient Kafka { get; }

    public static async Task<MessagingTestHost> StartAsync(KafkaFixture kafka, ITestOutputHelper output,
        Action<IServiceCollection> configureServices, Action<MessagingBuilder> configureMessaging,
        Action<MessagingOptions>? configureOptions = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new XunitLoggerProvider(output));

        configureServices(builder.Services);
        builder.Services.AddMessaging(configureMessaging, options =>
        {
            options.BootstrapServers = kafka.BootstrapAddress;
            options.ProducerClientId = TestNames.Unique("it");
            options.RetryDelay = TimeSpan.FromMilliseconds(50);
            configureOptions?.Invoke(options);
        });

        var host = builder.Build();
        await host.StartAsync();

        return new MessagingTestHost(host, new KafkaTestClient(kafka.BootstrapAddress));
    }

    public async Task<PublishOutcome> PublishAsync<TMessage>(TMessage message, string topic, string key,
        Dictionary<string, string>? headers = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        return await publisher.PublishAsync(message, new PublishOptions(key, topic, headers ?? new()),
            CancellationToken.None);
    }

    public async Task StopAsync()
    {
        if (_stopped) return;

        _stopped = true;
        await _host.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _host.Dispose();
    }
}
