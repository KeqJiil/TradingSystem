using Messaging.Abstractions;
using Messaging.Serializers;
using Messaging.Tests.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Messaging.Tests.TestSupport;

public sealed class PublisherHarness : IDisposable
{
    public const string Topic = "sample-topic";

    public RecordingPublishTerminal Terminal { get; } = new();

    public TraceLog Log { get; } = new();

    public PublishOutcomeLog Outcomes { get; } = new();

    public PublishInstanceLog Instances { get; } = new();

    public PublishContextLog Contexts { get; } = new();

    public ServiceProvider Provider { get; }

    public PublisherHarness(Action<MessagingBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(Log);
        services.AddSingleton(Outcomes);
        services.AddSingleton(Instances);
        services.AddSingleton(Contexts);

        services.AddMessaging(
            builder =>
            {
                builder.AddMessage<SampleMessage>(Topic, new JsonDefaultSerializer());
                configure?.Invoke(builder);
            },
            options => options.BootstrapServers = "localhost:9092");

        services.Replace(ServiceDescriptor.Singleton<IPublishTerminal>(Terminal));

        Provider = services.BuildServiceProvider();
    }

    public IServiceScope CreateScope()
    {
        return Provider.CreateScope();
    }

    public async Task<PublishOutcome> PublishAsync(SampleMessage message, PublishOptions? options)
    {
        using var scope = CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
        return await publisher.PublishAsync(message, options, CancellationToken.None);
    }

    public static PublishOptions DefaultOptions(string key = "key", Dictionary<string, string>? headers = null)
    {
        return new PublishOptions(key, Topic, headers ?? new Dictionary<string, string>());
    }

    public void Dispose()
    {
        Provider.Dispose();
    }
}

public static class PublishContexts
{
    public static PublishContext<SampleMessage> Sample(Dictionary<string, string>? headers = null)
    {
        return new PublishContext<SampleMessage>(
            PublisherHarness.Topic, "key", "message-id", nameof(SampleMessage),
            new SampleMessage("name", 1), headers ?? new Dictionary<string, string>());
    }
}