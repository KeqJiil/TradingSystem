using Messaging.Abstractions;
using Messaging.Serializers;
using Messaging.Tests.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Messaging.Tests.Registry;

public class DispatchTests
{
    public sealed record TestMessage(string Value);

    public sealed class ConsumerProbe
    {
        public List<TestMessage> Received { get; } = new();
        public List<object> Instances { get; } = new();
        public ConsumeOutcome Outcome { get; set; } = new(MessageConsumeResult.Success);
        public bool Throw { get; set; }
    }

    public sealed class CapturedContext
    {
        public object? Last { get; set; }
    }

    public sealed class TestConsumer : IMessageConsumer<TestMessage>
    {
        private readonly ConsumerProbe _probe;
        private readonly TraceLog _log;

        public TestConsumer(ConsumerProbe probe, TraceLog log)
        {
            _probe = probe;
            _log = log;
            probe.Instances.Add(this);
        }

        public Task<ConsumeOutcome> ConsumeAsync(TestMessage message, CancellationToken cancellationToken)
        {
            if (_probe.Throw) return Task.FromException<ConsumeOutcome>(new InvalidOperationException("boom"));

            _probe.Received.Add(message);
            _log.Entries.Add("consumer");
            return Task.FromResult(_probe.Outcome);
        }
    }

