namespace Messaging.Tests.TestSupport.Publish;

public sealed record PublishCall(
    string Topic,
    string Key,
    string MessageId,
    object? Payload,
    IReadOnlyDictionary<string, string> Headers);

public sealed class RecordingPublishTerminal : IPublishTerminal
{
    public List<PublishCall> Calls { get; } = new();

    public PublishOutcome Result { get; set; } = new(true);

    public Exception? ThrowOnSend { get; set; }

    public Task<PublishOutcome> SendAsync<TMessage>(string topic, string key, string messageId, TMessage payload,
        IDictionary<string, string> headers, CancellationToken ct)
    {
        if (ThrowOnSend is not null) return Task.FromException<PublishOutcome>(ThrowOnSend);

        Calls.Add(new PublishCall(topic, key, messageId, payload, new Dictionary<string, string>(headers)));
        return Task.FromResult(Result);
    }
}
