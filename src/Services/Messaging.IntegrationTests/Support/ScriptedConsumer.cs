namespace Messaging.IntegrationTests.Support;

public sealed class ConsumerRecorder<TMessage>
{
    private readonly List<TMessage> _calls = new();

    public Func<TMessage, int, ConsumeOutcome> Behavior { get; set; } = (_, _) => new ConsumeOutcome(MessageConsumeResult.Success);

    public Func<TMessage, Task>? Before { get; set; }

    public IReadOnlyList<TMessage> Calls
    {
        get
        {
            lock (_calls) return _calls.ToList();
        }
    }

    public int CallCount => Calls.Count;

    public int CallsFor(TMessage message)
    {
        return Calls.Count(c => Equals(c, message));
    }

    public Task WaitForCallsAsync(int count, TimeSpan? timeout = null)
    {
        return Wait.UntilAsync(() => CallCount >= count, $"{count} consumer calls, got {CallCount}", timeout);
    }

    internal int Record(TMessage message)
    {
        lock (_calls)
        {
            _calls.Add(message);
            return _calls.Count(c => Equals(c, message));
        }
    }
}

public sealed class ScriptedConsumer<TMessage>(ConsumerRecorder<TMessage> recorder) : IMessageConsumer<TMessage>
{
    public async Task<ConsumeOutcome> ConsumeAsync(TMessage message, CancellationToken cancellationToken)
    {
        var attempt = recorder.Record(message);

        if (recorder.Before is not null)
            await recorder.Before(message);

        return recorder.Behavior(message, attempt);
    }
}