    public sealed class ContextCaptureMiddleware(CapturedContext captured) : IMessageDeliveryMiddleware
    {
        public Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
            DeliveryDelegate next, CancellationToken cancellationToken)
        {
            captured.Last = context;
            return next();
        }
    }

    private sealed class Harness : IDisposable
    {
        public ServiceProvider Provider { get; }
        public ConsumerProbe Probe { get; } = new();
        public TraceLog Log { get; } = new();
        public CapturedContext Captured { get; } = new();
        public MessagingRegistry Registry { get; }
        public ConsumerBinding Binding { get; }
        public JsonDefaultSerializer Serializer { get; } = new();

        public Harness(Action<MessagingBuilder>? configure = null)
        {
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

        public byte[] Payload(string value) => Serializer.Serialize(new TestMessage(value)).Message;

        public void Dispose() => Provider.Dispose();
    }

    [Fact]
    public async Task Dispatch_ValidPayload_InvokesConsumerWithDeserializedMessage()
    {
        using var harness = new Harness();
        using var scope = harness.Provider.CreateScope();

        await harness.Binding.Dispatch(scope.ServiceProvider, harness.Raw(harness.Payload("hello")),
            CancellationToken.None);

        Assert.Equal(new TestMessage("hello"), Assert.Single(harness.Probe.Received));
    }

    [Theory]
    [InlineData(MessageConsumeResult.Success, null)]
    [InlineData(MessageConsumeResult.Retry, "try later")]
    [InlineData(MessageConsumeResult.DeadLetter, "poison")]
    public async Task Dispatch_ConsumerReturnsOutcome_OutcomeIsReturned(MessageConsumeResult kind, string? reason)
    {
        using var harness = new Harness();
        using var scope = harness.Provider.CreateScope();
        harness.Probe.Outcome = new ConsumeOutcome(kind, reason);

        var outcome = await harness.Binding.Dispatch(scope.ServiceProvider, harness.Raw(harness.Payload("x")),
            CancellationToken.None);

        Assert.Equal(kind, outcome.Kind);
        Assert.Equal(reason, outcome.Reason);
    }

    [Fact]
    public async Task Dispatch_InvalidPayload_ReturnsDeadLetterWithoutCallingConsumer()
    {
        using var harness = new Harness();
        using var scope = harness.Provider.CreateScope();

        var outcome = await harness.Binding.Dispatch(scope.ServiceProvider, harness.Raw([1, 2, 3]),
            CancellationToken.None);

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
        Assert.False(string.IsNullOrEmpty(outcome.Reason));
        Assert.Empty(harness.Probe.Received);
    }

    [Fact]
    public async Task Dispatch_RunsDeliveryMiddlewaresAroundConsumer()
    {
        using var harness = new Harness(b => b.AddDeliveryMiddleware<ProbeA>());
        using var scope = harness.Provider.CreateScope();

        await harness.Binding.Dispatch(scope.ServiceProvider, harness.Raw(harness.Payload("x")),
            CancellationToken.None);

        Assert.Equal(["A:before", "consumer", "A:after"], harness.Log.Entries);
    }

    [Fact]
    public async Task Dispatch_BuildsContextFromRawMessage()
    {
        using var harness = new Harness(b => b.AddDeliveryMiddleware<ContextCaptureMiddleware>());
        using var scope = harness.Provider.CreateScope();
        var timestamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var headers = new Dictionary<string, string> { ["custom"] = "value" };

        await harness.Binding.Dispatch(scope.ServiceProvider,
            harness.Raw(harness.Payload("hello"), headers, partition: 3, offset: 42, timestamp: timestamp),
            CancellationToken.None);

        var context = Assert.IsType<DeliveryContext<TestMessage>>(harness.Captured.Last);
        Assert.Equal("topic", context.Topic);
        Assert.Equal("group", context.ConsumerGroup);
        Assert.Equal(3, context.Partition);
        Assert.Equal(42, context.Offset);
        Assert.Equal(timestamp, context.Timestamp);
        Assert.Equal(nameof(TestMessage), context.MessageType);
        Assert.Equal(new TestMessage("hello"), context.Message);
        Assert.Equal("value", context.Headers["custom"]);
    }

    [Fact]
    public async Task Dispatch_MessageIdHeaderPresent_UsedAsContextMessageId()
    {
        using var harness = new Harness(b => b.AddDeliveryMiddleware<ContextCaptureMiddleware>());
        using var scope = harness.Provider.CreateScope();
        var headers = new Dictionary<string, string> { [MessagingHeaders.MessageId] = "message-1" };

        await harness.Binding.Dispatch(scope.ServiceProvider, harness.Raw(harness.Payload("x"), headers),
            CancellationToken.None);

        var context = Assert.IsType<DeliveryContext<TestMessage>>(harness.Captured.Last);
        Assert.Equal("message-1", context.MessageId);
    }

    [Fact]
    public async Task Dispatch_MessageIdHeaderMissing_ContextMessageIdIsEmpty()
    {
        using var harness = new Harness(b => b.AddDeliveryMiddleware<ContextCaptureMiddleware>());
        using var scope = harness.Provider.CreateScope();

        await harness.Binding.Dispatch(scope.ServiceProvider, harness.Raw(harness.Payload("x")),
            CancellationToken.None);

        var context = Assert.IsType<DeliveryContext<TestMessage>>(harness.Captured.Last);
        Assert.Equal(string.Empty, context.MessageId);
    }

    [Fact]
    public async Task Dispatch_ConsumerResolvedFromPassedScope_NewInstancePerScope()
    {
        using var harness = new Harness();
        using var scope1 = harness.Provider.CreateScope();
        using var scope2 = harness.Provider.CreateScope();
        var raw = harness.Raw(harness.Payload("x"));

        await harness.Binding.Dispatch(scope1.ServiceProvider, raw, CancellationToken.None);
        await harness.Binding.Dispatch(scope2.ServiceProvider, raw, CancellationToken.None);

        Assert.Equal(2, harness.Probe.Instances.Count);
        Assert.NotSame(harness.Probe.Instances[0], harness.Probe.Instances[1]);
    }

    [Fact]
    public async Task Dispatch_ConsumerThrows_ExceptionPropagates()
    {
        using var harness = new Harness();
        using var scope = harness.Provider.CreateScope();
        harness.Probe.Throw = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Binding.Dispatch(scope.ServiceProvider, harness.Raw(harness.Payload("x")),
                CancellationToken.None));
    }

    [Fact]
    public void Registry_LookupByTopicGroupAndMessageName_FindsBinding()
    {
        using var harness = new Harness();

        var binding = harness.Registry.Consumers[("topic", "group")][nameof(TestMessage)];

        Assert.Equal("topic", binding.Options.Topic);
        Assert.Equal("group", binding.Options.ConsumerGroup);
    }

    [Fact]
    public void Registry_LookupByWireMessageName_MatchesTypeName()
    {
        using var harness = new Harness();

        var key = Assert.Single(harness.Registry.Consumers[("topic", "group")].Keys);

        Assert.Equal(typeof(TestMessage).Name, key);
    }
}
