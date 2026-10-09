namespace Messaging.Tests.TestSupport.Consumers;

public sealed class ConsumerProbe
{
    public List<TestMessage> Received { get; } = new();
    public List<object> Instances { get; } = new();
    public ConsumeOutcome Outcome { get; set; } = new(MessageConsumeResult.Success);
    public bool Throw { get; set; }
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

public sealed class TestMessageHandler : IMessageConsumer<TestMessage>
{
    public Task<ConsumeOutcome> ConsumeAsync(TestMessage message, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));
    }
}

public sealed class TestMessageHandler2 : IMessageConsumer<TestMessage2>
{
    public Task<ConsumeOutcome> ConsumeAsync(TestMessage2 message, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));
    }
}
