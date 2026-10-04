using Messaging.Serializers;

namespace Messaging.Tests.TestSupport.Dispatch;

internal sealed class DispatchHarness : IDisposable
{
    public ServiceProvider Provider { get; }
    public ConsumerProbe Probe { get; } = new();
    public TraceLog Log { get; } = new();
    public CapturedContext Captured { get; } = new();
    public MessagingRegistry Registry { get; }
    public ConsumerBinding Binding { get; }
    public IMessageSerializer Serializer { get; }

    public DispatchHarness(Action<MessagingBuilder>? configure = null, IMessageSerializer? serializer = null)
    {
        Serializer = serializer ?? new JsonDefaultSerializer();
        var services = new ServiceCollection();
        services.AddSingleton(Probe);
        services.AddSingleton(Log);
        services.AddSingleton(Captured);

        services.AddMessaging(
            b =>
            {
                b.AddMessage<TestMessage>("topic", Serializer)
                    .AddConsumer<TestMessage, TestConsumer>(new ConsumerOptions("topic", "group"));
                configure?.Invoke(b);
            },
            o => o.BootstrapServers = "localhost:9092");

        Provider = services.BuildServiceProvider();
        Registry = Provider.GetRequiredService<MessagingRegistry>();
        Binding = Registry.Consumers[("topic", "group")][nameof(TestMessage)];
    }

    public RawMessage Raw(byte[] payload, Dictionary<string, string>? headers = null, int partition = 0,
        long offset = 0, DateTimeOffset? timestamp = null)
    {
        return new RawMessage(payload, "topic", "group", nameof(TestMessage), offset, partition,
            timestamp ?? DateTimeOffset.UtcNow, headers ?? new Dictionary<string, string>());
    }

    public byte[] Payload(string value)
    {
        return Serializer.Serialize(new TestMessage(value)).Message;
    }

    public void Dispose()
    {
        Provider.Dispose();
    }
}