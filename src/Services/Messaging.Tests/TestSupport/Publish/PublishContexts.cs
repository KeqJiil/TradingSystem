namespace Messaging.Tests.TestSupport.Publish;

public static class PublishContexts
{
    public static PublishContext<SampleMessage> Sample(Dictionary<string, string>? headers = null)
    {
        return new PublishContext<SampleMessage>(
            PublisherHarness.Topic, "key", "message-id", nameof(SampleMessage),
            new SampleMessage("name", 1), headers ?? new Dictionary<string, string>());
    }
}
